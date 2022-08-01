using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace TagReporter.Domains;

public sealed class ApplicationUser : IdentityUser
{
    public ApplicationUser()
    {
    }

    public ApplicationUser(string userName) : base(userName)
    {
    }

    public ICollection<IdentityUserClaim<string>> Claims { get; set; } = default!;
    public ICollection<IdentityUserLogin<string>> Logins { get; set; } = default!;
    public ICollection<IdentityUserToken<string>> Tokens { get; set; } = default!;
    public ICollection<ApplicationUserRole> UserRoles { get; set; } = default!;
}