cd src/Stokvel.Apiusing Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;

namespace Stokvel.Api.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly ISetupService _setup;
    public SetupController(ISetupService setup) => _setup = setup;

    [HttpGet("status")]
    public async Task<SetupStatusDto> Status(CancellationToken ct) => await _setup.GetStatusAsync(ct);

    [HttpPost("initialize")]
    public async Task<AuthResponse> Initialize(InitializeSetupRequest request, CancellationToken ct) =>
        await _setup.InitializeAsync(request, ct);
}

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) => _auth.RegisterAsync(request, ct);

    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => _auth.LoginAsync(request, ct);

    [HttpPost("accept-invite")]
    public Task<AuthResponse> AcceptInvite(AcceptInviteRequest request, CancellationToken ct) => _auth.AcceptInviteAsync(request, ct);

    [Authorize]
    [HttpGet("me")]
    public Task<AuthResponse> Me(CancellationToken ct) => _auth.MeAsync(ct);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}
