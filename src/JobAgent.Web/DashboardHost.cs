using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using JobAgent.Core.Applications;
using JobAgent.Infrastructure.Applications;
using JobAgent.Core.Answers;
using JobAgent.Infrastructure.Documents;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using Microsoft.AspNetCore.Http.Features;

namespace JobAgent.Web;

public static class DashboardHost
{
    public static WebApplication Build(string[] args, DashboardOptions? options = null)
    {
        options ??= new();
        if (options.EnableSyntheticCommands &&
            (options.BootstrapToken.Length != 64 || !options.BootstrapToken.All(Uri.IsHexDigit) ||
             options.BridgeToken.Length != 64 || !options.BridgeToken.All(Uri.IsHexDigit) ||
             string.Equals(options.BridgeToken, options.BootstrapToken, StringComparison.Ordinal)))
            throw new ArgumentException("UI and host bridge credentials must be distinct 256-bit tokens.", nameof(options));
        var root = FindRoot();
        var webRoot = options.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = webRoot });
        builder.WebHost.UseUrls("http://127.0.0.1:5178");
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2_100_000);
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(_ => new DemoWorkflow(options.DataDirectory, options.CareerOrigin, root));
        builder.Services.AddSingleton(_ => new LocalWorkspace(Path.Combine(options.DataDirectory, "personal"), root,
            new WindowsDpapiPayloadProtector()));
        var sessions = new ConcurrentDictionary<string, UiSession>();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var req = context.Request;
            if (req.Host.Host != "127.0.0.1" || req.Host.Port != context.Connection.LocalPort)
            { context.Response.StatusCode = 403; return; }
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            if (req.Path.StartsWithSegments("/internal/mcp"))
            {
                if (context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip) ||
                    req.Headers.Origin.Count != 0)
                { context.Response.StatusCode = 403; return; }
                var supplied = req.Headers["X-JobAgent-Bridge"].ToString();
                if (!options.EnableSyntheticCommands || options.BridgeExpiresAt <= DateTimeOffset.UtcNow ||
                    !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(options.BridgeToken)))
                { context.Response.StatusCode = 401; return; }
                if (!HttpMethods.IsPost(req.Method) || req.QueryString.HasValue ||
                    context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true)
                { context.Response.StatusCode = 400; return; }
            }
            if (req.Path.StartsWithSegments("/api"))
            {
                var write = !HttpMethods.IsGet(req.Method);
                var expectedOrigin = "http://" + req.Host;
                if (write && req.Headers.Origin != expectedOrigin)
                { context.Response.StatusCode = 403; return; }
                if (!(req.Path == "/api/session" && HttpMethods.IsPost(req.Method)))
                {
                    var cookie = req.Cookies["jobagent-session"];
                    if (cookie is null || !sessions.TryGetValue(cookie, out var session) || session.Expires <= DateTimeOffset.UtcNow)
                    { context.Response.StatusCode = 401; return; }
                    if (write && req.Headers["X-JobAgent-Csrf"] != session.Csrf)
                    { context.Response.StatusCode = 403; return; }
                    context.Items["session"] = session;
                }
            }
            try { await next(); }
            catch (PolicyException e)
            { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = e.Code }); }
            catch (DocumentImportException e)
            { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = e.Code }); }
            catch (Exception e) when (e is InvalidOperationException or IOException or ArgumentException)
            { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "LocalOperationFailed" }); }
        });
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", mode = "Fixture" }));
        app.MapPost("/api/session", (SessionRequest request, HttpContext context) =>
        {
            var actual = Encoding.UTF8.GetBytes(request.Token ?? "");
            if (!CryptographicOperations.FixedTimeEquals(actual, Encoding.UTF8.GetBytes(options.BootstrapToken)))
                return Results.Unauthorized();
            var session = new UiSession(Guid.NewGuid().ToString("N"), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), DateTimeOffset.UtcNow.AddHours(8));
            sessions[session.Id] = session;
            context.Response.Cookies.Append("jobagent-session", session.Id, new CookieOptions
            { HttpOnly = true, SameSite = SameSiteMode.Strict, IsEssential = true, Path = "/", MaxAge = TimeSpan.FromHours(8) });
            return Results.Ok(new { csrf = session.Csrf });
        });
        app.MapGet("/api/session", (HttpContext c) => new { csrf = ((UiSession)c.Items["session"]!).Csrf });
        app.MapGet("/api/state", async (DemoWorkflow w) => await w.GetStateAsync());
        app.MapPost("/api/demo/load", async (DemoWorkflow w) => { await w.LoadFixtureAsync(); return Results.NoContent(); });
        app.MapPost("/api/profile/confirm", async (DemoWorkflow w) => { await w.ConfirmProfileFromUiAsync(); return Results.NoContent(); });
        app.MapPost("/api/applications", async (DemoWorkflow w) => { await w.CreateDraftAsync(); return Results.NoContent(); });
        app.MapPost("/api/approve-share", async (DemoWorkflow w, HttpContext c) =>
        { await w.ShareAndFillFromUiAsync(((UiSession)c.Items["session"]!).Id); return Results.NoContent(); });
        app.MapPost("/api/approve-submit", async (DemoWorkflow w, HttpContext c) =>
        { await w.SubmitFromUiAsync(((UiSession)c.Items["session"]!).Id); return Results.NoContent(); });
        app.MapPost("/api/approve-host-submit/{applicationRef:guid}", async (DemoWorkflow w, HttpContext c, Guid applicationRef) =>
        {
            if (!options.EnableSyntheticCommands) return Results.NotFound();
            await w.ApproveForHostFromUiAsync(applicationRef, ((UiSession)c.Items["session"]!).Id);
            return Results.NoContent();
        });
        app.MapPost("/internal/mcp/applications", async (DemoWorkflow w) => await w.CreateSyntheticDraftForHostAsync());
        app.MapPost("/internal/mcp/applications/{applicationRef:guid}/review", async (DemoWorkflow w, Guid applicationRef) =>
            await w.RequestHostReviewAsync(applicationRef));
        app.MapPost("/internal/mcp/applications/{applicationRef:guid}/execute", async (DemoWorkflow w, Guid applicationRef) =>
            await w.ExecuteAlreadyApprovedAsync(applicationRef));
        app.MapPost("/api/cancel", async (DemoWorkflow w) => { await w.CancelAsync(); return Results.NoContent(); });
        app.MapGet("/api/workspace", async (LocalWorkspace w) => await w.GetAsync());
        app.MapPost("/api/workspace/import", async (LocalWorkspace w, HttpContext c) =>
            await w.ImportAsync(c.Request.Body, Uri.UnescapeDataString(c.Request.Headers["X-File-Name"].ToString()), c.RequestAborted));
        app.MapPost("/api/workspace/profile", async (LocalWorkspace w, ProfileReview review) => await w.ReviewProfileAsync(review));
        app.MapPost("/api/workspace/job", async (LocalWorkspace w, JobReview review) => await w.ReviewJobAsync(review));
        app.MapPost("/api/workspace/answer", async (LocalWorkspace w, FormQuestion question) => await w.ResolveAsync(question));
        app.MapPost("/api/workspace/answer-memory", async (LocalWorkspace w, WorkspaceAnswerReview review) => await w.ReviewAnswerAsync(review));
        app.MapPost("/api/workspace/answer-memory/revoke", async (LocalWorkspace w, WorkspaceAnswerRevocation request) => await w.RevokeAnswerAsync(request));
        app.MapGet("/api/workspace/export", async (LocalWorkspace w) => Results.File(
            Encoding.UTF8.GetBytes(await w.ExportAsync()), "application/json", "job-agent-local-export.json"));
        app.MapPost("/api/workspace/delete", async (LocalWorkspace w, DeleteWorkspaceRequest request) =>
        { await w.DeleteAsync(request.ExpectedRevision); return Results.NoContent(); });
        if (Directory.Exists(webRoot))
        {
            app.UseDefaultFiles(); app.UseStaticFiles();
            app.MapFallbackToFile("index.html");
        }
        return app;
    }
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JobAgent.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? AppContext.BaseDirectory;
    }
    private sealed record UiSession(string Id, string Csrf, DateTimeOffset Expires);
    private sealed record SessionRequest(string? Token);
    private sealed record DeleteWorkspaceRequest(long ExpectedRevision);
}
