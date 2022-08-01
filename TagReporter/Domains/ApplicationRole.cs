using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace TagReporter.Domains;

public sealed class ApplicationRole: IdentityRole
{
    public ICollection<ApplicationUserRole> UserRoles { get; set; } = default!;
}