using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class InvitesEndpoints
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    public static IEndpointRouteBuilder MapInvitesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/households/{id:guid}/invites", async (
            Guid id, CreateInviteRequest req, ICurrentUserAccessor cu, SettlDbContext db,
            IEmailSender email, IConfiguration config, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var household = await db.Households.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (household is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var isMember = await db.HouseholdMemberships.AnyAsync(m => m.HouseholdId == id && m.MemberId == me, ct);
            if (!isMember) return Results.Problem("Du är inte medlem i hushållet", statusCode: 403);

            var normalizedEmail = (req.Email ?? "").Trim().ToLowerInvariant();
            if (!AccountHelpers.IsValidEmail(normalizedEmail))
                return Results.Problem("Ogiltig e-postadress", statusCode: 400);

            var inviter = await db.Members.FirstAsync(m => m.Id == me, ct);

            var now = DateTimeOffset.UtcNow;
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                HouseholdId = id,
                Email = normalizedEmail,
                TokenHash = Hash(rawToken),
                InvitedByMemberId = me.Value,
                CreatedAt = now,
                ExpiresAt = now.Add(InviteLifetime)
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync(ct);

            var baseUrl = config["Web:BaseUrl"] ?? "http://localhost:5173";
            var acceptUrl = $"{baseUrl}/accept-invite?token={rawToken}";
            var emailSent = true;
            try
            {
                await email.SendInviteEmailAsync(normalizedEmail, household.Name, inviter.Name, acceptUrl, ct);
            }
            catch (InvalidOperationException)
            {
                // The invite row already exists (it can be resent or shared manually), so a
                // delivery failure shouldn't fail the whole request — just flag it for the UI.
                emailSent = false;
            }

            return Results.Created($"/households/{id}/invites/{invite.Id}",
                new InviteDto(invite.Id, invite.Email, invite.ExpiresAt, emailSent));
        }).WithName("CreateInvite")
            .Produces<InviteDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/households/{id:guid}/invites", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var isMember = await db.HouseholdMemberships.AnyAsync(m => m.HouseholdId == id && m.MemberId == me, ct);
            if (!isMember) return Results.Problem("Du är inte medlem i hushållet", statusCode: 403);

            // Ordered client-side: SQLite (used by tests) can't translate ORDER BY on
            // DateTimeOffset columns.
            var pending = await db.Invites
                .Where(i => i.HouseholdId == id && i.AcceptedAt == null)
                .ToListAsync(ct);
            return Results.Ok(pending
                .OrderBy(i => i.CreatedAt)
                .Select(i => new InviteDto(i.Id, i.Email, i.ExpiresAt, EmailSent: true)));
        }).WithName("GetHouseholdInvites")
            .Produces<List<InviteDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("/invites/{token}", async (
            string token, SettlDbContext db, UserManager<Member> users, CancellationToken ct) =>
        {
            var invite = await FindActiveInvite(db, token, ct);
            if (invite is null) return Results.Problem("Inbjudan hittades inte eller har gått ut", statusCode: 404);

            var household = await db.Households.FirstAsync(h => h.Id == invite.HouseholdId, ct);
            var inviter = await db.Members.FirstAsync(m => m.Id == invite.InvitedByMemberId, ct);
            var hasAccount = await users.FindByEmailAsync(invite.Email) is not null;

            return Results.Ok(new InvitePreviewDto(household.Name, inviter.Name, invite.Email, hasAccount));
        }).WithName("PreviewInvite")
            .AllowAnonymous()
            .Produces<InvitePreviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/invites/{token}/accept", async (
            string token, AcceptInviteRequest req, HttpContext http, SettlDbContext db,
            UserManager<Member> users, SignInManager<Member> signIn, CancellationToken ct) =>
        {
            var invite = await FindActiveInvite(db, token, ct);
            if (invite is null) return Results.Problem("Inbjudan hittades inte eller har gått ut", statusCode: 404);

            var member = await users.FindByEmailAsync(invite.Email);
            if (member is null)
            {
                if (string.IsNullOrWhiteSpace(req.Password))
                    return Results.Problem("Lösenord krävs", statusCode: 400);

                member = new Member
                {
                    Id = Guid.NewGuid(),
                    Name = req.Name?.Trim() is { Length: > 0 } n ? n : invite.Email,
                    AvatarColor = AccountHelpers.AvatarColorFor(invite.Email),
                    UserName = invite.Email,
                    Email = invite.Email,
                    EmailConfirmed = true
                };
                var created = await users.CreateAsync(member, req.Password);
                if (!created.Succeeded)
                    return Results.Problem("Lösenordet är för svagt (minst 8 tecken)", statusCode: 400);

                await signIn.SignInAsync(member, isPersistent: true);
            }
            else
            {
                var actingId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (actingId is null || !Guid.TryParse(actingId, out var actingMemberId) || actingMemberId != member.Id)
                    return Results.Problem("Logga in som den inbjudna e-postadressen för att acceptera", statusCode: 401);
            }

            var alreadyMember = await db.HouseholdMemberships
                .AnyAsync(m => m.HouseholdId == invite.HouseholdId && m.MemberId == member.Id, ct);
            if (!alreadyMember)
                db.HouseholdMemberships.Add(new HouseholdMembership
                { HouseholdId = invite.HouseholdId, MemberId = member.Id, JoinedAt = DateTimeOffset.UtcNow });

            invite.AcceptedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new MeDto(member.Id, member.Name, member.AvatarColor, member.AvatarEmoji, member.Email, member.EmailConfirmed));
        }).WithName("AcceptInvite")
            .AllowAnonymous()
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Dev-only: lets Playwright complete the invite/verification/reset flows without a
        // real inbox. The raw invite token is never persisted (only its hash is), so this
        // reads the in-memory store DevEmailSender populates, not the database — all 404
        // whenever Resend is configured.
        app.MapGet("/dev/invites/latest", (IHostEnvironment env, DevEmailLinkStore store) =>
        {
            if (!env.IsDevelopment()) return Results.NotFound();
            var url = store.LastInviteAcceptUrl;
            return url is null ? Results.NotFound() : Results.Ok(new { acceptUrl = url });
        }).WithName("GetLatestDevInvite")
            .AllowAnonymous();

        app.MapGet("/dev/verifications/latest", (IHostEnvironment env, DevEmailLinkStore store) =>
        {
            if (!env.IsDevelopment()) return Results.NotFound();
            var url = store.LastVerificationUrl;
            return url is null ? Results.NotFound() : Results.Ok(new { confirmUrl = url });
        }).WithName("GetLatestDevVerification")
            .AllowAnonymous();

        app.MapGet("/dev/password-resets/latest", (IHostEnvironment env, DevEmailLinkStore store) =>
        {
            if (!env.IsDevelopment()) return Results.NotFound();
            var url = store.LastPasswordResetUrl;
            return url is null ? Results.NotFound() : Results.Ok(new { resetUrl = url });
        }).WithName("GetLatestDevPasswordReset")
            .AllowAnonymous();

        return app;
    }

    private static async Task<Invite?> FindActiveInvite(SettlDbContext db, string token, CancellationToken ct)
    {
        // TokenHash equality is translated server-side; AcceptedAt/ExpiresAt are checked after
        // fetching — SQLite (used by tests) can't translate DateTimeOffset ordering comparisons.
        var hash = Hash(token);
        var invite = await db.Invites.SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        return invite is not null && invite.AcceptedAt is null && invite.ExpiresAt > DateTimeOffset.UtcNow
            ? invite
            : null;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
