using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Testing;
using TestSystem.Services.Features.Testing;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/testing/reviews")]
[Authorize(Policy = "TeacherOrAdmin")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReviewsController(IMediator mediator) => _mediator = mediator;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("pending")]
    public async Task<ActionResult<List<PendingReviewDto>>> GetPending()
    {
        return Ok(await _mediator.Send(new GetPendingReviewsQuery(UserId)));
    }

    [HttpPut("{answerId}")]
    public async Task<ActionResult<ReviewedAnswerDto>> ReviewAnswer(Guid answerId, [FromBody] ReviewAnswerRequest request)
    {
        return Ok(await _mediator.Send(new ReviewAnswerCommand(answerId, UserId, request.Status, request.Feedback, request.Points)));
    }
}
