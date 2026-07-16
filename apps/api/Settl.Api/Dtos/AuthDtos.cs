namespace Settl.Api.Dtos;

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in member's own view of themselves — distinct from <see cref="MemberDto"/>
/// (used for household member lists) since <see cref="EmailConfirmed"/> and <see cref="Email"/>
/// are session-relative, not something other members' rows need to carry. <c>Phone</c> is the
/// optional profile phone (ADR-0019); <c>PhoneVerified</c> is always false for now (no OTP —
/// tech-debt/0010), so it is display/contact data only and must never be treated as a lookup
/// key or auth factor.</summary>
public sealed record MeDto(
    Guid Id, string Name, string AvatarColor, string? AvatarEmoji, string? Email,
    bool EmailConfirmed, string? Phone, bool PhoneVerified);

/// <summary>Updates the acting member's own name + avatar emoji (ADR-0019). <see cref="AvatarEmoji"/>
/// null/empty resets the avatar to the letter initial. (PUT /me)</summary>
public sealed record UpdateMeRequest(string Name, string? AvatarEmoji);

/// <summary>Updates the acting member's own profile phone. <c>Phone</c> is normalised to E.164 and
/// stored UNVERIFIED (ADR-0019); an empty/null value clears it. Email stays the sole identity.
/// (PATCH /me)</summary>
public sealed record UpdateProfileRequest(string? Phone);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
