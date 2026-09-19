namespace JobAgent.Core.Answers;

public enum AnswerProposalReviewStatus { Proposed }
public enum AnswerProposalDisposition { RequiresReview, Abstained }

public sealed record AnswerProposal
{
    public int SchemaVersion { get; init; }
    public string SemanticKey { get; init; } = string.Empty;
    public string ProposedValue { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public List<string> EvidenceIds { get; init; } = [];
    public string Rationale { get; init; } = string.Empty;
    public AnswerProposalReviewStatus ReviewStatus { get; init; } = AnswerProposalReviewStatus.Proposed;
}

public sealed record AnswerProposalValidation
{
    public AnswerProposalDisposition Disposition { get; init; }
    public AnswerProposal? Proposal { get; init; }
    public string Reason { get; init; } = string.Empty;
}
