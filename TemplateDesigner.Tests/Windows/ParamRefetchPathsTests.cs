using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Každá cesta, která hne hodnotami parametrů sestavy, musí nechat rozhodnout jedno místo
/// (<c>RefreshPreviewAfterParamChange</c>), jestli stačí překreslit náhled, nebo je potřeba znovu
/// natáhnout data ze serveru. Když některá cesta jen překreslí, náhled zůstane na datech
/// **předchozího** nastavení, zatímco ovladače i tisk už jedou podle nového - a uživatel nemá jak
/// poznat, co vlastně platí.
///
/// Potkalo to dvě cesty naráz: dialog Tisk a otevření šablony (data se natáhnou při obnovení
/// instance, tedy dřív, než se aplikují uložené hodnoty parametrů). Test je zdrojový, protože
/// ViewModel se v testech nestaví - kryje se tím aspoň struktura, ne chování.
/// </summary>
public class ParamRefetchPathsTests
{
    /// <summary>Metody, které reagují na změnu hodnot parametrů. Přibude-li další cesta, patří sem.</summary>
    private static readonly string[] ParamChangePaths =
        ["OnAgendaParamChanged", "OnExternalParamsChanged", "RefreshParamsUiAfterLoad"];

    private const string Decider = "RefreshPreviewAfterParamChange";

    [Fact]
    public void KazdaCestaZmenyParametru_SePtaJednohoMista()
    {
        var source = ViewModelSource();
        var chybi = ParamChangePaths.Where(m => !MethodBody(source, m).Contains(Decider)).ToList();

        Assert.True(chybi.Count == 0,
            $"Tyhle cesty mění hodnoty parametrů, ale nevolají {Decider}() - u server-side parametru "
            + "pak náhled ukazuje data starého nastavení, zatímco tisk jede podle nového:\n"
            + string.Join("\n", chybi));
    }

    [Fact]
    public void JenRozhodujiciMisto_SpustiZnovunacteniDat()
    {
        // Druhý směr: kdyby si re-fetch volala některá cesta sama, obejde tím podmínku
        // „změnil se server-side parametr" a data se tahají i při změně čistě klientské volby.
        var source = ViewModelSource();
        var vinici = ParamChangePaths
            .Where(m => MethodBody(source, m).Contains("RefreshDataForCurrentInstanceAsync"))
            .ToList();

        Assert.True(vinici.Count == 0,
            $"Cesta si volá znovunačtení dat sama místo přes {Decider}():\n" + string.Join("\n", vinici));
    }

    [Fact]
    public void RozhodujiciMisto_VeZdrojiExistuje()
    {
        // Pojistka: kdyby se metoda přejmenovala, oba testy výše by prošly na prázdném vstupu.
        var source = ViewModelSource();

        Assert.Contains($"private void {Decider}()", source);
        foreach (var m in ParamChangePaths)
            Assert.True(MethodBody(source, m).Length > 0, $"Metoda {m} ve zdroji není - přejmenovaná?");
    }

    /// <summary>Tělo metody od její hlavičky po první uzavírací závorku ve sloupci jejího odsazení.</summary>
    private static string MethodBody(string source, string methodName)
    {
        var m = Regex.Match(source, @"(private|public|internal)[^\r\n]*\s" + Regex.Escape(methodName) + @"\s*\(");
        if (!m.Success) return "";
        var open = source.IndexOf('{', m.Index);
        if (open < 0) return "";
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
        }
        return "";
    }

    /// <summary>Zdroják ViewModelu ze všech jeho částí (<c>TemplateDesignerViewModel*.cs</c>, partial od 30. 10.).</summary>
    private static string ViewModelSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var soubory = Directory.GetFiles(Path.Combine(dir!.FullName, "TemplateDesigner", "dotnet", "Windows", "MainWindow"),
            "TemplateDesignerViewModel*.cs");
        Assert.True(soubory.Length > 1, "ViewModel se nenašel");
        return string.Join("\n", soubory.Order(StringComparer.Ordinal).Select(File.ReadAllText));
    }
}
