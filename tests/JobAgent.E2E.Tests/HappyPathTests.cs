using JobAgent.FakeCareerSite;
using JobAgent.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class HappyPathTests
{
    internal static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "JobAgent.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }

    [Fact]
    public async Task SyntheticCandidate_ToVerifiedReceipt()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-ui-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
            await site.StartAsync();
            var options = new DashboardOptions
            {
                DataDirectory = root,
                CareerOrigin = site.Urls.Single(),
                WebRootPath = Path.Combine(FindRepo(), "web", "dist")
            };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1360, Height = 1000 } });
            var errors = new List<string>();
            page.PageError += (_, error) => errors.Add(error);
            await page.GotoAsync(app.Urls.Single() + "/#token=" + options.BootstrapToken);
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Başvuru Atölyesi" })).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Sentetik CV'yi içeri aktar" }).ClickAsync();
            await Expect(page.GetByText("Synthetic Candidate", new() { Exact = true }).First).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Profili doğrula" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "İlanı değerlendir ve cevapları hazırla" }).ClickAsync();
            await Expect(page.GetByTestId("salary-answer")).ToContainTextAsync("100000");
            var store = site.Services.GetRequiredService<ReceiptStore>();
            Assert.Equal(0, store.Requests);
            await page.GetByRole(AriaRole.Button, new() { Name = "Veri paylaşımını onayla ve formu doldur" }).ClickAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Son gönderim onayı bekleniyor", new() { Timeout = 30000 });
            Assert.Empty(store.Receipts);
            var publicState = await (await page.APIRequest.GetAsync(app.Urls.Single() + "/api/state")).TextAsync();
            Assert.DoesNotContain("userSessionId", publicState);
            Assert.DoesNotContain("\"sharing\"", publicState);
            foreach (var cookie in await page.Context.CookiesAsync())
                if (cookie.Name == "jobagent-session") Assert.DoesNotContain(cookie.Value, publicState);
            var artifacts = Path.Combine(FindRepo(), "artifacts", "screenshots");
            Directory.CreateDirectory(artifacts);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "review.png"), FullPage = true });
            await page.GetByRole(AriaRole.Button, new() { Name = "Bu sentetik başvuruyu gönder" }).ClickAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Gönderim teyit edildi", new() { Timeout = 30000 });
            await Expect(page.GetByTestId("receipt-id")).ToContainTextAsync("SYN-");
            Assert.Single(store.Receipts);
            Assert.Equal("100000", store.Receipts.Single().Salary);
            Assert.Equal("3", store.Receipts.Single().ProfessionalYears);
            await page.ReloadAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Gönderim teyit edildi");
            Assert.Single(store.Receipts);
            Assert.Empty(errors);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "receipt.png"), FullPage = true });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }
}
