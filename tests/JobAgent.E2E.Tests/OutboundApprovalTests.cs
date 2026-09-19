using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Browser;
using Microsoft.Extensions.DependencyInjection;

namespace JobAgent.E2E.Tests;

public sealed class OutboundApprovalTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("approval-expired")]
    [InlineData("answers-expired")]
    public async Task ActualOutboundPostRechecksTimeAfterPageWait(string scenario)
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new AdjustableClock(now);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(BeforeSubmissionDispatch: async ct =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
        }));
        await site.StartAsync();
        var draft = BrowserPolicyTests.Draft(site.Urls.Single()) with
        { AnswersValidUntil = scenario == "answers-expired" ? now.AddMinutes(5) : null };
        var sharing = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.ShareData, "ui", now);
        var approval = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.Submit, "ui", now);
        // Independently test the package deadline even if a receipt claims a longer lifetime.
        if (scenario == "answers-expired") approval = approval with { ExpiresAt = now.AddMinutes(10) };
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()), clock);
        await browser.PrepareAsync(draft, sharing, BrowserPolicyTests.Resume);
        var store = site.Services.GetRequiredService<ReceiptStore>();
        var submission = browser.SubmitAsync(draft, approval);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(0, store.SubmissionPosts);
            clock.Set(now.AddMinutes(scenario == "approval-expired" ? 11 : scenario == "answers-expired" ? 6 : 1));
            release.TrySetResult();
            var evidence = await submission.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.Equal(scenario == "valid" ? 1 : 0, store.SubmissionPosts);
            if (scenario == "valid") Assert.NotNull(evidence);
            else Assert.Null(evidence);
        }
        finally { release.TrySetResult(); }
    }

    private sealed class AdjustableClock(DateTimeOffset initial) : TimeProvider
    {
        private long ticks = initial.UtcTicks;
        public void Set(DateTimeOffset value) => Interlocked.Exchange(ref ticks, value.UtcTicks);
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    }
}
