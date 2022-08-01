using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TagReporter.Domains;

public class ZoneTagUuid
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    public int ZoneId { get; set; }
    
    public Guid TagUuid { get; set; }
}