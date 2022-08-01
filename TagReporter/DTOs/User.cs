using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TagReporter.DTOs;

public class User {
    [Required] public string? Username { get; set; }
        
    [EmailAddress]
    public string? Email { get; set; }
        
    [Required] public string? Password { get; set; }

    public List<string> Roles { get; set; } = new();
}