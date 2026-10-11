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

namespace LearningPlatform.Academics.Features.Class.ListEnrollments;

internal static class ListEnrollmentsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/classes/{classId:guid}/enrollments", ListEnrollmentsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadEnrollments);
    }


    private static async Task<IResult> ListEnrollmentsAsync(
        Guid classId,
        HttpContext context,
        IQuerySession session,
        IIdentityDirectory identity,
        CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var canManageClass = await CanManageClassAsync(context, session, learningClass,
            cancellationToken);
        if (!canManageClass && !await session.Query<Enrollment>().AnyAsync(enrollment =>
                enrollment.LearningClassId == classId && enrollment.StudentId == actor!.UserId &&
                enrollment.Status == EnrollmentStatus.Active, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<Enrollment>().Where(enrollment =>
            enrollment.OrganizationId == learningClass.OrganizationId &&
            enrollment.LearningClassId == classId);
        if (!canManageClass)
        {
            query = query.Where(enrollment => enrollment.StudentId == actor!.UserId);
        }

        var enrollments = await query.OrderBy(enrollment => enrollment.EnrolledAt)
            .ToListAsync(cancellationToken);
        var names = await identity.FindMemberNamesAsync(actor!.OrganizationId,
            enrollments.Select(enrollment => enrollment.StudentId).ToArray(), cancellationToken);
        return Results.Ok(enrollments.Select(enrollment => EnrollmentResponse.From(enrollment) with
        {
            StudentDisplayName = names.GetValueOrDefault(enrollment.StudentId)
        }));
    }

}
