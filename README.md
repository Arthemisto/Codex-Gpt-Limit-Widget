# Codex GPT Limit Widget

A compact Windows desktop widget for Codex subscription limits, built with C# and WPF on .NET 10 LTS.

## Features

- Real remaining percentages and reset countdowns for the quota windows returned by Codex. Window lengths are taken from the service, including 5-hour and weekly windows when present.
- Lifetime token count in the top-right of Settings, using `account/usage/read`. This is the metric returned by the Codex-backed account service, not a verified total across every ordinary ChatGPT conversation. Missing or unavailable statistics display a dash and never block quota refresh.
- Last successful update time, manual refresh, and automatic refresh every 1 / 5 / 10 / 15 / 30 / 60 minutes (10 by default).
- Preserve the latest values on request failures and mark them stale. Unknown quotas display a dash, never an invented percentage.
- Light, dark, and transparent adaptive themes. Adaptive colors follow Windows light/dark preferences; background opacity is adjustable. Native background blur is not included.
- Independent Codex and ChatGPT accent colors, with a hue spectrum, saturation/brightness plane and RGB/HEX entry. Three widget sizes, visibility options, always-on-top and position lock.
- Save settings, remember window position, recover an off-screen window, launch Codex, and optional Windows startup registration.
- Tray menu, hide/restore, and single-instance behavior.
- The Codex helper exits between polls for existing or remembered logins. Only an ephemeral login keeps it running to retain its in-memory session.

ChatGPT chat limits are planned separately for V2 and are disabled in this release. No ChatGPT quota values are simulated.

The live quota-source probe currently returns only `codex` for the tested account. The existing OAuth connection has not provided ordinary ChatGPT chat quotas; a second accent does not imply a connected second quota source. Recheck without accessing conversations or credential files:

```powershell
dotnet run --project tests/LimitWidget.Checks -- --limit-catalog
```

## Run

Use the portable `LimitWidget.exe` from a local build. It includes the .NET runtime; it still requires an installed **Codex CLI**. The Codex desktop app is required for the Launch Codex button (`codex://`).

Keep the executable in a stable folder before enabling startup. Click **×** to hide to tray; use **Exit widget** in Settings or **Exit** in the tray menu to quit. Double-click the tray icon or run the executable again to restore the widget.

## Account and credentials

By default the widget tries the existing Codex login through the official `codex app-server` stdio protocol. It does not open or parse Codex credential files. API-key-only accounts do not provide these subscription limits.

Alternatively, choose **Sign in to ChatGPT**, then **Continue with Google** on the official page if that is your account's login method. Provider selection happens on ChatGPT's page; the widget does not implement its own Google OAuth client. This creates an isolated widget session under `%LocalAppData%/CodexGptLimitWidget/codex-home`, separate from the main Codex login:

- **Remember next sign-in** on: instructs Codex to use the Windows OS credential store (`keyring`), without a plaintext fallback.
- Off: instructs Codex to keep the new login in memory (`ephemeral`). You must sign in again after restarting the widget.
- The checkbox applies to the next browser sign-in. It does not change storage for an existing Codex session.
- **Disconnect widget** clears the widget's own active and remembered sessions. It does not log out the main Codex app.

Password entry happens in the system browser. The widget does not store tokens in settings, log protocol messages, send agent turns, or consume earned quota resets. Browser login may temporarily use Codex's local OAuth callback listener; normal widget communication uses stdio, not a listening network port.

The widget shows browser-login progress, supports cancellation, validates the official HTTPS login URL, and matches completion to the current login attempt. Cancelling or failing a new sign-in keeps the previous widget connection and quota snapshot. Authentication is verified with Codex before switching accounts.

Preferences and position are stored in `%LocalAppData%/CodexGptLimitWidget/`. Appearance changes preview immediately; **Save settings** persists preferences and applies the startup checkbox. Position is saved after moving or exiting. No administrator rights are required.

## Build and checks

Requires Windows and .NET SDK 10. A locally installed SDK at `.tools/dotnet` is also supported by the build script.

```powershell
.\build.ps1                  # Release build and deterministic checks
.\build.ps1 -Portable        # Also produce a standalone EXE and ZIP in artifacts/
.\build.ps1 -LiveCheck       # Also verify the existing Codex session and real quotas
```

For a normal SDK installation:

```powershell
dotnet run --project src/LimitWidget
```

Optional UI smoke check using the portable executable:

```powershell
.\artifacts\LimitWidget-win-x64\LimitWidget.exe --smoke-test artifacts/smoke
```

This uses temporary in-memory preferences and the existing Codex login for read-only quota checks. It writes render previews and `smoke-result.json`; it does not modify user settings or sign in/out. A working existing ChatGPT-backed Codex login is required.

The browser OAuth flow, startup after an actual Windows sign-in, and physical monitor hotplug require manual verification. Automated checks cover quota parsing, settings round-trip and corruption recovery, isolated ephemeral auth, live reads, UI themes/sizes, off-screen recovery, settings rendering, and hide/restore.

## References

- [Official Codex app-server protocol](https://learn.chatgpt.com/docs/app-server)
- [Official Codex authentication and credential storage](https://learn.chatgpt.com/docs/auth)
