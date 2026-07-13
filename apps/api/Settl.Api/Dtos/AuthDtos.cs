namespace Settl.Api.Dtos;

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);
