using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Styl sloupce na záložce Sloupce edituje vlastní editor (`CellStyleEditor`), ne formulář prvku: formulář
/// v okně sloupců vypadal cize (vyhledávání, sbalovací skupiny) a editor zůstal jako vědomá výjimka
/// (rozhodnutí Jan 28. 10., `B194` E13c). Sada stylu tak existuje dvakrát a do 28. 10. se rozešla
/// v pořadí, popiscích i mezích - tyhle testy drží editor ve shodě se sadou formuláře (`StyleFields.cls`).
/// </summary>
public class EditorStyluSloupceTests
{
    [Fact]
    public void PopiskyAPoradi_OdpovidajiSadeStyluFormulare()
    {
        var formular = Regex.Matches(StyleFields(), @"labelPrefix_""([^""]+)""")
            .Select(m => m.Groups[1].Value).ToList();
        var editor = Regex.Matches(Editor(), @"Text=""([^""]+)""\s+Style=""\{StaticResource FieldLabel\}""")
            .Select(m => m.Groups[1].Value).ToList();

        Assert.True(formular.Count >= 15, $"Načteno jen {formular.Count} polí - regex nesedí na StyleFields.cls?");
        Assert.Equal(formular, editor);
    }

    [Theory]
    [InlineData("FontSize", CellStyleVM.MinFontSize, CellStyleVM.MaxFontSize)]
    [InlineData("PaddingTop", 0f, CellStyleVM.MaxPadding)]
    [InlineData("PaddingRight", 0f, CellStyleVM.MaxPadding)]
    [InlineData("PaddingBottom", 0f, CellStyleVM.MaxPadding)]
    [InlineData("PaddingLeft", 0f, CellStyleVM.MaxPadding)]
    public void Meze_OdpovidajiFormulari(string pole, float min, float max)
    {
        var m = Regex.Match(StyleFields(), $@"AddDecimal\(prefix_""\.{pole}"",[^,]+,\s*""[^""]*"",\s*([\d.]+),\s*([\d.]+)");

        Assert.True(m.Success, $"pole {pole} v StyleFields.cls nenalezeno");
        Assert.Equal(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), min);
        Assert.Equal(float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), max);
    }

    [Fact]
    public void HodnotaMimoMeze_SeOmezi()
    {
        var vm = CellStyleVM.FromModel(null, ResolvedTextStyle.Default);

        vm.FontSize = 100f;
        Assert.Equal(CellStyleVM.MaxFontSize, vm.FontSize);
        vm.FontSize = 1f;
        Assert.Equal(CellStyleVM.MinFontSize, vm.FontSize);
        vm.PaddingTop = 50f;
        Assert.Equal(CellStyleVM.MaxPadding, vm.PaddingTop);
        vm.PaddingLeft = -1f;
        Assert.Equal(0f, vm.PaddingLeft);
        vm.FontSize = null;
        Assert.Null(vm.FontSize);
    }

    private static string StyleFields() => File.ReadAllText(Path.Combine(RepoRoot(),
        "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm", "StyleFields.cls"));

    private static string Editor() => File.ReadAllText(Path.Combine(RepoRoot(),
        "TemplateDesigner", "dotnet", "Controls", "CellStyleEditor.xaml"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln"))) dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return dir.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
