using JobAgent.Core.Models;

namespace JobAgent.Infrastructure.Workspace;

public sealed record WorkspaceModelPolicyUpdate(long ExpectedRevision, ModelProviderMode Mode,
    int MaxAnswerProposalOperationsPerApplication);
public sealed record WorkspaceApplicationProposalUsage(Guid ApplicationRef, int UsedOperations,
    int MaximumOperations, int RemainingOperations);
public sealed record WorkspaceModelPolicyView(long Revision, ModelProviderMode Mode, bool PaidApiEnabled,
    int MaxAnswerProposalOperationsPerApplication, IReadOnlyList<WorkspaceApplicationProposalUsage> Applications);

internal sealed partial record WorkspaceData
{
    public ModelRuntimePolicy? ModelPolicy { get; init; } = ModelRuntimePolicy.ZeroPaidDefault;
    public Dictionary<Guid, int>? AnswerProposalOperations { get; init; } = [];
}

public sealed partial class LocalWorkspace
{
    public Task<WorkspaceModelPolicyView> GetModelPolicyAsync() => WithWorkspace(async (revision, data) =>
    {
        await Task.CompletedTask;
        return ModelPolicyView(revision, data);
    });

    public Task<WorkspaceModelPolicyView> UpdateModelPolicyFromUiAsync(WorkspaceModelPolicyUpdate request) =>
        WithWorkspace(async (revision, data) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            CheckRevision(request.ExpectedRevision, revision);
            var policy = ModelRuntimePolicyRules.FromUi(request.Mode,
                request.MaxAnswerProposalOperationsPerApplication);
            var next = data with { ModelPolicy = policy };
            await SaveAsync(revision, next);
            return ModelPolicyView(revision + 1, next);
        });

    private static WorkspaceModelPolicyView ModelPolicyView(long revision, WorkspaceData data)
    {
        var policy = ModelRuntimePolicyRules.RequireValid(data.ModelPolicy);
        var usage = data.AnswerProposalOperations ?? throw new JobAgent.Core.Applications.PolicyException("ProviderPolicyInvalid");
        var applications = data.Applications
            .Select(application =>
            {
                var used = usage.GetValueOrDefault(application.Draft.Id);
                if (used < 0) throw new JobAgent.Core.Applications.PolicyException("ProviderPolicyInvalid");
                return new WorkspaceApplicationProposalUsage(application.Draft.Id, used,
                    policy.MaxAnswerProposalOperationsPerApplication,
                    Math.Max(0, policy.MaxAnswerProposalOperationsPerApplication - used));
            })
            .Where(item => item.UsedOperations > 0)
            .ToArray();
        return new(revision, policy.Mode, policy.PaidApiEnabled,
            policy.MaxAnswerProposalOperationsPerApplication, applications);
    }
}
