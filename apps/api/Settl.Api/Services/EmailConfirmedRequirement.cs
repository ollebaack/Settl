using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Settl.Api.Domain;

namespace Settl.Api.Services;

/// <summary>Gates the fallback authorization policy on <c>EmailConfirmed</c> — everything
/// except AllowAnonymous/"AuthenticatedOnly" endpoints (Program.cs) needs a verified email,
/// not just a session (the email-verification decision made alongside ADR-0011).</summary>
public sealed class EmailConfirmedRequirement : IAuthorizationRequirement;

public sealed class EmailConfirmedHandler(UserManager<Member> users) : AuthorizationHandler<EmailConfirmedRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, EmailConfirmedRequirement requirement)
    {
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null) return;

        var member = await users.FindByIdAsync(id);
        if (member is not null && member.EmailConfirmed) context.Succeed(requirement);
    }
}
