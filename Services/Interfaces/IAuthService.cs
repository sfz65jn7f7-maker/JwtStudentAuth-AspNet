using StudentAuth.Api.Dtos.Request;
using StudentAuth.Api.Dtos.Response;

namespace StudentAuth.Api.Services.Interfaces;

public interface IAuthService
{
    Task<StudentResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
}