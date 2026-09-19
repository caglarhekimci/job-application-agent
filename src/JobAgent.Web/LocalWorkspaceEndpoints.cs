using JobAgent.Infrastructure.Workspace;

namespace JobAgent.Web;

public static class LocalWorkspaceEndpoints
{
    public static void Map(WebApplication app)
    {
        // Every route inherits DashboardHost's local cookie + Origin + CSRF checks.
        app.MapGet("/api/workspace/application-panel", (LocalWorkspace w) => w.GetApplicationPanelAsync());
        app.MapPost("/api/workspace/applications", (LocalWorkspace w, WorkspaceApplicationCreate request) =>
            w.CreateApplicationFromUiAsync(request));
        app.MapPost("/api/workspace/applications/answers", (LocalWorkspace w, WorkspaceApplicationAnswerReview request) =>
            w.ReviewApplicationAnswersFromUiAsync(request));
        app.MapPost("/api/workspace/applications/questions", (LocalWorkspace w, WorkspaceApplicationQuestionReview request) =>
            w.UpdateApplicationQuestionsFromUiAsync(request));
        app.MapPost("/api/workspace/applications/{id:guid}/refresh", (LocalWorkspace w, Guid id, WorkspaceRevisionRequest request) =>
            w.RefreshApplicationSourcesFromUiAsync(id, request.ExpectedRevision));
        app.MapPost("/api/workspace/applications/{id:guid}/cancel", (LocalWorkspace w, Guid id, WorkspaceRevisionRequest request) =>
            w.CancelApplicationFromUiAsync(id, request.ExpectedRevision));
        app.MapPost("/api/workspace/job-proposals/{id:guid}/review", (LocalWorkspace w, Guid id, JobReview request) =>
            w.ReviewImportedJobFromUiAsync(id, request));
        app.MapPost("/api/workspace/proposals/{id:guid}/discard", (LocalWorkspace w, Guid id, WorkspaceRevisionRequest request) =>
            w.DiscardProposalAsync(id, request.ExpectedRevision));
    }
}

public sealed record WorkspaceRevisionRequest(long ExpectedRevision);
