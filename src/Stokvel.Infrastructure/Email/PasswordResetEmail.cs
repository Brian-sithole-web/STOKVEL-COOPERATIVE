namespace Stokvel.Infrastructure.Email;

public static class PasswordResetEmail
{
    public static string BuildHtml(string fullName, string resetUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(fullName) ? "Hello" : $"Hello {System.Net.WebUtility.HtmlEncode(fullName)}";
        return $"""
            <div style="font-family:Georgia,serif;background:#f4f1ea;padding:32px;">
              <div style="max-width:560px;margin:0 auto;background:#fff;border-radius:16px;padding:32px;color:#10261c;">
                <p style="letter-spacing:.2em;font-size:11px;color:#c4a35a;font-weight:700;">PKVELA COOPERATIVE</p>
                <h1 style="font-size:28px;margin:8px 0 12px;">Reset your password</h1>
                <p style="color:#6b6b6b;line-height:1.5;">
                  {greeting}, we received a request to reset the password for your pkvela account.
                  Open the link below to choose a new password and sign in.
                </p>
                <p style="margin:28px 0;">
                  <a href="{resetUrl}" style="display:inline-block;background:#063925;color:#f5f5f5;text-decoration:none;padding:12px 20px;border-radius:10px;font-weight:700;">
                    Reset your password
                  </a>
                </p>
                <p style="color:#6b6b6b;font-size:13px;">This link expires in 2 hours. If you did not ask for a reset, you can ignore this email.<br />If the button does not work, paste this address into your browser:<br />{System.Net.WebUtility.HtmlEncode(resetUrl)}</p>
              </div>
            </div>
            """;
    }
}
