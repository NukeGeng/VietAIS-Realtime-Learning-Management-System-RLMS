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

namespace LearningPlatform.Academics.Api;

public sealed record CourseResponse(
    Guid Id, string Code, string Title, string Description, CourseStatus Status,
    Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static CourseResponse From(Course course) => new(course.Id, course.Code, course.Title,
        course.Description, course.Status, course.CreatedBy, course.CreatedAt, course.UpdatedAt);
}
