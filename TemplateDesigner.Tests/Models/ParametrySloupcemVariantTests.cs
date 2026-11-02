using System.IO;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Serialization;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Parametry variant tras (řazení, nejvýš sloupců, slučování, písmena) mají účinek jen se sloupcem
/// Varianty tras v seznamu zastávek. Bez něj je lišta parametrů schová - uživatel má vidět jen
/// nastavení pro to, s čím v šabloně pracuje (rozhodnutí Jan 5. 10.).
/// </summary>
public class ParametrySloupcemVariantTests
{
    private static TemplateParameter ParametrVariant(string? visibleWhen = null) => new()
    {
        Name = "RAZENI_VARIANT", Type = "enum", VisibleWhen = visibleWhen,
        VisibleWhenColumn = StopFields.TravelTimeVariants,
    };

    private static Template Sablona(bool sloupecVariant)
    {
        var seznam = new StopListElement();
        seznam.Columns.First(c => c.Key == StopFields.TravelTimeVariants).Visible = sloupecVariant;
        return new Template { Sections = [new Section { Elements = [seznam] }] };
    }

    private static readonly Dictionary<string, object?> BezHodnot = [];

    [Fact]
    public void BezSloupceVariantSeSchova()
    {
        Assert.False(ParametrVariant().IsVisible(BezHodnot, Sablona(sloupecVariant: false)));
    }

    [Fact]
    public void SeSloupcemVariantSeUkaze()
    {
        Assert.True(ParametrVariant().IsVisible(BezHodnot, Sablona(sloupecVariant: true)));
    }

    [Fact]
    public void BezZnalostiSablonySeUkaze()
    {
        // Okno, které šablonu nemá, nesmí parametr schovat natrvalo.
        Assert.True(ParametrVariant().IsVisible(BezHodnot, template: null));
    }

    [Fact]
    public void PodminkaNaJinyParametrPlatiSoucasne()
    {
        // „První varianta bez písmene" má navíc podmínku na zapnutá písmena variant.
        var parametr = ParametrVariant(visibleWhen: "PISMENA_VARIANT=true");
        var sablona = Sablona(sloupecVariant: true);

        Assert.True(parametr.IsVisible(new Dictionary<string, object?> { ["PISMENA_VARIANT"] = "true" }, sablona));
        Assert.False(parametr.IsVisible(new Dictionary<string, object?> { ["PISMENA_VARIANT"] = "false" }, sablona));
    }

    [Fact]
    public void ParametrBezZavislostiNaSloupciSeUkazeVzdy()
    {
        var parametr = new TemplateParameter { Name = "TISK_SIPKY", Type = "bool" };

        Assert.True(parametr.IsVisible(BezHodnot, Sablona(sloupecVariant: false)));
    }

    /// <summary>Překlep v klíči sloupce na serveru by parametr schoval natrvalo - žádný prvek by
    /// takový sloupec neukazoval a nikdo by nepoznal proč.</summary>
    [Fact]
    public void ServerOdkazujeJenNaSloupceKtereProvkyZnaji()
    {
        var zname = StopListElement.GetDefaultColumns().Concat(LineTableElement.GetDefaultColumns())
            .Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var rx = new Regex(@"visibleWhenColumn""?\s*[,=]\s*""([A-Za-z_0-9]+)""");
        var odkazy = ZdrojeAgend.KodAgend()
            .SelectMany(f => rx.Matches(File.ReadAllText(f)).Select(m => (Soubor: Path.GetFileName(f), Klic: m.Groups[1].Value)))
            .ToList();

        Assert.NotEmpty(odkazy);   // pojistka regexu: zastávková agenda závislost má
        var nezname = odkazy.Where(o => !zname.Contains(o.Klic)).Select(o => $"{o.Soubor}: {o.Klic}").ToList();
        Assert.True(nezname.Count == 0, "Parametr závisí na sloupci, který žádný prvek nezná:\n" + string.Join("\n", nezname));
    }
}
