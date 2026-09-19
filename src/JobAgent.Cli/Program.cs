using JobAgent.FakeCareerSite;
using JobAgent.Web;
using Microsoft.Extensions.Hosting;
using JobAgent.Core.Evaluation;
using JobAgent.Infrastructure.Bridge;

if (args.Length == 3 && args[0] == "eval-expanded")
{
    var report = ExpandedFixtureEvaluationRunner.RunFile(args[1], new EvaluationRunOptions(args[2],
        new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero)));
    Console.WriteLine(ExpandedEvaluationJson.Serialize(report));
    return report.Results.All(result => result.ExpectedOutcomeMatched) ? 0 : 1;
}

if (args.Length == 3 && args[0] == "eval")
{
    var report = FixtureEvaluationRunner.RunFile(args[1], new EvaluationRunOptions(args[2],
        new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero)));
    Console.WriteLine(EvaluationJson.Serialize(report));
    return report.Results.All(result => result.ExpectedOutcomeMatched) ? 0 : 1;
}

if (args.Length != 1 || args[0] != "demo")
{
    Console.Error.WriteLine("Usage: JobAgent.Cli demo | eval|eval-expanded <synthetic-dataset.json> <code-revision>");
    return 2;
}

// A process owns both fixture hosts and disposes them together on Ctrl+C.
if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html")))
{
    Console.Error.WriteLine("Dashboard assets are missing. Run scripts/bootstrap.ps1 and rebuild the package.");
    return 1;
}
var options = new DashboardOptions
{
    EnableSyntheticCommands = Environment.GetEnvironmentVariable("JOBAGENT_ENABLE_SYNTHETIC_COMMANDS") == "1",
    EnableLocalCommands = Environment.GetEnvironmentVariable("JOBAGENT_ENABLE_LOCAL_COMMANDS") == "1",
    ExtendedControls = Environment.GetEnvironmentVariable("JOBAGENT_EXTENDED_FORM") == "1"
};
if (options.EnableSyntheticCommands && options.EnableLocalCommands)
{
    Console.Error.WriteLine("Choose either JOBAGENT_ENABLE_LOCAL_COMMANDS or JOBAGENT_ENABLE_SYNTHETIC_COMMANDS.");
    return 2;
}
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
    // Owning the runtime lock allows removal of a crashed instance's registration,
    // including when this launch explicitly keeps model commands disabled.
    File.Delete(Path.Combine(options.DataDirectory, HostBridgeRegistrationStore.FileName));
    await using var site = FakeCareerHost.Build("http://127.0.0.1:5179", new(ExtendedControls: options.ExtendedControls));
    await using var dashboard = DashboardHost.Build([], options);
    try
    {
        await site.StartAsync();
        await dashboard.StartAsync();
        if (options.EnableSyntheticCommands || options.EnableLocalCommands)
            await HostBridgeRegistrationStore.WriteAsync(options.DataDirectory,
                new(new Uri("http://127.0.0.1:5178/"), options.BridgeToken, options.BridgeInstanceId, options.BridgeExpiresAt));
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
    finally
    {
        if (options.EnableSyntheticCommands || options.EnableLocalCommands)
            await HostBridgeRegistrationStore.RemoveIfOwnedAsync(options.DataDirectory, options.BridgeInstanceId);
    }
}
return 0;
