using System.Text;

namespace LimitWidget.Services;

internal sealed class BoundedLineReader(TextReader reader, int limit)
{
    readonly char[] buffer = new char[4096];
    int offset, count;

    public async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        while (true)
        {
            if (offset == count)
            {
                count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                offset = 0;
                if (count == 0) return line.Length == 0 ? null : line.ToString().TrimEnd('\r');
            }
            char value = buffer[offset++];
            if (value == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length == limit) throw new IOException("Codex response exceeded the size limit.");
            line.Append(value);
        }
    }
}
