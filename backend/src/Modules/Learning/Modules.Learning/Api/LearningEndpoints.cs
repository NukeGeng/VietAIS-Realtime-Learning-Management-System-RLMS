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

namespace LearningPlatform.Learning.Api;

public static class LearningEndpoints
{
    public static void MapLearningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Learning.Features.Learning.GetClassProgress.GetClassProgressEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetMyProgress.GetMyProgressEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetCourseProgress.GetCourseProgressEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetMyCourseProgress.GetMyCourseProgressEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetStudentCourseContent.GetStudentCourseContentEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetStudentCurriculums.GetStudentCurriculumsEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetStudentCurriculumContent.GetStudentCurriculumContentEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetStudentCurriculumLessonDetail.GetStudentCurriculumLessonDetailEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.UpdateStudentCurriculumLessonProgress.UpdateStudentCurriculumLessonProgressEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetStudentLessonDetail.GetStudentLessonDetailEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.UpdateLessonProgress.UpdateLessonProgressEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.ListResources.ListResourcesEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.GetResource.GetResourceEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.CreateResource.CreateResourceEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.UploadResource.UploadResourceEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.PublishResource.PublishResourceEndpoint.Map(endpoints);
        LearningPlatform.Learning.Features.Learning.ArchiveResource.ArchiveResourceEndpoint.Map(endpoints);
    }

}
