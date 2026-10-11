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

internal static class AcademicAuthorization
{
    public static bool CanManageCourse(AuthenticatedActor actor, Course course) =>
        actor.OrganizationId == course.OrganizationId &&
        (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
         course.CreatedBy == actor.UserId);
}
