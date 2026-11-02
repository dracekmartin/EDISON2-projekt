using System.IO;
using System.Runtime.CompilerServices;

namespace TemplateDesigner.Tests;

/// <summary>Cesty ke zdrojům v repu pro testy, které čtou kód druhé strany (Caché třídy, C# zdroje, dokumentaci).</summary>
internal static class RepoCesty
{
    /// <summary>Kořen repa (složka s <c>EDISON2.sln</c>).</summary>
    public static string Koren { get; } = NajdiKoren();

    public static string TemplateDesigner(params string[] cesta) => Path.Combine([Koren, "TemplateDesigner", .. cesta]);

    /// <summary>Caché třídy návrháře: <c>Cache("Agendy", "ZastavkoveJrAd.cls")</c>.</summary>
    public static string Cache(params string[] cesta) =>
        Path.Combine([Koren, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", .. cesta]);

    public static string Dotnet(params string[] cesta) => Path.Combine([Koren, "TemplateDesigner", "dotnet", .. cesta]);

    private static string NajdiKoren([CallerFilePath] string tentoSoubor = "")
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        return (dir ?? new FileInfo(tentoSoubor).Directory!.Parent!).FullName;
    }
}
