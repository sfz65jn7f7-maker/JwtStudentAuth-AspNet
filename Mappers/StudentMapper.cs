using StudentAuth.Api.Dtos.Response;
using StudentAuth.Api.Models;

namespace StudentAuth.Api.Mappers;

public static class StudentMapper
{
    public static StudentResponseDto ToResponseDto(this Student student)
    {
        return new StudentResponseDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            Role = student.Role.ToString(),
            CreatedAt = student.CreatedAt,
            UpdatedAt = student.UpdatedAt
        };
    }
}