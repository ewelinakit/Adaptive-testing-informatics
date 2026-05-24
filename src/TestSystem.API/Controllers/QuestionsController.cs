using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Questions;
using TestSystem.Services.Features.Questions;
using TestSystem.Models.Enums;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/questions")]
[Authorize]
public class QuestionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public QuestionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<QuestionDto>>> GetAll(
        [FromQuery] Guid? topicModuleId, [FromQuery] DifficultyLevel? difficulty, [FromQuery] bool? isActive)
    {
        return Ok(await _mediator.Send(new GetQuestionsQuery(topicModuleId, difficulty, isActive)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<QuestionDto>> GetById(Guid id)
    {
        return Ok(await _mediator.Send(new GetQuestionByIdQuery(id)));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<QuestionDto>> Create([FromBody] CreateQuestionRequest request)
    {
        var result = await _mediator.Send(new CreateQuestionCommand(
            request.Text, request.Explanation, request.TopicModuleId, request.DifficultyLevel, request.Points,
            request.AnswerOptions, request.IsOpenAnswer, request.CorrectAnswerText, request.IgnoreCase, request.IgnoreSimilarLetters));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<QuestionDto>> Update(Guid id, [FromBody] UpdateQuestionRequest request)
    {
        return Ok(await _mediator.Send(new UpdateQuestionCommand(
            id,
            request.Text,
            request.Explanation,
            request.DifficultyLevel,
            request.Points,
            request.IsActive,
            request.AnswerOptions,
            request.IsOpenAnswer,
            request.CorrectAnswerText,
            request.IgnoreCase,
            request.IgnoreSimilarLetters)));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteQuestionCommand(id));
        return NoContent();
    }

    [HttpPost("bulk")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<QuestionDto>>> BulkCreate([FromBody] BulkCreateQuestionsRequest request)
    {
        var commands = request.Questions.Select(q =>
            new CreateQuestionCommand(
                q.Text,
                q.Explanation,
                q.TopicModuleId,
                q.DifficultyLevel,
                q.Points,
                q.AnswerOptions,
                q.IsOpenAnswer,
                q.CorrectAnswerText,
                q.IgnoreCase,
                q.IgnoreSimilarLetters)).ToList();
        return Ok(await _mediator.Send(new BulkCreateQuestionsCommand(commands)));
    }
}
