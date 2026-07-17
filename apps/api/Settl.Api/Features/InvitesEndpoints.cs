using System.Security.Claims;
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
    /// <summary>Invite lifetime (ADR-0011) — reused by both the email and SMS/contact paths.</summary>
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

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
            var rawToken = InviteTokens.NewRawToken();
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                HouseholdId = id,
                Channel = InviteChannel.Email,
                Email = normalizedEmail,
                TokenHash = InviteTokens.Hash(rawToken),
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
                new InviteDto(invite.Id, invite.Email ?? "", invite.ExpiresAt, emailSent));
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
            // Only email-channel invites carry an address to show as a pending "email — waiting"
            // row here; SMS/contact invites surface in the contacts tab instead (ADR-0019).
            var pending = await db.Invites
                .Where(i => i.HouseholdId == id && i.AcceptedAt == null && i.Email != null)
                .ToListAsync(ct);
            return Results.Ok(pending
                .OrderBy(i => i.CreatedAt)
                .Select(i => new InviteDto(i.Id, i.Email ?? "", i.ExpiresAt, EmailSent: true)));
        }).WithName("GetHouseholdInvites")
            .Produces<List<InviteDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("/invites/{token}", async (
            string token, SettlDbContext db, UserManager<Member> users, CancellationToken ct) =>
        {
            var invite = await FindActiveInvite(db, token, ct);
            if (invite is null) return Results.Problem("Inbjudan hittades inte eller har gått ut", statusCode: 404);

            var inviter = await db.Members.FirstAsync(m => m.Id == invite.InvitedByMemberId, ct);
            string? householdName = invite.HouseholdId is Guid hh
                ? (await db.Households.FirstOrDefaultAsync(h => h.Id == hh, ct))?.Name
                : null;

            // SMS invites carry no email — the invitee supplies their own on accept — and we
            // never reveal whether a number/email is already on Settl (ADR-0019: no oracle).
            var hasAccount = invite is { Channel: InviteChannel.Email, Email: not null }
                && await users.FindByEmailAsync(invite.Email) is not null;

            return Results.Ok(new InvitePreviewDto(
                householdName, inviter.Name, invite.Email, hasAccount, Contract.InviteChannel(invite.Channel)));
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

            var actingId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? actingMemberId = actingId is not null && Guid.TryParse(actingId, out var a) ? a : null;

            Member member;
            if (invite.Channel == InviteChannel.Email)
            {
                // Email channel binds to the invited address (ADR-0011 unchanged).
                var existing = await users.FindByEmailAsync(invite.Email!);
                if (existing is null)
                {
                    if (string.IsNullOrWhiteSpace(req.Password))
                        return Results.Problem("Lösenord krävs", statusCode: 400);
                    var created = await CreateAccountAsync(users, signIn, invite.Email!, req.Name, req.Password);
                    if (created is null) return Results.Problem("Lösenordet är för svagt (minst 8 tecken)", statusCode: 400);
                    member = created;
                }
                else
                {
                    if (actingMemberId != existing.Id)
                        return Results.Problem("Logga in som den inbjudna e-postadressen för att acceptera", statusCode: 401);
                    member = existing;
                }
            }
            else
            {
                // SMS channel carries no identity — the invitee brings their own email
                // (ADR-0005/0011: email stays the sole identity). A logged-in user accepts as
                // themselves; otherwise they create an account, and we never confirm whether
                // that email (or the invited number) was already registered.
                if (actingMemberId is Guid me)
                {
                    member = await users.FindByIdAsync(me.ToString())
                        ?? throw new InvalidOperationException("Authenticated member not found");
                }
                else
                {
                    var email = (req.Email ?? "").Trim().ToLowerInvariant();
                    if (!AccountHelpers.IsValidEmail(email))
                        return Results.Problem("Ogiltig e-postadress", statusCode: 400);
                    if (await users.FindByEmailAsync(email) is not null)
                        return Results.Problem("Det finns redan ett konto med den e-posten — logga in och öppna länken igen", statusCode: 401);
                    if (string.IsNullOrWhiteSpace(req.Password))
                        return Results.Problem("Lösenord krävs", statusCode: 400);
                    var created = await CreateAccountAsync(users, signIn, email, req.Name, req.Password);
                    if (created is null) return Results.Problem("Lösenordet är för svagt (minst 8 tecken)", statusCode: 400);
                    member = created;
                }
            }

            if (invite.HouseholdId is Guid householdId)
            {
                var alreadyMember = await db.HouseholdMemberships
                    .AnyAsync(m => m.HouseholdId == householdId && m.MemberId == member.Id, ct);
                if (!alreadyMember)
                    db.HouseholdMemberships.Add(new HouseholdMembership
                    { HouseholdId = householdId, MemberId = member.Id, JoinedAt = DateTimeOffset.UtcNow });
            }

            // Connection-on-accept: the reciprocal contact edge proves consent (ADR-0019).
            await AddContactEdgesAsync(db, invite.InvitedByMemberId, member.Id, ct);

            invite.AcceptedAt = DateTimeOffset.UtcNow;
            invite.PhoneNumber = null; // the raw number has served its purpose — don't retain it
            await db.SaveChangesAsync(ct);

            return Results.Ok(member.ToMeDto());
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
        app.MapGet("/dev/invites/latest", (IHostEnvironment env, DevEmailLinkStore store, string? email) =>
        {
            if (!env.IsDevelopment()) return Results.NotFound();
            // `email` scopes the lookup to one invitee so parallel e2e workers don't race on the
            // single most-recent slot; omit it for manual local dev ("just give me the last one").
            var url = email is not null ? store.InviteAcceptUrlFor(email) : store.LastInviteAcceptUrl;
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

        app.MapGet("/dev/sms-invites/latest", (IHostEnvironment env, DevEmailLinkStore store, string? phone) =>
        {
            if (!env.IsDevelopment()) return Results.NotFound();
            // `phone` (E.164) scopes the lookup to one invitee — see /dev/invites/latest.
            var url = phone is not null ? store.SmsInviteAcceptUrlFor(phone) : store.LastSmsInviteAcceptUrl;
            return url is null ? Results.NotFound() : Results.Ok(new { acceptUrl = url });
        }).WithName("GetLatestDevSmsInvite")
            .AllowAnonymous();

        return app;
    }

    /// <summary>Creates a new signed-in account with email as its identity (ADR-0005). Returns
    /// null if the password is rejected by Identity's policy.</summary>
    private static async Task<Member?> CreateAccountAsync(
        UserManager<Member> users, SignInManager<Member> signIn, string email, string? name, string password)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(),
            Name = name?.Trim() is { Length: > 0 } n ? n : email,
            AvatarColor = AccountHelpers.AvatarColorFor(email),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var created = await users.CreateAsync(member, password);
        if (!created.Succeeded) return null;

        await signIn.SignInAsync(member, isPersistent: true);
        return member;
    }

    /// <summary>Creates the reciprocal Member↔Member contact edges (ADR-0019), idempotently.
    /// No-op for a self-edge or when the edge already exists from an earlier accepted invite.</summary>
    private static async Task AddContactEdgesAsync(SettlDbContext db, Guid a, Guid b, CancellationToken ct)
    {
        if (a == b) return;
        var now = DateTimeOffset.UtcNow;
        await EnsureEdge(db, a, b, now, ct);
        await EnsureEdge(db, b, a, now, ct);
    }

    private static async Task EnsureEdge(SettlDbContext db, Guid owner, Guid contact, DateTimeOffset now, CancellationToken ct)
    {
        var exists = await db.Contacts.AnyAsync(c => c.OwnerMemberId == owner && c.ContactMemberId == contact, ct);
        if (!exists)
            db.Contacts.Add(new Contact { OwnerMemberId = owner, ContactMemberId = contact, CreatedAt = now });
    }

    private static async Task<Invite?> FindActiveInvite(SettlDbContext db, string token, CancellationToken ct)
    {
        // TokenHash equality is translated server-side; AcceptedAt/ExpiresAt are checked after
        // fetching — SQLite (used by tests) can't translate DateTimeOffset ordering comparisons.
        var hash = InviteTokens.Hash(token);
        var invite = await db.Invites.SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        return invite is not null && invite.AcceptedAt is null && invite.ExpiresAt > DateTimeOffset.UtcNow
            ? invite
            : null;
    }
}
