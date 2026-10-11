using LearningPlatform.Learning.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using LearningPlatform.Learning.Domain;
using LearningPlatform.Learning.Storage;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Learning.Features.Learning.Shared.CurriculumContent;
using static LearningPlatform.Learning.Features.Learning.Shared.CourseProgress;
using static LearningPlatform.Learning.Features.Learning.Shared.ResourceValidation;
using static LearningPlatform.Learning.Features.Learning.Shared.LearningAccess;

namespace LearningPlatform.Learning.Features.Learning.PublishResource;

internal static class PublishResourceEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/learning/resources/{resourceId:guid}/publish", PublishResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLearningResources);
    }


    private static Task<IResult> PublishResourceAsync(Guid resourceId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        ChangeResourceStatusAsync(resourceId, LearningResourceStatus.Published, context, session,
            academics, cancellationToken);

}
