using Settl.Api.Domain;
using Settl.Api.Dtos;

namespace Settl.Api.Services;

/// <summary>Small helpers shared by registration and invite acceptance.</summary>
public static class AccountHelpers
{
    /// <summary>The member's own self-view. <c>PhoneNumberConfirmed</c> is always false today
    /// (no OTP — tech-debt/0010), so the phone is display/contact data only (ADR-0019).</summary>
    public static MeDto ToMeDto(this Member m) =>
        new(m.Id, m.Name, m.AvatarColor, m.EmailConfirmed, m.PhoneNumber, m.PhoneNumberConfirmed);

    private static readonly string[] AvatarPalette =
        ["#dfe6cf", "#f0dcc3", "#d9e0ee", "#eed9d9", "#d9eee4", "#e8ddf0"];

    /// <summary>Cycled by email hash — deterministic, no state to track.</summary>
    public static string AvatarColorFor(string email) =>
        AvatarPalette[Math.Abs(email.GetHashCode()) % AvatarPalette.Length];

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try { return new System.Net.Mail.MailAddress(email).Address == email; }
        catch (FormatException) { return false; }
    }
}
