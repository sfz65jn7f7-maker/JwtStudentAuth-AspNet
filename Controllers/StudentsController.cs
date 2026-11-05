using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentAuth.Api.Dtos.Request;
using StudentAuth.Api.Dtos.Response;
using StudentAuth.Api.Enums;
using StudentAuth.Api.Services.Interfaces;

namespace StudentAuth.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<StudentResponseDto>> GetMe()
    {
        var student = await _studentService.GetByIdAsync(GetCurrentUserId());
        return Ok(student);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<StudentResponseDto>> Update(int id, UpdateStudentRequestDto request)
    {
        // Seul le propriétaire du compte peut le modifier
        if (id != GetCurrentUserId())
            return Forbid();

        var student = await _studentService.UpdateAsync(id, request);
        return Ok(student);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        // Le propriétaire ou un administrateur peut supprimer
        if (id != GetCurrentUserId() && !User.IsInRole(UserRole.Admin.ToString()))
            return Forbid();

        await _studentService.DeleteAsync(id);
        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(value!);
    }
}