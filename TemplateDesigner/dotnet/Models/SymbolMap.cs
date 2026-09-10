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

    /// <summary>Značky, které se ve <b>vývěsce</b> vypisují textem.</summary>
    /// <remarks>
    /// ⚠️ <b>Ve fontu přitom všechny tři jsou</b> - shodně na 180 (loď), 186 (wifi) a 179 (zásuvka);
    /// linkový JŘ je od 31. 8. kreslí piktogramem podle překladové tabulky legacy.
    /// <para><b>Vývěska je jiný případ:</b> její starý modul
    /// (<c>ALVA.TISKMHD.Main.ConvertSpecialniZnak</c>) zná jen dny, vozíček a částečnou
    /// bezbariérovost - loď, wifi ani zásuvku <b>vůbec nemá</b>. Přepnout je tu na piktogram by
    /// tedy nebyla parita se starým tiskem, ale nová featura nad jeho rámec. Proto zůstávají
    /// textem, dokud si o to někdo neřekne.</para>
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
    /// <para>Seznam rozhoduje <b>o každé značce zvlášť</b>, ne o celé buňce: „⚒ ♿" vyjde
    /// kladivem a vozíčkem vedle sebe. Kreslí to <c>SymbolRunRenderer</c>.</para>
    /// <para>⚠️ <b>Víceznakový kód není sám o sobě zkratka.</b> Do 27. 8. tu platilo, že piktogram
    /// je jen jednoznakový, a <c>BEV</c> se proto tisklo textem - jenže starý modul má celou tuhle
    /// rodinu (BEV, IDS, NKA, OBC, PCZ, PJI, POS, RBU, SAO, CBE, RBP) v číselníku znaků jako
    /// <b>symboly</b> (<c>JRapiLinPrtWin.mac:1556</c> a dál). Rozhoduje tedy jen tenhle seznam
    /// a to, jestli je pozice ve fontu ověřená.</para>
    /// <para>Co v seznamu není, se kreslí <b>textem</b>. „Pokračuje" <c>u</c> zůstává textem,
    /// dokud se nenajde stará sestava, kde se vyskytuje - v jádru sady není. ⚠️ Do 10. 9. tu
    /// stálo, že <c>|</c> a <c>&lt;</c> nebudou piktogramem nikdy, protože „font je nemá" -
    /// hledala se pozice těch znaků samotných (60 a 124, kde je opravdu jen osmiúhelník a čtverec)
    /// místo v překladové tabulce, kde jsou pod id 1 a 2. Viz <see cref="PruchodZastavkou"/>.</para>
    /// <para><b>Kde se pozice bere:</b> starý tiskový klient má překladovou tabulku
    /// <c>ALVA.TISKJR.Main.ConvertSpecialniZnak</c> (<c>C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Main.cls</c>) -
    /// id z číselníku <c>$$$SALL</c> → kód znaku ve fontu. Záporná hodnota tam znamená text nebo
    /// složenou značku, kterou si klient kreslí sám. ⚠️ Do 31. 8. se o té tabulce myslelo, že v repu
    /// není, a pozice se odvozovaly z významu; proto tu zbytek rodiny zůstával textem. <b>Tabulka je
    /// autorita</b> - nová položka se z ní opíše, nehádá.</para>
    /// <para><b>Textem zůstávají i podle legacy:</b> <c>IDS</c> (id 23 → „-11") a <c>NKA</c>
    /// (id 24 → „-12"). Nejsou to nedodělky - starý modul je taky netiskne obrázkem.</para>
    /// <para>Pozici i tak ověř vykreslením v <b>obou</b> fontech (postup v
    /// <c>elementy-architektura.md</c>) - tabulka říká kód, ne to, jestli na něm něco je.</para>
    /// </remarks>
    private static readonly Dictionary<string, string> PiktogramyLinky = new()
    {
        // Denní značky - pozice sedí na písmenu.
        ["X"] = "X",   // pracovní dny - zkřížená kladiva
        ["+"] = "+",   // svátky - kříž
        // Návaznosti: doloženo tiskem staré linky 330511 (24. 8.) - „vyčká" má **dutý** trojúhelník,
        // „navazuje" **plný**. Který je který, se poznalo počtem obrysů glyfu: `A` má dva (dutý),
        // `B` jeden (plný). V CZ i SK fontu shodně.
        ["m"] = "A",   // vyčká - dutý trojúhelník △
        ["^"] = "B",   // navazuje - plný trojúhelník ▲
        // Bezbariérovost spoje. Data ji posílají **víceznakovým** kódem, ne písmenem, a starý
        // modul ji má v číselníku znaků jako symbol (id 22 „BEV bezbarierovy", id 56 „CBE castecne
        // bezbarierovy vuz" - JRapiLinPrtWin.mac:1580 a :1596), takže se má tisknout piktogramem,
        // ne zkratkou. Pozice ověřená vykreslením tabulky glyfů v **obou** fontech: 72 je vozíček,
        // 70 vozíček s doprovodem.
        ["BEV"] = "H",   // bezbariérové vozidlo - vozíček
        ["CBE"] = "F",   // částečně bezbariérové vozidlo - vozíček s doprovodem
        // Atributy sdílené s vývěskou (písmenné kódy téhož).
        ["H"] = "H",   // bezbariér
        ["F"] = "F",   // částečně bezbariér
        ["L"] = "x",   // vlak - lokomotiva
        // ⚠️ Legacy má u autobusu 119 (`w`), my 122 (`z`) - a je to tak správně. Starý klient sází
        // fontem **„Timetable"** (`formTISKJRReportDotNet_Module.vb:2611`), my TimeTable2, a přesně
        // na 119 se ty dvě sady rozcházejí: v TimeTable2 CZ je tam vagon, v SK autobus (`B49`).
        // Na 122 je autobus v obou, takže `z` je jediná volba, která platí pro oba jazyky.
        // Do 31. 8. to viselo jako nerozhodnutý rozpor s legacy - není, jen jiný font.
        ["A"] = "z",   // autobus
        ["K"] = "L",   // jízdní kolo
        // Rodina atributů spoje. Pozice **nejsou odhad**: jsou opsané z překladové tabulky starého
        // tiskového klienta `ALVA.TISKJR.Main.ConvertSpecialniZnak`
        // (`C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Main.cls`), která překládá id z číselníku
        // `$$$SALL` (`JRapiLinPrtWin.mac:14`) na kód znaku ve fontu. V závorce je id a kód.
        ["PJI"]  = "L",   // kola (27 → 76) - totéž jízdní kolo jako `K`
        ["PCZ"]  = "p",   // zavazadla (26 → 112) - kufr; proto tam „pokračuje" být nesmí
        ["OBC"]  = "M",   // občerstvení (25 → 77) - příbor
        ["RBU"]  = "Z",   // radiobus, spoj jede na objednání (29 → 90) - telefon
        ["SAO"]  = "s",   // samoobslužný (30 → 115) - kolečko s „S"
        ["RBP"]  = "ð",   // jede podmínečně (57 → 240) - vykřičník
        ["POS"]  = "¨",   // zásilky (28 → 168)
        ["lod"]  = "´",   // loď (61 → 180)
        ["wifi"] = "º",   // wifi (65 → 186)
        ["zasu"] = "³",   // zásuvka 230 V (66 → 179)
    };

    /// <summary>
    /// Značky, které se v linkovém JŘ tisknou <b>jiným textem</b>, než jaký přijde v datech.
    /// </summary>
    /// <remarks>
    /// Nízkokapacitní vůz má v překladové tabulce legacy zápornou hodnotu (id 24 → „-12"), což
    /// u starého klienta znamená „vypiš text" - a ten text je <b>„n"</b>, ne „NKA"
    /// (<c>formTISKJRReportDotNet_Module.vb:3358</c>). IDS (id 23 → „-11") je na tom stejně, jen
    /// se u něj text se zkratkou shoduje, takže tady nemá řádek.
    /// </remarks>
    private static readonly Dictionary<string, string> ZkratkyLinkyJinymTextem = new()
    {
        ["NKA"] = "n",   // nízkokapacitní vůz (24 → „-12")
    };

    /// <summary>
    /// Značky trasy zastávky (číselník 16) - co se z atributů zastávky vůbec kreslí.
    /// </summary>
    /// <remarks>
    /// ⚠️ Doslovný opis legacy makra <c>STRASALINKYZNACKY</c> (<c>JRapiLinPrtWin.mac:17</c>).
    /// Zbytek číselníku jsou <b>řídicí</b> atributy (<c>PTpoS&gt;&lt;JE</c>, makro
    /// <c>STRASALINKYATRIBUTY</c>): mění vzhled řádku, ale samy se netisknou - <c>T</c> je „sázej
    /// tučně", <c>S</c> „vypiš stanoviště", <c>P</c>/<c>J</c> „rozděl řádek".
    /// </remarks>
    private const string ZnackyZastavky = "NVxyzALefgOCGabcDM";

    /// <summary>Kreslí se tahle značka trasy zastávky vůbec? Řídicí atributy ne.</summary>
    public static bool JeZnackaZastavky(string znak) =>
        znak.Length == 1 && ZnackyZastavky.Contains(znak[0]);

    /// <summary>Pozice piktogramů <b>atributů zastávky</b> ve symbolovém fontu.</summary>
    /// <remarks>
    /// ⚠️ <b>Vlastní mapa, ne <see cref="PiktogramyLinky"/></b> (`B123`). Atributy zastávky
    /// a atributy spoje jsou dva různé číselníky, které sdílejí písmena: <c>H</c> je u zastávky
    /// „cílová pro tablo", u spoje bezbariérové vozidlo - se společnou mapou by se zastávka
    /// kreslila vozíčkem. V datech se <c>H</c> nevyskytuje (census 10. 9.), takže to nic
    /// neopravuje, ale past to byla.
    /// <para>Pozice opsané z <c>ALVA.TISKJR.Main.ConvertSpecialniZnak</c> a <b>ověřené obrysem
    /// glyfu v obou fontech</b> (10. 9.) - čísla v té tabulce jsou pozice ve starém fontu
    /// Timetable, my sázíme TimeTable2. „Jen nástup" a „jen výstup" mají shodné obrysy
    /// (9 bodů, 6 tahů), což u dvojice šipek nahoru/dolů sedí.</para>
    /// <para>Textem zůstávají <c>x</c>/<c>y</c>/<c>z</c> (konvence JŘ), <c>C</c> clo a složené
    /// značky, které si starý klient kreslí vlastní větví: metro s indexem (<c>e</c>/<c>f</c>/
    /// <c>g</c> → -16/-17/-18) a paragrafy s indexem (<c>a</c>/<c>b</c>/<c>c</c> → -1/-2/-3).
    /// Sloučené metro <c>M</c> piktogram má (60 → 238) a používá ho číselník zastávek.</para>
    /// </remarks>
    private static readonly Dictionary<string, string> PiktogramyZastavky = new()
    {
        ["N"] = "m",   // jen nástup (31 → 109)
        ["V"] = "l",   // jen výstup (32 → 108)
        ["A"] = "z",   // autobus - naše pozice, ne legacy 119; viz PiktogramyLinky
        ["L"] = "x",   // vlak - lokomotiva
        ["O"] = "ö",   // letadlo (53 → 246)
        ["D"] = "´",   // loď (61 → 180)
        ["M"] = "î",   // metro sloučené (60 → 238)
        ["G"] = "P",   // paragraf (37 → 80)
    };

    /// <summary>Jak nakreslit značku trasy <b>zastávky</b> - viz <see cref="PiktogramyZastavky"/>.</summary>
    public static (SymbolRenderMode Mode, string Value) ResolveZastavka(string znak)
    {
        if (string.IsNullOrEmpty(znak)) return (SymbolRenderMode.Text, znak ?? "");
        if (PiktogramyZastavky.TryGetValue(znak, out var poz)) return (SymbolRenderMode.FontGlyf, poz);
        return (SymbolRenderMode.Text, znak);
    }

    /// <summary>
    /// Průchod zastávkou bez zastavení - „projíždí" a „jede jinudy".
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Nejsou to znaky, ale piktogramy.</b> Do 10. 9. se tiskly doslova jako <c>|</c>
    /// a <c>&lt;</c>, protože se pozice hledala u těch znaků samotných. Windowsový klient je má
    /// v překladové tabulce pod id 1 a 2 (<c>ConvertSpecialniZnak</c> → 74 a 75) a postskriptová
    /// varianta pod pozicemi 7 a 8 svého fontu (<c>JRapiLinPrtPS.mac:62</c>); doslovné
    /// <c>|</c>/<c>&lt;</c> používá jen <b>HTML</b> výstup téhož tisku
    /// (<c>JRapiLinHtm0.mac:43</c>), tedy varianta, které jsme se nechtěně podobali.
    /// <para>Ověřeno obrysem glyfu v obou fontech 10. 9.: 74 je 3,9 × 52,1 (svislice přes výšku
    /// řádku), 75 je 15,7 × 51,8 (vlnovka). Obojí shodné v CZ i SK.</para>
    /// </remarks>
    private static readonly Dictionary<string, string> PruchodyZastavkou = new()
    {
        ["|"] = "J",   // projíždí (id 1 → 74) - svislice
        ["<"] = "K",   // jede jinudy (id 2 → 75) - vlnovka
    };

    /// <summary>Pozice piktogramu pro průchod zastávkou, nebo null když to průchod není.</summary>
    public static string? PruchodZastavkou(string? hodnota) =>
        hodnota is not null && PruchodyZastavkou.TryGetValue(hodnota, out var poz) ? poz : null;

    private static bool JeOverenyPiktogramLinky(string znak) =>
        PiktogramyLinky.ContainsKey(znak)
        || (znak.Length == 1 && znak[0] >= '0' && znak[0] <= '9');

    /// <summary>Jak nakreslit značku v <b>linkovém</b> JŘ - viz <see cref="JeOverenyPiktogramLinky"/>,
    /// proč se tu nepoužívá totéž pravidlo jako u vývěsek.</summary>
    public static (SymbolRenderMode Mode, string Value) ResolveLinkovy(string znak)
    {
        if (string.IsNullOrEmpty(znak)) return (SymbolRenderMode.Text, znak ?? "");
        if (ZkratkyLinkyJinymTextem.TryGetValue(znak, out var text)) return (SymbolRenderMode.Text, text);
        if (!JeOverenyPiktogramLinky(znak)) return (SymbolRenderMode.Text, znak);
        // ⚠️ Cíl se bere z tabulky, **ne** přes `NaPoziciVeFontu`: značka „vyčká" míří na pozici
        // `A`, kterou by vývěskový překlad poslal dál na autobus.
        return (SymbolRenderMode.FontGlyf, PiktogramyLinky.TryGetValue(znak, out var poz) ? poz : znak);
    }

    /// <summary>Kreslí se značka piktogramem ze symbolového fontu?</summary>
    public static bool JePiktogram(string znak) => Resolve(znak).Mode == SymbolRenderMode.FontGlyf;

    /// <summary>Co se má vypsat - přeložená pozice u piktogramu, jinak značka beze změny.</summary>
    public static string Glyf(string znak) => Resolve(znak).Value;
}
