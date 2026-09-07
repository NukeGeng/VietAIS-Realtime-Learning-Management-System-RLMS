namespace LearningPlatform.Learning.Contracts;

public sealed record GradeEntryPublished(Guid OrganizationId, Guid LearningClassId, Guid? CourseId,
    Guid StudentId, Guid ActivityId, Guid GradeItemId, Guid GradeEntryId, string ActivityTitle,
    int AttemptNumber, decimal Score, decimal MaxScore, int GradeVersion,
    DateTimeOffset PublishedAt);
