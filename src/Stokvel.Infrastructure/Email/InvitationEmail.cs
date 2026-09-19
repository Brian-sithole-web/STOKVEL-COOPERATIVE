namespace Stokvel.Infrastructure.Email;

public static class InvitationEmail
{
    public static string BuildHtml(string groupName, string inviteUrl)
    {
        return $"""
            <div style="font-family:Georgia,serif;background:#f4f1ea;padding:32px;">
              <div style="max-width:560px;margin:0 auto;background:#fff;border-radius:16px;padding:32px;color:#10261c;">
                <p style="letter-spacing:.2em;font-size:11px;color:#c4a35a;font-weight:700;">PKVELA COOPERATIVE</p>
                <h1 style="font-size:28px;margin:8px 0 12px;">You have been invited</h1>
                <p style="color:#6b6b6b;line-height:1.5;">
                  You were invited to join <strong>{System.Net.WebUtility.HtmlEncode(groupName)}</strong>.
                  Open the link below to create your password and get your login details.
                </p>
                <p style="margin:28px 0;">
                  <a href="{inviteUrl}" style="display:inline-block;background:#063925;color:#f5f5f5;text-decoration:none;padding:12px 20px;border-radius:10px;font-weight:700;">
                    Create your password
                  </a>
                </p>
                <p style="color:#6b6b6b;font-size:13px;">This invitation expires in 14 days. If the button does not work, paste this address into your browser:<br />{System.Net.WebUtility.HtmlEncode(inviteUrl)}</p>
              </div>
            </div>
            """;
    }
}
