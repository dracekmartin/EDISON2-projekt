using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Záložku „Vlastní definice sloupců" v okně Minut odjezdů návrhář ukáže jen u agendy, která definici
/// opravdu čte. Do 24. 10. byla vidět všude a ve vývěsce MHD nic nedělala (`B194a` C5/C6, rozhodnutí
/// Jan: koncept AD, MHD ho nedostane). Agenda to hlásí příznakem <c>CustomColumns</c> v registru.
/// </summary>
public class VlastniSloupceAgendyTests
{
    public VlastniSloupceAgendyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>Příznak sedí s kódem: kdo definici čte (<c>ParseDefiniceSloupcu</c>), hlásí 1, ostatní ne.
    /// Jinak by se záložka ukázala tam, kde nic nedělá, nebo chyběla tam, kde funguje.</summary>
    [Fact]
    public void PriznakAgendySediSTim_ZdaDefiniciCte()
    {
        var priznak = new Regex(@"^Parameter CustomColumns As %Boolean = 1;", RegexOptions.Multiline);
        var rozpor = ZdrojeAgend.Agendy()
            .Select(f => (Soubor: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .Where(a => a.Text.Contains("..ParseDefiniceSloupcu(") != priznak.IsMatch(a.Text))
            .Select(a => a.Soubor)
            .ToList();

        Assert.Empty(rozpor);
        Assert.Contains("Parameter CustomColumns As %Boolean = 0;", File.ReadAllText(Path.Combine(ZdrojeAgend.Slozka, "BaseAgenda.cls")));
    }

    [Fact]
    public void Registr_CteKlicZeServeru_StarsiServerBezKliceUkazeZalozku()
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var zeServeru = JsonSerializer.Deserialize<List<AgendaRegistryEntry>>(
            """[{"code":"ZASTAVKOVY_JR_MHD","label":"x","cacheClass":"c","customColumns":false},{"code":"STARY","label":"x","cacheClass":"c"}]""", opts)!;

        Assert.False(zeServeru[0].CustomColumns);
        Assert.True(zeServeru[1].CustomColumns);
    }

    /// <summary>Schovaná záložka definici v šabloně nemění ani nekontroluje - OK ji vrátí, jak byla,
    /// i kdyby měla seznam značek, který by kontrola odmítla (uživatel by chybu neviděl).</summary>
    [Fact]
    public void SchovanaZalozka_OkVratiDefiniciBezeZmeny()
    {
        var definice = new List<DepartureColumnDefinition> { new() { Id = "1", Header = "Pracovní dny", IncludeMarks = "x" } };
        var el = new DepartureGridElement { CustomColumns = definice };
        var vm = new DepartureGroupEditorDialogViewModel(el, [], el.GroupColumns, el.HourColumnStyle, el.CustomColumns,
            ukazatVlastniSloupce: false);

        vm.OkCommand.Execute(null);

        Assert.False(vm.UkazatVlastniSloupce);
        Assert.Null(vm.CustomColumnsError);
        Assert.Same(definice, vm.ResultCustomColumns);
    }
}
