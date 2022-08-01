using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TagReporter.Domains;

public class Tag
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonPropertyName("uuid")]
    public Guid Uuid { get; set; }
    public string? Name { get; set; }
    public string? TagManagerName { get; set; }
    public string? TagManagerMac { get; set; }
        
    [NotMapped]
    public bool Checked { get; set; }
        
    [NotMapped]
    public WstAccount? Account { get; set; }
        
    [NotMapped]
    public List<DTOs.MeasurementRecord> Measurements { get; set; } = new();
}