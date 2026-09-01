using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Skládání stránek na fyzické listy - hlavně případ, kdy neskládá nic.
/// </summary>
/// <remarks>
/// Zákazník hlásil jako chybu tisku, že mu z šablony A5 na výšku leze A4 (#13158). Chyba to nebyla:
/// měl zapnuté uspořádání na list „A4 na výšku", kam se dva A5 na výšku (2 × 148 mm) do šířky
/// 210 mm nevejdou, takže mřížka vyšla 1×1 a papír se jen zvětšil. Volba se navíc persistuje per
/// agenda, takže o ní nemusel vědět - proto na to aplikace nově upozorní i s radou.
/// </remarks>
public class UsporadaniNaListTests
{
    private static SheetLayoutOptions Options(float sheetW, float sheetH, float pageW, float pageH)
    {
        var (rows, cols) = SheetComposer.ResolveGrid(SheetLayoutKeys.GridAuto, pageW, pageH, sheetW, sheetH);
        return new SheetLayoutOptions
        {
            SheetWidth = sheetW, SheetHeight = sheetH,
            Rows = rows, Cols = cols,
            PageWidth = pageW, PageHeight = pageH,
        };
    }

    [Fact]
    public void A5NaVysku_NaA4NaVysku_NeskladaNic()
    {
        // Přesně kolegův případ: dva A5 na výšku se do šířky A4 na výšku nevejdou.
        var o = Options(210f, 297f, 148f, 210f);

        Assert.Equal(1, o.SlotsPerSheet);
        var hlaska = o.VarovaniBezEfektu();
        Assert.NotNull(hlaska);
        Assert.Contains("nic neskládá", hlaska);
    }

    [Fact]
    public void HlaskaPoradiOtocenyList_KdyzSeNaNejVejdeVic()
    {
        var o = Options(210f, 297f, 148f, 210f);
        var hlaska = o.VarovaniBezEfektu();

        // A4 na šířku (297×210) pojme dva A5 na výšku - to je ta rada, kterou uživatel potřebuje.
        Assert.Contains("297×210", hlaska);
        Assert.Contains("2", hlaska);
    }

    [Fact]
    public void A5NaVysku_NaA4NaSirku_SkladaDve()
    {
        var o = Options(297f, 210f, 148f, 210f);

        Assert.Equal(2, o.SlotsPerSheet);
        Assert.Equal(1, o.Rows);
        Assert.Equal(2, o.Cols);
        Assert.Null(o.VarovaniBezEfektu());   // skládá → žádné varování
    }

    [Fact]
    public void StejnyFormatStrankyIListu_VarujeBezRadyNaOtoceni()
    {
        // A4 šablona na A4 list: otočený list by nepomohl, takže rada zní jinak.
        var o = Options(210f, 297f, 210f, 297f);

        var hlaska = o.VarovaniBezEfektu();
        Assert.NotNull(hlaska);
        Assert.Contains("vypněte", hlaska);
    }

    [Fact]
    public void A5NaVysku_NaA3NaVysku_SkladaDve()
    {
        // A3 na výšku (297×420): dva A5 vedle sebe (296 mm) se vejdou.
        var o = Options(297f, 420f, 148f, 210f);

        Assert.Equal(4, o.SlotsPerSheet);   // 2 sloupce × 2 řádky
        Assert.Null(o.VarovaniBezEfektu());
    }

    [Fact]
    public void ObaExportyPopisujiUsporadaniStejne()
    {
        // Varování bylo 1. 9. jen v exportu s daty z databáze, takže na prázdné šabloně (což je
        // cesta, kterou jde každý, kdo si to chce vyzkoušet) nebylo vidět nic. Popis proto vzniká
        // na jednom místě - kontrola hledá jediný výskyt neutrálního popisu ve zdroji.
        var zdroj = File.ReadAllText(CestaKViewModelu());
        int vyskytu = System.Text.RegularExpressions.Regex.Matches(zdroj, @"ShortLabel").Count;

        Assert.Equal(1, vyskytu);
    }

    private static string CestaKViewModelu()
    {
        // Od bin/Debug/netX/ zpět do repozitáře - test závisí jen na obsahu repa, ne na stroji.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "TemplateDesigner", "dotnet")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "TemplateDesigner", "dotnet", "Windows", "PrintDialog",
                            "PrintDialogViewModel.cs");
    }

    [Fact]
    public void PrilisVelkaStranka_NaListSeNevejde()
    {
        // Chyba konfigurace musí být hlášená, ne tiše přeskočená.
        var ex = Assert.Throws<ArgumentException>(() =>
            SheetComposer.ResolveGrid(SheetLayoutKeys.GridAuto, 420f, 297f, 210f, 297f));
        Assert.Contains("nevejde", ex.Message);
    }
}
