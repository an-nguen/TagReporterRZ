using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TagReporter.Domains;
using TagReporter.DTOs;

namespace TagReporter.Services;

public class UserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public UserService(UserManager<ApplicationUser> userManager,
        ILogger<UserService> logger,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _logger = logger;
        _signInManager = signInManager;
    }

    public async Task<ApplicationUser?> FindUserById(string userId) => await _userManager.FindByIdAsync(userId);
        

    public async Task<ApplicationUser?> FindUserByEmail(string email) => await _userManager.FindByEmailAsync(email);
        

    public async Task<ApplicationUser?> FindUser(string username) => await _userManager.FindByNameAsync(username);
        

    public async Task<(bool, List<IdentityError>)> Create(User user)
    {
        if (string.IsNullOrEmpty(user.Username))
            throw new Exception("user's username cannot be null or empty");
        var appUser = new ApplicationUser(user.Username)
        {
            Email = user.Email
        };

        var identityResult = await _userManager.CreateAsync(appUser, user.Password);
        if (identityResult.Succeeded)
        {
            return (true, new List<IdentityError>());
        }

        foreach (var error in identityResult.Errors)
        {
            _logger.LogError("Error code: {}\nDescription: {}", error.Code, error.Description);
        }

        return (false, identityResult.Errors.ToList());
    }

    public async Task<(bool, List<IdentityError>)> UpdateEmail(ApplicationUser user, string email)
    {
        var token = await _userManager.GenerateChangeEmailTokenAsync(user, email);
        var identityResult = await _userManager.ChangeEmailAsync(user, email, token);
        foreach (var error in identityResult.Errors)
        {
            _logger.LogError("Error code: {}\nDescription: {}", error.Code, error.Description);
        }

        return (identityResult.Succeeded, identityResult.Errors.ToList());
    }

    public async Task<(bool Succeeded, List<IdentityError>)> UpdateRoles(ApplicationUser user,
        IEnumerable<string> selectedRoleNames)
    {
        var updUser = await _userManager.FindByIdAsync(user.Id);
        var roleNames = selectedRoleNames.ToList();
            
        await _userManager.RemoveFromRolesAsync(updUser, await _userManager.GetRolesAsync(updUser));
        var result = await _userManager.AddToRolesAsync(updUser, roleNames);

        foreach (var error in result.Errors)
        {
            _logger.LogError("Error code: {}\nDescription: {}", error.Code, error.Description);
        }

        return (result.Succeeded, result.Errors.ToList());
    }

    public async Task<bool> UpdatePassword(ApplicationUser user, string oldPassword, string newPassword)
    {
        var identityResult = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
        foreach (var error in identityResult.Errors)
        {
            _logger.LogError("Error code: {}\nDescription: {}", error.Code, error.Description);
        }

        return identityResult.Succeeded;
    }

    public async Task<(bool Succeeded, List<IdentityError>)> UpdatePassword(ApplicationUser user, string password)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var identityResult = await _userManager.ResetPasswordAsync(user, token, password);
        foreach (var error in identityResult.Errors)
        {
            _logger.LogError("Error code: {}\nDescription: {}", error.Code, error.Description);
        }

        return (identityResult.Succeeded, identityResult.Errors.ToList());
    }

    public async Task<bool> Authenticate([Required] string email, [Required] string password)
    {
        var appUser = await _userManager.FindByEmailAsync(email);
        if (appUser == null) return false;
        var result = await _signInManager.PasswordSignInAsync(appUser, password, false, false);
        return result.Succeeded;
    }

    public async Task<(bool Succeeded, List<IdentityError>)> Delete(string username)
    {
        var appUser = await _userManager.FindByNameAsync(username);
        await _userManager.RemoveFromRolesAsync(appUser, await _userManager.GetRolesAsync(appUser));
            
        var identityResult = await _userManager.DeleteAsync(appUser);
        foreach (var error in identityResult.Errors)
        {
            _logger.LogError("Error code: {}\nDescription: {}", error.Code, error.Description);
        }

        return (identityResult.Succeeded, identityResult.Errors.ToList());
    }
        
    public List<ApplicationUser> FindAll() => _userManager.Users.ToList();

    public async Task<List<string>> FindRoles(ApplicationUser user) => new(await _userManager.GetRolesAsync(user));

    public async Task<ApplicationUser> FindUserByUsername(string username) => await _userManager.FindByNameAsync(username);
}