using System.IO;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Popisek nastavení v programu (deskriptor - formátovací lišta, protokol importu z PDF, asistent)
/// musí být tentýž jako ve formuláři prvku, který uživatel vidí nejčastěji (`B194` E11, plán G6).
/// Do 26. 10. se rozešly („Zalamování" × „Zalamovat řádky", „Výplň" × „Barva pozadí", „Šířka sl.
/// hodin" × „Šířka sloupce hodin").
/// </summary>
/// <remarks>
/// Deskriptor nenese klíč pole, proto test páruje podle popisku: popisek deskriptoru musí ve
/// formuláři prvku existovat. Formulář = jeho <c>*Edit.cls</c>, společná pole
/// (<c>BaseElementFields.cls</c>) a sada stylu (<c>StyleFields.cls</c>) i s předponami zón.
/// </remarks>
public class DescriptorLabelsMatchFormTests
{
    /// <summary>Vědomé výjimky - popisek v programu záměrně jiný než ve formuláři, s důvodem.</summary>
    private static readonly Dictionary<string, string> Vyjimky = new()
    {
        // Rychlý přístup ke čtyřem barvám zón tabulky (mvvm-patterns, Pattern 2) - formulář je má
        // ve skupině Záhlaví / Datové buňky jako „Barva pozadí" / „Barva textu", kanonické názvy jsou tyhle (§8).
        ["Barva pozadí záhlaví"] = "barvy zón tabulky",
        ["Barva textu záhlaví"] = "barvy zón tabulky",
        ["Barva pozadí buněk"] = "barvy zón tabulky",
        ["Barva textu buněk"] = "barvy zón tabulky",
        ["Tučné záhlaví"] = "promoted shortcut tabulek (mvvm-patterns)",
        // Ve formuláři je jinde než v hlavním formuláři prvku.
        ["Šířka sloupce hodin"] = "Minuty odjezdů: ve formuláři v záložce Sloupce (řádek Hodiny)",
        ["Zdroj dat"] = "pevná tabulka: jen u starých šablon v režimu tabulky z dat, formulář ho nenabízí",
    };

    public static TheoryData<string, string> Prvky => new()
    {
        { nameof(TextElement), "TextElementEdit" },
        { nameof(RectangleElement), "RectangleElementEdit" },
        { nameof(LineElement), "LineElementEdit" },
        { nameof(ImageElement), "ImageElementEdit" },
        { nameof(BarcodeElement), "BarcodeElementEdit" },
        { nameof(TableElement), "TableElementEdit" },
        { nameof(DynamicTableElement), "DynamicTableElementEdit" },
        { nameof(StopListElement), "StopListElementEdit" },
        { nameof(DepartureGridElement), "DepartureGridElementEdit" },
        { nameof(NotesLegendElement), "NotesLegendElementEdit" },
        { nameof(LineTableElement), "LineTableElementEdit" },
    };

    [Theory]
    [MemberData(nameof(Prvky))]
    public void PopisekVProgramuJeIVeFormulari(string prvek, string formular)
    {
        var element = (TemplateElement)Activator.CreateInstance(
            typeof(TemplateElement).Assembly.GetType($"TemplateDesigner.Models.Elements.{prvek}")!)!;
        var veFormulari = PopiskyFormulare(formular);

        var chybi = element.GetPropertyDescriptors()
            .Where(d => d is not ButtonPropertyDescriptor)
            .Select(d => d.Label)
            .Distinct()
            .Where(l => !veFormulari.Contains(l) && !Vyjimky.ContainsKey(l))
            .ToList();

        Assert.True(chybi.Count == 0,
            $"{prvek}: popisek v programu formulář {formular}.cls nezná - přepiš ho podle formuláře: "
            + string.Join(", ", chybi.Select(l => $"„{l}“")));
    }

    private static readonly Regex PoleRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""[^""]*"",\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex StylPopisekRx = new(@"labelPrefix_""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex StylVolaniRx = new(@"StyleFields\)\.Add\(([^\r\n]*)\)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    private static HashSet<string> PopiskyFormulare(string formular)
    {
        string text = File.ReadAllText(Cls(formular));
        var popisky = new HashSet<string>(PoleRx.Matches(text).Select(m => m.Groups[1].Value));
        popisky.UnionWith(PoleRx.Matches(File.ReadAllText(Cls("BaseElementFields"))).Select(m => m.Groups[1].Value));

        // Sada stylu: popisky bez předpony a s předponou zóny z volání v tomhle formuláři („Minuta: ").
        var styl = StylPopisekRx.Matches(File.ReadAllText(Cls("StyleFields"))).Select(m => m.Groups[1].Value).ToList();
        popisky.UnionWith(styl);
        foreach (Match volani in StylVolaniRx.Matches(text))
        {
            var argumenty = volani.Groups[1].Value.Split(',').Select(a => a.Trim().Trim('"')).ToList();
            if (argumenty.Count >= 8 && argumenty[7].Length > 0)
                popisky.UnionWith(styl.Select(s => argumenty[7] + s));
        }
        return popisky;
    }

    private static string Cls(string trida) => Path.Combine(EDInputFormDir(), trida + ".cls");

    private static string EDInputFormDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "TemplateDesigner")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }
}
