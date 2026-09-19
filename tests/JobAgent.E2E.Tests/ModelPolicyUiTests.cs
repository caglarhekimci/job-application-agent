using JobAgent.Web;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class ModelPolicyUiTests
{
    [Fact]
    public async Task UserCanSeeAndPersistTheLocalProposalOperationLimit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jobagent-model-policy-ui-" + Guid.NewGuid());
        var repo = HappyPathTests.FindRepo();
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DashboardOptions { DataDirectory = directory, WebRootPath = Path.Combine(repo, "web", "dist") };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync();
            page.SetDefaultTimeout(7000);
            var externalRequests = new List<string>();
            page.Request += (_, request) =>
            {
                if (!request.Url.StartsWith(app.Urls.Single(), StringComparison.Ordinal)) externalRequests.Add(request.Url);
            };

            await page.GotoAsync(app.Urls.Single() + "/#token=" + options.BootstrapToken);
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            var settings = page.GetByTestId("model-policy-settings");
            await Expect(settings).ToContainTextAsync("Codex modelini, token kullanımını veya hesap kotasını sınırlamaz");
            await Expect(settings.GetByLabel("Öneri sağlayıcısı")).ToHaveValueAsync("HostMediated");
            await Expect(settings.Locator("option[value=Api]")).ToHaveAttributeAsync("disabled", "");
            await settings.GetByLabel("Başvuru başına öneri işlemi sınırı").FillAsync("2");
            await settings.GetByLabel("Öneri sağlayıcısı").SelectOptionAsync("Fixture");
            await settings.GetByRole(AriaRole.Button, new() { Name = "Yerel öneri politikasını kaydet" }).ClickAsync();
            await Expect(settings.GetByRole(AriaRole.Status)).ToContainTextAsync("kaydedildi");

            await page.ReloadAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await Expect(settings.GetByLabel("Öneri sağlayıcısı")).ToHaveValueAsync("Fixture");
            await Expect(settings.GetByLabel("Başvuru başına öneri işlemi sınırı")).ToHaveValueAsync("2");
            Assert.Empty(externalRequests);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
