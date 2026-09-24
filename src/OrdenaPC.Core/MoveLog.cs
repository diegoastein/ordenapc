using System.Globalization;
using System.Text;

namespace OrdenaPC.Core;

/// <summary>
/// Registro CSV (separado por ";" y con BOM, para que Excel en español lo abra bien)
/// de cada movimiento: Fecha;Regla;Origen;Destino;Estado;Detalle.
/// </summary>
public sealed class MoveLog
{
    private const char Sep = ';';
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private static readonly string Header = string.Join(Sep, "Fecha", "Regla", "Origen", "Destino", "Estado", "Detalle");
    private readonly object _lock = new();

    public MoveLog(string path) => FilePath = path;

    public string FilePath { get; }

    public void Append(MoveResult r)
    {
        var fields = new[]
        {
            r.Fecha.ToString(DateFormat, CultureInfo.InvariantCulture),
            r.Regla, r.Origen, r.Destino, r.Estado.ToString(), r.Detalle,
        };
        var line = string.Join(Sep, fields.Select(Quote));

        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            bool isNew = !File.Exists(FilePath) || new FileInfo(FilePath).Length == 0;
            using var w = new StreamWriter(FilePath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            if (isNew) w.WriteLine(Header);
            w.WriteLine(line);
        }
    }

    public List<MoveResult> ReadAll()
    {
        string[] lines;
        lock (_lock)
        {
            if (!File.Exists(FilePath)) return new();
            lines = File.ReadAllLines(FilePath, Encoding.UTF8);
        }

        var result = new List<MoveResult>();
        foreach (var line in lines.Skip(1))
        {
            var f = ParseLine(line);
            if (f.Count < 6) continue;
            if (!DateTime.TryParseExact(f[0], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            if (!Enum.TryParse<MoveStatus>(f[4], out var status)) continue;
            result.Add(new MoveResult(date, f[1], f[2], f[3], status, f[5]));
        }
        return result;
    }

    private static string Quote(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.IndexOfAny(new[] { Sep, '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == Sep) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
