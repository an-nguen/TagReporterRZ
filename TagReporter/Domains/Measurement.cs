using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TagReporter.Domains;

public class Measurement
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    public int TagId { get; set; }
    
    public int PositionId { get; set; }
    
    public DateTimeOffset DateTime { get; set; }
    
    public decimal Temperature { get; set; }
    
    public decimal? Humidity { get; set; }
}