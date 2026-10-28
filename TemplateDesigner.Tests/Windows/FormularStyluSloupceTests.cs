using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EDControls;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Controls;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Styl sloupce na záložce Sloupce se edituje formulářem se sadou stylu formuláře prvku
/// (`TableColumnEdit.cls`, `B194` E13c). Formulář ukazuje i zděděné hodnoty, takže hlavní riziko je,
/// že je samo otevření a potvrzení zapeče do sloupce - sloupec by se pak odpojil od nastavení prvku.
/// </summary>
public class FormularStyluSloupceTests
{
    public FormularStyluSloupceTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static StopListElement Prvek() => new()
    {
        HeaderStyle = new TextStyle { FontSize = 11f, Bold = true },
        DataStyle = new TextStyle { TextColor = "#FF445566" },
    };

    private static List<Field> Formular() =>
    [
        new Field { Key = "Header.FontSize", Label = "Velikost",            FieldType = FieldType.Decimal },
        new Field { Key = "Header.Bold",     Label = "Tučné",               FieldType = FieldType.Bool },
        new Field { Key = "Data.FontSize",   Label = "Velikost",            FieldType = FieldType.Decimal },
        new Field { Key = "Data.TextColor",  Label = "Barva textu",         FieldType = FieldType.Color },
        new Field { Key = "Data.HAlign",     Label = "Zarovnání vodorovně", FieldType = FieldType.List },
    ];

    private static (List<Field> Fields, ColumnVisualStyle Target, Dictionary<string, string> PreFilled) Otevri(StopColumnVM row)
    {
        var fields = Formular();
        ElementFormBinder.ApplyInheritedValues(fields, row.InheritedStyleValues());
        var target = row.CreateStyleTarget();
        return (fields, target, ElementFormBinder.Prefill(fields, target));
    }

    private static void Potvrd(StopColumnVM row, List<Field> fields, ColumnVisualStyle target, Dictionary<string, string> preFilled)
    {
        foreach (var command in ElementFormBinder.ExtractCommands(fields, target, preFilled)) command.Execute();
        row.ApplyStyle(target);
    }

    private static StopColumnVM Radek(StopListElement prvek, ColumnVisualStyle? styl = null) =>
        StopColumnVM.FromModel(new StopColumn { Key = StopFields.StopName, Header = "Zastávka", Style = styl }, prvek);

    [Fact]
    public void Formular_UkazujeZdedeneHodnoty()
    {
        var (fields, _, _) = Otevri(Radek(Prvek()));

        Assert.Equal("11", fields.Single(f => f.Key == "Header.FontSize").Value);
        Assert.Equal("1", fields.Single(f => f.Key == "Header.Bold").Value);
        Assert.Equal("#FF445566", fields.Single(f => f.Key == "Data.TextColor").Value);
    }

    [Fact]
    public void PotvrzeniBezeZmeny_NezapeceZdedeneHodnoty()
    {
        var row = Radek(Prvek());
        var (fields, target, preFilled) = Otevri(row);

        Assert.Empty(ElementFormBinder.ExtractCommands(fields, target, preFilled));
        Potvrd(row, fields, target, preFilled);

        Assert.False(row.HasAnyOverride);
        Assert.Null(row.ToModel().Style);
    }

    [Fact]
    public void ZmenaJednohoPole_ZapiseJenJi()
    {
        var row = Radek(Prvek());
        var (fields, target, preFilled) = Otevri(row);

        fields.Single(f => f.Key == "Data.FontSize").Value = "12";
        Potvrd(row, fields, target, preFilled);

        var styl = row.ToModel().Style!;
        Assert.Equal(12f, styl.Data!.FontSize);
        Assert.Null(styl.Data.TextColor);   // dál se dědí z prvku
        Assert.Null(styl.Header);
        Assert.True(row.HasAnyOverride);
    }

    [Fact]
    public void VymazanePole_VratiDedeni()
    {
        var row = Radek(Prvek(), new ColumnVisualStyle { Data = new TextStyle { FontSize = 20f } });
        var (fields, target, preFilled) = Otevri(row);

        fields.Single(f => f.Key == "Data.FontSize").Value = "";
        Potvrd(row, fields, target, preFilled);

        Assert.Null(row.ToModel().Style);
    }

    [Fact]
    public void ResetStyluSloupce_OhlasiFormulari()
    {
        var row = Radek(Prvek(), new ColumnVisualStyle { Data = new TextStyle { FontSize = 20f } });
        var tab = new ColumnsTabViewModel([row]) { UsesStyleForm = true };
        var ohlaseno = 0;
        tab.StylesReset += () => ohlaseno++;

        tab.ResetStyleCommand.Execute(null);

        Assert.Equal(1, ohlaseno);
        Assert.Null(row.ToModel().Style);
    }

    // ── Vazba na serverové schéma ─────────────────────────────────────────────

    /// <summary>
    /// Formulář je vědomě mimo `EDInputFormSchemaDriftTests` (jeho výchozí hodnoty přepíše kaskáda),
    /// takže vazbu na C# hlídá tenhle test: každé pole musí být vlastnost stylu sloupce a mít hodnotu
    /// z kaskády. Bez vlastnosti by se změna tiše zahodila, bez zděděné hodnoty by formulář ukázal
    /// výchozí hodnotu ze schématu a uživatel by ji potvrzením zapekl.
    /// </summary>
    [Fact]
    public void KazdePoleSchematu_JeVlastnostStyluSloupceSeZdedenouHodnotou()
    {
        var schema = File.ReadAllText(Path.Combine(EDInputFormDir(), "TableColumnEdit.cls"));
        var prefixes = Regex.Matches(schema, @"StyleFields\)\.Add\(form,\s*""(\w+)""")
            .Select(m => m.Groups[1].Value).ToList();
        var suffixes = Regex.Matches(File.ReadAllText(Path.Combine(EDInputFormDir(), "StyleFields.cls")),
                @"prefix_""\.(\w+)""")
            .Select(m => m.Groups[1].Value).ToList();
        var keys = prefixes.SelectMany(p => suffixes.Select(s => $"{p}.{s}")).ToList();

        Assert.Equal(["Header", "Data"], prefixes);
        Assert.True(suffixes.Count >= 10, $"Načteno jen {suffixes.Count} polí sady stylu - regex nesedí na StyleFields.cls?");
        var inherited = Radek(Prvek()).InheritedStyleValues();
        Assert.All(keys, k =>
        {
            Assert.True(ElementPropertyAccessor.Exists(new ColumnVisualStyle(), k), $"pole {k} není vlastnost stylu sloupce");
            Assert.True(inherited.ContainsKey(k), $"pole {k} nemá hodnotu z kaskády");
        });
    }

    private static string EDInputFormDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln"))) dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return Path.Combine(dir.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
