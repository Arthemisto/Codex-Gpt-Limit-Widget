using System.IO;
using System.Text.Json;
namespace LimitWidget;
public sealed class WidgetSettings
{
 public bool ShowCodex { get; set; } = true;
 public bool ShowGpt { get; set; }
 public bool Locked { get; set; }
 public bool Topmost { get; set; }
 public string Theme { get; set; } = "Dark";
 public string Accent { get; set; } = "#39DFB5";
 public string ChatGptAccent { get; set; } = "#AB55F5";
 public double Width { get; set; } = 340;
 public double Opacity { get; set; } = 0.8;
 public int RefreshMinutes { get; set; } = 10;
 public bool RememberMe { get; set; } = true;
 public string SessionMode { get; set; } = "existing";
 public bool ShowUpdated { get; set; } = true;
 public bool ShowCountdown { get; set; } = true;
 public bool ShowLaunch { get; set; } = true;
 static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGptLimitWidget", "settings.json");
 public static WidgetSettings Load(string? filePath = null)
 {
  filePath ??= FilePath;
  try { return File.Exists(filePath) ? JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(filePath)) ?? new() : new(); }
  catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
 }
 public void Save(string? filePath = null)
 {
  filePath ??= FilePath;
  Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
  string temporary = filePath + ".tmp";
  File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
  File.Move(temporary, filePath, true);
 }
}
