using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>WebApplicationFactory integration tests for GET/PUT /me — the profile endpoint
/// that carries the avatar emoji (ADR-0019). The emoji is untrusted text rendered in other
/// members' UIs, so validation is the API's job (ADR-0006), not the client picker's.</summary>
public class MeEndpointTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<string> DetailAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("detail").GetString() ?? "";
    }

    [Fact]
    public async Task Me_carries_email_and_null_emoji_by_default()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        var me = await client.GetFromJsonAsync<MeDto>("/me", Web);

        Assert.NotNull(me!.Email);
        Assert.Null(me.AvatarEmoji);
    }

    [Fact]
    public async Task Put_me_sets_name_and_emoji()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        var res = await client.PutAsJsonAsync("/me", new UpdateMeRequest("Alexandra", "🦊"), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var updated = await res.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Equal("Alexandra", updated!.Name);
        Assert.Equal("🦊", updated.AvatarEmoji);

        // Persisted — a fresh read reflects the change.
        var me = await client.GetFromJsonAsync<MeDto>("/me", Web);
        Assert.Equal("🦊", me!.AvatarEmoji);
    }

    [Fact]
    public async Task Put_me_null_emoji_resets_to_initial()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        await client.PutAsJsonAsync("/me", new UpdateMeRequest("Alex", "🦊"), Web);
        var res = await client.PutAsJsonAsync("/me", new UpdateMeRequest("Alex", null), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var updated = await res.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Null(updated!.AvatarEmoji);
    }

    [Fact]
    public async Task Put_me_empty_emoji_resets_to_initial()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        await client.PutAsJsonAsync("/me", new UpdateMeRequest("Alex", "🦊"), Web);
        var res = await client.PutAsJsonAsync("/me", new UpdateMeRequest("Alex", "   "), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var updated = await res.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Null(updated!.AvatarEmoji);
    }

    [Theory]
    [InlineData("A")]           // plain letter
    [InlineData("🦊🐧")]        // two graphemes
    [InlineData("hej")]         // word
    [InlineData("<script>")]    // injection attempt, not an emoji
    public async Task Put_me_rejects_non_single_emoji(string bad)
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        var res = await client.PutAsJsonAsync("/me", new UpdateMeRequest("Alex", bad), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Ogiltig emoji", await DetailAsync(res));
    }

    [Fact]
    public async Task Put_me_blank_name_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        var res = await client.PutAsJsonAsync("/me", new UpdateMeRequest("   ", null), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Ange ditt namn", await DetailAsync(res));
    }

    [Fact]
    public async Task Put_me_requires_authentication()
    {
        using var factory = new SettlApiFactory();
        var anon = factory.CreateClient();

        var res = await anon.PutAsJsonAsync("/me", new UpdateMeRequest("Alex", "🦊"), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
