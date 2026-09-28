namespace TemplateDesigner.Models.Layout;

/// <summary>Sekce položená na listu návrháře (Y v mm od horní hrany listu).</summary>
/// <param name="Kopie">Hlavička, patička nebo „pod obsahem" nakreslená na dalším listu - jen drží
/// místo, upravuje se na listu, kde je poprvé.</param>
public sealed record SekceNaListu(Section Section, float YMm, bool Kopie);

/// <summary>Jeden list návrháře = jedna stránka dokladu.</summary>
/// <param name="Nazev">Jméno první obsahové sekce listu - u listu, který začíná novou stránkou,
/// je to jméno stránky („Rub").</param>
/// <param name="VyskaMm">Výška listu i s přesahem; nikdy méně než výška stránky.</param>
public sealed record ListNavrhu(int Index, string? Nazev, float VyskaMm, IReadOnlyList<SekceNaListu> Sekce)
{
    /// <summary>Kolik mm obsahu se na stránku nevejde (0 = vejde se).</summary>
    public float PresahMm(PageSize ps) => MathF.Max(0f, VyskaMm - ps.Height);
}

/// <summary>
/// Rozdělení šablony na stránky, jak je ukazuje návrhář (`B184`): nová stránka začíná sekcí se
/// „Začít na nové stránce", hlavička, patička a „pod obsahem" se opakují na každé stránce, kde
/// se tisknou.
/// </summary>
/// <remarks>
/// <para>Je to **předpis dokladu**, ne vysázený výsledek: obsah vyšší než stránka se tu neláme,
/// jen se vykáže jako přesah. Kolik fyzických stránek vyjde, ukáže náhled s daty.</para>
/// <para>⚠️ Stránkami se řídí i počítání místa pro sekce (<see cref="SectionLayoutMath"/>) - sekce
/// na rubu nesmí brát místo sekcím na líci. Dokud návrhář stránky neznal, přidání sekce kamkoli
/// zkrátilo celou šablonu, jako by byla jedna stránka.</para>
/// </remarks>
public static class DesignerPages
{
    /// <summary>Opakuje se sekce na stránkách (hlavička, patička, pod obsahem)? Jinak je to obsah.</summary>
    public static bool JeOpakovana(Section s) => s.Typ != TypSekce.Obsah;

    /// <summary>Obsahové sekce rozdělené po stránkách. Vždy aspoň jedna stránka (i prázdná).</summary>
    public static List<List<Section>> ObsahStranek(Template template)
    {
        var stranky = new List<List<Section>> { new() };
        foreach (var s in template.Sections)
        {
            if (JeOpakovana(s)) continue;
            if (s.ZacitNaNoveStrance && stranky[^1].Count > 0) stranky.Add([]);
            stranky[^1].Add(s);
        }
        return stranky;
    }

    /// <summary>
    /// Vyjde z předlohy víc stránek podle dat? Ano, když na ní je sekce s výškou podle obsahu,
    /// sekce, která se smí rozdělit přes stránky, nebo sekce opakovaná po řádcích dat. Jinak je to
    /// pevná stránka (faktura, stazka) - návrhář to odvozuje, uživatel nic nenastavuje.
    /// </summary>
    public static bool JePodleDat(IEnumerable<Section> obsahPredlohy) => obsahPredlohy.Any(s =>
        s.DesignerHeight is null
        || s.OverflowBehavior == SectionOverflowBehavior.SplitAcrossPages
        || s.PrintBehavior == SectionPrintBehavior.PerDataRow);

    /// <summary>
    /// Ze které stránky šablony vznikla každá vysázená stránka - podle první obsahové sekce na ní;
    /// stránka jen s hlavičkou a patičkou patří ke stránce šablony předchozí stránky.
    /// </summary>
    public static List<int> StrankySablony(Template template, IReadOnlyList<Page> stranky)
    {
        var vysledek = new List<int>(stranky.Count);
        int predloha = 0;
        foreach (var stranka in stranky)
        {
            if (stranka.Sections.FirstOrDefault(s => !JeOpakovana(s.Source)) is { } obsah)
                predloha = IndexStranky(template, obsah.Source);
            vysledek.Add(predloha);
        }
        return vysledek;
    }

    /// <summary>Tiskne se opakovaná sekce na stránce <paramref name="index"/> z <paramref name="pocet"/>?</summary>
    public static bool TiskneSeNaStrance(Section s, int index, int pocet) => s.Stranky switch
    {
        StrankySekce.JenPrvni    => index == 0,
        StrankySekce.JenPosledni => index == pocet - 1,
        _                        => true,
    };

