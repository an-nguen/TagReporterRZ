using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TagReporter.Domains
{
    public class Zone
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public string? Name { get; set; }
        [NotMapped]
        public bool Checked { get; set; }
        [NotMapped]
        public List<Guid> TagUuids { get; set; } = new ();

        [NotMapped]
        public List<Tag> Tags { get; set; } = new();
    }
}