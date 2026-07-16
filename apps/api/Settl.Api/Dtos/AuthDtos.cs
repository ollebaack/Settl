namespace Settl.Api.Dtos;

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in member's own view of themselves — distinct from <see cref="MemberDto"/>
/// (used for household member lists) since <see cref="EmailConfirmed"/> is session-relative,
/// not something other members' rows need to carry. <c>Phone</c> is the optional profile phone
/// (ADR-0019); <c>PhoneVerified</c> is always false for now (no OTP — tech-debt/0010), so it is
/// display/contact data only and must never be treated as a lookup key or auth factor.</summary>
public sealed record MeDto(
    Guid Id, string Name, string AvatarColor, bool EmailConfirmed, string? Phone, bool PhoneVerified);

/// <summary>Updates the acting member's own profile. <c>Phone</c> is normalised to E.164 and
/// stored UNVERIFIED (ADR-0019); an empty/null value clears it. Email stays the sole identity.</summary>
public sealed record UpdateProfileRequest(string? Phone);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
