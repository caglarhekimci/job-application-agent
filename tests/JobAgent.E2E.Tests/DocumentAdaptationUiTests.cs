using System.Text;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using JobAgent.Web;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class DocumentAdaptationUiTests
{
    [Fact]
    public async Task UserComparesExactSourceApprovesBoundProposalAndDownloadsWithoutExternalRequests()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jobagent-document-adaptation-ui-" + Guid.NewGuid());
        var repo = HappyPathTests.FindRepo();
        Directory.CreateDirectory(directory);
        try
        {
            using (var workspace = new LocalWorkspace(Path.Combine(directory, "personal"), repo,
                       new WindowsDpapiPayloadProtector()))
            {
                var imported = await workspace.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n',
                    "Synthetic User", "Professional Experience: Built payment services in C#.",
                    "Personal Project: Created a weather application."))), "synthetic.txt");
                var profile = await workspace.ReviewProfileAsync(new()
                {
                    ExpectedRevision = imported.Revision,
                    FullName = "Synthetic User",
                    Email = "synthetic@example.invalid",
                    Experience =
                    [
                        new ReviewedExperience
                        {
                            SourceSpan = "line 2", Start = new DateOnly(2020, 1, 1), End = new DateOnly(2024, 1, 1),
                            Role = "Developer", Kind = ExperienceKind.Professional, Skills = ["C#"]
                        },
                        new ReviewedExperience
                        {
                            SourceSpan = "line 3", Start = new DateOnly(2019, 1, 1), End = new DateOnly(2019, 12, 1),
                            Role = "Maker", Kind = ExperienceKind.PersonalProject, Skills = ["TypeScript"]
                        }
                    ]
                });
                await workspace.ReviewJobAsync(new()
                {
                    ExpectedRevision = profile.Revision,
                    Employer = "Synthetic Employer",
                    Title = "Developer",
                    Text = "Build local tools with C#.",
                    SourceUrl = "https://example.invalid/jobs/42",
                    Requirements =
                    [
                        new JobRequirement
                        {
                            Id = "csharp", RequirementText = "C#", Type = RequirementType.Skill,
                            Importance = RequirementImportance.Mandatory, Skill = "C#"
                        }
                    ]
                });
            }
            var options = new DashboardOptions
            {
                DataDirectory = directory,
                WebRootPath = Path.Combine(repo, "web", "dist")
            };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear();
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync();
            page.SetDefaultTimeout(10_000);
            var externalRequests = new List<string>();
            page.Request += (_, request) =>
            {
                if (!request.Url.StartsWith(app.Urls.Single(), StringComparison.Ordinal))
                    externalRequests.Add(request.Url);
            };
            await page.GotoAsync(app.Urls.Single() + "/#token=" + options.BootstrapToken);
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Başvuru taslağı oluştur", Exact = true }).ClickAsync();
            var card = page.GetByTestId("personal-application");

            await card.GetByRole(AriaRole.Button, new() { Name = "CV ve ön yazı önerisi oluştur", Exact = true }).ClickAsync();
            var adaptation = card.GetByTestId("document-adaptation");
            await Expect(adaptation).ToContainTextAsync("Kaynak CV");
            await Expect(adaptation).ToContainTextAsync("Önerilen CV");
            await Expect(adaptation).ToContainTextAsync("Professional Experience: Built payment services in C#.");
            await Expect(adaptation).ToContainTextAsync("Profesyonel iş");
            await Expect(adaptation).ToContainTextAsync("Kişisel proje");
            var confirmation = adaptation.GetByLabel("Bu belge içeriklerini ve kaynaklarını inceledim.",
                new() { Exact = true });
            await Expect(confirmation).Not.ToBeCheckedAsync();
            await confirmation.CheckAsync();
            await adaptation.GetByRole(AriaRole.Button, new() { Name = "Öneriyi yeniden oluştur", Exact = true }).ClickAsync();
            await Expect(confirmation).Not.ToBeCheckedAsync();
            await confirmation.CheckAsync();
            await adaptation.GetByRole(AriaRole.Button, new() { Name = "Bu belge paketini onayla", Exact = true }).ClickAsync();
            await Expect(adaptation.GetByText("Belge paketi onaylandı", new() { Exact = true })).ToBeVisibleAsync();

            var download = await page.RunAndWaitForDownloadAsync(() =>
                adaptation.GetByRole(AriaRole.Link, new() { Name = "Uyarlanmış CV’yi indir", Exact = true }).ClickAsync());
            var downloadPath = await download.PathAsync();
            var content = await File.ReadAllTextAsync(downloadPath);
            Assert.Contains("Professional Experience: Built payment services in C#.", content, StringComparison.Ordinal);
            Assert.DoesNotContain("five years", content, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(externalRequests);
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(repo, "artifacts", "fr15-document-adaptation.png"), FullPage = true
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
