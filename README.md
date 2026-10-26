# Codex GPT Limit Widget

Early Windows desktop UI prototype, written in C# / WPF.

## Run

Requires Windows and .NET SDK 9 (temporary prototype target matching the installed SDK).

```powershell
dotnet run --project src/LimitWidget/LimitWidget.csproj
```

The prototype displays **sample data only**. It does not sign in, read credentials, or make network requests.

Use the gear to preview accent colors, sizes, light/dark/translucent themes, and two/four-ring layouts. Drag the header to move the widget; lock prevents movement. The arrow button toggles always-on-top. Close using ×.

Refresh interval uses a stepped slider. Background opacity applies to Adaptive glass only; text stays opaque. Save settings persists appearance, service visibility, size, lock, topmost and interval to `%LocalAppData%/CodexGptLimitWidget/settings.json`. Changes preview immediately; unsaved changes are lost on restart. Account controls are placeholders and credentials are never saved by this prototype.

Authentication, real polling, native adaptive glass, startup registration and launching Codex are not yet implemented.
