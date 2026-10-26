using System.Text.Json;
namespace LimitWidget;
internal sealed record WindowPlacement(double Left, double Top)
{
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGptLimitWidget", "position.json");
    public static WindowPlacement? Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(FilePath));
            return value != null && double.IsFinite(value.Left) && double.IsFinite(value.Top) ? value : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
