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
 public double Width { get; set; } = 390;
 public double Opacity { get; set; } = 0.8;
 public int RefreshMinutes { get; set; } = 10;
 static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGptLimitWidget", "settings.json");
 public static WidgetSettings Load()
 {
  try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(FilePath)) ?? new() : new(); }
  catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
 }
 public void Save()
 {
  Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
  string temporary = FilePath + ".tmp";
  File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
  File.Move(temporary, FilePath, true);
 }
}
