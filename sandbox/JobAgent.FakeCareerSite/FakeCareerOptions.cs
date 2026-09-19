namespace JobAgent.FakeCareerSite;

public sealed record FakeCareerOptions(string? RedirectTarget = null,
    bool DropSubmissionResponse = false, bool AddRequiredQuestion = false,
    bool AutoSubmitOnInput = false, string? WebSocketTarget = null,
    bool TamperSalaryOnSubmit = false, string? MalformedReceipt = null);
