using System.Globalization;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Vlastnosti prvku jsou na dvou místech: formulář v Caché (<c>*Edit.cls</c>, hlavní editační cesta, opravuje se bez nového
/// exe) a deskriptory v C# (lišta formátu, úprava více prvků, asistent, protokol importu z PDF). Rozhodnutí Jan 30. 10.:
/// neslučovat, ale hlídat přísněji. Popisek hlídá <see cref="DescriptorLabelsMatchFormTests"/>;
/// tady se deskriptor spáruje s polem formuláře podle popisku (při shodě popisků ve více skupinách i podle skupiny)
/// a porovná se skupina, meze, jednotka, druh a volby. Do 30. 10. se lišilo 24 skupin a meze nehlídalo nic.
/// </summary>
public class PanelProtiFormulariTests
{
    public static TheoryData<string> Prvky => [.. ElementCatalog.All.Select(e => e.Type)];

    /// <summary>Vědomé rozdíly: prvek|popisek|co → důvod (co = skupina, nejmenší, největší, jednotka, druh, volby).</summary>
    private static readonly Dictionary<string, string> Vyjimky = new(StringComparer.Ordinal)
    {
        ["LineTableElement|Nejvýš km sloupců|nejmenší"] = "v panelu 0 = všechny sloupce, ve formuláři prázdné pole (číselné pole panelu prázdné být nemůže)",
        ["DepartureGridElement|Symbol: Velikost|nejmenší"] = "v panelu 0 = automaticky podle velikosti minut, ve formuláři prázdné pole",
    };

    [Theory]
    [MemberData(nameof(Prvky))]
    public void DeskriptorOdpovidaPoliFormulare(string typ)
    {
        var prvek = ElementCatalog.Create(typ, 0, 0);
        var formular = FormularePrvku.Pole(ElementCatalog.FormClass(typ).Split('.')[^1]);
        Assert.True(formular.Count > 5, $"{typ}: ve formuláři se našlo jen {formular.Count} polí - čtení formuláře je rozbité");

        var nalezy = new List<string>();
        foreach (var d in prvek.GetPropertyDescriptors().Where(d => d is not ButtonPropertyDescriptor))
        {
            var kandidati = formular.Where(p => p.Popisek == d.Label).ToList();
            if (kandidati.Count == 0) continue;   // popisek mimo formulář hlídá DescriptorLabelsMatchFormTests
            var pole = kandidati.Count == 1 ? kandidati[0] : kandidati.FirstOrDefault(p => p.Skupina == d.Group);
            if (pole is null)
            {
                nalezy.Add($"„{d.Label}\" ({d.Group}): formulář ho má ve skupinách {string.Join(", ", kandidati.Select(k => k.Skupina))}");
                continue;
            }
            nalezy.AddRange(Rozdily(d, pole)
                .Where(r => !Vyjimky.ContainsKey($"{typ}|{d.Label}|{r.Co}"))
                .Select(r => $"„{d.Label}\" ({pole.Klic}): {r.Text}"));
        }

        Assert.True(nalezy.Count == 0, $"{typ}: panel se liší od formuláře {ElementCatalog.FormClass(typ)}:\n" + string.Join("\n", nalezy));
    }

    /// <summary>Párování musí najít většinu vlastností - jinak by test výš procházel nad prázdnem.</summary>
    [Fact]
    public void ParovaniNajdeVetsinuVlastnosti()
    {
        int vsech = 0, sparovanych = 0;
        foreach (var e in ElementCatalog.All)
        {
            var formular = FormularePrvku.Pole(ElementCatalog.FormClass(e.Type).Split('.')[^1]);
            foreach (var d in ElementCatalog.Create(e.Type, 0, 0).GetPropertyDescriptors().Where(d => d is not ButtonPropertyDescriptor))
            {
                vsech++;
                if (formular.Any(p => p.Popisek == d.Label)) sparovanych++;
            }
        }
        Assert.True(sparovanych > 150 && sparovanych > vsech * 0.8,
            $"spárováno jen {sparovanych} z {vsech} vlastností panelu - čtení formulářů je rozbité");
    }

    /// <summary>Výjimka, která už nic nevysvětluje, by jen visela.</summary>
    [Fact]
    public void VyjimkyPlati()
    {
        foreach (var klic in Vyjimky.Keys)
        {
            var (typ, popisek, co) = (klic.Split('|')[0], klic.Split('|')[1], klic.Split('|')[2]);
            var d = ElementCatalog.Create(typ, 0, 0).GetPropertyDescriptors().Single(x => x.Label == popisek);
            var pole = FormularePrvku.Pole(ElementCatalog.FormClass(typ).Split('.')[^1]).Single(p => p.Popisek == popisek);
            Assert.Contains(Rozdily(d, pole), r => r.Co == co);
        }
    }

    private static IEnumerable<(string Co, string Text)> Rozdily(PropertyDescriptor d, PoleFormulare pole)
    {
        if (d.Group != pole.Skupina) yield return ("skupina", $"skupina „{d.Group}\" × formulář „{pole.Skupina}\"");

        switch (d)
        {
            case FloatPropertyDescriptor f:
                if (pole.Druh is not ("decimal" or "number")) { yield return ("druh", $"číslo × formulář {pole.Druh}"); break; }
                if (!StejnaMez(f.Min, pole.Min, prazdna: 0f)) yield return ("nejmenší", $"nejmenší {f.Min} × formulář {Popis(pole.Min)}");
                if (!StejnaMez(f.Max, pole.Max, prazdna: 9999f)) yield return ("největší", $"největší {f.Max} × formulář {Popis(pole.Max)}");
                if (f.Unit != pole.Jednotka) yield return ("jednotka", $"jednotka „{f.Unit}\" × formulář „{pole.Jednotka}\"");
                break;
            case BoolPropertyDescriptor:
                if (pole.Druh != "bool") yield return ("druh", $"zaškrtávátko × formulář {pole.Druh}");
                break;
            case ColorPropertyDescriptor:
                if (pole.Druh != "color") yield return ("druh", $"barva × formulář {pole.Druh}");
                break;
            case EnumPropertyDescriptor e:
                if (pole.Druh != "list") { yield return ("druh", $"výběr × formulář {pole.Druh}"); break; }
                var panel = e.Options.Select(o => (Kod: Kod(o.Value), o.Label)).ToList();
                var form = pole.Volby.Where(v => v.Kod.Length > 0).Select(v => (v.Kod, v.Popisek)).ToList();
                if (!panel.SequenceEqual(form))
                    yield return ("volby", $"volby {string.Join(", ", panel.Select(v => $"{v.Kod}={v.Label}"))} × formulář {string.Join(", ", form.Select(v => $"{v.Kod}={v.Popisek}"))}");
                break;
        }
    }

    /// <summary>Kód volby, jak ho zapisuje formulář: název hodnoty výčtu, ano/ne jako 1/0.</summary>
    private static string Kod(object hodnota) => hodnota switch
    {
        bool b => b ? "1" : "0",
        _ => hodnota.ToString()!,
    };

    /// <summary>Mez formuláře „" = bez meze; deskriptor ji pak má mít výchozí (0 / 9999).</summary>
    private static bool StejnaMez(float panel, string? formular, float prazdna) =>
        string.IsNullOrEmpty(formular)
            ? panel == prazdna
            : float.TryParse(formular, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && Math.Abs(f - panel) < 0.001f;

    private static string Popis(string? mez) => string.IsNullOrEmpty(mez) ? "bez meze" : mez;
}
