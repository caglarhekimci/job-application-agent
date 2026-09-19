using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Applications;
using JobAgent.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class ResumableSyntheticUiTests
{
    [Fact]
    public async Task ExpiredReviewedPreferences_AreReResolvedBeforeAnySharing()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-expiry-ui-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true));
            await site.StartAsync();
            await using var workflow = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo(), extendedControls: true);
            await workflow.LoadFixtureAsync();
            await workflow.ConfirmProfileFromUiAsync();
            var paused = await workflow.CreateSyntheticDraftForHostAsync();
            var expires = DateTimeOffset.UtcNow.AddSeconds(1);
            var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
            var record = await journal.GetAsync(paused.ApplicationRef);
            Assert.NotNull(record);

            await workflow.ReviewAnswersFromUiAsync(paused.ApplicationRef, record.Draft.PayloadHash(),
            [
                Reviewed("preference.work.mode", "remote", expires),
                Reviewed("preference.travel", "false", expires),
                Reviewed("preference.contact.method", "email", expires)
            ]);
            await Task.Delay(1200);
            await workflow.GetStateAsync();

            var refreshed = await workflow.GetHostStatusAsync(paused.ApplicationRef);
            Assert.Equal(ApplicationStatus.NeedsInput, refreshed.Status);
            Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MissingPreferences_AreReviewedWithFreshConfirmationBeforeOneVerifiedSubmission()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-resumable-ui-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true));
            await site.StartAsync();
            var options = new DashboardOptions
            {
                DataDirectory = root,
                CareerOrigin = site.Urls.Single(),
                ExtendedControls = true,
                WebRootPath = Path.Combine(HappyPathTests.FindRepo(), "web", "dist")
            };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1360, Height = 1000 } });
            var errors = new List<string>(); page.PageError += (_, error) => errors.Add(error);

            await page.GotoAsync(app.Urls.Single() + "/#token=" + options.BootstrapToken);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sentetik CV'yi içeri aktar" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Profili doğrula" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "İlanı değerlendir ve cevapları hazırla" }).ClickAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Bilgi gerekiyor");
            var store = site.Services.GetRequiredService<ReceiptStore>();
            Assert.Equal(0, store.SubmissionPosts);
            await page.ReloadAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Bilgi gerekiyor");
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
            var stateBody = await (await page.APIRequest.GetAsync(app.Urls.Single() + "/api/state")).TextAsync();
            Assert.Contains("questionReview", stateBody);
            Assert.Contains("preference.work.mode", stateBody);
            Assert.Contains("\"key\"", stateBody);

            await page.GetByLabel("Çalışma biçimi", new() { Exact = true }).SelectOptionAsync("remote");
            await page.GetByLabel("Seyahat tercihi", new() { Exact = true }).SelectOptionAsync("false");
            await page.GetByLabel("İletişim tercihi", new() { Exact = true }).SelectOptionAsync("phone");
            await page.GetByLabel("Cevap kapsamı", new() { Exact = true }).SelectOptionAsync("Application");
            var confirmation = page.GetByLabel("Bu cevapları, kapsamı ve geçerlilik süresini inceledim.", new() { Exact = true });
            await confirmation.CheckAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Kendi CV ve ilanım" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Sentetik test" }).ClickAsync();
            await Expect(confirmation).Not.ToBeCheckedAsync();

            await page.GetByLabel("Çalışma biçimi", new() { Exact = true }).SelectOptionAsync("remote");
            await page.GetByLabel("Seyahat tercihi", new() { Exact = true }).SelectOptionAsync("false");
            await page.GetByLabel("İletişim tercihi", new() { Exact = true }).SelectOptionAsync("phone");
            await page.GetByLabel("Cevap kapsamı", new() { Exact = true }).SelectOptionAsync("Application");
            await confirmation.CheckAsync();
            await page.GetByLabel("Çalışma biçimi", new() { Exact = true }).SelectOptionAsync("hybrid");
            await Expect(confirmation).Not.ToBeCheckedAsync();
            await page.GetByLabel("Çalışma biçimi", new() { Exact = true }).SelectOptionAsync("remote");
            await confirmation.CheckAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "İncelediğim cevapları bu kapsamda kaydet" }).ClickAsync();

            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Bilgi gerekiyor");
            await Expect(confirmation).Not.ToBeCheckedAsync();
            await page.GetByLabel("Tercih edilen arama zamanı", new() { Exact = true }).FillAsync("Weekday afternoons");
            await confirmation.CheckAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "İncelediğim cevapları bu kapsamda kaydet" }).ClickAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Veri paylaşım onayı bekleniyor");
            await Expect(confirmation).Not.ToBeCheckedAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Veri paylaşımını onayla ve formu doldur" }).ClickAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Son gönderim onayı bekleniyor", new() { Timeout = 30000 });
            Assert.Equal(0, store.SubmissionPosts);
            await page.GetByRole(AriaRole.Button, new() { Name = "Bu sentetik başvuruyu gönder" }).ClickAsync();
            await Expect(page.GetByTestId("application-status")).ToContainTextAsync("Gönderim teyit edildi", new() { Timeout = 30000 });

            var receipt = Assert.Single(store.Receipts);
            Assert.Equal("remote", receipt.WorkMode);
            Assert.False(receipt.Travel);
            Assert.Equal("phone", receipt.ContactMethod);
            Assert.Equal("Weekday afternoons", receipt.ContactWindow);
            Assert.Equal(1, store.SubmissionPosts);
            Assert.Empty(errors);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static ReviewedAnswerMemoryUpdate Reviewed(string key, string answer, DateTimeOffset expiresAt) => new()
    {
        SemanticKey = key,
        Language = "en",
        Answer = answer,
        Scope = JobAgent.Core.Profiles.AnswerScopeType.Application,
        ExpiresAt = expiresAt
    };
}
