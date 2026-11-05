using StudentAuth.Api.Dtos.Request;
using StudentAuth.Api.Dtos.Response;

namespace StudentAuth.Api.Services.Interfaces;

public interface IStudentService
{
    Task<StudentResponseDto> GetByIdAsync(int id);
    Task<StudentResponseDto> UpdateAsync(int id, UpdateStudentRequestDto request);
    Task DeleteAsync(int id);
}