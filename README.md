# Altruista834OutboundMonitoring

Enterprise-grade .NET Framework 4.7.2 monitoring solution for outbound lifecycle of `C` and `Pend_C` files.

## What this solution includes
- Real-time polling + `FileSystemWatcher` event tracing.
- Independent monitoring per process (`C`, `Pend_C`) and per day.
- Fault tolerance with retry + exception isolation (loop never hard-stops on file errors).
- Step-wise SLA handling, stuck-file alerts, missing-file alerts, drop summary alerts.
- Email de-duplication to prevent alert spam.
- Demo mode support through `config.json` values.
- Unit and simulation tests with NUnit (VS2019-compatible).

## Solution structure
- `Altruista834OutboundMonitoring` (Console app)
- `Altruista834OutboundMonitoring.Tests` (NUnit tests)

## NuGet packages
### Console project
- `Newtonsoft.Json` (13.0.3)

### Test project
- `Microsoft.NET.Test.Sdk` (17.10.0)
- `NUnit` (3.14.0)
- `NUnit3TestAdapter` (4.5.0)

## Create and run in Visual Studio 2019
1. Open `Altruista834OutboundMonitoring.sln` in Visual Studio 2019.
2. Restore NuGet packages (`Build` -> `Restore NuGet Packages`).
3. Ensure startup project is `Altruista834OutboundMonitoring`.
4. Update `Altruista834OutboundMonitoring/config.json` for your Windows Server paths and SMTP.
5. Press `F5` or run from command line:
   - `msbuild Altruista834OutboundMonitoring.sln /t:Build /p:Configuration=Debug`
   - `Altruista834OutboundMonitoring\bin\Debug\Altruista834OutboundMonitoring.exe`

## Run tests
- Visual Studio Test Explorer, or:
  - `msbuild Altruista834OutboundMonitoring.sln /t:Build /p:Configuration=Debug`
  - `vstest.console.exe Altruista834OutboundMonitoring.Tests\bin\Debug\net472\Altruista834OutboundMonitoring.Tests.dll`

## Demo mode instructions
1. Set `DemoMode=true` in `config.json`.
2. Point folder paths to local directories.
3. Set short polling/stability timings (already pre-filled in sample config).
4. Create files with today's date token in name (or set `RequireTodayDateInName=false`).
5. To simulate SLA breach, create a very large file in Proprietary folder and reduce `MbPerHourProcessingRatio`.

## Flow explanation
1. **Step 1:** VendorExtractUtility `.txt` monitoring (6:00–8:30 IST) for missing, stuck (>10 min), and stability/lock checks.
2. **Step 2:** Proprietary monitoring with dynamic processing-time estimate and 10:00 AM SLA risk emails.
3. **Step 3:** HOLD folder expects `.x12` only, validates presence/stability/date.
4. **Step 4:** DROP folder before 9:55 AM, sends structured summary (name, size, modified date/time).

## Negative scenarios handled
- Network folder unavailable / path missing.
- Permission/file access failures.
- Partial copy / lock detection.
- Invalid/missing config JSON.
- SMTP exceptions.
- Duplicate email suppression.
- Timezone conversion issues (configurable timezone ID).
- Exceptions inside loop are logged and execution continues.

## Production deployment recommendations
- Run as Windows Scheduled Task at startup or Windows Service wrapper.
- Use service account with least required file-share + SMTP permissions.
- Configure rolling logs (swap `LoggingService` with NLog/Serilog sink in production).
- Store SMTP secrets securely (DPAPI/Windows Credential Manager/secret vault).
- Add health check endpoint (if hosted as service) and Ops dashboards.
- Configure SIEM forwarding for warning/error logs.
- Add centralized alert throttling policy if multiple servers run active-active.
