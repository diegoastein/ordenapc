using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

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

    // DataContractJsonSerializer viene tanto en .NET Framework como en .NET 8, sin paquetes extra.
    private static DataContractJsonSerializer Serializer() => new(typeof(AppConfig));

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();
        try
        {
            using var fs = File.OpenRead(path);
            return ((AppConfig?)Serializer().ReadObject(fs) ?? new AppConfig()).FillDefaults();
        }
        catch (Exception ex) when (ex is not IOException and not UnauthorizedAccessException)
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
        using (var fs = File.Create(tmp))
        using (var writer = JsonReaderWriterFactory.CreateJsonWriter(fs, Encoding.UTF8, ownsStream: false, indent: true))
        {
            Serializer().WriteObject(writer, this);
            writer.Flush();
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    // El deserializador no ejecuta constructores: lo que falte en el JSON queda en null.
    private AppConfig FillDefaults()
    {
        Reglas ??= new();
        Excluidos ??= new();
        if (IntervaloBarridoMin <= 0) IntervaloBarridoMin = 30;
        foreach (var r in Reglas)
        {
            r.Nombre ??= "";
            r.CarpetaOrigen ??= "";
            r.CarpetaDestino ??= "";
            r.Extensiones ??= new();
            r.PalabrasClave ??= new();
        }
        return this;
    }
}
