using StudentAuth.Api.Models;

namespace StudentAuth.Api.Services.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(Student student);
    RefreshToken GenerateRefreshToken(int studentId);
}