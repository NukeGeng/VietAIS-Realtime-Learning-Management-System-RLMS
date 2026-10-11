using LearningPlatform.Academics.Api;
using JasperFx;
using LearningPlatform.Academics.Contracts;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Academics.Api.ClassEndpoints;

namespace LearningPlatform.Academics.Features.Class.DropEnrollment;

internal static class DropEnrollmentEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/academics/classes/{classId:guid}/enrollments/{studentId:guid}",
                DropEnrollmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageEnrollments);
    }


    private static async Task<IResult> DropEnrollmentAsync(
        Guid classId,
        Guid studentId,
        HttpContext context,
        IDocumentSession session,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out _))
        {
            return Results.Forbid();
        }

        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(context, session, learningClass, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot change enrollment." });
        }

        var enrollment = await session.LoadAsync<Enrollment>(
            AcademicConstants.EnrollmentId(classId, studentId), cancellationToken);
        if (enrollment is null || enrollment.Status != EnrollmentStatus.Active)
        {
            return Results.NotFound();
        }

        enrollment.Status = EnrollmentStatus.Dropped;
        enrollment.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(enrollment);
        await session.SaveChangesAsync(cancellationToken);
        await PublishEnrollmentReportingRefreshAsync(messageBus, loggerFactory, enrollment);
        return Results.NoContent();
    }

}
