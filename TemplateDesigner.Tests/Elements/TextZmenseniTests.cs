using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Text, který se nevejde do prvku, se zmenší - jako v buňkách tabulek a v prvcích jízdních řádů.
/// </summary>
/// <remarks>
/// Do 3. 9. textový prvek jako jediný text tvrdě ořezával (podnět kolega 3. 9.): pravidlo „pevná
/// výška zmenší písmo, prázdná nechá prvek dorůst" mělo jen druhou polovinu.
///
/// <para>Rozhodnutí Jan 3. 9.: zmenšuje se **bez volby a i v hotových šablonách** (konzistence
/// s ostatními prvky), ale s **dolní mezí** - polovina nastavené velikosti, nikdy pod 4 pt. Pod ní
/// se text ořízne jako dřív a řekne se to.</para>
/// </remarks>
public class TextZmenseniTests
{
    /// <summary>Text s pevnou výškou, do které se dlouhý obsah nevejde.</summary>
    private static TextElement Prvek(string text, float? vyska, bool wrap = true, float fontPt = 12f) => new()
    {
        X = 0, Y = 0, Width = 40, Height = vyska,
        Text = text,
        WordWrap = wrap,
        Style = new TextStyle { FontFamily = "Arial", FontSize = fontPt },
    };

    private const string Dlouhy =
        "Zastávka Praha, Hlavní nádraží, stanoviště číslo 12 pro linky mezinárodní dopravy";

    [Fact]
    public void KratkyTextSeVejde_Nezmensuje()
    {
        Assert.Equal(1f, Prvek("Linka 42", 20f).ComputeShrinkScale("Linka 42"));
    }

    [Fact]
    public void NovyPrvekMaZalamovaniVypnute()
    {
        // Rozhodnutí Jan 3. 9.: nový textový prvek je typicky nadpis nebo jeden údaj na řádku,
        // kde zalomení rozhodí layout víc, než pomůže. ⚠️ Výchozí hodnotu drží i formulář
        // (`TextElementEdit.cls`) - rozpor mezi nimi chytne EDInputFormSchemaDriftTests.
        Assert.False(new TextElement().WordWrap);
    }

    [Fact]
    public void HotovaSablonaSiNeseVlastniHodnotu()
    {
        // Změna výchozí hodnoty nesmí přepsat hotové šablony. Drží to serializace: bool se do
        // JSON zapisuje vždy (vynechává se jen null), takže si každý prvek nese tu svou.
        var puvodni = new TextElement { X = 0, Y = 0, Width = 40, Height = 10, Text = "a", WordWrap = true };
        var sablona = Sablona(puvodni);

        var json = TemplateSerializer.Serialize(sablona);
        Assert.Contains("\"wordWrap\": true", json);

        var nactena = TemplateSerializer.Deserialize(json);
        var prvek = Assert.IsType<TextElement>(nactena.Sections[0].Elements[0]);
        Assert.True(prvek.WordWrap, "šablona s zapnutým zalamováním ho musí mít i po načtení");
    }

    [Fact]
    public void DlouhyTextVPevneVysce_Zmensi()
    {
        var scale = Prvek(Dlouhy, 8f).ComputeShrinkScale(Dlouhy);

        Assert.True(scale < 1f, $"mělo se zmenšit, je {scale}");
    }

    [Fact]
    public void DynamickaVyska_Nezmensuje_PrvekDoroste()
    {
        // Prázdná výška znamená „engine ji počítá z obsahu" - zmenšovat by odebralo dynamiku
        // bez toho, že o to kdo požádal. Totéž pravidlo jako u prvků jízdních řádů.
        Assert.Equal(1f, Prvek(Dlouhy, null).ComputeShrinkScale(Dlouhy));
    }

    [Fact]
    public void JednoradkovyText_ZmensiSeNaSirku()
    {
        // Bez zalamování je text jeden řádek a přetéká do strany, ne na výšku.
        var scale = Prvek(Dlouhy, 20f, wrap: false).ComputeShrinkScale(Dlouhy);

        Assert.True(scale < 1f, $"jednořádkový text se měl zmenšit na šířku, je {scale}");
    }

    [Fact]
    public void ZmenseniNejdePodPulkuNastaveneVelikosti()
    {
        // Mez drží čitelnost: extrémně dlouhý text do malého prvku se zmenší na polovinu a dál ne.
        var text = string.Join(" ", Enumerable.Repeat("dlouhé slovo", 200));
        var scale = Prvek(text, 6f, fontPt: 20f).ComputeShrinkScale(text);

        Assert.Equal(TextElement.MinScale, scale, precision: 3);
    }

    [Fact]
    public void ZmenseniNejdePodCtyriBody()
    {
        // U malého písma je polovina pod 4 pt, což je hranice, kterou dovolí zadat pole Velikost.
        var text = string.Join(" ", Enumerable.Repeat("dlouhé slovo", 200));
        var prvek = Prvek(text, 4f, fontPt: 6f);

        float vyslednaVelikost = 6f * prvek.ComputeShrinkScale(text);

        Assert.True(vyslednaVelikost >= TextElement.MinFontPt - 0.01f,
            $"písmo spadlo na {vyslednaVelikost:0.##} pt, mez je {TextElement.MinFontPt} pt");
    }

