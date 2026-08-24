namespace TemplateDesigner.Models;

/// <summary>Jak se značka jízdního řádu kreslí.</summary>
public enum SymbolRenderMode
{
    /// <summary>Piktogram ze symbolového Timetable fontu.</summary>
    FontGlyf,

    /// <summary>Běžný text v písmu prvku - buď je to konvence („x" = na znamení), nebo značka
    /// v symbolovém fontu prostě není.</summary>
    Text,
}

/// <summary>
/// Jediný zdroj pravdy „značka → jak ji nakreslit". Sdílí ho zastávkové i linkové jízdní řády,
/// aby se táž značka nekreslila v jedné sestavě jinak než v druhé.
/// </summary>
/// <remarks>
/// <para><b>Proč mapa a ne prosté „pošli znak do fontu".</b> Pozice glyfů v Timetable fontu se
/// <b>neshodují s písmeny číselníku</b>. Naivní překlad vytiskne „na znamení" jako <b>vláček</b>
/// (na pozici <c>x</c> je lokomotiva), autobus jako vagon a podobně. Ověřeno vytažením obrysů
/// všech 94 tisknutelných pozic z <c>Fonts/TimeTable2.ttf</c>; postup a tabulka pozic jsou
/// v <c>.claude/docs/elementy-architektura.md</c>.</para>
/// <para><b>Slovenština:</b> CZ a SK font se liší přesně ve <b>3 z 94</b> pozic (<c>D</c>, <c>w</c>,
/// <c>x</c>), takže se tu jazyk neřeší - řeší ho výběr rodiny fontu
/// (<c>FontResolver.SymbolFontFamily</c>).</para>
/// <para><b>Vědomě mimo:</b> složené značky (piktogram + číslo) - jsou málo časté a přidají se,
/// až se objeví v datech (rozhodnutí Jan 24. 8.).</para>
/// </remarks>
public static class SymbolMap
{
    /// <summary>Značky, které se dle konvence JŘ tisknou jako <b>prostý text</b>, i když jsou
    /// jednoznakové - „na znamení" a jeho časové varianty. Bez toho by vyjel vláček.</summary>
    private static bool JeKonvencneText(string znak) => znak is "x" or "y" or "z";

    /// <summary>Překlad značky číselníku na pozici v symbolovém fontu. Co tu není, se kreslí
    /// beze změny - u bezbariéru (<c>H</c>/<c>F</c>) a kroužkových dnů to sedí.</summary>
    /// <remarks>
    /// Pozice ověřené 2026-08-03 vytažením obrysů glyfů: <c>w</c> = železniční vagon ·
    /// <c>x</c> = lokomotiva · <c>z</c> = autobus · <c>L</c> = jízdní kolo · <c>y</c> = restaurace.
    /// </remarks>
    private static string NaPoziciVeFontu(string znak) => znak switch
    {
        "A" => "z",   // autobus (0x7A) - na 0x77 je vagon, ne autobus
        "L" => "x",   // vlak - lokomotiva (0x78)
        "K" => "L",   // přeprava jízdních kol (0x4C); „K" je náš marker, číselník pro kola znak nemá
        _ => znak,
    };

    /// <summary>Značky, které v symbolovém fontu <b>nejsou</b> - vypíšou se textem.</summary>
    /// <remarks>
    /// ⚠️ <b>Provizorium</b> (rozhodnutí Jan 24. 8.): informace se aspoň neztratí, ale v tisku
    /// vypadá jinak než zbytek značek. Pořádné řešení (doplnit do fontu, nebo vlastní piktogramy)
    /// je otevřené - viz backlog.
    /// </remarks>
    private static bool ChybiVeFontu(string znak) => znak is "lod" or "wifi" or "zasu";

    /// <summary>Jak nakreslit tuhle značku a co konkrétně vypsat.</summary>
    public static (SymbolRenderMode Mode, string Value) Resolve(string znak)
    {
        if (string.IsNullOrEmpty(znak)) return (SymbolRenderMode.Text, znak ?? "");
        if (ChybiVeFontu(znak)) return (SymbolRenderMode.Text, znak);
        if (JeKonvencneText(znak)) return (SymbolRenderMode.Text, znak);
        // Víceznakové značky (MHD, BEV, IDS, WC…) jsou zkratky, ne piktogramy.
        if (znak.Length != 1) return (SymbolRenderMode.Text, znak);
        return (SymbolRenderMode.FontGlyf, NaPoziciVeFontu(znak));
    }

    /// <summary>Značky <b>linkového</b> JŘ, u kterých je pozice ve fontu ověřená měřením.</summary>
    /// <remarks>
    /// ⚠️ Tady se schválně jede na <b>povolený seznam</b>, ne na pravidlo „jednoznakové = piktogram",
    /// které platí u vývěsek. Slovník linkového JŘ je jiný a hned první pokus by šlápl vedle:
    /// „pokračuje" má v číselníku značku <c>p</c>, jenže na <c>p</c> je ve fontu <b>kufr</b>.
    /// Denní značky naopak sedí a v jízdních řádech se tak opravdu tisknou: <c>X</c> = zkřížená
    /// kladiva (pracovní dny), <c>6</c>/<c>7</c> = kroužkové číslice, <c>+</c> = kříž (svátky).
    /// <para>Co v seznamu není, se kreslí <b>textem</b> - tedy jako dosud. Týká se to i značek
    /// <c>|</c> (projíždí) a <c>&lt;</c> (jede jinudy), které jsou typografická konvence, ne
    /// piktogram, a značek <c>m</c>/<c>u</c>/<c>^</c> (návaznosti), jejichž pozice <b>měřená
    /// není</b>. Doplnit je smí až někdo, kdo je ověří v <b>obou</b> fontech - postup
    /// v <c>elementy-architektura.md</c>. Hádat je znamená vytisknout cizí piktogram.</para>
    /// </remarks>
    private static bool JeOverenyPiktogramLinky(string znak) =>
        znak is "X" or "+" or "H" or "F" or "L" or "A" or "K"
        || (znak.Length == 1 && znak[0] >= '0' && znak[0] <= '9');

    /// <summary>Jak nakreslit značku v <b>linkovém</b> JŘ - viz <see cref="JeOverenyPiktogramLinky"/>,
    /// proč se tu nepoužívá totéž pravidlo jako u vývěsek.</summary>
    public static (SymbolRenderMode Mode, string Value) ResolveLinkovy(string znak)
    {
        if (string.IsNullOrEmpty(znak)) return (SymbolRenderMode.Text, znak ?? "");
        if (!JeOverenyPiktogramLinky(znak)) return (SymbolRenderMode.Text, znak);
        return (SymbolRenderMode.FontGlyf, NaPoziciVeFontu(znak));
    }

    /// <summary>Kreslí se značka piktogramem ze symbolového fontu?</summary>
    public static bool JePiktogram(string znak) => Resolve(znak).Mode == SymbolRenderMode.FontGlyf;

    /// <summary>Co se má vypsat - přeložená pozice u piktogramu, jinak značka beze změny.</summary>
    public static string Glyf(string znak) => Resolve(znak).Value;
}
