using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TagReporter.DTOs;
using TagReporter.Services;

namespace TagReporter;

public static class WebHostExtensions {
    public static async Task InitAdminRole(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<RoleService>();
        var role = roleService?.FindAll().FirstOrDefault(r => r.NormalizedName == "ADMIN");
        if (role == null) await roleService?.Create(new Role { Name = "ADMIN" })!;
    }
    public static async Task InitAdminUser(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        var roleService = scope.ServiceProvider.GetRequiredService<RoleService>();
        var user = await userService.FindUser("admin");
        var adminRole = roleService.FindAll().First(r => r.NormalizedName == "ADMIN");
        if (user == null){
            var name = Environment.GetEnvironmentVariable("ASPNETCORE_ADMIN_NAME");
            var password = Environment.GetEnvironmentVariable("ASPNETCORE_ADMIN_PASSWORD");
            var newUser = new User
            {
                Username = name ?? "admin",
                Password = password ?? "admin",
                Email = "",
                Roles = new List<string>
                {
                    adminRole.Name
                }
            };
            await userService.Create(newUser);
        }
    }
}