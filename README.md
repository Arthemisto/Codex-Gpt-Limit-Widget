# Codex GPT Limit Widget

Early Windows desktop UI prototype, written in C# / WPF.

## Run

Requires Windows and .NET SDK 9 (temporary prototype target matching the installed SDK).

```powershell
dotnet run --project src/LimitWidget/LimitWidget.csproj
```

The prototype displays **sample data only**. It does not sign in, read credentials, or make network requests.

Use the gear to preview accent colors, sizes, light/dark/translucent themes, and two/four-ring layouts. Drag the header to move the widget; lock prevents movement. Close using ×. Settings are in memory only. Authentication, real limits, native adaptive glass, startup registration, persistence and launching Codex are not yet implemented.
