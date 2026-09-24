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

    /// <summary>Minutos que un archivo tiene que estar sin modificarse antes de moverlo (0 = sin espera).</summary>
    public int EsperaMinutos { get; set; } = 5;

    public List<Mailbox> Buzones { get; set; } = new();
    public List<CleanupRule> Limpiezas { get; set; } = new();

    /// <summary>PIN de administrador (derivado con PinHasher). Vacío = sin PIN.</summary>
    public string PinHash { get; set; } = "";
    public string PinSalt { get; set; } = "";

    /// <summary>false (por defecto) = si alguien cierra la app, una tarea programada la vuelve a abrir.</summary>
    public bool NoReabrir { get; set; }

    /// <summary>Se cerró a propósito con "Salir": la tarea programada no la reabre hasta el próximo inicio.</summary>
    public bool Detenido { get; set; }

    /// <summary>Horas sin acceso a un destino antes de mostrar la alerta grande.</summary>
    public int AlertaHoras { get; set; } = 3;

    /// <summary>A quién avisar cuando algo falla (aparece en la alerta).</summary>
    public string Contacto { get; set; } = "";

    [IgnoreDataMember]
    public bool TienePin => !string.IsNullOrEmpty(PinHash);

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
        if (EsperaMinutos < 0) EsperaMinutos = 0;
        if (AlertaHoras <= 0) AlertaHoras = 3;
        Buzones ??= new();
        Limpiezas ??= new();
        PinHash ??= "";
        PinSalt ??= "";
        Contacto ??= "";
        foreach (var b in Buzones)
        {
            b.Nombre ??= "";
            b.CarpetaDestino ??= "";
            if (b.Color == 0) b.Color = Mailbox.DefaultColor;
            if (b.Ancho <= 0) b.Ancho = Mailbox.DefaultWidth;
            if (b.Alto <= 0) b.Alto = Mailbox.DefaultHeight;
        }
        foreach (var c in Limpiezas)
        {
            c.CarpetaOrigen ??= "";
            c.CarpetaDestino ??= "";
            if (c.Dias <= 0) c.Dias = 30;
        }
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
