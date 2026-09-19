namespace Stokvel.Infrastructure.Email;

public sealed class MailSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string FromName { get; set; } = "pkvela Cooperative";
    public string FromAddress { get; set; } = "noreply@pkvela.coop";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AppBaseUrl { get; set; } = "http://localhost:5173";
}
