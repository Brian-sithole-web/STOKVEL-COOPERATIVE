using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;

namespace Stokvel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("platform")]
    public Task<PlatformDashboardDto> Platform(CancellationToken ct) => _dashboard.PlatformAsync(ct);

    [HttpGet("group/{groupId:guid}")]
    public Task<GroupDashboardDto> Group(Guid groupId, CancellationToken ct) => _dashboard.GroupAsync(groupId, ct);

    [HttpGet("member/{groupId:guid}")]
    public Task<MemberDashboardDto> Member(Guid groupId, CancellationToken ct) => _dashboard.MemberAsync(groupId, ct);
}

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet("groups/{groupId:guid}/statement")]
    public Task<GroupStatementDto> GroupStatement(Guid groupId, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct) =>
        _reports.GroupStatementAsync(groupId, from, to, ct);

    [HttpGet("groups/{groupId:guid}/members")]
    public Task<IReadOnlyList<MemberStatementDto>> Members(Guid groupId, CancellationToken ct) =>
        _reports.MemberStatementsAsync(groupId, ct);

    [HttpGet("loans")]
    public Task<IReadOnlyList<LoanReportRowDto>> Loans([FromQuery] Guid? groupId, CancellationToken ct) =>
        _reports.LoanReportAsync(groupId, ct);
}

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public Task<IReadOnlyList<NotificationDto>> Mine(CancellationToken ct) => _notifications.MineAsync(ct);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;
    public AuditController(IAuditService audit) => _audit = audit;

    [HttpGet]
    public Task<IReadOnlyList<AuditLogDto>> List([FromQuery] Guid? groupId, CancellationToken ct) =>
        _audit.ListAsync(groupId, ct);
}

[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet]
    public Task<PlatformSettingsDto> Get(CancellationToken ct) => _settings.GetAsync(ct);

    [HttpPut]
    public async Task<IActionResult> Update(UpdatePlatformSettingsRequest request, CancellationToken ct)
    {
        await _settings.UpdateAsync(request, ct);
        return NoContent();
    }
}
