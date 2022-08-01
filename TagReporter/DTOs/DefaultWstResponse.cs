using System.Collections.Generic;
using Newtonsoft.Json;

namespace TagReporter.DTOs;

public class DefaultWstResponse<T>
{
    [JsonProperty("d")]
    public List<T>? D { get; set; }
}