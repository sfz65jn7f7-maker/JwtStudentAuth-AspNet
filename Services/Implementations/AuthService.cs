using StudentAuth.Api.Dtos.Request;
using StudentAuth.Api.Dtos.Response;
using StudentAuth.Api.Enums;
using StudentAuth.Api.Exceptions;
using StudentAuth.Api.Mappers;
using StudentAuth.Api.Models;
using StudentAuth.Api.Repositories.Interfaces;
using StudentAuth.Api.Services.Interfaces;

namespace StudentAuth.Api.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        IStudentRepository studentRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _studentRepository = studentRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<StudentResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var email = NormalizeEmail(request.Email);

        if (await _studentRepository.EmailExistsAsync(email))
            throw new ConflictException("Un compte existe déjà avec cet email.");

        var student = new Student
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Student
        };

        await _studentRepository.AddAsync(student);

        return student.ToResponseDto();
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var student = await _studentRepository.GetByEmailAsync(NormalizeEmail(request.Email));

        // Même message dans les deux cas pour ne pas révéler si l'email existe
        if (student is null || !_passwordHasher.Verify(request.Password, student.PasswordHash))
            throw new UnauthorizedException("Email ou mot de passe incorrect.");

        return await CreateAuthResponseAsync(student);
    }

    public async Task<AuthResponseDto> RefreshAsync(string refreshToken)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(refreshToken)
            ?? throw new UnauthorizedException("Refresh token invalide.");

        // Un token déjà révoqué qui revient = vol possible : on coupe toutes les sessions
        if (stored.IsRevoked)
        {
            await _refreshTokenRepository.RevokeAllByStudentIdAsync(stored.StudentId);
            throw new UnauthorizedException("Refresh token révoqué. Veuillez vous reconnecter.");
        }

        if (stored.IsExpired)
            throw new UnauthorizedException("Refresh token expiré. Veuillez vous reconnecter.");

        // Rotation : l'ancien token est révoqué, un nouveau est émis
        stored.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(stored);

        return await CreateAuthResponseAsync(stored.Student);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(refreshToken);

        // Idempotent : token inconnu ou déjà révoqué, on ne fait rien
        if (stored is null || stored.IsRevoked)
            return;

        stored.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(stored);
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(Student student)
    {
        var (accessToken, accessExpiresAt) = _tokenService.GenerateAccessToken(student);
        var refreshToken = _tokenService.GenerateRefreshToken(student.Id);

        await _refreshTokenRepository.AddAsync(refreshToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            Student = student.ToResponseDto()
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}