using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Assessment.Domain;

public static class AssessmentConstants
{
    public static Guid QuestionVersionId(Guid questionBankItemId, int version) =>
        StableId($"question-version:{questionBankItemId:D}:{version}");

    public static Guid ActivityQuestionId(Guid activityId, Guid questionVersionId) =>
        StableId($"activity-question:{activityId:D}:{questionVersionId:D}");

    public static Guid StudentActivityId(Guid activityId, Guid studentId) =>
        StableId($"student-activity:{activityId:D}:{studentId:D}");

    public static Guid SubmissionId(Guid activityId, Guid studentId) =>
        StableId($"submission:{activityId:D}:{studentId:D}");

    public static Guid ClassOverviewViewId(Guid learningClassId, Guid activityId) =>
        StableId($"class-overview:{learningClassId:D}:{activityId:D}");

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
