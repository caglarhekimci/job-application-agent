using JobAgent.FakeCareerSite;
using JobAgent.Web;
using Microsoft.Extensions.Hosting;
using JobAgent.Core.Evaluation;

if (args.Length == 3 && args[0] == "eval")
{
    var report = FixtureEvaluationRunner.RunFile(args[1], new EvaluationRunOptions(args[2],
        new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero)));
    Console.WriteLine(EvaluationJson.Serialize(report));
    return report.Results.All(result => result.ExpectedOutcomeMatched) ? 0 : 1;
}

if (args.Length != 1 || args[0] != "demo")
{
    Console.Error.WriteLine("Usage: JobAgent.Cli demo | eval <synthetic-dataset.json> <code-revision>");
    return 2;
}

// A process owns both fixture hosts and disposes them together on Ctrl+C.
if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html")))
{
    Console.Error.WriteLine("Dashboard assets are missing. Run scripts/bootstrap.ps1 and rebuild the package.");
    return 1;
}
var options = new DashboardOptions();
if (Environment.GetEnvironmentVariable("JOBAGENT_RUNTIME_DIR") is { Length: > 0 } runtimeDirectory)
    options = options with { DataDirectory = Path.GetFullPath(runtimeDirectory) };
Directory.CreateDirectory(options.DataDirectory);
FileStream runtimeLock;
try { runtimeLock = new FileStream(Path.Combine(options.DataDirectory, "runtime.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
catch (IOException)
{
    Console.Error.WriteLine("The demo is already running. Stop that instance before starting another.");
    return 1;
}
using (runtimeLock)
{
    await using var site = FakeCareerHost.Build("http://127.0.0.1:5179");
    await using var dashboard = DashboardHost.Build([], options);
    try
    {
        await site.StartAsync();
        await dashboard.StartAsync();
        Console.WriteLine("SYNTHETIC LOCAL DEMO — no employer receives any application.");
        Console.WriteLine("Open this private session link. Do not publish it:");
        Console.WriteLine("http://127.0.0.1:5178/#token=" + options.BootstrapToken);
        Console.WriteLine("Ctrl+C stops both local hosts. State is kept in your local application data folder.");
        await dashboard.WaitForShutdownAsync();
    }
    catch (IOException)
    {
        Console.Error.WriteLine("Ports 5178/5179 are unavailable. Close the previous demo or the app using these ports.");
        return 1;
    }
}
return 0;
