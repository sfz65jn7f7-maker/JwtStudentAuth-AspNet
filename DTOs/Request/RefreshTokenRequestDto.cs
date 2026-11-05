using System.ComponentModel.DataAnnotations;

namespace StudentAuth.Api.Dtos.Request;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}