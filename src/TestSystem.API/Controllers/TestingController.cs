using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Testing;
using TestSystem.Services.Features.Testing;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/testing")]
[Authorize]
public class TestingController : ControllerBase
{
    private readonly IMediator _mediator;
    public TestingController(IMediator mediator) => _mediator = mediator;

    [HttpPost("start")]
    public async Task<ActionResult<TestSessionDto>> StartTest([FromBody] StartTestRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _mediator.Send(new StartTestCommand(userId, request.TestId)));
    }

    [HttpGet("{sessionId}/next-question")]
    public async Task<ActionResult<TestQuestionDto>> GetNextQuestion(Guid sessionId)
    {
        var result = await _mediator.Send(new GetNextQuestionQuery(sessionId));
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpPost("{sessionId}/submit")]
    public async Task<ActionResult<SubmitAnswerResponse>> SubmitAnswer(Guid sessionId, [FromBody] SubmitAnswerRequest request)
    {
        return Ok(await _mediator.Send(new SubmitAnswerCommand(sessionId, request.QuestionId, request.SelectedAnswerOptionIds, request.TextAnswer)));
    }

    [HttpPost("{sessionId}/finish")]
    public async Task<ActionResult<TestSessionDto>> FinishTest(Guid sessionId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _mediator.Send(new FinishTestCommand(sessionId, userId)));
    }

    [HttpGet("{sessionId}/result")]
    public async Task<ActionResult<TestResultDto>> GetResult(Guid sessionId)
    {
        return Ok(await _mediator.Send(new GetTestResultQuery(sessionId)));
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<TestSessionDto>>> GetHistory()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _mediator.Send(new GetTestHistoryQuery(userId)));
    }
}
