using System.Text.Json;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Bridge;
using JobAgent.Mcp;
using JobAgent.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using ModelContextProtocol.Client;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class HostUiWorkflowTests
{
    [Fact]
    public async Task OptInStdioCommandsRequireSeparateBrowserUiApprovalThenSubmitOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-host-ui-" + Guid.NewGuid());
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
            await site.StartAsync();
            var options = new DashboardOptions
            {
                DataDirectory = root,
                CareerOrigin = site.Urls.Single(),
                EnableSyntheticCommands = true,
                WebRootPath = Path.Combine(HappyPathTests.FindRepo(), "web", "dist")
            };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            await HostBridgeRegistrationStore.WriteAsync(root, new(new Uri(app.Urls.Single()),
                options.BridgeToken, options.BridgeInstanceId, options.BridgeExpiresAt));
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")!;
            var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
            environment["DOTNET_ROOT"] = dotnetRoot;
            environment["JOBAGENT_RUNTIME_DIR"] = root;
            environment["JOBAGENT_ENABLE_SYNTHETIC_COMMANDS"] = "1";
            await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Name = "synthetic-host-ui-test",
                Command = Path.Combine(dotnetRoot, "dotnet.exe"),
                Arguments = [typeof(ReadOnlyTools).Assembly.Location],
                InheritEnvironmentVariables = false,
                EnvironmentVariables = environment,
                ShutdownTimeout = TimeSpan.FromSeconds(2)
            }));
            var tools = await client.ListToolsAsync();
            Assert.Equal(6, tools.Count);
            var executeTool = Assert.Single(tools, t => t.Name == "application_execute_approved");
            Assert.False(executeTool.ProtocolTool.Annotations!.ReadOnlyHint);
            Assert.True(executeTool.ProtocolTool.Annotations.DestructiveHint);
            Assert.All(tools, t => Assert.False(t.ProtocolTool.Annotations!.OpenWorldHint));
            Assert.DoesNotContain(tools, t => t.Name.Contains("grant") || t.Name.Contains("approve_from"));
            var unconfirmed = await Assert.ThrowsAsync<McpProtocolException>(() => client.CallToolAsync("application_create_draft").AsTask());
            Assert.Contains("ProfileReviewRequired", unconfirmed.Message);

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1360, Height = 1000 } });
            var pageErrors = new List<string>(); page.PageError += (_, e) => pageErrors.Add(e);
            await page.GotoAsync(app.Urls.Single() + "/#token=" + options.BootstrapToken);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sentetik CV'yi içeri aktar" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Profili doğrula" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "İlanı değerlendir ve cevapları hazırla" })).ToBeVisibleAsync();
            var draft = await client.CallToolAsync("application_create_draft");
            Assert.NotEqual(true, draft.IsError);
            var reference = draft.StructuredContent!.Value.GetProperty("applicationRef").GetString()!;
            var arguments = new Dictionary<string, object?> { ["applicationRef"] = reference };
            Assert.NotEqual(true, (await client.CallToolAsync("application_prepare_review", arguments)).IsError);
            var illegal = new Dictionary<string, object?>(arguments) { ["approval"] = true };
            Assert.True((await client.CallToolAsync("application_execute_approved", illegal)).IsError);
            // No reload: the UI must discover a draft created by the external host.
            await Expect(page.GetByTestId("salary-answer")).ToContainTextAsync("100000", new() { Timeout = 10000 });
            var store = site.Services.GetRequiredService<ReceiptStore>();
            Assert.Equal(0, store.SubmissionPosts);
            await page.GetByRole(AriaRole.Button, new() { Name = "Veri paylaşımını onayla ve formu doldur" }).ClickAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Son gönderim onayı bekleniyor", new() { Timeout = 30000 });
            var premature = await Assert.ThrowsAsync<McpProtocolException>(() => client.CallToolAsync("application_execute_approved", arguments).AsTask());
            Assert.Contains("ConsentRequired", premature.Message);
            Assert.Equal(0, store.SubmissionPosts);
            await page.GetByRole(AriaRole.Button, new() { Name = "Codex’in bu sentetik başvuruyu göndermesine izin ver" }).ClickAsync();
            await Expect(page.GetByText("Gönderim izni verildi. Codex oturumunuza dönüp işlemi devam ettirin.")).ToBeVisibleAsync();
            Assert.Equal(0, store.SubmissionPosts); // Approval alone cannot click Submit.
            var submitted = await client.CallToolAsync("application_execute_approved", arguments);
            Assert.NotEqual(true, submitted.IsError);
            Assert.NotEqual(true, (await client.CallToolAsync("application_execute_approved", arguments)).IsError);
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Gönderim teyit edildi", new() { Timeout = 10000 });
            var receipt = Assert.Single(store.Receipts);
            Assert.Equal(1, store.SubmissionPosts); Assert.Equal("100000", receipt.Salary); Assert.Equal("3", receipt.ProfessionalYears);
            await Expect(page.GetByTestId("receipt-id")).ToContainTextAsync(receipt.Id);
            var status = await client.CallToolAsync("application_get_status", arguments);
            Assert.Contains("SubmittedVerified", status.StructuredContent!.Value.GetRawText());
            Assert.DoesNotContain(options.BridgeToken, JsonSerializer.Serialize(submitted));
            Assert.DoesNotContain(options.BootstrapToken, JsonSerializer.Serialize(submitted));
            Assert.Empty(pageErrors);
            var screenshots = Path.Combine(HappyPathTests.FindRepo(), "artifacts", "screenshots");
            Directory.CreateDirectory(screenshots);
            await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "host-approved-receipt.png"), FullPage = true });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
