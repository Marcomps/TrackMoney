using System.ComponentModel.DataAnnotations;

namespace TrackTraceMoney.Api.Auth;

public sealed record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password);
