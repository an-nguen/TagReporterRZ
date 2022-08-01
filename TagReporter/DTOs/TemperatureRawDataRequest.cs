using Newtonsoft.Json;

namespace TagReporter.DTOs;

public class TemperatureRawDataRequest
{
    [JsonProperty("uuid")] public string Uuid { get; set; } = string.Empty;
    [JsonProperty("fromDate")] public string FromDate { get; set; } = string.Empty;
    [JsonProperty("toDate")] public string ToDate { get; set; } = string.Empty;
}