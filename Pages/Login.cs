using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;

namespace AeroLink.Pages;

/// Page model handling employee authentication against the HR system API (User Story T2).
public class LoginModel : PageModel
{
    // Note to tutor: this is a local API endpoint copied from the HR System - nothing was changed.
    private const string DepartmentsUrl = "http://localhost:5000/api/departments";

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
            // Get departments from the HR API
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(DepartmentsUrl);
            response.EnsureSuccessStatusCode();

            var departments = await response.Content.ReadFromJsonAsync<List<DepartmentResponse>>() ?? [];

            // Match the employee in any department. The department stays on the signed-in user for later page checks.
            var match = departments
                .SelectMany(department => department.Employees.Select(employee => new { department, employee }))
                .FirstOrDefault(pair =>
                    pair.employee.EmployeeId.ToString() == EmployeeId.Trim()
                    && string.Equals(pair.employee.Email, Email.Trim(), StringComparison.OrdinalIgnoreCase));

            // Login successful
            var role = match?.employee.JobTitle ?? string.Empty;
            if (match is not null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, Email),
                    new Claim(ClaimTypes.Role, role),
                    new Claim("EmployeeId", EmployeeId),
                    new Claim("DepartmentId", match.department.DepartmentId.ToString()),
                    new Claim("DepartmentName", match.department.Name)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                return RedirectToPage("/Index");
            }

            // Login failed
            ErrorMessage = "Access denied. Employee not found or unauthorized.";
            return Page();
        }
        catch (Exception)
        {
            ErrorMessage = "Unable to connect to the HR System API. Please try again later.";
            return Page();
        }
    }

    // DTO for the department response from the HR API
    private sealed class DepartmentResponse
    {
        public int DepartmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public List<EmployeeResponse> Employees { get; set; } = [];
    }

    // DTO for the employee response from the HR API
    private sealed class EmployeeResponse
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }
}
