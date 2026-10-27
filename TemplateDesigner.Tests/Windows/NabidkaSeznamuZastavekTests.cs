using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// `B194d` E5: Seznam zastávek nabízí jen to, k čemu agenda šablony posílá data. Vývěska MHD nemá
/// sloupky, varianty tras ani stanoviště podle spojů - sloupce by zůstaly prázdné a „Více stanovišť"
/// by nic nedělalo. Zapnutý sloupec zůstává v nabídce, aby šel vypnout.
/// </summary>
public class NabidkaSeznamuZastavekTests
{
    public NabidkaSeznamuZastavekTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static readonly Func<string, bool> JakoMhd = klic => StopListElement.SloupecNabizet(klic, maSloupek: false, maVarianty: false);

    [Fact]
    public void Mhd_NenabiziSloupekAniVarianty()
    {
        var vm = new StopColumnsEditorDialogViewModel(new StopListElement(), [], JakoMhd);
        var klice = vm.ColumnVMs.Select(c => c.Key).ToList();

        Assert.DoesNotContain(StopFields.Sloupek, klice);
        Assert.DoesNotContain(StopFields.TravelTimeVariants, klice);
        Assert.Contains(StopFields.Nastupiste, klice);   // označník MHD má
    }

    [Fact]
    public void BezPravidla_NabiziVse()
    {
        var vm = new StopColumnsEditorDialogViewModel(new StopListElement(), []);
        var klice = vm.ColumnVMs.Select(c => c.Key).ToList();

        Assert.Contains(StopFields.Sloupek, klice);
        Assert.Contains(StopFields.TravelTimeVariants, klice);
    }

    /// <summary>Šablona s už zapnutým sloupkem (z AD, nebo z doby před E5) ho v okně vidí, jinak by ho
    /// nešlo vypnout. Vypnutý nenabízený sloupec se ze šablony neztratí - OK ho vrátí, jak byl.</summary>
    [Fact]
    public void ZapnutySeNabidne_VypnutyZustaneVSablone()
    {
        var el = new StopListElement
        {
            Columns =
            [
                new() { Key = StopFields.StopName, Visible = true },
                new() { Key = StopFields.Sloupek, Visible = true },
                new() { Key = StopFields.TravelTimeVariants, Visible = false, Width = 9f },
            ],
        };
        var vm = new StopColumnsEditorDialogViewModel(el, el.Columns, JakoMhd);

        Assert.Contains(StopFields.Sloupek, vm.ColumnVMs.Select(c => c.Key));
        Assert.DoesNotContain(StopFields.TravelTimeVariants, vm.ColumnVMs.Select(c => c.Key));

        vm.OkCommand.Execute(null);

        var varianty = Assert.Single(vm.ResultColumns, c => c.Key == StopFields.TravelTimeVariants);
        Assert.False(varianty.Visible);
        Assert.Equal(9f, varianty.Width);
    }

    [Fact]
    public void ViceStanovist_JenSeStanovistiDleSpoju()
    {
        Assert.False(StopListElement.PoleFormulareViditelne(nameof(StopListElement.MultiPlatformMode), stanovisteDleSpoju: false));
        Assert.True(StopListElement.PoleFormulareViditelne(nameof(StopListElement.MultiPlatformMode), stanovisteDleSpoju: true));
        Assert.True(StopListElement.PoleFormulareViditelne("ShowStopIndentation", stanovisteDleSpoju: false));
    }

    [Fact]
    public void Registr_CteVariantyTras_StarsiServerBezKliceNabidne()
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var zeServeru = JsonSerializer.Deserialize<List<AgendaRegistryEntry>>(
            """[{"code":"ZASTAVKOVY_JR_MHD","label":"x","cacheClass":"c","variantyTras":false},{"code":"STARY","label":"x","cacheClass":"c"}]""", opts)!;

        Assert.False(zeServeru[0].VariantyTras);
        Assert.True(zeServeru[1].VariantyTras);
    }

    /// <summary>Příznak sedí s daty: varianty hlásí jen agenda, která u zastávek posílá jejich časy
    /// (`TRASA_MIN`). Jinak by se sloupec nabízel prázdný, nebo chyběl tam, kde funguje.</summary>
    [Fact]
    public void PriznakVariantSediSDaty()
    {
        var priznak = new Regex(@"^Parameter VariantyTras As %Boolean = 1;", RegexOptions.Multiline);
        var rozpor = Directory.GetFiles(AgendyDir(), "*.cls")
            .Where(f => Path.GetFileName(f) != "BaseAgenda.cls")
            .Select(f => (Soubor: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .Where(a => a.Text.Contains("\"TRASA_MIN\"") != priznak.IsMatch(a.Text))
            .Select(a => a.Soubor)
            .ToList();

        Assert.Empty(rozpor);
        Assert.Contains("Parameter VariantyTras As %Boolean = 0;", File.ReadAllText(Path.Combine(AgendyDir(), "BaseAgenda.cls")));
    }

    private static string AgendyDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return Path.Combine(dir!.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
