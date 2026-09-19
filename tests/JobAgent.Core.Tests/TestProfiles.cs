using JobAgent.Core;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

internal static class TestProfiles
{
    public static readonly DateTimeOffset Now = new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    public static CandidateProfile Synthetic() => SyntheticData.Profile() with
    {
        LocalConfirmations =
        [
            new() { Field = ProfileField.FullName, ConfirmedAt = Now.AddDays(-1) },
            new() { Field = ProfileField.Email, ConfirmedAt = Now.AddDays(-1) },
            new() { Field = ProfileField.Salary, ConfirmedAt = Now.AddDays(-1) }
        ]
    };
}
