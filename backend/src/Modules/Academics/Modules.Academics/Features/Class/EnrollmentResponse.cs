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

public sealed record EnrollmentResponse(Guid Id, Guid LearningClassId, Guid StudentId,
    EnrollmentStatus Status, DateTimeOffset EnrolledAt)
{
    public string? StudentDisplayName { get; init; }

    public static EnrollmentResponse From(Enrollment enrollment) => new(enrollment.Id,
        enrollment.LearningClassId, enrollment.StudentId, enrollment.Status, enrollment.EnrolledAt);
}
