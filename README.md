# IAB251 - Assessment 2 Codebase

## Overview

This is a ASP.net Core Razor starter template, built using DOTNET V10.

@lachietech and @Yuvuni, make sure you branch from the main branch to work on your individual features :)

## Codebase Summary

The app is an ASP.NET Core Razor Pages project named AeroLink. A page is a `.cshtml` view plus a `.cshtml.cs` page model. The file path under `Pages/` is the URL, so `Pages/Index.cshtml` is `/`.

- `AeroLink.slnx` / `AeroLink.csproj` — solution and project file. Targets .NET 10.
- `Program.cs` — starts the app and registers Razor Pages.
- `Pages/Index` and `Pages/Privacy` — the two starter pages.
- `Pages/Error` — shown when an unhandled error occurs outside Development.
- `Pages/Shared/_Layout.cshtml` — shared header, nav, and footer. Other pages render inside it.
- `Pages/_ViewStart.cshtml` — applies that layout to every page.
- `Pages/_ViewImports.cshtml` — shared usings and tag helpers for the `Pages` folder.

---

- `wwwroot/` — static files: `css/site.css`, `js/site.js`, and Bootstrap/jQuery under `lib/`.
- `appsettings.json` — logging and host settings. `appsettings.Development.json` overrides them while developing.
- `Properties/launchSettings.json` — local URLs (`https://localhost:7200` and `http://localhost:5282`).
- `.gitignore` — keeps build output (`bin/`, `obj/`) out of git.
