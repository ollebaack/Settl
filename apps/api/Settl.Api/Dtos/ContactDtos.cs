namespace Settl.Api.Dtos;

/// <summary>
/// Sends a blind invite (ADR-0019). <c>Channel</c> is "sms" or "email"; supply <c>Phone</c>
/// for SMS (E.164, normalised server-side) or <c>Email</c> for email. <c>HouseholdId</c> is
/// optional — set it to also add the invitee to a household on accept (the "invite to
/// household from contacts" flow), or leave it null for a contact-only invite. Typing a
/// number NEVER reveals whether it is already on Settl: there is no lookup, only a send.
/// </summary>
public sealed record CreateContactInviteRequest(string Channel, string? Phone, string? Email, Guid? HouseholdId);

/// <summary>Always the same shape whether or not the number/email is already a Settl user —
/// no enumeration oracle. <c>Delivered</c> is false when the SMS/email provider failed but the
/// invite row still exists (it can be resent).</summary>
public sealed record ContactInviteResultDto(Guid Id, string Channel, DateTimeOffset ExpiresAt, bool Delivered);

/// <summary>An accepted contact (the Member↔Member edge). <c>SharedHouseholdCount</c> is
/// derived server-side (ADR-0006) — the UI renders "I N hushåll med dig", never computes it.</summary>
public sealed record ContactDto(Guid MemberId, string Name, string AvatarColor, int SharedHouseholdCount);

/// <summary>An outstanding invite the current user sent that hasn't been accepted yet. Shows
/// the raw phone/email the sender themselves typed (no third party's data is revealed).</summary>
public sealed record PendingInviteDto(
    Guid Id, string Channel, string? Phone, string? Email, DateTimeOffset SentAt, DateTimeOffset ExpiresAt);

/// <summary>A saved contact with their status for one household: "member" (already joined),
/// "pending" (an unaccepted invite to their address exists), or "invitable" (pick to invite).</summary>
public sealed record InvitableContactDto(Guid MemberId, string Name, string AvatarColor, string Status);

/// <summary>Invites an existing saved contact (by member id) to a household — the wishlist
/// "reuse a saved contact" flow. The server reads the contact's email itself, so the client
/// never sees another member's address. Joining still requires the contact to accept (ADR-0011).</summary>
public sealed record InviteContactRequest(Guid ContactMemberId);
