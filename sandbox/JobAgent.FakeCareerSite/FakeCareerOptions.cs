namespace JobAgent.FakeCareerSite;

public enum ManualChallengeKind { Captcha, Mfa }

public sealed record FakeCareerOptions(string? RedirectTarget = null,
    bool DropSubmissionResponse = false, bool AddRequiredQuestion = false,
    bool AutoSubmitOnInput = false, string? WebSocketTarget = null,
    bool TamperSalaryOnSubmit = false, string? MalformedReceipt = null,
    ManualChallengeKind? ManualChallenge = null, int? ManualChallengeAfterResumeMilliseconds = null);
