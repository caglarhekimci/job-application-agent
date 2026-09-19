using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using JobAgent.Web;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class PersonalApplicationUiTests
{
    [Fact]
    public async Task UserReviewsMissingAnswerAndResumesSameApplicationInBrowser()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jobagent-application-ui-" + Guid.NewGuid());
        var repo = HappyPathTests.FindRepo();
        Directory.CreateDirectory(directory);
        try
        {
            using (var workspace = new LocalWorkspace(Path.Combine(directory, "personal"), repo, new WindowsDpapiPayloadProtector()))
            {
                var imported = await workspace.ImportAsync(new MemoryStream("Synthetic User\nProfessional C# developer 2020-2024"u8.ToArray()), "synthetic.txt");
                var profile = await workspace.ReviewProfileAsync(new()
                {
                    ExpectedRevision = imported.Revision,
                    FullName = "Synthetic User",
                    Email = "synthetic@example.invalid",
                    Experience = [new() { SourceSpan = "line 2", Start = new(2020, 1, 1), End = new(2024, 1, 1), Role = "Developer", Skills = ["C#"] }]
                });
                await workspace.ReviewJobAsync(new()
                {
                    ExpectedRevision = profile.Revision,
                    Employer = "Synthetic Employer",
                    Title = "Developer",
                    Text = "Build local C# tools."
                });
            }
            var options = new DashboardOptions { DataDirectory = directory, WebRootPath = Path.Combine(repo, "web", "dist") };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync();
            page.SetDefaultTimeout(7000);
            var requests = new List<string>();
            page.Request += (_, request) => { if (!request.Url.StartsWith(app.Urls.Single(), StringComparison.Ordinal)) requests.Add(request.Url); };
            await page.GotoAsync(app.Urls.Single() + "/#token=" + options.BootstrapToken);
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Başvuru taslağı oluştur", Exact = true }).ClickAsync();
            var card = page.GetByTestId("personal-application");
            await Expect(card.GetByTestId("application-status")).ToHaveTextAsync("Bilgi bekliyor");
            var id = await card.GetAttributeAsync("data-application-id");
            await card.GetByLabel("Yanıtlanacak soru", new() { Exact = true }).SelectOptionAsync("motivation");
            await card.GetByLabel("Başvuru cevabınız", new() { Exact = true }).FillAsync("C# ile yerel araçlar geliştirmek istiyorum.");
            await card.GetByLabel("Başvuru cevabının kapsamı", new() { Exact = true }).SelectOptionAsync("Application");
            await card.GetByLabel("Cevabı ve kullanım kapsamını inceledim.", new() { Exact = true }).CheckAsync();
            await card.GetByRole(AriaRole.Button, new() { Name = "Cevabı kaydet ve devam et", Exact = true }).ClickAsync();
            await Expect(card.GetByTestId("application-status")).ToHaveTextAsync("Yerel incelemeye hazır");
            await page.ReloadAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await Expect(card).ToHaveAttributeAsync("data-application-id", id!);
            await Expect(card).ToContainTextAsync("C# ile yerel araçlar geliştirmek istiyorum.");
            await card.GetByLabel("Yeni sorunun metni", new() { Exact = true }).FillAsync("Uzaktan çalışma tercihiniz?");
            await card.GetByRole(AriaRole.Button, new() { Name = "Soruyu başvuruya ekle", Exact = true }).ClickAsync();
            await Expect(card.GetByTestId("application-status")).ToHaveTextAsync("Bilgi bekliyor");
            await Expect(card.GetByLabel("Cevabı ve kullanım kapsamını inceledim.", new() { Exact = true })).Not.ToBeCheckedAsync();
            Assert.Empty(requests);
            await page.ScreenshotAsync(new() { Path = Path.Combine(repo, "artifacts", "personal-application.png"), FullPage = true });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
