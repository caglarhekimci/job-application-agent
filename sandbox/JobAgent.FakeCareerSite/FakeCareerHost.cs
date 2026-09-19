namespace JobAgent.FakeCareerSite;

public static class FakeCareerHost
{
    public static WebApplication Build(string url, FakeCareerOptions? options = null)
    {
        options ??= new();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(url);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ReceiptStore>();
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 2_100_000);
        var app = builder.Build();
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            context.RequestServices.GetRequiredService<ReceiptStore>().RecordRequest();
            if (context.Request.Host.Host != "127.0.0.1")
            { context.Response.StatusCode = 403; return; }
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next();
        });
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", synthetic = true }));
        var html = options.AutoSubmitOnInput ? FormHtml.Replace("document.querySelector('#next').onclick=", "document.querySelector('input[name=name]').addEventListener('input',()=>fetch('/api/applications',{method:'POST',body:new FormData(document.querySelector('#application'))})); document.querySelector('#next').onclick=", StringComparison.Ordinal) : FormHtml;
        if (options.WebSocketTarget is { } socketTarget)
            html = html.Replace("</script>", "new WebSocket(" + System.Text.Json.JsonSerializer.Serialize(socketTarget) + ");</script>", StringComparison.Ordinal);
        if (options.TamperSalaryOnSubmit)
            html = html.Replace("body:new FormData(e.target)", "body:(()=>{const f=new FormData(e.target);f.set('salary','1');return f;})()", StringComparison.Ordinal);
        app.MapGet("/socket", async (HttpContext context) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            { using var socket = await context.WebSockets.AcceptWebSocketAsync(); }
        });
        app.MapGet("/jobs/synthetic-dotnet", () => options.RedirectTarget is { } target
            ? Results.Redirect(target)
            : Results.Content(options.AddRequiredQuestion ? html.Replace("<fieldset id=\"questions\" hidden>",
                "<fieldset id=\"questions\" hidden><label>New required question<input name=\"newQuestion\" required></label>", StringComparison.Ordinal)
                : html, "text/html; charset=utf-8"));
        app.MapGet("/api/receipts/{key}", (string key, ReceiptStore store) =>
            store.Find(key) is { } receipt ? Results.Ok(receipt) : Results.NotFound());
        app.MapPost("/api/applications", async (HttpRequest request, ReceiptStore store) =>
        {
            store.RecordSubmission();
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "FormRequired" });
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("resume");
            if (form["synthetic"] != "true" || form["name"] != "Synthetic Candidate"
                || form["email"] != "candidate@example.invalid" || file is null
                || file.Length is <= 0 or > 2_000_000 || !Guid.TryParse(form["applicationKey"], out _))
                return Results.BadRequest(new { error = "OnlySyntheticApplicationsAllowed" });
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            if (!System.Text.Encoding.UTF8.GetString(bytes).StartsWith("SYNTHETIC CV", StringComparison.Ordinal))
                return Results.BadRequest(new { error = "SyntheticResumeRequired" });
            if (!decimal.TryParse(form["salary"], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var salary) || salary <= 0
                || !int.TryParse(form["years"], out var years) || years < 0)
                return Results.BadRequest(new { error = "InvalidAnswer" });
            var receipt = new SyntheticReceipt("SYN-" + Guid.NewGuid().ToString("N"),
                form["applicationKey"].ToString(), form["salary"].ToString(), form["years"].ToString(),
                Path.GetFileName(file.FileName), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
            var saved = store.Add(receipt);
            if (options.DropSubmissionResponse)
            { request.HttpContext.Abort(); return Results.Empty; }
            if (options.MalformedReceipt is { } malformed)
                return Results.Content(malformed, "application/json");
            return Results.Ok(saved);
        });
        return app;
    }

    private const string FormHtml = """
        <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Synthetic career site</title>
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{font:18px system-ui;background:#f3f6f7;color:#172b32;max-width:720px;margin:48px auto;padding:24px}label{display:block;margin:22px 0}input{display:block;padding:12px;font:inherit;width:90%}button{padding:14px 20px;font:inherit;background:#145b52;color:white;border:0;border-radius:8px}fieldset{border:0;padding:0}[hidden]{display:none!important}.banner{background:#fff2bf;padding:16px}</style></head>
        <body><h1>SYNTHETIC TEST SITE</h1><p class="banner">No employer receives this. Fictional candidate and job only.</p>
        <p>Synthetic Labs · .NET Developer · Remote</p>
        <form id="application"><input type="hidden" name="synthetic" value="true"><input id="key" type="hidden" name="applicationKey">
        <fieldset id="contact"><legend>1. Contact details</legend>
        <label>Full name<input name="name" required></label><label>Email<input name="email" type="email" required></label>
        <button id="next" type="button">Continue</button></fieldset>
        <fieldset id="questions" hidden><legend>2. Questions and resume</legend>
        <label>Expected monthly net salary (TRY)<input name="salary" type="number" min="1" required></label>
        <label>Professional C# years<input name="years" type="number" min="0" required></label>
        <label>Resume<input name="resume" type="file" accept=".txt" required></label>
        <button type="submit">Submit synthetic application</button></fieldset></form>
        <p id="error" role="alert"></p><p data-testid="receipt" id="receipt" aria-live="polite"></p>
        <script>
        document.querySelector('#key').value = new URLSearchParams(location.search).get('applicationKey') || crypto.randomUUID();
        document.querySelector('#next').onclick=()=>{document.querySelector('#contact').hidden=true;document.querySelector('#questions').hidden=false;};
        document.querySelector('#application').onsubmit=async e=>{e.preventDefault();
          try{const response=await fetch('/api/applications',{method:'POST',body:new FormData(e.target)});
            const data=await response.json();if(!response.ok)throw new Error(data.error);
            document.querySelector('#receipt').textContent='Application received: '+data.id;
            document.querySelector('#receipt').dataset.receiptId=data.id;
            e.target.hidden=true;
          }catch(error){document.querySelector('#error').textContent='Result not confirmed: '+error.message;}
        };
        </script></body></html>
        """;
}
