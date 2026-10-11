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

namespace LearningPlatform.Assessment.Features.Essay.CreateAssignment;

internal static class CreateAssignmentEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/classes/{classId:guid}/essay-assignments",
                CreateAssignmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> CreateAssignmentAsync(Guid classId,
        CreateEssayAssignmentRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot receive a new essay assignment." });
        }

        try
        {
            var policy = request.ValidationPolicy ?? new ReportValidationPolicy();
            policy.AllowedFileTypes ??= [];
            policy.RequiredSections ??= [];
            EssayRules.ValidatePolicy(policy);
            var title = EssayRules.RequireTitle(request.Title);
            var description = request.Description?.Trim() ?? "";
            if (description.Length > EssayRules.MaxDescriptionLength || request.MaxScore <= 0 ||
                request.MaxScore > 100_000 || request.MaxGroupSize is < 1 or > 100)
            {
                throw new AssessmentValidationException("Essay score, description or group size is invalid.");
            }

            var now = DateTimeOffset.UtcNow;
            var assignment = new EssayAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                CourseId = learningClass.CourseId,
                Title = title,
                Description = description,
                Deadline = request.Deadline,
                MaxScore = request.MaxScore,
                MaxGroupSize = request.MaxGroupSize,
                AllowResubmission = request.AllowResubmission,
                Status = EssayAssignmentStatus.Draft,
                ValidationPolicy = policy,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(assignment);
            session.Insert(new GradeItem
            {
                Id = EssayConstants.GradeItemId(assignment.Id),
                OrganizationId = assignment.OrganizationId,
                LearningClassId = assignment.LearningClassId,
                ActivityId = Guid.Empty,
                EssayAssignmentId = assignment.Id,
                CourseId = assignment.CourseId,
                Title = assignment.Title,
                MaxScore = assignment.MaxScore,
                Weight = 1m,
                Status = GradeItemStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/essay-assignments/{assignment.Id:D}",
                EssayAssignmentResponse.From(assignment));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
