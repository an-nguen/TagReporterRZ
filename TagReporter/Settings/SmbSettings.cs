using System;

namespace TagReporter.Settings;

public class SmbSettings
{
    public string Host { get; set; } = string.Empty;
    public string ShareName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}