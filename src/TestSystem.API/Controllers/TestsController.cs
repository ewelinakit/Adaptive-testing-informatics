using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Tests;
using TestSystem.Services.Features.Tests;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/tests")]
[Authorize]
public class TestsController : ControllerBase
{
    private readonly IMediator _mediator;
    public TestsController(IMediator mediator) => _mediator = mediator;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Teacher: create test
    [HttpPost]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<TestDto>> Create([FromBody] CreateTestRequest request)
    {
        var result = await _mediator.Send(new CreateTestCommand(
            request.Title, request.Description, request.GroupId, request.TopicModuleId, request.QuestionIds, UserId,
            request.TimeLimitMinutes, request.MaxAttempts, request.ShuffleQuestions, request.ShuffleAnswers,
            request.AvailableFrom, request.AvailableTo, request.ShowCorrectAnswers));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // Get tests by group
    [HttpGet("by-group/{groupId}")]
    public async Task<ActionResult<List<TestDto>>> GetByGroup(Guid groupId)
    {
        return Ok(await _mediator.Send(new GetTestsByGroupQuery(groupId)));
    }

    // Get test by id
    [HttpGet("{id}")]
    public async Task<ActionResult<TestDto>> GetById(Guid id)
    {
        return Ok(await _mediator.Send(new GetTestByIdQuery(id)));
    }

    // Teacher: update test
    [HttpPut("{id}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<TestDto>> Update(Guid id, [FromBody] UpdateTestRequest request)
    {
        return Ok(await _mediator.Send(new UpdateTestCommand(
            id, UserId, request.Title, request.Description, request.IsActive,
            request.TimeLimitMinutes, request.MaxAttempts,
            request.ShuffleQuestions, request.ShuffleAnswers,
            request.AvailableFrom, request.AvailableTo,
            request.ClearTimeLimitMinutes, request.ClearMaxAttempts,
            request.ClearAvailableFrom, request.ClearAvailableTo,
            request.ShowCorrectAnswers)));
    }

    // Teacher: delete test
    [HttpDelete("{id}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteTestCommand(id, UserId));
        return NoContent();
    }

    // Student: get assigned tests (all groups)
    [HttpGet("assigned")]
    public async Task<ActionResult<List<AssignedTestDto>>> GetAssigned()
    {
        return Ok(await _mediator.Send(new GetAssignedTestsQuery(UserId)));
    }

    // Student: get assigned tests by group
    [HttpGet("assigned/group/{groupId}")]
    public async Task<ActionResult<List<AssignedTestDto>>> GetAssignedByGroup(Guid groupId)
    {
        return Ok(await _mediator.Send(new GetAssignedTestsByGroupQuery(groupId, UserId)));
    }
}