    [Fact]
    public void PodMezi_SeTextOrizne_AOhlasiSe()
    {
        var text = string.Join(" ", Enumerable.Repeat("dlouhé slovo", 200));
        var prvek = Prvek(text, 6f, fontPt: 20f);

        Assert.True(prvek.ContentOverflows(null), "pod mezí se text ořízne - musí to jít poznat");

        var hlaska = ContentShrinkWarnings.Summarize(Sablona(prvek), [new DataContext(new Dictionary<string, object?>())]);
        Assert.NotNull(hlaska);
        Assert.Contains("nevejde", hlaska);
        Assert.Contains("zkrátit", hlaska);
    }

    [Fact]
    public void ZmenseniBezOrezu_HlasiJenProcenta()
    {
        var prvek = Prvek(Dlouhy, 8f);

        var hlaska = ContentShrinkWarnings.Summarize(Sablona(prvek), [new DataContext(new Dictionary<string, object?>())]);

        Assert.NotNull(hlaska);
        Assert.Contains("Text", hlaska);              // jméno prvku z palety
        Assert.Contains("%", hlaska);
        Assert.DoesNotContain("zkrátit", hlaska);     // vešlo se, není co zkracovat
    }

    [Fact]
    public void VejdeSe_ZadnaHlaska()
    {
        var hlaska = ContentShrinkWarnings.Summarize(
            Sablona(Prvek("Linka 42", 20f)), [new DataContext(new Dictionary<string, object?>())]);

        Assert.Null(hlaska);
    }

    [Fact]
    public void RenderKresliZmensenymPismem_VejdeSeVicRadku()
    {
        // Pojistka, že se škála dostane do kreslení, ne jen do hlášky. Měří se **počet vykreslených
        // řádků**: to je přesně to, co uživatel vidí. ⚠️ „Jak hluboko text sahá" se měřit nedá -
        // oříznutý text v plné velikosti zaplní prvek až ke spodní hraně stejně jako zmenšený,
        // jen mu chybí konec (změřeno 3. 9.: 63 z 64 px v obou případech).
        var prvek = Prvek(Dlouhy, 8f);
        var data = new DataContext(new Dictionary<string, object?>());

        int radky = VykreslenychRadku(prvek, data);

        // Bez zmenšení se do 8 mm vejdou 2 řádky dvanáctibodového písma, se zmenšením na 52 % čtyři.
        Assert.True(radky >= 4, $"vykreslily se {radky} řádky - vypadá to na oříznutí, ne zmenšení");
    }

    [Fact]
    public void RenderNezmensujeText_kteryVejdeVPlneVelikosti()
    {
        // Druhá strana téže pojistky: text, který se vejde, se nesmí zmenšovat „pro jistotu" -
        // jinak by se celá aplikace tiskla menším písmem, než co je v šablonách nastavené.
        // Měří se výška vykreslených glyfů: ve vysokém prvku se text vejde v plné velikosti,
        // takže jsou vyšší než v nízkém, kde se písmo zmenšilo.
        var data = new DataContext(new Dictionary<string, object?>());

        int vysokyGlyf = VyskaPrvnihoRadku(Prvek(Dlouhy, 60f), data);
        int nizkyGlyf  = VyskaPrvnihoRadku(Prvek(Dlouhy, 8f), data);

        Assert.Equal(1f, Prvek(Dlouhy, 60f).ComputeShrinkScale(Dlouhy));
        Assert.True(vysokyGlyf > nizkyGlyf,
            $"glyfy: vysoký prvek {vysokyGlyf} px, nízký {nizkyGlyf} px - písmo se zmenšilo i tam, kde nemuselo");
    }

    /// <summary>Počet vykreslených řádků textu - souvislá pásma obrazových řádků s černým pixelem.</summary>
    private static int VykreslenychRadku(TextElement prvek, DataContext data) =>
        Pasma(prvek, data).Count;

    /// <summary>Výška prvního vykresleného řádku v px - roste s velikostí písma.</summary>
    private static int VyskaPrvnihoRadku(TextElement prvek, DataContext data)
    {
        var pasma = Pasma(prvek, data);
        return pasma.Count > 0 ? pasma[0] : 0;
    }

    /// <summary>Výšky souvislých pásem obarvených obrazových řádků = jednotlivé řádky textu.</summary>
    private static List<int> Pasma(TextElement prvek, DataContext data)
    {
        const float mmToPx = 8f;
        int w = (int)(prvek.Width * mmToPx), h = (int)((prvek.Height ?? 8f) * mmToPx);
        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, mmToPx, data));
        }

        var pasma = new List<int>();
        int aktualni = 0;
        for (int y = 0; y < h; y++)
        {
            bool cerny = false;
            for (int x = 0; x < w; x++)
                if (bitmap.GetPixel(x, y).Red < 128) { cerny = true; break; }

            if (cerny) aktualni++;
            else if (aktualni > 0) { pasma.Add(aktualni); aktualni = 0; }
        }
        if (aktualni > 0) pasma.Add(aktualni);
        return pasma;
    }

    private static Template Sablona(TemplateElement el)
    {
        var t = new Template();
        t.Sections.Clear();
        var section = new Section { Name = "Obsah", DesignerHeight = 50 };
        section.Elements.Add(el);
        t.Sections.Add(section);
        return t;
    }
}
