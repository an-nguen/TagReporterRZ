using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TagReporter.Domains;
using TagReporter.DTOs;

namespace TagReporter.Services;

public class RoleService {
    private readonly RoleManager<ApplicationRole> _roleManager;
        
    public RoleService(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public List<ApplicationRole> FindAll() => _roleManager.Roles.ToList();

    public async Task<(bool, List<IdentityError>)> Create(Role role)
    {
        if (string.IsNullOrEmpty(role.Name)) throw new Exception("role name cannot be empty or null"); 
        var result = await _roleManager.CreateAsync(new ApplicationRole
        {
            Name = role.Name
        });
        return (result.Succeeded, result.Errors != null ? result.Errors.ToList() : new List<IdentityError>());
    }

    public async Task<IdentityResult> Delete(ApplicationRole role) => await _roleManager.DeleteAsync(role);
}