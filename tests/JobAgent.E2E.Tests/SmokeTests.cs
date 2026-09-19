using System.Net;
using JobAgent.FakeCareerSite;
using JobAgent.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JobAgent.E2E.Tests;

public sealed class SmokeTests
{
    [Fact]
    public async Task BothLocalHosts_ExposeHealth()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await using var dashboard = DashboardHost.Build([]);
        dashboard.Urls.Clear();
        dashboard.Urls.Add("http://127.0.0.1:0");
        await site.StartAsync();
        await dashboard.StartAsync();
        using var client = new HttpClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(site.Urls.Single() + "/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(dashboard.Urls.Single() + "/health")).StatusCode);
    }

    [Fact]
    public async Task FakeCareerSite_ReceivesOnlySyntheticApplication()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync(site.Urls.Single() + "/jobs/synthetic-dotnet");
        await Expect(page.GetByRole(AriaRole.Heading)).ToContainTextAsync("SYNTHETIC TEST SITE");
        await page.GetByLabel("Full name").FillAsync("Synthetic Candidate");
        await page.GetByLabel("Email").FillAsync("candidate@example.invalid");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();
        await page.GetByLabel("Expected monthly net salary (TRY)").FillAsync("100000");
        await page.GetByLabel("Professional C# years").FillAsync("3");
        await page.GetByLabel("Resume").SetInputFilesAsync(new FilePayload
        {
            Name = "synthetic-resume.txt",
            MimeType = "text/plain",
            Buffer = System.Text.Encoding.UTF8.GetBytes("SYNTHETIC CV\nSynthetic Candidate\n")
        });
        await page.GetByRole(AriaRole.Button, new() { Name = "Submit synthetic application" }).ClickAsync();
        await Expect(page.GetByTestId("receipt")).ToContainTextAsync("SYN-");
        Assert.Single(site.Services.GetRequiredService<ReceiptStore>().Receipts);
        var receipt = site.Services.GetRequiredService<ReceiptStore>().Receipts.Single();
        Assert.Equal("100000", receipt.Salary);
        Assert.Equal("3", receipt.ProfessionalYears);
        Assert.Equal("synthetic-resume.txt", receipt.FileName);
    }
}
