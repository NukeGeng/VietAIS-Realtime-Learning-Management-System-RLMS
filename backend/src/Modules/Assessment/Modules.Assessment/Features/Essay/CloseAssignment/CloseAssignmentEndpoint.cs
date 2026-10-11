using LearningPlatform.Assessment.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Storage;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Assessment.Api.EssayEndpoints;

namespace LearningPlatform.Assessment.Features.Essay.CloseAssignment;

internal static class CloseAssignmentEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-assignments/{assignmentId:guid}/close",
                CloseAssignmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static Task<IResult> CloseAssignmentAsync(Guid assignmentId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeAssignmentStatusAsync(assignmentId, EssayAssignmentStatus.Closed, context, session,
            academics, messageBus, loggerFactory, cancellationToken);

}
