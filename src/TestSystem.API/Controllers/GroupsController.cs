using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestSystem.Services.DTOs.Groups;
using TestSystem.Services.Features.Groups;

namespace TestSystem.API.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public class GroupsController : ControllerBase
{
    private readonly IMediator _mediator;
    public GroupsController(IMediator mediator) => _mediator = mediator;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Teacher: create group
    [HttpPost]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<GroupDto>> Create([FromBody] CreateGroupRequest request)
    {
        var result = await _mediator.Send(new CreateGroupCommand(request.Name, request.Description, UserId));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // Teacher: get my groups
    [HttpGet("my")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<List<GroupDto>>> GetMyGroups()
    {
        return Ok(await _mediator.Send(new GetMyGroupsQuery(UserId)));
    }

    // Get group by id
    [HttpGet("{id}")]
    public async Task<ActionResult<GroupDto>> GetById(Guid id)
    {
        return Ok(await _mediator.Send(new GetGroupByIdQuery(id)));
    }

    // Teacher: update group
    [HttpPut("{id}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<ActionResult<GroupDto>> Update(Guid id, [FromBody] UpdateGroupRequest request)
    {
        return Ok(await _mediator.Send(new UpdateGroupCommand(id, UserId, request.Name, request.Description, request.IsActive)));
    }

    // Teacher: delete group
    [HttpDelete("{id}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteGroupCommand(id, UserId));
        return NoContent();
    }

    // Get group members
    [HttpGet("{id}/members")]
    public async Task<ActionResult<List<GroupMemberDto>>> GetMembers(Guid id)
    {
        return Ok(await _mediator.Send(new GetGroupMembersQuery(id)));
    }

    // Teacher: remove member
    [HttpDelete("{groupId}/members/{memberId}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid memberId)
    {
        await _mediator.Send(new RemoveGroupMemberCommand(groupId, memberId, UserId));
        return NoContent();
    }

    // Student: join group by invite code
    [HttpPost("join")]
    public async Task<ActionResult<GroupDto>> JoinGroup([FromBody] JoinGroupRequest request)
    {
        return Ok(await _mediator.Send(new JoinGroupByCodeCommand(request.InviteCode, UserId)));
    }

    // Student: get my groups
    [HttpGet("my-memberships")]
    public async Task<ActionResult<List<GroupDto>>> GetMyMemberships()
    {
        return Ok(await _mediator.Send(new GetMyGroupsAsStudentQuery(UserId)));
    }

    // Student: leave group
    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveGroup(Guid id)
    {
        await _mediator.Send(new LeaveGroupCommand(id, UserId));
        return Ok();
    }
}
