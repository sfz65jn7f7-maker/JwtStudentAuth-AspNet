using StudentAuth.Api.Dtos.Request;
using StudentAuth.Api.Dtos.Response;
using StudentAuth.Api.Exceptions;
using StudentAuth.Api.Mappers;
using StudentAuth.Api.Models;
using StudentAuth.Api.Repositories.Interfaces;
using StudentAuth.Api.Services.Interfaces;

namespace StudentAuth.Api.Services.Implementations;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;

    public StudentService(
        IStudentRepository studentRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher)
    {
        _studentRepository = studentRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<StudentResponseDto> GetByIdAsync(int id)
    {
        var student = await GetStudentOrThrowAsync(id);
        return student.ToResponseDto();
    }

    public async Task<StudentResponseDto> UpdateAsync(int id, UpdateStudentRequestDto request)
    {
        var student = await GetStudentOrThrowAsync(id);
        var newEmail = request.Email.Trim().ToLowerInvariant();

        if (newEmail != student.Email && await _studentRepository.EmailExistsAsync(newEmail))
            throw new ConflictException("Cet email est déjà utilisé par un autre compte.");

        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.Email = newEmail;
        student.UpdatedAt = DateTime.UtcNow;

        var passwordChanged = !string.IsNullOrWhiteSpace(request.Password);
        if (passwordChanged)
            student.PasswordHash = _passwordHasher.Hash(request.Password!);

        await _studentRepository.UpdateAsync(student);

        // Changement de mot de passe : toutes les sessions existantes sont fermées
        if (passwordChanged)
            await _refreshTokenRepository.RevokeAllByStudentIdAsync(student.Id);

        return student.ToResponseDto();
    }

    public async Task DeleteAsync(int id)
    {
        var student = await GetStudentOrThrowAsync(id);
        await _studentRepository.DeleteAsync(student); // les refresh tokens sont supprimés en cascade
    }

    private async Task<Student> GetStudentOrThrowAsync(int id)
    {
        return await _studentRepository.GetByIdAsync(id)
            ?? throw new NotFoundException("Étudiant introuvable.");
    }
}