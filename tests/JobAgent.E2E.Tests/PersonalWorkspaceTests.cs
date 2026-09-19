using JobAgent.Web;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class PersonalWorkspaceTests
{
    [Fact]
    public async Task LocalUploadReviewEvaluationAnswerExportAndDelete()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-personal-ui-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var options = new DashboardOptions
            {
                DataDirectory = root,
                WebRootPath = Path.Combine(HappyPathTests.FindRepo(), "web", "dist")
            };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync();
            var unexpectedRequests = new List<string>();
            page.Request += (_, r) => { if (!r.Url.StartsWith(app.Urls.Single(), StringComparison.Ordinal)) unexpectedRequests.Add(r.Url); };
            await page.GotoAsync(app.Urls.Single() + "/#token=" + options.BootstrapToken);
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await page.GetByLabel("CV dosyanız", new() { Exact = true }).SetInputFilesAsync(new FilePayload
            { Name = "synthetic-personal.txt", MimeType = "text/plain", Buffer = "Synthetic Local Candidate\nProfessional Experience: C# developer 2020-2024"u8.ToArray() });
            await page.GetByLabel("Ad soyad", new() { Exact = true }).FillAsync("Synthetic Local Candidate");
            await page.GetByRole(AriaRole.Button, new() { Name = "Sentetik test", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await Expect(page.GetByLabel("Ad soyad", new() { Exact = true })).ToHaveValueAsync("Synthetic Local Candidate");
            await page.GetByLabel("E-posta", new() { Exact = true }).FillAsync("local@example.invalid");
            await page.GetByLabel("Maaş beklentisi · aylık net TRY").FillAsync("100000");
            await page.GetByRole(AriaRole.Button, new() { Name = "Deneyim ekle" }).ClickAsync();
            await page.GetByLabel("CV’deki kaynak bölüm").SelectOptionAsync("line 2");
            await page.GetByLabel("Görev", new() { Exact = true }).FillAsync("Developer");
            await page.GetByLabel("Başlangıç", new() { Exact = true }).FillAsync("2020-01-01");
            await page.GetByLabel("Bitiş · sürüyorsa boş bırakın").FillAsync("2024-01-01");
            await page.GetByLabel("Beceriler · virgülle ayırın").FillAsync("C#");
            await page.GetByRole(AriaRole.Button, new() { Name = "Bu profil bilgilerini doğrula ve kaydet" }).ClickAsync();
            await Expect(page.GetByText("Profil kaydedildi", new() { Exact = false })).ToBeVisibleAsync();
            await page.GetByLabel("İşveren", new() { Exact = true }).FillAsync("Synthetic Employer");
            await page.GetByLabel("İlan başlığı", new() { Exact = true }).FillAsync("C# developer");
            await page.GetByLabel("İlan metni", new() { Exact = true }).FillAsync("At least 2 years of professional C# experience.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Koşul ekle" }).ClickAsync();
            await page.GetByLabel("İlandaki koşul").FillAsync("At least 2 years of professional C# experience.");
            await page.GetByLabel("Beceri", new() { Exact = true }).FillAsync("C#");
            await page.GetByLabel("En az kaç yıl").FillAsync("2");
            await page.GetByRole(AriaRole.Button, new() { Name = "İlan koşullarını doğrula ve değerlendir" }).ClickAsync();
            await Expect(page.GetByTestId("local-evaluation")).ToContainTextAsync("koşullar eşleşiyor");
            await page.GetByLabel("Soru", new() { Exact = true }).SelectOptionAsync("salary.expected.monthly.net.TRY");
            await page.GetByRole(AriaRole.Button, new() { Name = "Cevabı kontrol et" }).ClickAsync();
            await Expect(page.GetByTestId("local-answer")).ToContainTextAsync("100000");
            await page.GetByLabel("Hatırlanacak soru", new() { Exact = true }).SelectOptionAsync("motivation");
            await page.GetByLabel("İncelediğiniz cevap", new() { Exact = true }).FillAsync("Bu şirketin yerel araçlarını geliştirmek istiyorum.");
            await page.GetByLabel("Cevabın kapsamı", new() { Exact = true }).SelectOptionAsync("Company");
            await page.GetByLabel("Cevabı ve seçtiğim kapsamı inceledim.", new() { Exact = true }).CheckAsync();
            await page.GetByLabel("İlan başlığı", new() { Exact = true }).FillAsync("Senior C# developer");
            await page.GetByRole(AriaRole.Button, new() { Name = "İlan koşullarını doğrula ve değerlendir" }).ClickAsync();
            await Expect(page.GetByText("İşlem sürüyor…", new() { Exact = true })).ToHaveCountAsync(0);
            await Expect(page.GetByLabel("Cevabı ve seçtiğim kapsamı inceledim.", new() { Exact = true })).Not.ToBeCheckedAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Cevabı bu kapsamda hatırla", Exact = true })).ToBeDisabledAsync();
            await page.GetByLabel("Cevabı ve seçtiğim kapsamı inceledim.", new() { Exact = true }).CheckAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Cevabı bu kapsamda hatırla", Exact = true }).ClickAsync();
            await Expect(page.GetByTestId("answer-memory")).ToContainTextAsync("Synthetic Employer");
            await page.GetByRole(AriaRole.Button, new() { Name = "Bu ilan için kontrol et", Exact = true }).ClickAsync();
            await Expect(page.GetByTestId("local-answer")).ToContainTextAsync("Bu şirketin yerel araçlarını geliştirmek istiyorum.");
            await page.ReloadAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await Expect(page.GetByTestId("answer-memory")).ToContainTextAsync("Bu şirketin yerel araçlarını geliştirmek istiyorum.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Bu cevabın kullanımını kaldır", Exact = true }).ClickAsync();
            await Expect(page.GetByTestId("answer-memory")).ToContainTextAsync("Henüz hatırlanan cevap yok");
            var screenshot = Path.Combine(HappyPathTests.FindRepo(), "artifacts", "screenshots", "personal-workspace.png");
            Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
            await page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
            var export = await page.APIRequest.GetAsync(app.Urls.Single() + "/api/workspace/export");
            Assert.True(export.Ok); Assert.Contains("Synthetic Local Candidate", await export.TextAsync());
            await page.ReloadAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await Expect(page.GetByLabel("Ad soyad", new() { Exact = true })).ToHaveValueAsync("Synthetic Local Candidate");
            await Expect(page.GetByLabel("Görev", new() { Exact = true })).ToHaveValueAsync("Developer");
            await page.GetByLabel("CV, profil, ilan ve önceki sürümlerin bu çalışma alanından silinmesini istiyorum.").CheckAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Yerel verilerimi sil" }).ClickAsync();
            await Expect(page.GetByText("synthetic-personal.txt", new() { Exact = true })).ToHaveCountAsync(0);
            Assert.Empty(unexpectedRequests);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }
}
