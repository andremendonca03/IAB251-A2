# AeroLink — IAB251 Assessment 2

ASP.NET Core Razor Pages application for importing flight manifests, recording baggage outcomes, approving exceptions and viewing loading summaries. Uses .NET 10, Entity Framework Core with SQLite, and ClosedXML for Excel imports. The sibling `HRSystem` project provides employee and department records for login.

## Run locally

Install the .NET 10 SDK and the .NET 8 runtime required by the HR service. Run these commands from the `IAB251-A2` folder in separate terminals:

```powershell
dotnet run --project ../HRSystem/HRSystem.csproj --launch-profile HRSystem
```

```powershell
dotnet run --launch-profile https
```

Open `https://localhost:7200`. If the development certificate is not trusted, run `dotnet dev-certs https --trust`. The HTTP profile is also available on port 5282. The HR service exposes HTTP on port 5000 and HTTPS on port 5001; login currently calls `http://localhost:5000/api/departments`.

Connection strings are in each project's `appsettings.json`. SQLite files are created on first startup with `EnsureCreated`; there are no database migrations. AeroLink starts with an empty manifest until a workbook is imported.

Sign in using an employee ID and matching email from the HR records. The job title becomes the role claim, and department information is also stored in the authentication cookie. The Operations module link uses department ID `1`. The roles used by the baggage workflow are `Baggage Handler` and `BG_Supervisor`.

## Code map

| Location | Responsibility |
| --- | --- |
| `Program.cs` | Services, cookie authentication, page routes and database creation. |
| `Models/` | Flight, bag and exception records; standalone handling acknowledgement rule. |
| `Data/FlightManifestContext.cs` | SQLite context named `BaggageHandlingContext`; preserves imported IDs. |
| `Data/ManifestWorkbook.cs` | Workbook parsing and validation without database writes. |
| `Pages/Login.cs`, `Logout.cs` | Employee matching, authentication claims and sign-out. |
| `Pages/Index.cs` | Home-page modules and blocked-access messages. |
| `Pages/baggage-handling.cs` | Manifest display and replacement workbook imports. |
| `Components/FlightManifestViewComponent.cs` | Flight table and partial flight ID search. |
| `Pages/baggage-handling/flight.cs` | Bag changes, exception approval, display rows and loading totals. |
| `Pages/summary-report.cs` | Supervisor report for finalised flights in the current manifest. |
| `Pages/*.cshtml`, `Pages/Shared/` | Razor views and shared layout; handlers use matching `.cs` files. |
| `wwwroot/css/site.css` | Shared appearance, status badges and responsive baggage tables. |

Baggage form JavaScript lives in `flight.cshtml` and collects handling acknowledgement and exception descriptions. Row approval controls use separate forms outside the bulk status form. The handlers validate decisions and permissions independently of the UI. `wwwroot/js/site.js` is reserved for shared behaviour.

## Manifest import

Upload an `.xlsx` workbook on Baggage Management. A successful import **replaces all current flights, bags and exceptions**, including existing approvals, in one transaction. Parsing happens before replacement; failed imports leave current records intact.

The following sheet and column names are case-sensitive. Headers belong in the first row; column order may vary.

| Sheet | Required headers |
| --- | --- |
| `Flights` | `FlightId`, `FlightLabel`, `FlightNumber`, `DepartureAirport`, `ArrivalAirport`, `ScheduledDepartureUtc`, `AircraftId`, `LoadingStatus` |
| `Bags` | `BagId`, `FlightId`, `Tag`, `HandlingType`, `HandlingInstruction`, `Outcome` |
| `Exceptions` | `ExceptionId`, `BagId`, `FlightId`, `Category`, `Description`, `Status`, `DecisionReason` |

The parser checks required values, whole-number IDs, departure dates and duplicate IDs within each sheet. Blank data rows are skipped; `DecisionReason` may be empty. Dates are treated as UTC. Imported relationships and status strings are not validated against a fixed set of permitted values by the parser.

## Loading and approval rules

- Handlers and supervisors can set bags to `Pending`, `Complete`, `Missing` or `Damaged`. Non-standard handling requires acknowledgement; missing and damaged outcomes require an issue description.
- Bags with exceptions require a `BG_Supervisor` to choose **Load** or **Keep unloaded** and enter a non-blank justification. The ordinary status form cannot complete an exception bag.
- Approval saves `Complete` / `ApprovedToLoad` or `Unloaded` / `KeptUnloaded` on the bag and exception respectively. `DecisionReason` stores the choice, supervisor identity and trimmed justification. Approval preserves the reported issue description.
- A later status update reopens existing exceptions and clears the previous sign-off. Reporting another missing or damaged issue can replace its description.
- A flight becomes `Complete` when every bag is loaded or approved to remain unloaded and all exceptions are approved. Otherwise a saved update sets it to `Started`. Unloaded bags count as resolved but do not contribute to the loaded total.
- The supervisor's **Summary Report** button reads finalised flights from the current manifest. It excludes unresolved flights and regenerates on each visit; no historical report snapshot is stored.

## Tests

Run from this folder:

```powershell
dotnet test AeroLink.csproj
dotnet test AeroLink.csproj --filter FullyQualifiedName~BaggageApprovalTests
```

`BaggageTests.cs` contains three acknowledgement rule tests. `BaggageAprovalTests.cs` contains 32 approval and justification cases, covering access restrictions, invalid input, both decisions, persistence, flight totals and reopening an approval. Each approval case creates an isolated in-memory SQLite database and calls the page handlers directly; these tests do not exercise browser validation or HTTP middleware. They never use the application's manifest database.

If the running app locks the build output, stop it before running tests, or use a separate output folder:

```powershell
dotnet test AeroLink.csproj -p:UseAppHost=false --output "$env:TEMP/aerolink-tests"
```

The HR service has its own [README](../HRSystem/README.md) and [API specification](../HRSystem/API_SPECIFICATION.md).