    /// <summary>
    /// Sekce, které sdílejí tiskovou plochu stránky se sekcí <paramref name="section"/>: obsah její
    /// stránky a opakované sekce, které se na ní tisknou. U opakované sekce je to stránka, kde se
    /// tiskne poprvé - tam se upravuje.
    /// </summary>
    public static List<Section> SekceStranky(Template template, Section section)
    {
        var obsah = ObsahStranek(template);
        int index = JeOpakovana(section)
            ? Enumerable.Range(0, obsah.Count).FirstOrDefault(i => TiskneSeNaStrance(section, i, obsah.Count))
            : Math.Max(0, obsah.FindIndex(p => p.Contains(section)));
        return SekceStrankyPodleIndexu(template, obsah, index);
    }

    /// <summary>Sekce poslední stránky.</summary>
    public static List<Section> SekcePosledniStranky(Template template)
    {
        var obsah = ObsahStranek(template);
        return SekceStrankyPodleIndexu(template, obsah, obsah.Count - 1);
    }

    /// <summary>Sekce stránky podle jejího pořadí (0 = první); mimo rozsah = poslední.</summary>
    public static List<Section> SekceStranky(Template template, int index)
    {
        var obsah = ObsahStranek(template);
        return SekceStrankyPodleIndexu(template, obsah, Math.Clamp(index, 0, obsah.Count - 1));
    }

    /// <summary>
    /// Pořadí stránky, na které se sekce upravuje: obsahová sekce = její stránka, opakovaná =
    /// první stránka, kde se tiskne. Bez sekce (nic není vybrané) poslední stránka.
    /// </summary>
    public static int IndexStranky(Template template, Section? section)
    {
        var obsah = ObsahStranek(template);
        if (section is null) return obsah.Count - 1;
        if (JeOpakovana(section))
            return Enumerable.Range(0, obsah.Count).FirstOrDefault(i => TiskneSeNaStrance(section, i, obsah.Count));
        int i = obsah.FindIndex(p => p.Contains(section));
        return i < 0 ? obsah.Count - 1 : i;
    }

    /// <summary>
    /// Kam do šablony vložit novou obsahovou sekci, aby byla na konci stránky
    /// <paramref name="index"/>: za poslední obsahovou sekci té stránky. Prázdná šablona = na konec.
    /// </summary>
    public static int PoziceNaKonciStranky(Template template, int index)
    {
        var obsah = ObsahStranek(template);
        var stranka = obsah[Math.Clamp(index, 0, obsah.Count - 1)];
        return stranka.Count == 0 ? template.Sections.Count : template.Sections.IndexOf(stranka[^1]) + 1;
    }

    private static List<Section> SekceStrankyPodleIndexu(Template template, List<List<Section>> obsah, int index) =>
        [.. template.Sections.Where(s => JeOpakovana(s)
            ? TiskneSeNaStrance(s, index, obsah.Count)
            : obsah[index].Contains(s))];

    /// <summary>
    /// Listy návrháře: nahoře hlavičky, pak obsah stránky a „pod obsahem", patičky u spodní hrany
    /// listu. Každá sekce je upravitelná právě jednou; na dalších listech je kopie.
    /// </summary>
    public static List<ListNavrhu> Rozloz(Template template)
    {
        var ps = template.PageSize;
        var obsah = ObsahStranek(template);
        int pocet = obsah.Count;
        var upravitelna = new HashSet<Section>();
        var listy = new List<ListNavrhu>();

        for (int i = 0; i < pocet; i++)
        {
            var sekce = new List<SekceNaListu>();
            float y = ps.MarginTop;

            void Poloz(Section s)
            {
                sekce.Add(new SekceNaListu(s, y, Kopie: !upravitelna.Add(s)));
                y += s.EffectiveDesignerHeight;
            }

            foreach (var s in template.Sections)
                if (s.Typ == TypSekce.HlavickaStranky && TiskneSeNaStrance(s, i, pocet)) Poloz(s);
            foreach (var s in obsah[i]) Poloz(s);
            foreach (var s in template.Sections)
                if (s.Typ == TypSekce.PodObsahemStranky && TiskneSeNaStrance(s, i, pocet)) Poloz(s);

            var paticky = template.Sections
                .Where(s => s.Typ == TypSekce.PatickaStranky && TiskneSeNaStrance(s, i, pocet)).ToList();
            float patickyMm = paticky.Sum(s => s.EffectiveDesignerHeight);
            float vyska = MathF.Max(ps.Height, y + patickyMm + ps.MarginBottom);
            // Patička drží u spodní hrany listu; když se obsah nevejde, jde až za něj (do přesahu).
            y = vyska - ps.MarginBottom - patickyMm;
            foreach (var s in paticky) Poloz(s);

            listy.Add(new ListNavrhu(i, obsah[i].FirstOrDefault()?.Name, vyska, sekce));
        }
        return listy;
    }
}
