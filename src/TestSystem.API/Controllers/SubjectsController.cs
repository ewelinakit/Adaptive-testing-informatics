using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Subjects;
using TestSystem.Services.Features.Subjects;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SubjectsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<SubjectDto>>> GetAll()
    {
        return Ok(await _mediator.Send(new GetSubjectsQuery()));
    }

    [HttpPost]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<SubjectDto>> Create([FromBody] CreateSubjectRequest request)
    {
        return Ok(await _mediator.Send(new CreateSubjectCommand(request.Name, request.Description)));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<SubjectDto>> Update(Guid id, [FromBody] UpdateSubjectRequest request)
    {
        return Ok(await _mediator.Send(new UpdateSubjectCommand(id, request.Name, request.Description)));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteSubjectCommand(id));
        return NoContent();
    }
}
