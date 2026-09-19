using JobAgent.Core.Applications;

namespace JobAgent.Core.Models;

public enum ModelProviderMode
{
    Fixture,
    HostMediated,
    Api
}

public sealed record ModelRuntimePolicy
{
    public const int DefaultMaximumOperations = 4;
    public const int HardMaximumOperations = 10;

    public ModelProviderMode Mode { get; init; } = ModelProviderMode.HostMediated;
    public int MaxAnswerProposalOperationsPerApplication { get; init; } = DefaultMaximumOperations;
    public bool PaidApiEnabled => false;

    public static ModelRuntimePolicy ZeroPaidDefault => new();
}

public static class ModelRuntimePolicyRules
{
    public static ModelRuntimePolicy RequireValid(ModelRuntimePolicy? policy)
    {
        if (policy is null || !Enum.IsDefined(policy.Mode) ||
            policy.MaxAnswerProposalOperationsPerApplication is < 1 or > ModelRuntimePolicy.HardMaximumOperations)
            throw new PolicyException("ProviderPolicyInvalid");
        return policy;
    }

    public static void RequireHostProposal(ModelRuntimePolicy? policy)
    {
        var valid = RequireValid(policy);
        if (valid.Mode == ModelProviderMode.Api) throw new PolicyException("PaidApiDisabled");
        if (valid.Mode != ModelProviderMode.HostMediated) throw new PolicyException("ProviderModeDisabled");
    }

    public static void RequireAnswerProposal(ModelRuntimePolicy? policy, int usedOperations)
    {
        RequireHostProposal(policy);
        if (usedOperations < 0) throw new PolicyException("ProviderPolicyInvalid");
        if (usedOperations >= policy!.MaxAnswerProposalOperationsPerApplication)
            throw new PolicyException("BudgetExceeded");
    }

    public static ModelRuntimePolicy FromUi(ModelProviderMode mode, int maximumOperations)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Unknown provider mode.", nameof(mode));
        if (maximumOperations is < 1 or > ModelRuntimePolicy.HardMaximumOperations)
            throw new ArgumentOutOfRangeException(nameof(maximumOperations));
        if (mode == ModelProviderMode.Api) throw new PolicyException("PaidApiDisabled");
        return new() { Mode = mode, MaxAnswerProposalOperationsPerApplication = maximumOperations };
    }
}
