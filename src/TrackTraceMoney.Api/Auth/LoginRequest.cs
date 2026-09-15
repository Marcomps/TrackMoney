using System.ComponentModel.DataAnnotations;

namespace TrackTraceMoney.Api.Auth;

public sealed record LoginRequest(
    [property: Required] string Email,
    [property: Required] string Password);
