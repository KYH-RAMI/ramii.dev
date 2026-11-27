namespace ramii.dev.Core.Configuration;

public class ImapSettings
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "mail.infomaniak.com";
    public int Port { get; set; } = 993;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FolderName { get; set; } = "Backstage";
    public bool UseSsl { get; set; } = true;
    public bool AutoSyncEnabled { get; set; } = false;
    public int AutoSyncIntervalSeconds { get; set; } = 300;
}
