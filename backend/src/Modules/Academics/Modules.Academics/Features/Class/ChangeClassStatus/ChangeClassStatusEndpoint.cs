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

namespace LearningPlatform.Academics.Features.Class.ChangeClassStatus;

internal static class ChangeClassStatusEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes/{classId:guid}/status", ChangeClassStatusAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
    }


    private static async Task<IResult> ChangeClassStatusAsync(
        Guid classId,
        ChangeClassStatusRequest request,
        HttpContext context,
        IDocumentSession session,
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

        if (!CanChangeStatus(learningClass.Status, request.Status))
        {
            return Results.Conflict(new { error = "The class cannot make that state transition." });
        }

        learningClass.Status = request.Status;
        learningClass.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(learningClass);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(ClassResponse.From(learningClass));
    }

}
