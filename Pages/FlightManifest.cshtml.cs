using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;

namespace AeroLink.Pages;

/// Page model handling employee authentication against the HR system API (User Story T2).
public class FlightManifestModel : PageModel
{

    public void OnGet() { }

}
