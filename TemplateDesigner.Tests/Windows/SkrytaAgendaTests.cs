using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using NSubstitute;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Skrytá (nezveřejněná) agenda se nesmí vrátit pamětí aplikace.
/// </summary>
/// <remarks>
/// Linkové JŘ se před vydáním skryly, ale Jan je v aplikaci pořád viděl - a bylo to takhle:
/// naposledy použitou agendu si aplikace ukládá do uživatelských preferencí a po startu ji
/// obnovila **bez ohledu na to, jestli je zveřejněná**. Stala se tím agendou šablony a výběr agend
/// ji pak ukázal, protože aktuální agendu zobrazuje vždy (aby šly otevřít hotové šablony).
/// Skrytí se tedy dalo obejít tím, že si ji člověk kdysi vyzkoušel.
/// </remarks>
public class SkrytaAgendaTests
{
    public SkrytaAgendaTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    // ⚠️ Fiktivní kódy, ne reálné agendy. Testy tím nezávisí na tom, která agenda je právě výchozí
    // (mění se podle toho, na čem se pracuje) - s reálnými kódy stačilo přepnout výchozí agendu
    // na tu, která je tady „skrytá", a `SkrytaAgendaZPreferenci_SeNeobnovi` spadl na fallbacku.
    private static readonly AgendaRegistryEntry Zverejnena =
        new("TEST_ZVEREJNENA", "Zveřejněná (test)", "ALVA.Test.Zverejnena", Released: true);

    private static readonly AgendaRegistryEntry Skryta =
        new("TEST_SKRYTA", "Skrytá (test)", "ALVA.Test.Skryta", Released: false);

    /// <summary>Preference s uloženou naposledy použitou agendou.</summary>
    private static async Task<TdPreferencesService> PrefsSPosledniAgendou(string kod)
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync(Arg.Any<string>(), Arg.Any<string>())
             .Returns(_ => Task.FromResult<JsonNode?>(JsonNode.Parse($$"""{"LastAgendaCode":"{{kod}}"}""")));

        var service = new TdPreferencesService(prefs);
        await service.LoadAsync();
        return service;
    }

    [Fact]
    public async Task SkrytaAgendaZPreferenci_SeNeobnovi()
    {
        var prefs = await PrefsSPosledniAgendou(Skryta.Code);
        var vm = new TemplateDesignerViewModel(null, prefs, [Zverejnena, Skryta]);

        Assert.NotEqual(Skryta.Code, vm.State.CurrentTemplate.AgendaCode);
    }

    [Fact]
    public async Task ZverejnenaAgendaZPreferenci_SeObnovi()
    {
        var prefs = await PrefsSPosledniAgendou(Zverejnena.Code);
        var vm = new TemplateDesignerViewModel(null, prefs, [Zverejnena, Skryta]);

        Assert.Equal(Zverejnena.Code, vm.State.CurrentTemplate.AgendaCode);
    }

    [Fact]
    public void VychoziSablonaMaZverejnenouAgendu()
    {
        // Konstanta ve výchozí šabloně obchází skrytí: nová šablona nastartuje na skryté agendě
        // a výběr ji pak ukáže, protože aktuální agendu zobrazuje vždy (nález Jan 1. 9.).
        // ⚠️ Test proto NEkontroluje konkrétní kód - ten se legitimně mění podle toho, na čem se
        // pracuje (24. 8. linkový, 1. 9. zastávkový, 3. 9. zpátky linkový). Kontroluje **vazbu**:
        // výchozí agenda musí mít v `.cls` `Released = 1`. Kdo agendu skryje a zapomene na konstantu,
        // spadne tady.
        var kod = TemplateDesignerViewModel.CreateSampleTemplate().AgendaCode;
        var zverejnene = ZverejneneAgendyZRepa();

        Assert.NotEmpty(zverejnene);
        Assert.Contains(kod, zverejnene);
    }

    /// <summary>Kódy agend, které mají v Caché třídě `Released = 1`. Čte se z repa, ne ze serveru -
    /// test smí záviset jen na obsahu repa.</summary>
    private static List<string> ZverejneneAgendyZRepa()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "TemplateDesigner")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var agendy = Path.Combine(dir!.FullName, "TemplateDesigner", "CACHE", "ALVA",
                                  "TemplateDesigner", "Agendy");
        var kody = new List<string>();
        foreach (var soubor in Directory.GetFiles(agendy, "*.cls"))
        {
            var text = File.ReadAllText(soubor);
            if (!Regex.IsMatch(text, @"Parameter\s+Released[^=]*=\s*1\s*;")) continue;

            // `AgendaVariantCode` je stabilní persistovaný klíč agendy - týž, co nese šablona.
            var kod = Regex.Match(text, @"Parameter\s+AgendaVariantCode[^=]*=\s*""([^""]+)""");
            if (kod.Success) kody.Add(kod.Groups[1].Value);
        }
        return kody;
    }

    [Fact]
    public async Task SkrytaAgendaZLaunchArgumentu_SeNastavi()
    {
        // Kód ze shellu je explicitní příkaz zvenčí; skrytá agenda tam znamená chybu konfigurace
        // shellu, ne zapamatovaný stav uživatele - proto se tady filtr záměrně neuplatní.
        var prefs = await PrefsSPosledniAgendou(Zverejnena.Code);
        var vm = new TemplateDesignerViewModel(null, prefs, [Zverejnena, Skryta], Skryta.Code);

        Assert.Equal(Skryta.Code, vm.State.CurrentTemplate.AgendaCode);
    }
}
