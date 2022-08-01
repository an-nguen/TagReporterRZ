using Newtonsoft.Json;

namespace TagReporter.DTOs;

public class TemperatureRawDataResponse
{
    [JsonProperty("__type")] public string? Type { get; set; }
    [JsonProperty("time")] public string? Time { get; set; }
    [JsonProperty("temp_degC")] public double TempDegC { get; set; }
    [JsonProperty("cap")] public double Cap { get; set; }
    [JsonProperty("lux")] public double Lux { get; set; }
    [JsonProperty("battery_volts")] public double BatteryVolts { get; set; }
}