namespace OrdenaPC.Tests;

public sealed class TempDir : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ordenapc-test-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Root);

    public string Dir(string name)
    {
        var p = Path.Combine(Root, name);
        Directory.CreateDirectory(p);
        return p;
    }

    public static string CreateFile(string dir, string name, string content = "contenido")
    {
        var p = Path.Combine(dir, name);
        File.WriteAllText(p, content);
        return p;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}
