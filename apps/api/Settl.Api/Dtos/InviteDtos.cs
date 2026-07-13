namespace Settl.Api.Dtos;

public sealed record CreateInviteRequest(string Email);

/// <summary><c>EmailSent</c> is false when the invite was created but the email provider
/// failed to deliver it — the invite still exists and can be shared manually or retried.</summary>
public sealed record InviteDto(Guid Id, string Email, DateTimeOffset ExpiresAt, bool EmailSent);

/// <summary>Shown before accepting, so the web page knows whether to ask for a password
/// (no account yet) or to prompt a login (email already has one).</summary>
public sealed record InvitePreviewDto(string HouseholdName, string InviterName, string Email, bool HasAccount);

/// <summary><c>Password</c> is required only when the invited email has no account yet;
/// <c>Name</c> is used to create that account (ignored otherwise).</summary>
public sealed record AcceptInviteRequest(string? Name, string? Password);
