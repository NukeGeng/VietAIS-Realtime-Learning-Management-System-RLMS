using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JasperFx;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using IdentityDomainOptions = LearningPlatform.Identity.Domain.IdentityOptions;
using DomainIdentityConstants = LearningPlatform.Identity.Domain.IdentityConstants;

namespace LearningPlatform.Identity.Api;

public sealed record AccountResponse(
    Guid Id,
    Guid OrganizationId,
    string Email,
    string DisplayName,
    AccountStatus Status,
    IReadOnlyCollection<string> Permissions)
{
    public static AccountResponse From(UserAccount account, IEnumerable<string> permissions) =>
        new(account.Id, account.OrganizationId, account.Email, account.DisplayName,
            account.Status, permissions.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray());
}
