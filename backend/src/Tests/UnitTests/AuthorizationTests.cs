using System.Security.Claims;
using LearningPlatform.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace LearningPlatform.UnitTests;

public sealed class AuthorizationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Missing_or_invalid_organization_is_not_authorized(string? organization)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (organization is not null)
        {
            claims.Add(new(AuthenticatedActor.OrganizationClaim, organization));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new OrganizationMemberRequirement();
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await new OrganizationMemberHandler().HandleAsync(context);
        Assert.False(context.HasSucceeded);
        Assert.False(AuthenticatedActor.TryCreate(principal, out _));
    }

    [Fact]
    public async Task Teacher_role_does_not_grant_permission()
    {
        var principal = Principal(new Claim(ClaimTypes.Role, "Teacher"));
        var requirement = new PermissionRequirement("submission.grade");
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await new PermissionHandler().HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Explicit_permission_grants_access_without_a_role()
    {
        var principal = Principal(new Claim(AuthenticatedActor.PermissionClaim, "submission.grade"));
        var requirement = new PermissionRequirement("submission.grade");
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await new PermissionHandler().HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public void Unauthenticated_claims_cannot_create_an_actor()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(Principal().Claims));
        Assert.False(AuthenticatedActor.TryCreate(principal, out _));
    }

    [Fact]
    public void Empty_user_id_cannot_create_an_actor()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
            new Claim(AuthenticatedActor.OrganizationClaim, Guid.NewGuid().ToString())
        ], "test"));
        Assert.False(AuthenticatedActor.TryCreate(principal, out _));
    }

    private static ClaimsPrincipal Principal(params Claim[] additional) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim(AuthenticatedActor.OrganizationClaim, Guid.NewGuid().ToString()),
        .. additional
    ], "test"));
}
