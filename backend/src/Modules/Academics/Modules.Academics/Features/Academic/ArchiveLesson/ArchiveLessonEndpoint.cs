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

namespace LearningPlatform.Academics.Features.Academic.ArchiveLesson;

internal static class ArchiveLessonEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/modules/{moduleId:guid}/lessons/{lessonId:guid}/archive",
                ArchiveLessonAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static Task<IResult> ArchiveLessonAsync(Guid courseId, Guid moduleId, Guid lessonId,
        HttpContext context, IDocumentSession session, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeContentStatusAsync(courseId, moduleId, lessonId, AcademicContentStatus.Archived,
            context, session, messageBus, loggerFactory, cancellationToken);

}
