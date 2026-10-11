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

public sealed record CurriculumResponse(Guid Id, Guid CourseId, string Title, string Description,
    AcademicContentStatus Status, Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static CurriculumResponse From(Curriculum curriculum) => new(curriculum.Id,
        curriculum.CourseId, curriculum.Title, curriculum.Description, curriculum.Status,
        curriculum.CreatedBy, curriculum.CreatedAt, curriculum.UpdatedAt);
}
