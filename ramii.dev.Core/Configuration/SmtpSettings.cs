namespace ramii.dev.Core.Configuration;

public class SmtpSettings
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "mail.infomaniak.com";
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string SenderName { get; set; } = "ramii.dev";
    public string SenderAddress { get; set; } = "kontakt@ramii.dev";
    public bool UseStartTls { get; set; } = true;
}
