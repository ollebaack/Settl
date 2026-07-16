namespace Settl.Api.Dtos;

public sealed record CreateInviteRequest(string Email);

/// <summary><c>EmailSent</c> is false when the invite was created but the email provider
/// failed to deliver it — the invite still exists and can be shared manually or retried.</summary>
public sealed record InviteDto(Guid Id, string Email, DateTimeOffset ExpiresAt, bool EmailSent);

/// <summary>Shown before accepting. <c>HouseholdName</c> is null for a contact-only invite
/// (ADR-0019). For email invites the web page learns whether to ask for a password (no account
/// yet) or prompt a login; for SMS invites <c>Email</c> is null and <c>HasAccount</c> is always
/// false — the invitee supplies their own email, and we never reveal registration status.</summary>
public sealed record InvitePreviewDto(
    string? HouseholdName, string InviterName, string? Email, bool HasAccount, string Channel);

/// <summary><c>Password</c> is required only when creating a new account (no account for the
/// invited email/the supplied email yet); <c>Name</c> names that account. <c>Email</c> is
/// supplied by the invitee only for SMS invites — email-channel invites bind to the invited
/// address and ignore it (ADR-0005/0011: email stays the sole identity).</summary>
public sealed record AcceptInviteRequest(string? Name, string? Email, string? Password);
