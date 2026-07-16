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
            RegisterRequest req, UserManager<Member> users, SignInManager<Member> signIn,
            IEmailSender email, IConfiguration config, CancellationToken ct) =>
        {
            var name = req.Name?.Trim() ?? "";
            var reqEmail = req.Email?.Trim() ?? "";
            if (name.Length == 0) return Results.Problem("Namn krävs", statusCode: 400);
            if (!AccountHelpers.IsValidEmail(reqEmail)) return Results.Problem("Ogiltig e-postadress", statusCode: 400);

            var member = new Member
            {
                Id = Guid.NewGuid(),
                Name = name,
                AvatarColor = AccountHelpers.AvatarColorFor(reqEmail),
                UserName = reqEmail,
                Email = reqEmail,
                EmailConfirmed = false
            };

            var result = await users.CreateAsync(member, req.Password ?? "");
            if (!result.Succeeded)
                return Results.Problem(DescribeError(result), statusCode: 400);

            await signIn.SignInAsync(member, isPersistent: true);
            await SendVerificationEmailAsync(member, users, email, config, ct);

            return Results.Created("/me", new MeDto(member.Id, member.Name, member.AvatarColor, member.AvatarEmoji, member.Email, member.EmailConfirmed));
        }).WithName("Register")
            .AllowAnonymous()
            .Produces<MeDto>(StatusCodes.Status201Created)
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

            return Results.Ok(new MeDto(member.Id, member.Name, member.AvatarColor, member.AvatarEmoji, member.Email, member.EmailConfirmed));
        }).WithName("Login")
            .AllowAnonymous()
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/auth/logout", async (SignInManager<Member> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).WithName("Logout")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        app.MapPost("/auth/confirm-email", async (
            ConfirmEmailRequest req, UserManager<Member> users, CancellationToken ct) =>
        {
            var member = await users.FindByIdAsync(req.UserId.ToString());
            if (member is null) return Results.Problem("Länken är ogiltig eller har gått ut", statusCode: 400);

            var result = await users.ConfirmEmailAsync(member, req.Token);
            if (!result.Succeeded) return Results.Problem("Länken är ogiltig eller har gått ut", statusCode: 400);

            return Results.NoContent();
        }).WithName("ConfirmEmail")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/auth/resend-verification", async (
            ICurrentUserAccessor cu, UserManager<Member> users, IEmailSender email, IConfiguration config, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var member = await users.FindByIdAsync(me.Value.ToString());
            if (member is null) return Results.Problem("Ingen användare", statusCode: 404);
            if (member.EmailConfirmed) return Results.Problem("E-postadressen är redan verifierad", statusCode: 400);

            await SendVerificationEmailAsync(member, users, email, config, ct);
            return Results.NoContent();
        }).WithName("ResendVerification")
            .RequireAuthorization("AuthenticatedOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/auth/forgot-password", async (
            ForgotPasswordRequest req, UserManager<Member> users, IEmailSender email, IConfiguration config, CancellationToken ct) =>
        {
            var member = await users.FindByEmailAsync(req.Email?.Trim() ?? "");
            // Always the same response whether or not the account exists — a different one
            // would let callers enumerate registered emails.
            if (member is not null)
            {
                var token = await users.GeneratePasswordResetTokenAsync(member);
                var baseUrl = config["Web:BaseUrl"] ?? "http://localhost:5173";
                var resetUrl = $"{baseUrl}/reset-password?userId={member.Id}&token={Uri.EscapeDataString(token)}";
                try
                {
                    await email.SendPasswordResetEmailAsync(member.Email!, resetUrl, ct);
                }
                catch (InvalidOperationException)
                {
                    // The reset token already exists; a delivery failure shouldn't change the
                    // response, which is always NoContent regardless of account existence.
                }
            }
            return Results.NoContent();
        }).WithName("ForgotPassword")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        app.MapPost("/auth/reset-password", async (
            ResetPasswordRequest req, UserManager<Member> users, SignInManager<Member> signIn, CancellationToken ct) =>
        {
            var member = await users.FindByIdAsync(req.UserId.ToString());
            if (member is null) return Results.Problem("Länken är ogiltig eller har gått ut", statusCode: 400);

            var result = await users.ResetPasswordAsync(member, req.Token, req.NewPassword ?? "");
            if (!result.Succeeded) return Results.Problem(DescribeResetError(result), statusCode: 400);

            await signIn.SignInAsync(member, isPersistent: true);
            return Results.Ok(new MeDto(member.Id, member.Name, member.AvatarColor, member.AvatarEmoji, member.Email, member.EmailConfirmed));
        }).WithName("ResetPassword")
            .AllowAnonymous()
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task SendVerificationEmailAsync(
        Member member, UserManager<Member> users, IEmailSender email, IConfiguration config, CancellationToken ct)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(member);
        var baseUrl = config["Web:BaseUrl"] ?? "http://localhost:5173";
        var confirmUrl = $"{baseUrl}/confirm-email?userId={member.Id}&token={Uri.EscapeDataString(token)}";
        try
        {
            await email.SendVerificationEmailAsync(member.Email!, confirmUrl, ct);
        }
        catch (InvalidOperationException)
        {
            // The account (or, for resend, the confirmation token) already exists — a
            // delivery failure shouldn't fail the request. The user can ask to resend.
        }
    }

    private static string DescribeError(IdentityResult result) =>
        result.Errors.Any(e => e.Code == "DuplicateUserName" || e.Code == "DuplicateEmail")
            ? "E-postadressen används redan"
            : "Lösenordet är för svagt (minst 8 tecken)";

    private static string DescribeResetError(IdentityResult result) =>
        result.Errors.Any(e => e.Code == "InvalidToken")
            ? "Länken är ogiltig eller har gått ut"
            : "Lösenordet är för svagt (minst 8 tecken)";
}
