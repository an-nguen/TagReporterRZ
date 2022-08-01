namespace TagReporter.Domains;

public interface IEmailSettings {
    string? Email { get; set; }
    string? Password { get; set; }
        
    string? SmtpServer { get; set; }
    short SmtpServerPort { get; set; }
}
    
public class EmailSettings: IEmailSettings {

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? SmtpServer { get; set; }
    public short SmtpServerPort { get; set; }
}