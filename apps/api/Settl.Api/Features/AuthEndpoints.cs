using Microsoft.AspNetCore.Identity;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/register", async (
            RegisterRequest req, UserManager<Member> users, SignInManager<Member> signIn, CancellationToken ct) =>
        {
            var name = req.Name?.Trim() ?? "";
            var email = req.Email?.Trim() ?? "";
            if (name.Length == 0) return Results.Problem("Namn krävs", statusCode: 400);
            if (!AccountHelpers.IsValidEmail(email)) return Results.Problem("Ogiltig e-postadress", statusCode: 400);

            var member = new Member
            {
                Id = Guid.NewGuid(),
                Name = name,
                AvatarColor = AccountHelpers.AvatarColorFor(email),
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await users.CreateAsync(member, req.Password ?? "");
            if (!result.Succeeded)
                return Results.Problem(DescribeError(result), statusCode: 400);

            await signIn.SignInAsync(member, isPersistent: true);
            return Results.Created("/me", new MemberDto(member.Id, member.Name, member.AvatarColor));
        }).WithName("Register")
            .AllowAnonymous()
            .Produces<MemberDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/auth/login", async (
            LoginRequest req, UserManager<Member> users, SignInManager<Member> signIn, CancellationToken ct) =>
        {
            var member = await users.FindByEmailAsync(req.Email?.Trim() ?? "");
            if (member is null)
                return Results.Problem("Fel e-post eller lösenord", statusCode: 401);

            var result = await signIn.PasswordSignInAsync(member, req.Password ?? "", isPersistent: true, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.Problem("Fel e-post eller lösenord", statusCode: 401);

            return Results.Ok(new MemberDto(member.Id, member.Name, member.AvatarColor));
        }).WithName("Login")
            .AllowAnonymous()
            .Produces<MemberDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/auth/logout", async (SignInManager<Member> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).WithName("Logout")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static string DescribeError(IdentityResult result) =>
        result.Errors.Any(e => e.Code == "DuplicateUserName" || e.Code == "DuplicateEmail")
            ? "E-postadressen används redan"
            : "Lösenordet är för svagt (minst 8 tecken)";
}
