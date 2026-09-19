using Microsoft.Extensions.Configuration;

namespace Stokvel.Infrastructure.Email;

public static class MailLink
{
    public static string ResolveAppBaseUrl(IConfiguration? config, string? clientOrigin)
    {
        var allowedOrigins = config?.GetSection("Cors:Origins").Get<string[]>()
                             ?? ["http://localhost:5173", "http://localhost:5174", "http://localhost:5175", "http://localhost:5176"];
        var origin = clientOrigin?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(origin)
            && allowedOrigins.Any(allowed => string.Equals(allowed.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase)))
        {
            return origin;
        }

        return (config?["Mail:AppBaseUrl"] ?? "http://localhost:5175").TrimEnd('/');
    }

    public static string PasswordReset(string appBaseUrl, string email, string token) =>
        $"{appBaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
}
