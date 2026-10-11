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

namespace LearningPlatform.Academics.Features.Academic.ArchiveModule;

internal static class ArchiveModuleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/modules/{moduleId:guid}/archive",
                ArchiveModuleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static Task<IResult> ArchiveModuleAsync(Guid courseId, Guid moduleId,
        HttpContext context, IDocumentSession session, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeContentStatusAsync(courseId, moduleId, null, AcademicContentStatus.Archived,
            context, session, messageBus, loggerFactory, cancellationToken);

}
