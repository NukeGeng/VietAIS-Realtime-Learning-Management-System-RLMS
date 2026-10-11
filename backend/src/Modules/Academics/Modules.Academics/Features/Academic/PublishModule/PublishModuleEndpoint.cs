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
using static LearningPlatform.Academics.Api.AcademicEndpoints;

namespace LearningPlatform.Academics.Features.Academic.PublishModule;

internal static class PublishModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/modules/{moduleId:guid}/publish",
                PublishModuleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static Task<IResult> PublishModuleAsync(Guid courseId, Guid moduleId,
        HttpContext context, IDocumentSession session, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeContentStatusAsync(courseId, moduleId, null, AcademicContentStatus.Published,
            context, session, messageBus, loggerFactory, cancellationToken);

}
