namespace Settl.Api.Dtos;

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in member's own view of themselves — distinct from <see cref="MemberDto"/>
/// (used for household member lists) since <see cref="EmailConfirmed"/> is session-relative,
/// not something other members' rows need to carry.</summary>
public sealed record MeDto(Guid Id, string Name, string AvatarColor, bool EmailConfirmed);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
