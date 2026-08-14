using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;

namespace Stokvel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/contributions")]
public class ContributionsController : ControllerBase
{
    private readonly IContributionService _contributions;
    public ContributionsController(IContributionService contributions) => _contributions = contributions;

    [HttpGet]
    public Task<IReadOnlyList<ContributionDto>> List(Guid groupId, CancellationToken ct) =>
        _contributions.ListAsync(groupId, ct);

    [HttpPost]
    public Task<ContributionDto> Record(Guid groupId, RecordContributionRequest request, CancellationToken ct) =>
        _contributions.RecordAsync(groupId, request, ct);

    [HttpPost("{contributionId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid groupId, Guid contributionId, CancellationToken ct)
    {
        await _contributions.ConfirmAsync(groupId, contributionId, ct);
        return NoContent();
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid groupId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        await _contributions.GenerateMonthlyObligationsAsync(groupId, year, month, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/instalments")]
public class InstalmentsController : ControllerBase
{
    private readonly IContributionService _contributions;
    public InstalmentsController(IContributionService contributions) => _contributions = contributions;

    [HttpGet]
    public Task<IReadOnlyList<InstalmentDto>> List(Guid groupId, [FromQuery] Guid? memberId, CancellationToken ct) =>
        _contributions.ListInstalmentsAsync(groupId, memberId, ct);
}

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/ledger")]
public class LedgerController : ControllerBase
{
    private readonly ILedgerService _ledger;
    public LedgerController(ILedgerService ledger) => _ledger = ledger;

    [HttpGet]
    public Task<LedgerSummaryDto> Get(Guid groupId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        _ledger.GetLedgerAsync(groupId, from, to, ct);

    [HttpGet("transactions")]
    public Task<IReadOnlyList<TransactionDto>> Transactions(Guid groupId, [FromQuery] Guid? memberId, CancellationToken ct) =>
        _ledger.ListTransactionsAsync(groupId, memberId, ct);

    [HttpPost("transactions/{transactionId:guid}/reverse")]
    public async Task<IActionResult> Reverse(Guid groupId, Guid transactionId, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        await _ledger.ReverseAsync(groupId, transactionId, request.Reason, ct);
        return NoContent();
    }
}
