using Newtonsoft.Json;

namespace TagReporter.DTOs;

public class TagResponse
{
    [JsonProperty("__type")]
    public string? Type { get; set; }
    [JsonProperty("managerName")]
    public string? ManagerName { get; set; }
    [JsonProperty("mac")]
    public string? Mac { get; set; }
    [JsonProperty("mirrors")]
    public object[]? Mirrors { get; set; }
    [JsonProperty("dbid")]
    public int Dbid { get; set; }
    [JsonProperty("notificationJS")]
    public string? NotificationJs { get; set; }
    [JsonProperty("name")]
    public string? Name { get; set; }
    [JsonProperty("uuid")]
    public string? Uuid { get; set; }
    [JsonProperty("comment")]
    public string? Comment { get; set; }
    [JsonProperty("slaveId")]
    public int SlaveId { get; set; }
    [JsonProperty("tagType")]
    public int TagType { get; set; }
    [JsonProperty("discon")]
    public object? Discon { get; set; }
    [JsonProperty("lastComm")]
    public long LastComm { get; set; }
    [JsonProperty("alive")]
    public bool Alive { get; set; }
    [JsonProperty("signaldBm")]
    public int SignalDbm { get; set; }
    [JsonProperty("batteryVolt")]
    public double BatteryVolt { get; set; }
    [JsonProperty("beeping")]
    public bool Beeping { get; set; }
    [JsonProperty("lit")]
    public bool Lit { get; set; }
    [JsonProperty("migrationPending")]
    public bool MigrationPending { get; set; }
    [JsonProperty("beepDurationDefault")]
    public int BeepDurationDefault { get; set; }
    [JsonProperty("eventState")]
    public int EventState { get; set; }
    [JsonProperty("tempEventState")]
    public int TempEventState { get; set; }
    public bool OutOfRange { get; set; }
    [JsonProperty("tempSpurTh")]
    public int TempSpurTh { get; set; }
    [JsonProperty("lux")]
    public int Lux { get; set; }
    [JsonProperty("temperature")]
    public double Temperature { get; set; }
    [JsonProperty("tempCalOffset")]
    public double TempCalOffset { get; set; }
    [JsonProperty("capCalOffset")]
    public double CapCalOffset { get; set; }
    public object? image_md5 { get; set; }
    public double cap { get; set; }
    public int capRaw { get; set; }
    public int az2 { get; set; }
    public int capEventState { get; set; }
    public int lightEventState { get; set; }
    public bool shorted { get; set; }
    public object? zmod { get; set; }
    public object? thermostat { get; set; }
    public object? playback { get; set; }
    public int postBackInterval { get; set; }
    public uint rev { get; set; }
    public uint version1 { get; set; }
    public int freqOffset { get; set; }
    public int freqCalApplied { get; set; }
    public int reviveEvery { get; set; }
    public int oorGrace { get; set; }
    public object? tempBL { get; set; }
    public object? capBL { get; set; }
    public object? luxBL { get; set; }
    public double LBTh { get; set; }
    public bool enLBN { get; set; }
    public int txpwr { get; set; }
    public bool rssiMode { get; set; }
    [JsonProperty("ds18")]
    public bool Ds18 { get; set; }
    [JsonProperty("v2flag")]
    public int V2Flag { get; set; }
    [JsonProperty("batteryRemaining")]
    public double BatteryRemaining { get; set; }

}