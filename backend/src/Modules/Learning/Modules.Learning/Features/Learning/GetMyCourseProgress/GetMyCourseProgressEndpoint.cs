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

namespace LearningPlatform.Learning.Features.Learning.GetMyCourseProgress;

internal static class GetMyCourseProgressEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/classes/{classId:guid}/course-progress/me",
                GetMyCourseProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
    }


    private static Task<IResult> GetMyCourseProgressAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        GetCourseProgressCoreAsync(classId, Guid.Empty, context, session, academics, cancellationToken);

}
