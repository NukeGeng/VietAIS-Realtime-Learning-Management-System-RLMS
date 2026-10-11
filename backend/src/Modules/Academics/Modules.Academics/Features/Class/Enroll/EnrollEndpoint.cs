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

namespace LearningPlatform.Academics.Features.Class.Enroll;

internal static class EnrollEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes/{classId:guid}/enrollments", EnrollAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageEnrollments);
    }


    private static async Task<IResult> EnrollAsync(
        Guid classId,
        EnrollStudentRequest request,
        HttpContext context,
        IDocumentSession session,
        IIdentityDirectory identity,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
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

        if (await identity.FindActiveMemberAsync(actor!.OrganizationId, request.StudentId,
                cancellationToken) is null ||
            !await identity.HasPermissionAsync(actor.OrganizationId, request.StudentId,
                PermissionCatalog.ReadClasses, cancellationToken))
        {
            return Results.BadRequest(new { error = "The student is not an active class member." });
        }

        var enrollmentId = AcademicConstants.EnrollmentId(classId, request.StudentId);
        var enrollment = await session.LoadAsync<Enrollment>(enrollmentId, cancellationToken);
        if (enrollment is not null && enrollment.Status == EnrollmentStatus.Active)
        {
            return Results.Conflict(new { error = "The student is already enrolled in this class." });
        }

        var now = DateTimeOffset.UtcNow;
        if (enrollment is null)
        {
            enrollment = new Enrollment
            {
                Id = enrollmentId,
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                StudentId = request.StudentId,
                Status = EnrollmentStatus.Active,
                EnrolledAt = now,
                UpdatedAt = now
            };
            session.Insert(enrollment);
        }
        else if (enrollment.Status == EnrollmentStatus.Dropped)
        {
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.EnrolledAt = now;
            enrollment.UpdatedAt = now;
            session.Update(enrollment);
        }
        else
        {
            return Results.Conflict(new { error = "A completed enrollment cannot be reopened." });
        }

        await session.SaveChangesAsync(cancellationToken);
        await PublishEnrollmentReportingRefreshAsync(messageBus, loggerFactory, enrollment);
        return Results.Created($"/api/academics/classes/{classId:D}/enrollments/{request.StudentId:D}",
            EnrollmentResponse.From(enrollment));
    }

}
