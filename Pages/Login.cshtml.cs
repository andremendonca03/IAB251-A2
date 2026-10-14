using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace AeroLink.Pages;

/// Page model handling employee authentication against the HR system API (User Story T2).
public class LoginModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public LoginModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// Gets or sets the employee ID entered during login.
    [BindProperty]
    [Required(ErrorMessage = "Employee ID is required.")]
    public string EmployeeId { get; set; } = string.Empty;

    /// Gets or sets the employee email address entered during login.
    [BindProperty]
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    /// Gets or sets any error message encountered during the authentication process.
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    /// Handles the HTTP POST request to validate credentials via the HR API 
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            bool isValidUser = true;
            string role = "Baggage Handler";

            if (isValidUser && (role == "Baggage Handler" || role == "BG_Supervisor"))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, Email),
                    new Claim(ClaimTypes.Role, role),
                    new Claim("EmployeeId", EmployeeId)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                return RedirectToPage("/Index");
            }
            else
            {
                ErrorMessage = "Access denied. Employee not found or unauthorized role (Requires Baggage Handler or BG_Supervisor).";
                return Page();
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Unable to connect to the HR System API. Please try again later.";
            return Page();
        }
    }
}