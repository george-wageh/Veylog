using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Veylog;

public class LoginModel : PageModel
{
    private readonly VeylogTokenManager _veylogTokenManager;

    public LoginModel(VeylogTokenManager veylogTokenManager)
    {
        _veylogTokenManager = veylogTokenManager;
    }

    [BindProperty]
    public string Token { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = "Token is required.";
            return Page();
        }

        // Validate token
        if (!_veylogTokenManager.IsValid(Token))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;

            ErrorMessage = "Token is invalid or expired.";
            return Page();
        }

        // Get the CreatedAt of the CURRENT token
        var createdAt = _veylogTokenManager.CreatedAt;

        if (!createdAt.HasValue)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;

            ErrorMessage = "Token is invalid or expired.";
            return Page();
        }

        var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.Name,
                    "Veylog"),

                // Store the token generation time in this session
                new Claim(
                    "VeylogTokenCreatedAt",
                    createdAt.Value.UtcTicks.ToString())
            };

        var identity = new ClaimsIdentity(
            claims,
            "VeylogScheme");

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            "VeylogScheme",
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,

                // Session lifetime
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return Redirect("/veylog");
    }
}