using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;
using Stokvel.Domain;

namespace Stokvel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _groups;
    public GroupsController(IGroupService groups) => _groups = groups;

    [HttpGet]
    public Task<IReadOnlyList<GroupListItemDto>> List(CancellationToken ct) => _groups.ListAsync(ct);

    [HttpPost]
    public Task<GroupDetailDto> Create(CreateGroupRequest request, CancellationToken ct) => _groups.CreateAsync(request, ct);

    [HttpGet("{id:guid}")]
    public Task<GroupDetailDto> Get(Guid id, CancellationToken ct) => _groups.GetAsync(id, ct);

    [HttpPut("{id:guid}/rules")]
    public async Task<IActionResult> UpdateRules(Guid id, UpdateGroupRulesRequest request, CancellationToken ct)
    {
        await _groups.UpdateRulesAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/invites")]
    public Task<InviteResultDto> Invite(Guid id, InviteMemberRequest request, CancellationToken ct) =>
        _groups.InviteAsync(id, request, ct);

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] InviteMemberRequest request, [FromQuery] GroupRole role = GroupRole.Member, CancellationToken ct = default)
    {
        await _groups.AddMemberByEmailAsync(id, request.Email, role, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public Task<IReadOnlyList<MemberDto>> Members(Guid id, CancellationToken ct) => _groups.GetMembersAsync(id, ct);

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await _groups.SubmitForApprovalAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        await _groups.ApproveAndActivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        await _groups.SuspendAsync(id, request.Reason, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await _groups.CloseAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _groups.DeleteInactiveAsync(id, ct);
        return NoContent();
    }
}

public record ReasonRequest(string Reason);
