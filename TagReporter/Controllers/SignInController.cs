using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TagReporter.Domains;

namespace TagReporter.Controllers;

[Route("/SignIn")]
public class SignInController : Controller
{
    private SignInManager<ApplicationUser> _signInManager;
    private ILogger<SignInController> _logger;
    
    public SignInController(SignInManager<ApplicationUser> signInManager,
        ILogger<SignInController> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult OnGet()
    {
        return Redirect("~/Login");
    }
    
    [HttpPost]
    public async Task<IActionResult> OnPost(string userName, string password)
    {
        var result = await _signInManager.PasswordSignInAsync(userName, password, false, false);
        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            return Redirect("~/");
        }

        return Redirect("~/Login?error=InvalidUserPassword");
    }
    
    [HttpGet]
    [Route("Logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return Redirect("~/");
    }
}