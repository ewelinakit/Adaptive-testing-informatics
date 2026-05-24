using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Statistics;
using TestSystem.Services.Features.Statistics;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IMediator _mediator;
    public StatisticsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("student/{studentId}")]
    public async Task<ActionResult<StudentStatsDto>> GetStudentStats(Guid studentId)
    {
        return Ok(await _mediator.Send(new GetStudentStatsQuery(studentId)));
    }

    [HttpGet("my")]
    public async Task<ActionResult<StudentStatsDto>> GetMyStats()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _mediator.Send(new GetStudentStatsQuery(userId)));
    }

    [HttpGet("group/{groupId}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<GroupStatsDto>> GetGroupStats(Guid groupId)
    {
        return Ok(await _mediator.Send(new GetGroupStatsQuery(groupId)));
    }

    [HttpGet("overview")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<OverviewStatsDto>> GetOverview()
    {
        return Ok(await _mediator.Send(new GetOverviewStatsQuery()));
    }
}
