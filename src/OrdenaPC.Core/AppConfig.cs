using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrdenaPC.Core;

public enum NotificationMode { Nunca, CadaArchivo, Resumen }

public sealed class AppConfig
{
    public List<Rule> Reglas { get; set; } = new();
    public int IntervaloBarridoMin { get; set; } = 30;
    public NotificationMode Notificaciones { get; set; } = NotificationMode.Resumen;

    /// <summary>Hasta que se revisa el primer barrido, la vigilancia automática no mueve nada.</summary>
    public bool PrimerBarridoHecho { get; set; }

    public bool Pausado { get; set; }

    /// <summary>Archivos devueltos con "Deshacer": no se vuelven a mover solos.</summary>
    public List<string> Excluidos { get; set; } = new();

    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OrdenaPC");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Json) ?? new AppConfig();
        }
        catch (JsonException)
        {
            // Se guarda el archivo dañado aparte para no perder las reglas y se arranca de cero.
            File.Copy(path, path + ".corrupto", overwrite: true);
            return new AppConfig();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
