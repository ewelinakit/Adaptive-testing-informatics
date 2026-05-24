using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Topics;
using TestSystem.Services.Features.Topics;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/topic-modules")]
[Authorize]
public class TopicModulesController : ControllerBase
{
    private readonly IMediator _mediator;
    public TopicModulesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<TopicModuleDto>>> GetAll([FromQuery] Guid? subjectId)
    {
        return Ok(await _mediator.Send(new GetTopicsQuery(subjectId)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TopicModuleDto>> GetById(Guid id)
    {
        return Ok(await _mediator.Send(new GetTopicByIdQuery(id)));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<TopicModuleDto>> Create([FromBody] CreateTopicRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _mediator.Send(new CreateTopicCommand(request.Title, request.SubjectId, request.OrderIndex, userId));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<TopicModuleDto>> Update(Guid id, [FromBody] UpdateTopicRequest request)
    {
        return Ok(await _mediator.Send(new UpdateTopicCommand(id, request.Title, request.SubjectId, request.OrderIndex)));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteTopicCommand(id));
        return NoContent();
    }
}
