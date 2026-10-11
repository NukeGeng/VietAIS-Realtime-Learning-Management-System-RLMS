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

public sealed record CourseProgressResponse(Guid LearningClassId, Guid CourseId, int TotalLessons,
    LessonProgressEntryResponse[] Entries)
{
    /// <summary>
    /// The progress headline is derived from the same published-lesson entries
    /// that are returned to the page. For a teacher view this is the average
    /// completed lesson count across the selected class; for a student view it
    /// is that student's count.
    /// </summary>
    public decimal CompletedLessons
    {
        get
        {
            if (TotalLessons == 0) return 0;
            var groups = Entries.GroupBy(entry => entry.StudentId).ToArray();
            return groups.Length == 0
                ? 0
                : decimal.Round((decimal)groups.Average(group => group.Count(entry =>
                    entry.Status == LessonProgressStatus.Completed)), 2);
        }
    }

    public decimal CompletionPercent
    {
        get
        {
            if (TotalLessons == 0)
            {
                return 0m;
            }

            var groups = Entries.GroupBy(entry => entry.StudentId).ToArray();
            return groups.Length == 0
                ? 0m
                : decimal.Round(groups.Sum(group => group.Count(entry =>
                    entry.Status == LessonProgressStatus.Completed)) * 100m /
                    (groups.Length * TotalLessons), 2);
        }
    }
}
