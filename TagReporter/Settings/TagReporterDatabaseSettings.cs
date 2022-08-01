namespace TagReporter.Settings;

public interface ITagReporterDatabaseSettings
{
    string? ConnectionString { get; set; }
    string? DatabaseName { get; set; }
    string? ZoneCollectionName { get; set; }
        
    string? TagCollectionName { get; set; }
        
    string? ConfigCollectionName { get; set; }
        
    string? AccountCollectionName { get; set; }
}
public class TagReporterDatabaseSettings: ITagReporterDatabaseSettings
{
    public string? ConnectionString { get; set; }
    public string? DatabaseName { get; set; }
    public string? ZoneCollectionName { get; set; }
        
    public string? TagCollectionName { get; set; }
        
    public string? ConfigCollectionName { get; set; }
        
    public string? AccountCollectionName { get; set; }

    public bool IsValid() => !string.IsNullOrEmpty(ConnectionString)
                             && !string.IsNullOrEmpty(DatabaseName)
                             && !string.IsNullOrEmpty(ZoneCollectionName)
                             && !string.IsNullOrEmpty(TagCollectionName)
                             && !string.IsNullOrEmpty(ConfigCollectionName)
                             && !string.IsNullOrEmpty(AccountCollectionName);
}