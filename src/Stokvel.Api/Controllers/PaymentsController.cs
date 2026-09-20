using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;

namespace Stokvel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/payments")]
public class GroupPaymentsController : ControllerBase
{
    private readonly ICardPaymentService _payments;
    public GroupPaymentsController(ICardPaymentService payments) => _payments = payments;

    [HttpGet("first")]
    public Task<FirstPaymentDto> First(Guid groupId, CancellationToken ct) =>
        _payments.GetFirstPaymentAsync(groupId, ct);

    [HttpPost("card")]
    public Task<CardPaymentDto> StartCard(Guid groupId, StartCardPaymentRequest request, CancellationToken ct) =>
        _payments.StartCheckoutAsync(groupId, request, ct);
}

[ApiController]
[Authorize]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ICardPaymentService _payments;
    public PaymentsController(ICardPaymentService payments) => _payments = payments;

    [HttpGet("{paymentId:guid}")]
    public Task<CardPaymentDto> Get(Guid paymentId, CancellationToken ct) =>
        _payments.GetAsync(paymentId, ct);

    [HttpPost("{paymentId:guid}/challenge")]
    public Task<CardPaymentDto> Challenge(Guid paymentId, CancellationToken ct) =>
        _payments.BeginBankChallengeAsync(paymentId, ct);

    [HttpPost("{paymentId:guid}/complete")]
    public Task<CardPaymentDto> Complete(Guid paymentId, CompleteCardPaymentRequest request, CancellationToken ct) =>
        _payments.CompleteAsync(paymentId, request, ct);
}
