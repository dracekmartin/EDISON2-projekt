using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Serialization;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Katalog prvků je jediný popis druhu prvku, ze kterého se staví paleta, nabídka vložení, továrna,
/// jména, agendy prvků, seznam pro asistenta a nápověda F1.
/// Ručně zůstávají třída, atribut <see cref="JsonDerivedTypeAttribute"/>, formulář `.cls` a ikona -
/// tyhle testy hlídají, že s katalogem souhlasí. Do 30. 10. nehlídalo nic konstantu v
/// <see cref="ElementTypes"/> bez atributu a nový prvek se zapisoval na 12 až 20 míst.
/// </summary>
public class ElementCatalogTests
{
    private static HashSet<string> KatalogTypy() => ElementCatalog.All.Select(e => e.Type).ToHashSet();

    [Fact]
    public void Katalog_ObsahujeKazdouKonstantuElementTypes()
    {
        var konstanty = typeof(ElementTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();

        Assert.True(konstanty.SetEquals(KatalogTypy()),
            $"ElementTypes × katalog: jen v ElementTypes {string.Join(", ", konstanty.Except(KatalogTypy()))}, " +
            $"jen v katalogu {string.Join(", ", KatalogTypy().Except(konstanty))}");
    }

    [Fact]
    public void Katalog_SouhlasiSTypyVJsonSablony()
    {
        var atributy = typeof(TemplateElement).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToDictionary(a => (string)a.TypeDiscriminator!, a => a.DerivedType);

        Assert.True(atributy.Keys.ToHashSet().SetEquals(KatalogTypy()),
            "Typ v katalogu bez [JsonDerivedType] na TemplateElement (nebo naopak) - šablona s ním nepůjde uložit ani načíst.");
        foreach (var prvek in ElementCatalog.All)
            Assert.Equal(atributy[prvek.Type], prvek.ClrType);
    }

    [Fact]
    public void Katalog_ObsahujeKazdouTriduPrvku()
    {
        var tridy = typeof(TemplateElement).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(TemplateElement)) && !t.IsAbstract).ToHashSet();

        Assert.True(tridy.SetEquals(ElementCatalog.All.Select(e => e.ClrType)),
            "Třída prvku, která v katalogu není: " + string.Join(", ", tridy.Except(ElementCatalog.All.Select(e => e.ClrType)).Select(t => t.Name)));
    }

    [Fact]
    public void KazdyPrvek_SeZalozIKatalogemSeSpravnymTypemAPozici()
    {
        foreach (var prvek in ElementCatalog.All)
        {
            var el = ElementCatalog.Create(prvek.Type, 12f, 7f);

            Assert.IsType(prvek.ClrType, el);
            Assert.Equal((12f, 7f), (el.X, el.Y));
            Assert.Equal(prvek.Width, el.Width);
        }
    }

    [Fact]
    public void KazdyPrvek_MaFormularVCache()
    {
        var formulare = Path.Combine(ZdrojeAgend.Koren(), "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
        var chybi = ElementCatalog.All
            .Where(e => !File.Exists(Path.Combine(formulare, $"{e.Type}Edit.cls")))
            .Select(e => e.Type).ToList();

        Assert.True(chybi.Count == 0, "Prvek bez formuláře {Typ}Edit.cls: " + string.Join(", ", chybi));
        Assert.All(ElementCatalog.All, e =>
            Assert.Equal($"ALVA.TemplateDesigner.EDInputForm.{e.Type}Edit", ElementCatalog.FormClass(e.Type)));
    }

    [Fact]
    public void KazdyPrvek_MaIkonuVEdIcons()
    {
        var ikony = File.ReadAllText(Path.Combine(ZdrojeAgend.Koren(), "EDControls", "dotnet", "EDIcons", "EDIcons.xaml"));
        var chybi = ElementCatalog.All.Where(e => !ikony.Contains($"x:Key=\"{e.IconKey}\"")).Select(e => e.IconKey).ToList();

        Assert.True(chybi.Count == 0, "Ikona, která v EDIcons.xaml není: " + string.Join(", ", chybi));
    }

    [Fact]
    public void JmenaJsouJedinecnaADruhyAgendZname()
    {
        Assert.Equal(ElementCatalog.All.Count, ElementCatalog.All.Select(e => e.Name).Distinct().Count());
        Assert.All(ElementCatalog.All.SelectMany(e => e.AgendaKinds),
            k => Assert.Contains(k, new[] { KontraktDat.Zastavkove, KontraktDat.Linkove }));
    }

    /// <summary>Agendy prvků se odvozují z katalogu - chování palety a vkládání se tím nesmí změnit.</summary>
    [Fact]
    public void PrvkyAgend_ZustavajiJakoDriv()
    {
        Assert.Equal([ElementTypes.StopList, ElementTypes.DepartureGrid, ElementTypes.NotesLegend],
            AgendaCapabilities.ScheduleElementTypes(KontraktDat.Zastavkove));
        Assert.Equal([ElementTypes.LineTable, ElementTypes.NotesLegend],
            AgendaCapabilities.ScheduleElementTypes(KontraktDat.Linkove));
        Assert.Empty(AgendaCapabilities.ScheduleElementTypes(""));
    }

    [Fact]
    public void JmenaProUzivatele_BereZKatalogu()
    {
        foreach (var prvek in ElementCatalog.All)
        {
            Assert.Equal(prvek.Name, ElementNames.ForType(prvek.Type));
            Assert.Equal(prvek.Name, ElementNames.ForUser(ElementCatalog.Create(prvek.Type, 0, 0)));
        }
    }
}
