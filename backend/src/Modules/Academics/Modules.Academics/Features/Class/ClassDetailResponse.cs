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

public sealed record ClassDetailResponse(ClassResponse Class, IReadOnlyList<Guid> TeacherIds)
{
    public static ClassDetailResponse From(LearningClass learningClass,
        IEnumerable<ClassTeacherAssignment> teachers) => new(ClassResponse.From(learningClass),
        teachers.Select(teacher => teacher.TeacherId).ToArray());
}
