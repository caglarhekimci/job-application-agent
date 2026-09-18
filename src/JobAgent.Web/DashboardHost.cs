namespace JobAgent.Web;

public static class DashboardHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://127.0.0.1:5178");
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", mode = "Fixture" }));
        return app;
    }
}
