using Microsoft.AspNetCore.Http;
using Stokvel.Application.Common;
using System.Security.Claims;

namespace Stokvel.Infrastructure.Security;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public bool IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var value = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? _http.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
                           ?? _http.HttpContext?.User.FindFirstValue("email")
                           ?? string.Empty;

    public string FullName => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
                              ?? _http.HttpContext?.User.FindFirstValue("name")
                              ?? string.Empty;

    public bool IsPlatformAdmin =>
        _http.HttpContext?.User.IsInRole(Domain.SystemRoles.PlatformAdmin) == true
        || _http.HttpContext?.User.HasClaim("role", Domain.SystemRoles.PlatformAdmin) == true
        || _http.HttpContext?.User.HasClaim(ClaimTypes.Role, Domain.SystemRoles.PlatformAdmin) == true;
}
