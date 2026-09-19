using JobAgent.Web;

var options = new DashboardOptions();
var app = DashboardHost.Build(args, options);
Console.WriteLine("Local synthetic demo. Open this private session link in your browser:");
Console.WriteLine("http://127.0.0.1:5178/#token=" + options.BootstrapToken);
await app.RunAsync();
