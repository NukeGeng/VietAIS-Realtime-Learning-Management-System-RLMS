using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Communication.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Communication.Api;

public sealed record NotificationResponse(Guid Id, Guid UserId, string Type, string Title, string Body,
    Guid? SourceId, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt)
{
    public static NotificationResponse From(Notification notification) => new(notification.Id,
        notification.UserId, notification.Type, LocalizedTitle(notification), LocalizedBody(notification),
        notification.SourceId, notification.ReadAt, notification.CreatedAt);

    internal static string LocalizedTitleForGrouping(Notification item) => LocalizedTitle(item);
    internal static string LocalizedBodyForGrouping(Notification item) => LocalizedBody(item);

    private static bool IsSchedule(Notification item) => item.Type.StartsWith("schedule-", StringComparison.Ordinal);
    private static string LocalizedTitle(Notification item) => !IsSchedule(item) ? item.Title : item.Title switch
    {
        "New class session" => "Lịch học mới",
        "Class session changed" => "Lịch học đã thay đổi",
        "Class session cancelled" => "Lịch học đã hủy",
        _ => item.Title
    };
    private static string LocalizedBody(Notification item) => !IsSchedule(item) ? item.Body : item.Body
        .Replace("A new class session has been scheduled.", "Một buổi học mới đã được lên lịch.", StringComparison.Ordinal)
        .Replace("A class session has been rescheduled.", "Buổi học đã được đổi lịch.", StringComparison.Ordinal)
        .Replace("A class session has been cancelled.", "Buổi học đã được hủy.", StringComparison.Ordinal)
        .Replace("Session:", "Thời gian (UTC):", StringComparison.Ordinal);
}
