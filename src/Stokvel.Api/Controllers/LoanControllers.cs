using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;
using Stokvel.Domain;

namespace Stokvel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/group-loans")]
public class GroupLoansController : ControllerBase
{
    private readonly ILoanService _loans;
    public GroupLoansController(ILoanService loans) => _loans = loans;

    [HttpGet("eligibility")]
    public Task<EligibilityResultDto> Eligibility(Guid groupId, [FromQuery] decimal amount, [FromQuery] LendingSource source, CancellationToken ct) =>
        _loans.GroupEligibilityAsync(groupId, amount, source, ct);

    [HttpGet]
    public Task<IReadOnlyList<GroupLoanDto>> List(Guid groupId, CancellationToken ct) =>
        _loans.ListGroupLoansAsync(groupId, ct);

    [HttpPost]
    public Task<GroupLoanDto> Create(Guid groupId, CreateGroupLoanRequest request, CancellationToken ct) =>
        _loans.CreateGroupLoanAsync(groupId, request, ct);

    [HttpPost("{loanId:guid}/submit")]
    public Task<GroupLoanDto> Submit(Guid groupId, Guid loanId, CancellationToken ct) =>
        _loans.SubmitGroupLoanAsync(groupId, loanId, ct);

    [HttpPost("{loanId:guid}/decide")]
    public Task<GroupLoanDto> Decide(Guid groupId, Guid loanId, DecideLoanRequest request, CancellationToken ct) =>
        _loans.DecideGroupLoanAsync(groupId, loanId, request, ct);

    [HttpPost("{loanId:guid}/disburse")]
    public Task<GroupLoanDto> Disburse(Guid groupId, Guid loanId, CancellationToken ct) =>
        _loans.DisburseGroupLoanAsync(groupId, loanId, ct);

    [HttpPost("{loanId:guid}/repayments")]
    public async Task<IActionResult> Repay(Guid groupId, Guid loanId, RecordRepaymentRequest request, CancellationToken ct)
    {
        await _loans.RecordGroupRepaymentAsync(groupId, loanId, request, ct);
        return NoContent();
    }

    [HttpGet("{loanId:guid}")]
    public Task<GroupLoanDto> Get(Guid groupId, Guid loanId, CancellationToken ct) => _loans.GetGroupLoanAsync(loanId, ct);

    [HttpGet("{loanId:guid}/schedule")]
    public Task<IReadOnlyList<LoanRepaymentDto>> Schedule(Guid groupId, Guid loanId, CancellationToken ct) =>
        _loans.GroupScheduleAsync(loanId, ct);
}

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/member-loans")]
public class MemberLoansController : ControllerBase
{
    private readonly ILoanService _loans;
    public MemberLoansController(ILoanService loans) => _loans = loans;

    [HttpGet("eligibility")]
    public Task<EligibilityResultDto> Eligibility(Guid groupId, [FromQuery] Guid memberId, [FromQuery] decimal amount, CancellationToken ct) =>
        _loans.MemberEligibilityAsync(groupId, memberId, amount, ct);

    [HttpGet]
    public Task<IReadOnlyList<MemberLoanDto>> List(Guid groupId, [FromQuery] Guid? memberId, CancellationToken ct) =>
        _loans.ListMemberLoansAsync(groupId, memberId, ct);

    [HttpPost]
    public Task<MemberLoanDto> Create(Guid groupId, CreateMemberLoanRequest request, CancellationToken ct) =>
        _loans.CreateMemberLoanAsync(groupId, request, ct);

    [HttpPost("{loanId:guid}/decide")]
    public Task<MemberLoanDto> Decide(Guid groupId, Guid loanId, DecideLoanRequest request, CancellationToken ct) =>
        _loans.DecideMemberLoanAsync(groupId, loanId, request, ct);

    [HttpPost("{loanId:guid}/disburse")]
    public Task<MemberLoanDto> Disburse(Guid groupId, Guid loanId, CancellationToken ct) =>
        _loans.DisburseMemberLoanAsync(groupId, loanId, ct);

    [HttpPost("{loanId:guid}/repayments")]
    public async Task<IActionResult> Repay(Guid groupId, Guid loanId, RecordRepaymentRequest request, CancellationToken ct)
    {
        await _loans.RecordMemberRepaymentAsync(groupId, loanId, request, ct);
        return NoContent();
    }

    [HttpGet("{loanId:guid}")]
    public Task<MemberLoanDto> Get(Guid groupId, Guid loanId, CancellationToken ct) => _loans.GetMemberLoanAsync(loanId, ct);

    [HttpGet("{loanId:guid}/schedule")]
    public Task<IReadOnlyList<LoanRepaymentDto>> Schedule(Guid groupId, Guid loanId, CancellationToken ct) =>
        _loans.MemberScheduleAsync(loanId, ct);
}

[ApiController]
[Authorize]
[Route("api/loans")]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loans;
    public LoansController(ILoanService loans) => _loans = loans;

    [HttpGet("group")]
    public Task<IReadOnlyList<GroupLoanDto>> AllGroup(CancellationToken ct) => _loans.ListGroupLoansAsync(null, ct);
}
