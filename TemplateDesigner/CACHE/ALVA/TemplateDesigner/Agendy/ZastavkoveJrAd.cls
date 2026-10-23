Include GENERAL

/// Agenda: **Zastávkový JŘ z dat autobusové dopravy (AD)** = hlavní JŘ modul / meziměstská
/// + příměstská doprava (legacy JRMAIN). Sourozenec `ZastavkoveJrMhd` - stejný výstup
/// (zastávková vývěska: StopList + DepartureGrid + NotesLegend), jiný datový zdroj.
///
/// Datový zdroj (legacy globály, viz TD `ad-data-mapa.md`):
/// - `^[dataDB]JRL(sku,lin,...)` - linka, trasa ("J"), spoje ("T"/"Z"), trasy spojů ("W"), PATCH
/// - `^[dataDB]JRZ(czst,...)`    - zastávky (názvy, zóny, GPS, časové varianty)
/// - `^[dataDB]JRSET(...)`       - číselníky (zóny, NZ, nadpisy sloupců)
///
/// Reimplementace legacy větve `ALVA.TISKMHD.Data.LoadData` s `VolanoZ=JRMAIN`
/// (→ `WinTiskMhd^JRapiExp10`). Legacy výstup lze vygenerovat jako zlatou referenci přes
/// `cache_query ALVA.TISKMHD.Data/LoadData` (viz `ad-data-mapa.md` §9).
///
/// **Stav:** GetData fázový engine hotový - trasa, spoje, FTL sloupce (skupiny dnů), odjezdy,
/// atributy zastávek, datumová omezení (jede/nejede), dopravce, titulek/informace, **rozdíly tras**
/// (1:1 `OmTrasa^JRapiLinPrtMhd`: "neobsluhuje zast. X" / "jede přes…" / "pokračuje…", marker od "B").
/// Ověřeno 1:1 proti zlaté referenci (0365001: marker "B" = "neobsluhuje zast. Lesní hřbitov").
/// **Zbývá pro úplné 1:1:** atributy spoje (např. bezbariér "72") na minutách + legendě, adresa
/// provozovny v POPIS_DOPRAVCE, časy jízdy ve StopListu (TrasaMin), ověřit filtr atributů zastávek
/// A/L. Detail `ad-data-mapa.md` §10.
///
/// Sdílené Build*/AddStandard* helpery jsou zatím **duplikované** z `ZastavkoveJrMhd`
/// (stejný výstupní formát). Extrakce do společného helperu/baseclass až podle výsledků
/// (ne předem) - viz dohoda v projektových poznámkách.
Class ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaVariantCode = "ZASTAVKOVY_JR_AD";

Parameter Agenda = "ZASTAVKOVY_JR";

Parameter AgendaLabel = "Zastávkový JŘ";

Parameter Variant = "AD";

Parameter VariantLabel = "AD (linková doprava)";

Parameter Released = 1;

Parameter InstanceParamName = "LINKA_ID";

Parameter ElementKind = "STOP";

Parameter InstanceLabel = "linka";

Parameter InstanceLabelPlural = "linky";

/// Vrátí DataSchema - shodné se zastávkovým JŘ MHD (stejný výstupní formát).
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    s def = ##class(%DynamicObject).%New()

    s fields = ##class(%DynamicArray).%New()
    d ..AddStandardJRFields(fields)
    s def.fields = fields

    s collections = ##class(%DynamicArray).%New()
    d ..AddStandardJRCollections(collections)
    s def.fieldCollections = collections

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    ret $$$OK
}

/// Vrátí seznam Fixed šablon tabulek.
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    s t1 = ##class(%DynamicObject).%New()
    s t1.key   = "ZAHLAVI_LINKY"
    s t1.label = "Záhlaví linky"
    d arr.%Push(t1)

    s t2 = ##class(%DynamicObject).%New()
    s t2.key   = "PLATNOST_DOPRAVCE"
    s t2.label = "Platnost a dopravce"
    d arr.%Push(t2)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    ret $$$OK
}

/// Vrátí definici Fixed šablony.
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s key = $g(^||ML("IN", "key"))

    if key = "ZAHLAVI_LINKY" {
        d ..WriteTemplate2Col([
            ["Linka",       "{CISLO_LINKY}"],
            ["Označení",    "{LOKALNI_OZNACENI_LINKY}"],
            ["Směr",        "{KONECNA_ZASTAVKA}"],
            ["Zastávka",    "{POPIS_ZASTAVKA}"],
            ["Platnost",    "Platí od {DATUM_PLATNOSTI_OD} do {DATUM_PLATNOSTI_DO}"]
        ])
    }
    elseif key = "PLATNOST_DOPRAVCE" {
        d ..WriteTemplate2Col([
            ["Platnost",  "Platí od {DATUM_PLATNOSTI_OD} do {DATUM_PLATNOSTI_DO}"],
            ["Dopravce",  "{POPIS_DOPRAVCE}"],
            ["Typ JR",    "{TYP_JR}"]
        ])
    }

    ret $$$OK
}

/// Vrátí parametry sestavy. Instance AD je určená skupinou linek + linkou (^JRL(sku,lin)).
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    // FillParams zde netřeba - AddStandardJRParameters IdFa/dataDB nečte (viz konvence GetParameters).
    s arr = ##class(%DynamicArray).%New()

    // Linka se vybírá v Instance Pickeru (ne jako parametr sestavy) - viz GetAvailableInstances.
    // SKUPINA_LINEK_ID je zatím fixní (default = první skupina v ^JRL, shodně s pickerem i GetData);
    // až bude Návrhář spouštěn z JŘ/MHD, pošle se skupina (příp. výchozí linka) jako launch kontext.
    d ..AddStandardJRParameters(arr)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    ret $$$OK
}

/// Vrátí seznam linek pro Instance Picker (umožní proklikávání stránek v preview).
/// IN["search"] = substring filtr (case-insensitive), IN["limit"] = max (default 50),
/// Skupiny linek pro filtr Instance Pickeru - první subscript ^JRL(sku). Název skupiny je 1. `~`-piece
/// hodnoty ^JRL(sku) (např. "Vertikála k 1.1. - ŘÁDNÉ~01.01.2024~..."). OUT["1"] = [{ value, label }].
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s arr = ##class(%DynamicArray).%New()
        s sku = ""
        f {
            s sku = $o(^[dataDB]JRL(sku))
            q:sku=""
            i 'sku continue                  // přeskoč nenumerický / konfig uzel

            s nazev = $p($g(^[dataDB]JRL(sku)), "~", 1)
            s obj = ##class(%DynamicObject).%New()
            d obj.%Set("value", sku)
            d obj.%Set("label", $s(nazev'="":nazev, 1:"Skupina "_sku))
            d arr.%Push(obj)
        }
        s ^||ML("OUT","1") = arr.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// IN["SKUPINA_LINEK_ID"] = skupina linek (default první v ^JRL).
/// OUT["1"] = JSON pole { "value": lin, "display": "číslo - název", "subtitle": "Platnost ..." }.
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s search = $zcvt($g(^||ML("IN","search")), "L")
        s limit  = +$g(^||ML("IN","limit"))
        i 'limit s limit = 50

        s sku = +$g(^||ML("IN","SKUPINA_LINEK_ID"))
        i 'sku s sku = $o(^[dataDB]JRL(""))      // první skupina linek

        s arr = ##class(%DynamicArray).%New()
        s count = 0, lin = ""
        f {
            s lin = $o(^[dataDB]JRL(sku,lin))
            q:lin=""
            i '$d(^[dataDB]JRL(sku,lin,"I")) continue

            s nazev   = ..NazevLinky(dataDB, sku, lin)
            s cislo   = +lin                          // bez vedoucí nuly (subscript ^JRL ji drží)
            s display = cislo
            i nazev '= "" s display = cislo_" - "_nazev
            i (search '= "") && ('$f($zcvt(display,"L"), search)) continue

            s plati = $g(^[dataDB]JRL(sku,lin,"PATCH","Plati"))
            s subtitle = ""
            i $p(plati,"~",1) '= "" {
                s subtitle = "Platnost od "_$p(plati,"~",1)
                i $p(plati,"~",2) '= "" s subtitle = subtitle_" do "_$p(plati,"~",2)
            }

            s obj = ##class(%DynamicObject).%New()
            // "string" type - .NET čte přes GetString(); bez toho číselné cislo/value → JSON number → chyba
            d obj.%Set("value", lin, "string")
            d obj.%Set("cislo", cislo, "string")
            d obj.%Set("nazev", nazev, "string")
            d obj.%Set("display", display, "string")
            i subtitle '= "" d obj.%Set("subtitle", subtitle, "string")
            d arr.%Push(obj)

            s count = count + 1
            q:count>=limit
        }

        s ^||ML("OUT","1") = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vrátí data pro tisk: 1 stránka per zastávka × směr (reimpl. legacy `WinTiskMhd^JRapiExp10`).
///
/// Fázový engine:
///   1. Načte linku + trasu (^JRL(sku,lin,"J"), "I", PATCH)
///   2. Načte spoje (^JRL(sku,lin,"T"/"Z",sp)) + trasy spojů ("W") + omezení (,22 / ,32)
///   3. Odvodí sloupce (skupiny dnů) z FTL (MHD_TYP_R) + Plk + CNZ
///   4. Promítne odjezdy per zastávka × sloupec, anotace + datumová omezení
///   5. Sestaví poznámky (legenda: atributy + omezení)
///   6. Iteruje zastávky × směry → OUT[$i] = JSON stránka (values + rows ZASTAVKY/ODJEZDY/POZNAMKY)
/// Zlatá reference výstupu: `ad-data-mapa.md` §9.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd                       // cache per-linka/per-směr invariantů (served, bezbariér) - fresh per request
        d ..ZacitDuvodyPrazdne()
        d ..NactiNastaveniNazvu()         // tvar názvu zastávky + „název pro MHD" (platí pro celou sestavu)
        d ..NactiJazyk()                  // jazyk textů značek = kultura šablony (#12843)
        d ..NactiTextyZnacek(dataDB)      // vysvětlivky dnů z číselníku zákazníka (musí až po NactiJazyk)

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        // Default = první skupina v ^JRL - shodně s GetAvailableInstances. Picker posílá jen
        // LINKA_ID, takže GetData musí číst tutéž skupinu, kterou picker listoval.
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        // LINKA_ID nebo INSTANCE_ID (alias z Instance Pickeru) - co přijde
        s lin = ..NormalizLinku($g(^||ML("IN","LINKA_ID")))
        i lin = "" s lin = ..NormalizLinku($g(^||ML("IN","INSTANCE_ID")))
        i lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))
        i '$d(^[dataDB]JRL(sku,lin,"I")) $$$ThrowStatus($$$ERROR($$$GeneralError, "Linka "_sku_"/"_lin_" neexistuje v ^JRL"))

        // #8828: rozdělit vývěsku per označník - vývěska obsluhovaná z N označníků → N stránek,
        // každá s odjezdy jen z toho označníku. Default vyp = jedna vývěska (odjezdy smíchané).
        s rozdelitOzn = ($zcvt($g(^||ML("IN","ROZDELIT_PER_OZNACNIK")),"L") = "true")
        // #13088: kterým údajem se řídí stanoviště (dělení stránek, výčty). Default = označník.
        s zdrojSt = $s($g(^||ML("IN","ZDROJ_STANOVISTE")) = "Sloupek" : "SL", 1 : "OZN")

        // Má-li vlastní sloupec zadané období, spočítá „kdy spoj jede" ISY - jedním voláním na celou
        // linku, protože stránek stavíme N. Bez období se nevolá nic a dnešní cesta se nemění.
        d ..PripravitJede(.sys, dataDB, sku, lin)

        // Multi-page: jedna stránka per zastávka × směr (Tam + Zpět). Aktuální zastávka =
        // ze které se počítají odjezdy; StopList ukazuje úsek od ní ve směru jízdy.
        // ⚠️ Výchozí pořadí má vnější smyčku ZASTÁVKA a vnitřní SMĚR - obě vývěsky téže zastávky
        // musí jít v PDF za sebou, aby se daly vytisknout a pověsit na oba označníky jedním
        // výjezdem. Tak to dělá i starý tisk (`JRapiLinPrtMhd1.mac:397-398`, Tc vnější, Smer vnitřní).
        // `B161`: druhá volba prohodí smyčky a zpáteční směr projde odzadu, takže cestující projde
        // trasu tam a pak zpět.
        i $g(^||ML("IN","PORADI_STRANEK")) = "Po směrech (tam vzestupně, zpět sestupně)" {
            f smer = "T", "Z" {
                s poradi = ""
                f {
                    s poradi = $s(smer="Z":$o(^[dataDB]JRL(sku,lin,"J",poradi),-1), 1:$o(^[dataDB]JRL(sku,lin,"J",poradi)))
                    q:poradi=""
                    i '$isvalidnum(poradi) continue
                    i $p($g(^[dataDB]JRL(sku,lin,"J",poradi)),":",1) = "" continue   // bod bez zastávky
                    d ..VydatStrankyZastavky(dataDB, sku, lin, poradi, smer, rozdelitOzn, zdrojSt, IdFa)
                }
            }
        }
        else {
            s poradi = ""
            f {
                s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi))
                q:poradi=""
                i '$isvalidnum(poradi) continue
                i $p($g(^[dataDB]JRL(sku,lin,"J",poradi)),":",1) = "" continue   // bod bez zastávky
                f smer = "T", "Z" {
                    d ..VydatStrankyZastavky(dataDB, sku, lin, poradi, smer, rozdelitOzn, zdrojSt, IdFa)
                }
            }
        }
        // Vývěska bez odjezdů se vynechává (konečná), takže linka bez jediné stránky je ta, odkud
        // nejede nic odnikud. Důvod stejnými slovy jako linkové agendy (`BaseAgenda.PrazdnaStranka`, `B193g`).
        s maSpoje = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).MaSpoje(dataDB, sku, lin, "T") || ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).MaSpoje(dataDB, sku, lin, "Z")
        d ..VydatDuvodPrazdne($s('maSpoje:"Linka nemá v jízdních řádech žádné spoje.", 1:"Z žádné zastávky linky neodjíždí žádný spoj. Zkontrolujte časy spojů a trasu linky v jízdních řádech."))
    }
    catch (ex) { $$$CatchErrRest }
    k ^||TiskAd                        // úklid working global i po chybě (self-healing kill je na začátku)
    ret $$$OK
}

/// Vydá stránky jedné zastávky v jednom směru do `^||ML("OUT")`. Obvykle jednu; se zapnutým
/// dělením per stanoviště tolik, kolik jich spoje obsluhují. Volá se ze dvou různých pořadí
/// stránek (`B161`), proto samostatně - tělo smyčky musí být v obou shodné.
/// Helper - volá se jen z GetData, která catch má.
ClassMethod VydatStrankyZastavky(dataDB As %String, sku As %String, lin As %String, poradi As %String, smer As %String, rozdelitOzn As %Boolean, zdrojSt As %String, IdFa As %String)
{
    // Rozdělení per označník: jen když je zapnuté a vývěska má >1 označník obsluhovaný spoji.
    if rozdelitOzn {
        s csvOzn = ..NastupisteSpoju(dataDB, sku, lin, smer, poradi, zdrojSt)
        if (csvOzn '= "") && ($l(csvOzn,",") > 1) {
            f oi=1:1:$l(csvOzn,",") {
                s page = ..BuildStrankaZastavky(dataDB, sku, lin, poradi, smer, $p(csvOzn,",",oi), IdFa, zdrojSt)
                i page '= "" s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
            }
            ret        // rozděleno → jedinou (smíchanou) stránku nevytvářet
        }
    }
    s page = ..BuildStrankaZastavky(dataDB, sku, lin, poradi, smer, "", IdFa, zdrojSt)
    i page '= "" s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
}

/// Sestaví JSON stránku zastávkového JŘ pro jednu linku - sdíleno mezi GetData (živě ze session
/// firmy) a GetMockData (hardcoded CB_GWBUS pro live preview).
/// dataDB = data namespace; sku/lin = identifikace linky v ^JRL. Vrací %DynamicObject, nebo ""
/// když linka neexistuje.
/// smer = "T" (Tam, pořadí nahoru k poslední zastávce) nebo "Z" (Zpět, pořadí dolů k první).
/// currentPoradi = pořadí aktuální zastávky (prázdné = první). Vrací %DynamicObject nebo "".
/// filtrOznacnik (#8828, volitelné): když neprázdné, stránka nese jen odjezdy spojů z tohoto označníku
/// vývěskové zastávky a NASTUPISTE = tento označník. Prázdné = všechny odjezdy (dnešní chování).
/// IdFa (volitelné): potřebuje ho jen barva linky - ostatní data JŘ modulu jsou vázaná na namespace,
/// ne na firmu. Prázdné → barva linky se nenaplní (šablona pak použije bílou).
ClassMethod BuildStrankaZastavky(dataDB As %String, sku As %String, lin As %String, currentPoradi As %String = "", smer As %String = "T", filtrOznacnik As %String = "", IdFa As %String = "", zdrojSt As %String = "OZN") As %DynamicObject
{
    i '$d(^[dataDB]JRL(sku,lin,"I")) q ""
    // B188: údaje zastávek z registru k „platí od" linky (časové varianty), jako starý tisk
    s ^||TiskAd("zastavky","hrl") = ..HorologPlatnostiOdLinky(dataDB, sku, lin)
    s slJakoStanoviste = (zdrojSt = "SL")            // #13088: stanoviště = sloupek, ne označník

    // currentPoradi nezadáno → první zastávka trasy
    i currentPoradi = "" {
        s p = ""
        f {
            s p = $o(^[dataDB]JRL(sku,lin,"J",p))
            q:p=""
            i ($isvalidnum(p)) && ($p($g(^[dataDB]JRL(sku,lin,"J",p)),":",1)'="") s currentPoradi=p quit
        }
    }

    // ── hlavička linky ──────────────────────────────────────────────
    // SeT_MhD (delim "~"): $1 lokální označení, $2 titulek, $5 informace (1:1 sTIT^JRapiLinPrtMhd1).
    s setMhd = $g(^[dataDB]JRL(sku,lin,"MhD","SeT_MhD"))
    s lokalni = ..LokalniOznaceniLinkyAd(dataDB, sku, lin)
    s titulek = $p(setMhd,"~",2)
    i titulek = "" s titulek = "Městská hromadná doprava..."     // legacy MHD default
    s informace = $replace($p(setMhd,"~",5),"<br>",$c(13,10))    // cnvTxt: <br> → newline

    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            +lin)
    d vals.%Set("LOKALNI_OZNACENI_LINKY", lokalni)
    d vals.%Set("TITULEK",                titulek)
    d vals.%Set("POPIS_LINKY",            ..NazevLinky(dataDB, sku, lin))
    d vals.%Set("POPIS_DOPRAVCE",         ..PopisDopravce(dataDB, sku, lin))
    d vals.%Set("DOPRAVCI_SOUHRNNY_JR",         ..DopravciSouhrnnyJr(dataDB, sku, lin, ..VypsatSpojeDopravce()))
    // Skalární údaje = PRVNÍ dopravce linky (u většiny linek jediný). Linka s víc dopravci se
    // skládá z kolekce DOPRAVCI - jeden řádek by tu ostatní zamlčel, proto to říká i nápověda pole.
    d ..DopravceSkalary(dataDB, sku, lin, vals)
    d vals.%Set("BARVA_LINKY",            ..BarvaLinky(IdFa, dataDB, sku, lin))
    // #12997: JŘ umí od 3. 8. víc barev na linku. Klient je používá jako barvu textu (1.) a pozadí
    // (2.), ale modul jim význam nedává - proto neutrální název a význam nechává na šabloně.
    d vals.%Set("BARVA_LINKY_2",          ..BarvaLinky(IdFa, dataDB, sku, lin, 2))
    s plati = $g(^[dataDB]JRL(sku,lin,"PATCH","Plati"))
    s platiOd = $p(plati,"~",1)
    s platiDo = $p(plati,"~",2)
    // Jen pro starší šablony - v nabídce dat už není (Jan 23. 10.), tvar se proto nemění.
    d vals.%Set("POPIS_PLATNOSTI",        $s(platiOd'="":"Platí od "_platiOd, 1:""))
    d vals.%Set("DATUM_PLATNOSTI_OD",     platiOd)
    d vals.%Set("DATUM_PLATNOSTI_DO",     platiDo)
    // Typ JR (AD-N2) - PATCH,"OBJ" (gPrpLin dta3), znění jako u MHD (BaseAgenda.TypJrLinkyAd).
    // Pozn.: zastávková větev (JRapiLinPrtMhd) OBJ nečte → razítko nestaví automaticky;
    // TYP_JR je volitelný placeholder pro autora šablony (PS/knižní feature).
    d vals.%Set("TYP_JR",                 ..TypJrLinkyAd(dataDB, sku, lin))
    d vals.%Set("QR_KOD",                 $g(^[dataDB]JRL(sku,lin,"PATCH","QR_CODE")))
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  $g(^[dataDB]JRL(sku,lin,"PATCH","PoznPodDopr")))
    // Poznámka nad linku (AD-N1) - legacy gPrpLin dta4 = PATCH,"Head"
    d vals.%Set("POZNAMKA_NAD_LINKU",     $g(^[dataDB]JRL(sku,lin,"PATCH","Head")))
    d vals.%Set("INFORMACE",              informace)
    // #13088 / #13093: výlukové texty linky jako samostatné pole - klient si je tiskne ve zvláštním
    // poli, ne v bloku poznámek. Pole je inertní (naplní se vždy, ale zobrazí jen když si ho položí),
    // takže na rozdíl od poznámek v legendě není potřeba ho gatovat parametrem.
    d vals.%Set("VYLUKOVE_TEXTY",         ..VylukoveTexty(dataDB, sku, lin))
    // NEJEDOU_SPOJE je legacy misnomer - nese NÁSTUPIŠTĚ vývěskové zastávky (J-věta #3 = nastTam:nastZpet,
    // dta23 v JRapiExp10). Per směr (T=tam, Z=zpět). 1:1 gold.
    s nastupiste = $p($p($g(^[dataDB]JRL(sku,lin,"J",currentPoradi)),"#",3),":",$s(smer="Z":2,1:1))
    s sloupekAkt = ..SloupekZTrasy($g(^[dataDB]JRL(sku,lin,"J",currentPoradi)), smer '= "Z")
    // #8828: rozdělená stránka nese své stanoviště - do toho údaje, který stanoviště určuje
    i filtrOznacnik '= "" {
        i slJakoStanoviste { s sloupekAkt = filtrOznacnik } else { s nastupiste = filtrOznacnik }
    }
    d vals.%Set("NASTUPISTE",             nastupiste)         // A2: NEJEDOU_SPOJE je misnomer → NASTUPISTE
    d vals.%Set("NEJEDOU_SPOJE",          nastupiste)         // zastaralé (kompatibilita) → NASTUPISTE
    d vals.%Set("SLOUPEK",                sloupekAkt)
    // Prvek si podle toho vybere, který údaj řídí režimy více hodnot a řádky (jeden zdroj pravdy)
    d vals.%Set("ZDROJ_STANOVISTE",       $s(slJakoStanoviste:"Sloupek", 1:"Označník"))

    // ── trasa → zastávky (CELÁ trasa ve směru jízdy, vč. projetých; zóny + na znamení) ──
    // Tam: vzestupně 1→N. Zpět: sestupně N→1. Aktuální = JE_AKTUALNI; projeté mají casJizdy=0 a bez
    // atributů (jako MHD). StopList je defaultně skryje; „tisk projetých" je zobrazí ztlumeně.
    s zastavky = ##class(%DynamicArray).%New()
    s aktualniNazev = "", aktualniCzst = "", posledniNazev = "", posledniCzst = "", prvniCzst = ""
    k atrSeen, prestupSeen                                      // atributy zastávek viděné na stránce (znak → text) pro legendu
    k servedPoradi
    // Jen zastávky dosažitelné z TÉTO vývěsky (#7959) - zastávka, kam odtud nic nejede, by zabrala
    // řádek s „<" ve sloupci časů i ve všech variantách.
    d ..ObsluhovanePoradiOd(dataDB, sku, lin, smer, currentPoradi, .servedPoradi)
    s jeTam = (smer = "T")

    // TR2 (opt-in SOUHRN_NA_ZNAMENI): když jsou VŠECHNY obsluhované neprojeté zastávky „na znamení" („x"),
    // vytiskne se jeden agregátní řádek legendy místo × u každé. Default off = per-zastávka (gold/CZ beze změny).
    s souhrnNaZn = 0
    i $zcvt($g(^||ML("IN","SOUHRN_NA_ZNAMENI")),"L") = "true" {   // tolerantně - starší klient posílal "True"
        s souhrnNaZn = 1, naZnCnt = 0, ppNZ = ""
        f {
            s ppNZ = $o(servedPoradi(ppNZ))
            q:ppNZ=""
            i jeTam,(ppNZ<currentPoradi) continue        // projetá se do souhrnu nepočítají
            i 'jeTam,(ppNZ>currentPoradi) continue
            s vNZ = $g(^[dataDB]JRL(sku,lin,"J",ppNZ))
            i $p(vNZ,":",1)="" continue                  // bod bez zastávky
            s naZnCnt = naZnCnt + 1
            i '(..AtributyZastavky(dataDB, $p(vNZ,":",1), $p($p(vNZ,":",2),"#",1))["x") s souhrnNaZn = 0  q
        }
        i 'naZnCnt s souhrnNaZn = 0
    }

    // Konečná směru = za currentPoradi není žádná obsluhovaná zastávka → jen příjezdy, board se netiskne
    // (1:1 legacy - přeskakuje pouze poslední zastávku směru, ne per-spoj konečné).
    s jeDal = 0, pp = ""
    f {
        s pp = $o(servedPoradi(pp))
        q:pp=""
        i jeTam, (pp > currentPoradi) s jeDal = 1 quit
        i 'jeTam, (pp < currentPoradi) s jeDal = 1 quit
    }
    i 'jeDal q ""

    // základní trasa pro časy jízdy (TrasaMin) - reprezentativní trasa spoje stavícího na vývěskové
    // zastávce; W-mapu rozparsujeme jednou a v cyklu zastávek z ní jen čteme
    s zaklCt = ..ZakladniCisloTrasy(dataDB, sku, lin, smer, currentPoradi)
    d ..WMapa(dataDB, sku, lin, zaklCt, .zaklWMap)

    // ── varianty tras (dynamické „TrasaMin") - KOVED #13048 bod 5 ────────────────
    // Varianta = SLED OBSLUHOVANÝCH ZASTÁVEK **od vývěskové zastávky dál** (1:1 legacy dedup dle
    // `prutmbmp` nad smyčkou `f mytc=Tc:Krok:TcDo`, JRapiLinPrtMhd1 ř.266-300), NE časový profil -
    // spoje stejné trasy s jinými časy se slévají. Proč jen ten úsek: `JeVUsekuVariant`.
    // Čas sloupce = MAXIMUM přes spoje varianty (legacy „ponechá se delší čas") = „nejpozději za X min".
    // Signatura jen přes ZOBRAZENÉ zastávky (servedPoradi): staví = „S", nestaví = „<" (`<` i `|`),
    // mimo trasu = prázdno. Označník do identity NEJDE - dal by vizuálně identické časové sloupce
    // (KOVED „jiný označník = jiná varianta" řeší řádek pro každý označník + vývěska dle označníků, #8828).
    // ZÁMĚR: na rozdělené stránce (#8828, filtrOznacnik) se varianty NEFILTRUJÍ na spoje označníku -
    // sloupce/písmena tak zůstávají shodné napříč stránkami téže zastávky (dvě vývěsky vedle sebe na
    // témže sloupku si neprotiřečí); filtruje se jen mřížka odjezdů.
    // bool parametry číst tolerantně malými písmeny - starší klient posílal C# ToString() "True"
    s prvniBez   = ($zcvt($g(^||ML("IN","PRVNI_BEZ_PISMENE")),"L") '= "false")
    s razeniVar  = $g(^||ML("IN","RAZENI_VARIANT"))
    k variantSig, variantPocet, variantRepSpoj, variantCas, variantPat, spojVarianta, variantCt, variantDelka
    k variantSigText, variantSlouceno
    s poradiVar = 0, spV = ""
    f {
        s spV = $o(^[dataDB]JRL(sku,lin,smer,spV))
        q:spV=""
        i '$isvalidnum(spV) continue
        s hlavaV = $p($g(^[dataDB]JRL(sku,lin,smer,spV)),"#",1)
        s ctV = $p(hlavaV,":",3)
        i ctV = "" continue
        d ..WMapa(dataDB, sku, lin, ctV, .wMapV)
        i '..OdjizdiZde(.wMapV, currentPoradi, $p(hlavaV,":",2)) continue   // odtud nejede → nedává časový sloupec
        s sigV = "", ppV = ""
        f {
            s ppV = $o(servedPoradi(ppV))  q:ppV=""
            i '$isvalidnum(ppV) continue
            i '..JeVUsekuVariant(ppV, currentPoradi, jeTam) continue   // úsek před vývěskou do identity nejde
            s vv = $g(wMapV(ppV))
            s $p(sigV,"~",ppV) = $s(vv="":"", (vv="<")||(vv="|"):"<", 1:"S")
        }
        i '$d(variantSig(sigV)) {
            s poradiVar = poradiVar + 1
            s variantSig(sigV) = poradiVar
            s variantSigText(poradiVar) = sigV        // `B142`: identita zpětně, pro test podmnožiny
        }
        s viV = variantSig(sigV)
        s spojVarianta(spV) = viV
        s variantPocet(viV) = $g(variantPocet(viV)) + 1
        // Kandidát na průběh varianty: delší jízda vyhrává (dosah mají členové varianty shodný, to je
        // jejich definice), při shodě nižší číslo trasy kvůli determinismu. Legacy „nechám průběh
        // s delším časem" - `JRapiLinPrtMhd1.mac` ř.290-294.
        s delkaV = ..DelkaProfilu(.wMapV, currentPoradi, .servedPoradi, jeTam)
        i ('$d(variantDelka(viV))) || (delkaV > variantDelka(viV)) || ((delkaV = variantDelka(viV)) && (ctV < variantCt(viV))) {
            s variantDelka(viV) = delkaV, variantCt(viV) = ctV, variantRepSpoj(viV) = spV
        }
    }

    // `B142`: varianta, která staví jen tam, kde staví i jiná, se dá slít do té největší nadmnožiny.
    // ⚠️ Vědomě se tím ZTRÁCÍ informace - u slitých spojů není ve sloupci vidět, které zastávky
    // vynechávají, a časy ukazuje ta delší varianta (vynechaná zastávka posouvá i následující časy).
    // Rozdíly tras to nahrazují jen zčásti: vypínají se u linky a měří se proti zaškrtnuté základní
    // trase, ne proti druhé variantě. Proto jen na vyžádání (rozhodnutí Jan 11. 9.).
    // Cíl je vždy NEJVĚTŠÍ nadmnožina, takže sám nikdy slitý není a řetězce A ⊆ B ⊆ C se vyřeší
    // jedním průchodem - kdyby se slévalo po dvojicích, záleželo by na pořadí.
    i $zcvt($g(^||ML("IN","SLUCOVAT_PODMNOZINY")),"L") = "true" {
        f viA=1:1:poradiVar {
            s staveniA = ..PocetZastaveni($g(variantSigText(viA)))
            s cil = 0, cilStaveni = 0
            f viB=1:1:poradiVar {
                i viB = viA continue
                i '..JeUsekemVarianty($g(variantSigText(viA)), $g(variantSigText(viB))) continue
                s staveniB = ..PocetZastaveni($g(variantSigText(viB)))
                i staveniB '> staveniA continue          // shodný počet = táž varianta, tu slil dedup
                i (cil = 0) || (staveniB > cilStaveni) || ((staveniB = cilStaveni) && (+$g(variantDelka(viB)) > +$g(variantDelka(cil)))) {
                    s cil = viB, cilStaveni = staveniB
                }
            }
            i cil s variantSlouceno(viA) = cil
        }
        // Četnost přebírá přeživší varianta - na ní visí řazení „Dle četnosti ze zastávky".
        s viA = ""
        f {
            s viA = $o(variantSlouceno(viA))
            q:viA=""
            s cilV = variantSlouceno(viA)
            s variantPocet(cilV) = $g(variantPocet(cilV)) + $g(variantPocet(viA))
        }
        // Spoje slité varianty musí ukazovat na písmeno té přeživší, jinak by mířily na zmizelý sloupec.
        s spM = ""
        f {
            s spM = $o(spojVarianta(spM))
            q:spM=""
            i $d(variantSlouceno(spojVarianta(spM))) s spojVarianta(spM) = variantSlouceno(spojVarianta(spM))
        }
    }

    // Hodnoty variantních sloupců = průběh JEDNÉ vybrané trasy, nikdy se nemíchají. Dřív se bralo
    // maximum per zastávka přes spoje varianty, což mohlo dát kombinaci časů, kterou nejede žádný spoj.
    // Z téže trasy vyjde i úsek před vývěskou (projeté zastávky, opt-in „tisk projetých") a stanoviště
    // varianty, takže celý sloupec odpovídá jednomu reálnému spoji.
    f viY=1:1:poradiVar {
        d ..WMapa(dataDB, sku, lin, $g(variantCt(viY)), .wMapSel)
        s ppV = ""
        f {
            s ppV = $o(servedPoradi(ppV))  q:ppV=""
            i '$isvalidnum(ppV) continue
            s vvS = $g(wMapSel(ppV))
            s variantPat(viY,ppV) = $s(vvS="":"", (vvS="<")||(vvS="|"):"<", 1:"S")
            i ..CasHodnota(vvS) '= "" s variantCas(viY,ppV) = ..CasJizdyZMapy(.wMapSel, currentPoradi, ppV)
        }
    }

    // pořadí sloupců variant: dle četnosti ze zastávky (KOVED default - nejvíc spojů první,
    // shoda četností → pořadí výskytu) / „Stabilní" = pořadí výskytu spojů / dle délky jízdy
    // `B142`: slitá varianta se do seznamu nedostane - její spoje už ukazují na přeživší sloupec.
    // Nepřečíslovává se, jen se přeskočí, takže díra v pořadí nevznikne (na pořadí visí písmena).
    k displayVar
    s pocetSloupcu = 0
    i razeniVar = "Stabilní (pořadí spojů)" {
        f viY=1:1:poradiVar {
            i $d(variantSlouceno(viY)) continue
            s pocetSloupcu = pocetSloupcu + 1
            s displayVar(pocetSloupcu) = viY
        }
    } elseif (razeniVar = "Dle délky jízdy (nejkratší první)") || (razeniVar = "Dle délky jízdy (nejdelší první)") {
        // Nejrychlejší varianta stojí hned vedle sloupce „Čas", nejpomalejší vpravo - cestující
        // pak čte sloupce jako škálu, ne jako nahodilé pořadí. Délka = kumulativní čas nejdál
        // od vývěskové zastávky, tedy táž hodnota, kterou se vybírá průběh varianty.
        // Shoda délek → pořadí výskytu, ať výstup nezávisí na pořadí průchodu.
        s opacne = (razeniVar = "Dle délky jízdy (nejdelší první)")
        k radArr
        f viY=1:1:poradiVar {
            i $d(variantSlouceno(viY)) continue
            s radArr($s(opacne:-$g(variantDelka(viY)), 1:+$g(variantDelka(viY))),viY) = ""
        }
        s dKey = ""
        f {
            s dKey = $o(radArr(dKey))  q:dKey=""
            s viK = ""
            f {
                s viK = $o(radArr(dKey,viK))  q:viK=""
                s pocetSloupcu = pocetSloupcu + 1
                s displayVar(pocetSloupcu) = viK
            }
        }
    } else {
        k radArr
        f viY=1:1:poradiVar {
            i $d(variantSlouceno(viY)) continue
            s radArr(-$g(variantPocet(viY)),viY) = ""
        }
        s cKey = ""
        f {
            s cKey = $o(radArr(cKey))  q:cKey=""
            s viK = ""
            f {
                s viK = $o(radArr(cKey,viK))  q:viK=""
                s pocetSloupcu = pocetSloupcu + 1
                s displayVar(pocetSloupcu) = viK
            }
        }
    }

    // `B181` (TRANSDEV #12843): nejvýš N sloupců - prvních N podle zvoleného řazení, zbytek se
    // netiskne. ⚠️ Vědomá ztráta informace: spoje skrytých variant nemají sloupec, a protože písmena
    // se dávají jen tištěným sloupcům, nemají ani písmeno. Kombinace s písmeny variant tak nedává
    // smysl a ani se neřeší (rozhodnutí Jan 25. 9.). Prázdné (i 0) = bez omezení.
    s maxSloupcu = +$g(^||ML("IN","MAX_SLOUPCU_VARIANT"))
    i (maxSloupcu > 0) && (pocetSloupcu > maxSloupcu) {
        f colZ=(maxSloupcu+1):1:pocetSloupcu k displayVar(colZ)
        s pocetSloupcu = maxSloupcu
    }

    // Emituj VŠECHNY zastávky směru (vč. projetých, jako MHD) - StopList je filtruje dle „tisk projetých".
    s zonaBezPismen = ..ZonaBezPismen(dataDB, sku, lin)   // nastavení je per linka, ne per zastávka
    s poradi = ""
    s cnt = 0
    f {
        i jeTam { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi)) }
        else    { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi), -1) }
        q:poradi=""
        i '$isvalidnum(poradi) continue
        s jeProjeta = $s(jeTam:(poradi < currentPoradi), 1:(poradi > currentPoradi))   // projetá = před aktuální ve směru
        s veta = $g(^[dataDB]JRL(sku,lin,"J",poradi))
        s czst = $p(veta,":",1)
        i czst="" continue
        i '$d(servedPoradi(poradi)) continue                // jen obsluhované zastávky (jako legacy)
        s atributy  = ..AtributyZastavky(dataDB, czst, $p($p(veta,":",2),"#",1))   // trasa linky + registr zastávek
        s zony      = ..ZonyZastavky(dataDB, veta, czst, zonaBezPismen)   // trasa linky + záskok na registr zastávek
        s odsazeni  = +$p(veta,"#",5)                        // dta7 „odrazeni" - odsazení názvu o N znaků (legacy mkPLinWin/JRapiLin: 5. #-segment)
        s rawNazev = ..NazevZastavky(dataDB, czst)
        s jeAkt = (poradi = currentPoradi)
        s casJizdy = ..CasJizdySloupce(.zaklWMap, currentPoradi, poradi, jeProjeta)
        s cnt = cnt + 1
        // „na znamení" se tiskne přes atribut „x" (níže) - legacy UVOD tiskne jen „x". NaZnameni=0,
        // ať StopList nepřidá ještě syntetický SignalStopSymbol „×" (jinak duplicita „× x").
        s z = ..BuildZastavka(poradi, rawNazev, poradi, casJizdy, cnt, jeAkt, 0, odsazeni)
        f zi=1:1:$l(zony,";") {
            s zx = $p(zony,";",zi)
            i zx '= "" d ..AddZona(z, zx)
        }
        // atributy zastávky → symbol u názvu + řádek legendy (jen neprojeté + významové znaky; analogicky MHD).
        // Přestupy (vlak „L" / autobus „A") jdou nad rámec legacy - posílají se vždy jako data zastávky,
        // ale legendu NEplní; jejich symbol řídí přepínač „Přestupy" na prvku StopList (default vyp).
        i 'jeProjeta {
            f ai=1:1:$l(atributy) {
                s znak = $e(atributy, ai)
                s jePrestup = ..JePrestupovyAtribut(znak)
                i '..JeViditelnyAtribut(znak) && 'jePrestup continue
                i souhrnNaZn && (znak = "x") continue    // TR2: souhrn místo per-zastávka ×
                s txtAtr = ..TextAtributu(znak)
                d ..AddAtribut(z, znak, txtAtr)
                // Přestupy jdou do legendy zvlášť (kategorie „prestup") - piktogram autobusu/vlaku
                // sám o sobě neřekne, co znamená. Šablona, která značku netiskne, si řádek schová
                // render-time filtrem (stejně jako „× na znamení"), takže se nikomu neobjeví omylem.
                i jePrestup { s prestupSeen(znak) = txtAtr } else { s atrSeen(znak) = txtAtr }
            }
        }
        // varianty časů (dynamické TrasaMin) - sloupce v pořadí displayVar, hodnota = průběh vybrané trasy
        s trasaMin = ##class(%DynamicArray).%New()
        f colX=1:1:pocetSloupcu {
            s viX = displayVar(colX)
            s patX = $g(variantPat(viX,poradi))
            i patX = "S" { d trasaMin.%Push(+$g(variantCas(viX,poradi))) }
            else { d trasaMin.%Push(patX) }
        }
        d z.%Set("TRASA_MIN", trasaMin)
        // KOVED #13048 bod 4 (duplikace): nástupiště per varianta (zarovnané s TRASA_MIN) - z reprezentativní
        // trasy varianty (W pole 7, fallback J-věta #3). Prázdno = varianta zde nestaví. Element z toho
        // v režimu „Řádek pro každý označník" udělá N řádků (časy jen variant daného označníku).
        s nastVar = ##class(%DynamicArray).%New()
        s sloupekVar = ##class(%DynamicArray).%New()
        s nastJv = $p($p(veta,"#",3),":",$s(jeTam:1,1:2))    // fallback z trasy linky (J-věta dle směru)
        s sloupekJv = ..SloupekZTrasy(veta, jeTam)
        f colX=1:1:pocetSloupcu {
            s viX = displayVar(colX)
            i $g(variantPat(viX,poradi)) = "S" {
                d ..WNastupisteMapa(dataDB, sku, lin, $g(variantCt(viX)), .nMapV)
                s nastX = $g(nMapV(poradi))
                i nastX = "" s nastX = nastJv
                d nastVar.%Push(nastX)
                d ..WNastupisteMapa(dataDB, sku, lin, $g(variantCt(viX)), .nMapVS, "SL")
                s sloupekX = $g(nMapVS(poradi))
                i sloupekX = "" s sloupekX = sloupekJv
                d sloupekVar.%Push(sloupekX)
            } else {
                d nastVar.%Push("")                          // varianta zde nestaví → bez nástupiště
                d sloupekVar.%Push("")
            }
        }
        d z.%Set("NASTUPISTE_VARIANT", nastVar)
        d z.%Set("SLOUPEK_VARIANT", sloupekVar)
        // TR3: označník per zastávka (J-věta #3, dle směru). Na rozdělené stránce (#8828) nese
        // ŘÁDEK vývěskové zastávky označník své stránky - jinak by stránky 11/22 vypadaly identicky
        // (J-věta má jednu hodnotu pro obě).
        // TR3 / #13088: obě hodnoty z trasy dle směru. Na rozdělené stránce (#8828) nese ŘÁDEK
        // vývěskové zastávky stanoviště své stránky - jinak by stránky 11/22 vypadaly identicky
        // (J-věta má jednu hodnotu pro obě). Přebíjí se ten údaj, který stanoviště určuje.
        s nastRadek = nastJv, sloupekRadek = sloupekJv
        i (filtrOznacnik '= "") && jeAkt {
            i slJakoStanoviste { s sloupekRadek = filtrOznacnik } else { s nastRadek = filtrOznacnik }
        }
        d z.%Set("NASTUPISTE", nastRadek)
        d z.%Set("SLOUPEK", sloupekRadek)
        d z.%Set("SLOUPEK_POPIS", ..PopisSloupku(dataDB, czst, sloupekRadek))
        // KOVED #13048 bod 4: nástupiště dle SPOJŮ (W pole 8, fallback J-věta) - CSV dle četnosti,
        // první = nejčetnější. Element z toho staví režimy „nejčetnější" / „výčet". Na rozdělené
        // stránce u vývěskové zastávky rovněž jen označník stránky (stránka JE o něm).
        s prebitStanoviste = ((filtrOznacnik '= "") && jeAkt)
        d z.%Set("NASTUPISTE_SPOJU", $s(prebitStanoviste && 'slJakoStanoviste : filtrOznacnik, 1 : ..NastupisteSpoju(dataDB, sku, lin, smer, poradi)))
        d z.%Set("SLOUPEK_SPOJU",    $s(prebitStanoviste && slJakoStanoviste  : filtrOznacnik, 1 : ..NastupisteSpoju(dataDB, sku, lin, smer, poradi, "SL")))
        d z.%Set("CISLO_ZASTAVKA_CIS", ..CisloZastavkyCis(dataDB, czst))
        d zastavky.%Push(z)
        i jeAkt s aktualniNazev = rawNazev, aktualniCzst = czst
        i prvniCzst = "" s prvniCzst = czst              // výchozí zastávka směru = první obsluhovaná
        s posledniNazev = rawNazev, posledniCzst = czst
    }

    // #8828: rozdělená vývěska nese označník v názvu zastávky (jako KOVED XLSX „…, sloupek 1") -
    // titulek stránky se jinak mezi rozdělenými stránkami nijak viditelně neliší. Bez rozdělení beze změny.
    i filtrOznacnik '= "" s aktualniNazev = aktualniNazev_", "_$s(slJakoStanoviste:"sloupek", 1:"označník")_" "_filtrOznacnik
    d vals.%Set("POPIS_ZASTAVKA",      aktualniNazev)
    d vals.%Set("CISLO_ZASTAVKA",      aktualniCzst)
    d vals.%Set("SLOUPEK_POPIS",       ..PopisSloupku(dataDB, aktualniCzst, sloupekAkt))
    d vals.%Set("POPIS_ZASTAVKA_CIS",  ..NazevZastavkyCis(dataDB, aktualniCzst))
    d vals.%Set("SMER",                smer)                  // A1: T/Z (KONECNA_ZASTAVKA = konečná)
    d vals.%Set("KONECNA_ZASTAVKA",          posledniNazev)
    d vals.%Set("KONECNA_ZASTAVKA_CIS",      ..NazevZastavkyCis(dataDB, posledniCzst))
    // KOVED #13088: CIS čísla zastávek. „Výchozí" a „cílová" se berou ve SMĚRU stránky (u směru Zpět
    // jsou tedy obrácené proti směru Tam) - stránka je vždy o jednom směru, jinak by čísla nesouhlasila
    // s tím, co je na ní vytištěné. Aktuální = zastávka, o které stránka je.
    // Přípona _CIS = „totéž, ale podle CIS číselníku" (jako POPIS_ZASTAVKA_CIS / KONECNA_ZASTAVKA_CIS).
    // Bez ní to vedle CISLO_ZASTAVKA vypadá jako nesouvisející pole, ne jako jeho druhý zdroj.
    d vals.%Set("CISLO_ZASTAVKA_CIS",         ..CisloZastavkyCis(dataDB, aktualniCzst))
    d vals.%Set("CISLO_ZASTAVKA_VYCHOZI_CIS", ..CisloZastavkyCis(dataDB, prvniCzst))
    d vals.%Set("CISLO_ZASTAVKA_CILOVA_CIS",  ..CisloZastavkyCis(dataDB, posledniCzst))

    // FTL = formát sloupců mřížky (PATCH MHD_TYP_R); nzCfg = NZ konfig pro prázdninové dělení (MHD_NZ1_NZ2|MHD_NENZ1_NENZ2)
    s ftl   = +$g(^[dataDB]JRL(sku,lin,"PATCH","MHD_TYP_R"))
    // Override FTL z parametru „Rozdělení sloupců" (prázdné/auto = dle linky). Mapuje label → FTL mód;
    // engine (NacistSkupinyDnu + ZarazeniSpoje) zůstává beze změny.
    s rozdSl = $g(^||ML("IN","ROZDELENI_SLOUPCU"))
    s ftl = $case(rozdSl, "Pracovní / Sobota / Neděle+svátky":0, "Pracovní / víkend (So+Ne+svátky)":2, "Prázdniny: Prac. mimo+o prázd. / víkend":1, "Prázdniny: Prac. mimo+o prázd. / Sobota / Neděle+svátky":3, :ftl)
    s nzCfg = $g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NZ1_NZ2"))_"|"_$g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NENZ1_NENZ2"))
    // Vlastní definice sloupců ze šablony (DEFINICE_SLOUPCU) - když přijde, přebíjí FTL rozvržení
    // i linkové seznamy značek CELÉ (žádné míchání „dny ze šablony, značky z linky"). Bez definice
    // se nic nemění - dnešní chování je backup.
    s pocetDefSloupcu = ..ParseDefiniceSloupcu(.defSloupce)
    // NZ konfigurace linky pro klienta - editor sloupců z ní předvyplní prázdninové vzory
    // (čísla značek „jede/nejede o prázdninách" + poziční „nevypisovat značku"), aby vzor
    // dal 1:1 stejnou vývěsku jako dnešní rozvržení téže linky.
    d vals.%Set("NZ_KONFIG", nzCfg)
    d vals.%Set("NEVYPISOVAT_ZNACKY_SLOUPCU", $g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NEZNZ123")))
    // hotové vzory sloupců - klient je nestaví sám, aby parita hlídala to, co reálně posílá
    d vals.%Set("PRESETY_SLOUPCU", ..PresetySloupcu(dataDB, sku, lin))

    // ── odjezdy + poznámky (omezení + rozdíly tras + atributy spoje → anotace + legenda) ────
    k poznMap, pismenoSpoj, rozdilLegenda, bezbarLegenda, komentareLegenda, dnyLegenda, varPismeno, spojPismenoVar, odjAtrLegenda, znakyLinky, pokrLegenda, pokrPoradi
    // Značka varianty u minuty a rozdílová poznámka jsou DVĚ NEZÁVISLÉ věci a běží vedle sebe -
    // značka řekne „kterým sloupcem", poznámka „kudy". Tak to má i starý modul: u minuty skládá
    // obojí zvlášť (`JRapiLinPrtMhd1.mac:529`, značka sloupce `:297`). Do 13. 8. u nás režim písmen
    // poznámky vypínal, takže si uživatel musel vybrat mezi spárováním minuty se sloupcem a
    // vysvětlením trasy; sloupce bez písmene („T1") pak neměly ani jedno.
    d ..RozdilyTras(dataDB, sku, lin, smer, currentPoradi, .pismenoSpoj, .rozdilLegenda)
    // Písmena variant nejsou volba: bez nich nejde u minuty poznat, který časový sloupec pro ni
    // platí, a na lince bez rozdílů tras nezůstalo u minut vůbec nic (nadpisy padaly na „T1"/„T2").
    // Volbou zůstává jen „první varianta bez písmene" - ta zároveň drží vývěsku s jedinou variantou
    // bez zbytečného „A" u každé minuty. (Rozhodnutí Jan 17. 8., naměřeno na lince 0330520.)
    s variantyTras = ##class(%DynamicArray).%New()
    // Sada písmen z nastavení linky (jinak výchozí, disjunktní proti rozdílům tras) - čte se jednou,
    // je per linka.
    s sadaVariant = ..SadaVariant(dataDB, sku, lin)
    f colY=1:1:pocetSloupcu {
        s pism = ..PismenoVarianty(colY, prvniBez, sadaVariant)
        i pism '= "" s varPismeno(displayVar(colY)) = pism
        d variantyTras.%Push(pism)
    }
    // spoj → písmeno jeho varianty (anotace u minuty odjezdu)
    s spX = ""
    f {
        s spX = $o(spojVarianta(spX))  q:spX=""
        i $g(varPismeno(spojVarianta(spX))) '= "" s spojPismenoVar(spX) = varPismeno(spojVarianta(spX))
    }
    d vals.%Set("VARIANTY_TRAS", variantyTras)
    k nezarazeneSpoje, obdobiMimoPlatnost
    s odjezdy = ..NacistOdjezdy(dataDB, sku, lin, smer, currentPoradi, ftl, nzCfg, .poznMap, .pismenoSpoj, .bezbarLegenda, .komentareLegenda, .dnyLegenda, .spojPismenoVar, filtrOznacnik, zdrojSt, .odjAtrLegenda, .pokrLegenda, .defSloupce, .nezarazeneSpoje, .obdobiMimoPlatnost)
    // Zastávka bez odjezdů v daném směru (konečná) - stránku negenerovat. Výjimka: když odjezdy
    // existují a jen nespadly do žádného vlastního sloupce, stránka MUSÍ ven, jinak zmizí
    // i s varováním NEZARAZENE_SPOJE (celá vývěska by se tiše ztratila bez upozornění).
    i (odjezdy.%Size() = 0) && ('$d(nezarazeneSpoje)) && ($g(obdobiMimoPlatnost) = "") q ""
    // Vlastní sloupce: odjezdy, které nespadly do žádného sloupce - chybějící minuta je jinak
    // neviditelná vada, proto ji stránka nese a klient ukáže varování (návrhář i tisk).
    i pocetDefSloupcu {
        s csvNezarazene = ""
        f ni=1:1:+$g(nezarazeneSpoje) s csvNezarazene = csvNezarazene_$s(csvNezarazene="":"", 1:", ")_nezarazeneSpoje(ni)
        d vals.%Set("NEZARAZENE_SPOJE", csvNezarazene)
        // čísla spojů zvlášť - klient podle nich počítá SPOJE, ne odjezdy (tentýž spoj vypadne
        // na každé zastávce linky, takže počet časů hlásí desítky problémů místo jednoho)
        s csvSpoju = "", spX = ""
        f { s spX = $o(nezarazeneSpoje("spoj", spX))  q:spX=""  s csvSpoju = csvSpoju_$s(csvSpoju="":"", 1:",")_spX }
        d vals.%Set("NEZARAZENE_SPOJE_CISLA", csvSpoju)
        // sloupec, jehož období leží mimo platnost jízdního řádku, by jinak zůstal prázdný
        // a uživatel by neměl jak zjistit proč (šablona přežila překlopení jízdního řádku)
        d vals.%Set("OBDOBI_MIMO_PLATNOST", $g(obdobiMimoPlatnost))
    }
    i souhrnNaZn s atrSeen("x") = ..TextSouhrnuNaZnameni()   // TR2: agregátní řádek místo per-zastávka „na znamení"
    // Vývěska je samostatná stránka → legenda vysvětluje jen značky, které na TÉTO stránce reálně jsou.
    // Ostatní kategorie to mají samo (plní se z vytištěných odjezdů), rozdíly tras ne - ty se počítají
    // per zastávka nezávisle na tom, které odjezdy stránka nese (rozdělení per označník, spoj bez
    // rozpoznaného sloupce dnů). Písmena se NEpřečíslovávají, jen se zahodí nepoužitá vysvětlivka -
    // stejné písmeno tak znamená totéž na všech stránkách téže zastávky.
    d ..ProrezatLegenduRozdilu(odjezdy, .rozdilLegenda)
    d ..BezbarierKonfig(dataDB, sku, lin, .isinv, .iscbe, .allBBP)   // allBBP → agregátní legenda bezbariér (cache)
    // Zákazník si značku často vysvětluje sám vlastní poznámkou u linky („R - k jízdence…"), takže
    // jeho vysvětlivka vyhrává a naši generovanou zahodíme - jinak stojí dvě pod sebou. Musí se
    // zjistit PŘED SestavPoznamky, tam se legenda skládá.
    d ..ZnakyVysvetleneLinkou(dataDB, sku, lin, .znakyLinky)
    s poznamky = ..SestavPoznamky(.poznMap, .atrSeen, .rozdilLegenda, .bezbarLegenda, .komentareLegenda, .dnyLegenda, allBBP, .prestupSeen, .odjAtrLegenda, .znakyLinky, .pokrLegenda)

    // #13093 bod 1: textové poznámky z vlastností linky (tarifní / všeobecné / radiobus, výluky
    // zvlášť). Emitujeme **vždy** otagované kategorií; které z nich blok ukáže, drží šablona - je to
    // čistě „co se zobrazí". Výchozí stav v prvku je vypnuto, takže existujícím šablonám nenaskočí.
    d ..PoznamkyLinky(dataDB, sku, lin, poznamky)

    // směrové poznámky linky (tab MHD: poznámky tam/zpět/vše) - 1:1 legacy JRapiLinPrtMhd1 ř.440-447.
    // Tam → klíče ">" + "<>"; Zpět → "<" + "<>". Plain text bez znaku.
    s smeroveKlice = $s(smer="Z":"<,<>", 1:">,<>")
    f indexKlice=1:1:2 {
        s smerovyKlic = $p(smeroveKlice, ",", indexKlice)
        s pozicePoznamky = ""
        f {
            s pozicePoznamky = $o(^[dataDB]JRL(sku,lin,"MhD","SeT_MhD",smerovyKlic,pozicePoznamky))
            q:pozicePoznamky=""
            s textPoznamky = $g(^[dataDB]JRL(sku,lin,"MhD","SeT_MhD",smerovyKlic,pozicePoznamky))
            i textPoznamky '= "" d poznamky.%Push(..BuildPoznamka("text", "", textPoznamky, "", "", "smerove"))
        }
    }

    // ── sestavení stránky ───────────────────────────────────────────
    s out = ##class(%DynamicObject).%New()
    d out.%Set("values", vals)
    s rows = ##class(%DynamicObject).%New()
    d rows.%Set("ZASTAVKY", zastavky)
    i pocetDefSloupcu { d rows.%Set("SKUPINY_DNU", ..NacistSkupinyDnuDleDefinice(.defSloupce)) }
    else { d rows.%Set("SKUPINY_DNU", ..NacistSkupinyDnu(ftl)) }
    d rows.%Set("ODJEZDY", odjezdy)
    d rows.%Set("POZNAMKY", poznamky)
    d rows.%Set("DOPRAVCI", ..BuildDopravci(dataDB, sku, lin))
    d out.%Set("rows", rows)
    q out
}

/// Vrátí sloupce skupin dnů dle FTL (1:1 s rozvržením v `JRapiLinPrtMhd1` ř.30-34):
///   FTL=0: Prac | So | Ne+svátky                         (3 sloupce)
///   FTL=1: Prac mimo prázd | Prac o prázd | So+Ne+svátky  (3 sloupce)
///   FTL=2: Prac | So+Ne+svátky                            (2 sloupce)
///   FTL=3: Prac mimo prázd | Prac o prázd | So | Ne+svátky (4 sloupce)
ClassMethod NacistSkupinyDnu(ftl As %String) As %DynamicArray
{
    s skupiny = ##class(%DynamicArray).%New()
    i ftl = 1 {
        d skupiny.%Push(..BuildSkupinaDnu("1", "Pracovní dny mimo prázdniny", "", 1))
        d skupiny.%Push(..BuildSkupinaDnu("2", "Pracovní dny o prázdninách", "", 2))
        d skupiny.%Push(..BuildSkupinaDnu("3", "Soboty, neděle a svátky", "", 3))
    } elseif ftl = 2 {
        d skupiny.%Push(..BuildSkupinaDnu("1", "Pracovní dny", "", 1))
        d skupiny.%Push(..BuildSkupinaDnu("2", "Soboty, neděle a svátky", "", 2))
    } elseif ftl = 3 {
        d skupiny.%Push(..BuildSkupinaDnu("1", "Pracovní dny mimo prázdniny", "", 1))
        d skupiny.%Push(..BuildSkupinaDnu("2", "Pracovní dny o prázdninách", "", 2))
        d skupiny.%Push(..BuildSkupinaDnu("3", "Soboty", "", 3))
        d skupiny.%Push(..BuildSkupinaDnu("4", "Neděle a svátky", "", 4))
    } else {
        d skupiny.%Push(..BuildSkupinaDnu("1", "Pracovní dny", "", 1))
        d skupiny.%Push(..BuildSkupinaDnu("2", "Soboty", "", 2))
        d skupiny.%Push(..BuildSkupinaDnu("3", "Neděle a svátky", "", 3))
    }
    q skupiny
}

/// Spočítá odjezdy z aktuální zastávky pro daný směr.
/// Pro každý spoj: odjezd = Odjezd spoje + offset času na aktuální zastávce (z trasy spoje "W").
/// `<` na zastávce = spoj zde nestaví → přeskočit. Zařazení do sloupců dnů dle FTL+Plk+CNZ (`ZarazeniSpoje`),
/// s vlastní definicí sloupců (defSloupce z ParseDefiniceSloupcu) dle `ZarazeniSpojeDleDefinice`;
/// nezarazeneSpoje pak sbírá časy odjezdů, které nespadly do žádného sloupce (→ NEZARAZENE_SPOJE).
ClassMethod NacistOdjezdy(dataDB As %String, sku As %String, lin As %String, smer As %String, currentPoradi As %String, ftl As %String, nzCfg As %String, ByRef poznMap, ByRef pismenoSpoj, ByRef bezbarLegenda, ByRef komentareLegenda, ByRef dnyLegenda, ByRef spojPismenoVar, filtrOznacnik As %String = "", zdrojSt As %String = "OZN", ByRef odjAtrLegenda, ByRef pokrLegenda, ByRef defSloupce, ByRef nezarazeneSpoje, ByRef obdobiMimoPlatnost) As %DynamicArray
{
    s odjezdy = ##class(%DynamicArray).%New()
    i currentPoradi = "" q odjezdy

    // bezbariér konfig (per linka): isinv/iscbe = feature povolená, allBBP = všechny spoje bezbar.
    d ..BezbarierKonfig(dataDB, sku, lin, .isinv, .iscbe, .allBBP)

    // „nevypisovat NZ ve sloupci 1-4" - per linka, 4 sloty. Potlačuje **značku u minuty**, ne spoj.
    s nzNoShow = $g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NEZNZ123"))

    // období vlastních sloupců: rozsahy dat převedené na horolog proti platnosti jízdního řádku.
    // Sloupec, kterému po ořezu nezbyl žádný rozsah, se hlásí ven - jinak zmlkne bez vysvětlení.
    k obdobiSloupcu
    s obdobiMimoPlatnost = ""
    i +$g(defSloupce) {
        d ..ObdobiSloupcu(dataDB, sku, lin, .defSloupce, .obdobiSloupcu)
        s obdobiMimoPlatnost = $g(obdobiSloupcu("mimo"))
    }

    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue

        s spoj       = $g(^[dataDB]JRL(sku,lin,smer,sp))
        s hlava      = $p(spoj, "#", 1)
        s cisloTrasy = $p(hlava, ":", 3)
        s odjezd     = +$p(hlava, ":", 4)
        s plk        = $p(hlava, ":", 7)
        s cnz        = $p(spoj, "#", 7)        // negativní značka spoje (legacy arrsp dta29)

        // offset času na aktuální zastávce z trasy spoje (cachovaná W-mapa); pobytová zast. = příjezd+čekání
        d ..WMapa(dataDB, sku, lin, cisloTrasy, .wMap)
        i '..OdjizdiZde(.wMap, currentPoradi, $p(hlava, ":", 2)) continue
        s offset = ..CasHodnota($g(wMap(currentPoradi)))

        // #8828: filtr per stanoviště - na stránce jen spoje odjíždějící z daného stanoviště vývěsky.
        // Spoj úplně bez stanoviště (prázdná W-trasa i J-věta) patří na stránku NEJČETNĚJŠÍHO
        // (první v CSV) - jinak by při rozdělení zmizel ze všech stránek (ztráta odjezdu).
        // #13088: `zdrojSt` říká, jestli je stanoviště označník, nebo sloupek - filtr, titulek
        // i řádky musí brát týž údaj, jinak stránka tvrdí „sloupek 2" a filtruje podle označníku.
        if filtrOznacnik '= "" {
            s jSegmentStanoviste = $p(..PoziceStanoviste(zdrojSt), "~", 2)
            d ..WNastupisteMapa(dataDB, sku, lin, cisloTrasy, .nMapF, zdrojSt)
            s spojOzn = $g(nMapF(currentPoradi))
            i spojOzn = "" s spojOzn = $p($p($g(^[dataDB]JRL(sku,lin,"J",currentPoradi)),"#",jSegmentStanoviste),":",$s(smer="Z":2,1:1))
            i spojOzn = "" s spojOzn = $p(..NastupisteSpoju(dataDB, sku, lin, smer, currentPoradi, zdrojSt), ",", 1)
            i spojOzn '= filtrOznacnik continue
        }

        s celkem = odjezd + offset
        s hod = (celkem\60)#24, min = celkem#60

        // anotace z datumových omezení spoje (omzId 11-15, 22) - sdílená přes všechny skupiny spoje.
        // Marker = číslo negativní značky (cnz) + typ „nz" (invertovaně), 1:1 legacy gold (anotace
        // „2$<cnz>", legenda „2#<cnz>#<text>"; ověřeno KV_DP i CB_CSADAD). Text bere per-spoj omzId
        // (SestavTextOmezeni) - InPlatDef se nečte (drží zastaralý seed datumů). cnz prázdné → fallback
        // na sekvenční číslo + typ „text" (zachová staré chování pro spoj bez NZ).
        s znak = "", typOmez = "text"
        s txt = ..SestavTextOmezeni(dataDB, sku, lin, smer, sp)
        i txt '= "" {
            i cnz '= "" {
                s znak = cnz, typOmez = "nz"
                // legenda se zapíše až u sloupce, kde se značka reálně tiskne - když je potlačená
                // ve všech sloupcích spoje, nemá v legendě co dělat (legacy noNZ2)
            } else {
                s znak = ..ZiskejMarker(.poznMap, txt)
            }
        }

        // marker rozdílu trasy (z RozdilyTras) - u minuty jako první, dle legacy ZnkOm=ZnkTc_…
        // Vedle něj běží písmeno varianty trasy (kategorie „varianta-trasy" - mimo filtry rozdílů
        // a ručních komentářů): značka říká „kterým sloupcem", poznámka „kudy". Jsou nezávislé,
        // jedno druhé nevypíná - viz komentář u RozdilyTras výše.
        s pismRT = $g(pismenoSpoj(sp))
        s pismVarTx = $g(spojPismenoVar(sp))

        // atributy spoje - bezbariér (inv = hlava:15) / částečně (cbe = hlava:22); SpecZnak 72=H / 70=F
        s inv = (isinv && +$p(hlava,":",15))
        s cbe = (iscbe && +$p(hlava,":",22))
        i inv s bezbarLegenda("H") = ..TextBezbarier(allBBP)
        // #13093 bod 2: přeprava kol (hlava:19, legacy dta20 PrepJiKola) a místenkování (hlava:6, dta6).
        // U KOVED 3 602 spojů s kolem a 6 739 s R (měřeno 4. 8. na všech 17 029 linkách).
        // Emitujeme **vždy** otagované; zapnutí drží šablona
        // (prvek odjezdů), protože je to čistě „co se zobrazí" - ne to, co se má z DB dopočítat.
        s kolo = +$p(hlava,":",19)
        s mistenka = ($p(hlava,":",6) '= "")
        s pokrZnak = ..PokracovaniZnak(dataDB, sku, lin, smer, sp, .pokrLegenda, .pokrPoradi)
        i kolo s odjAtrLegenda(..ZnakKola()) = "specialni~"_..TextKola()_"~odjezd-kola"
        i mistenka s odjAtrLegenda("R") = "text~"_..TextMistenky()_"~odjezd-mistenka"
        i cbe s bezbarLegenda("F") = ..TextCastecneBezbarier()

        // ruční komentáře spoje (tab MHD: Komentáře = SeT_SpeC přiřazené spoji přes MhD_SpeC).
        // V rozsahu „Spoje, nepoznámkovat" (MHDSPNPZN) se anotace jen OZNAČÍ (vRozsahu) - co se reálně
        // schová (ruční / rozdíly tras / obojí) rozhoduje TD filtr dle nastavení šablony. Caché emituje
        // nadmnožinu (nedropuje), aby šly volit všechny varianty.
        s vRozsahu = +..NepoznamkovatSpoj(dataDB, sku, lin, sp, currentPoradi)
        s komentarZnaky = $g(^[dataDB]JRL(sku,lin,smer,sp,"MhD_SpeC"))
        f indexZnaku=1:1:$l(komentarZnaky) {
            s komentarZnak = $e(komentarZnaky, indexZnaku)
            i komentarZnak '= "" s komentareLegenda(komentarZnak) = $g(^[dataDB]JRL(sku,lin,"MhD","SeT_SpeC",komentarZnak))
        }

        // spoj může patřit do více sloupců dnů (FTL layout + prázdninové dělení dle CNZ)
        i +$g(defSloupce) { s skupiny = ..ZarazeniSpojeDleDefinice(.defSloupce, plk, cnz, smer, sp, .obdobiSloupcu) }
        else { s skupiny = ..ZarazeniSpoje(ftl, plk, cnz, nzCfg) }
        i skupiny = "" {
            // vlastní sloupce: nezařazený odjezd se hlásí (jinak tiše zmizí z vývěsky);
            // automaticky = legacy tiché přeskočení (KamPrt = 0, nerozpoznané Plk)
            // hlásí se čas i číslo spoje: tentýž spoj vypadne na každé zastávce zvlášť, takže
            // pouhý počet časů dělá z jednoho problému desítky (nález Jan 10. 8.)
            i +$g(defSloupce) {
                s nezarazeneSpoje($i(nezarazeneSpoje)) = hod_":"_$tr($j(min,2)," ",0)
                s nezarazeneSpoje("spoj", sp) = ""
            }
            continue
        }
        // Týdenní platnost - kroužkové denní značky. V režimu „automaticky" je sada dána rozvržením
        // (viz DenniZnacky), u vlastních sloupců se počítá **per sloupec**: značka se tiskne, jen
        // když spoj nepokrývá celý sloupec, a to je u každého sloupce jinak.
        i '+$g(defSloupce) {
            s dnyZnaky = ..DenniZnacky(ftl, plk)
            f di=1:1:$l(dnyZnaky) { s dnyLegenda($e(dnyZnaky,di)) = ..TextDneVTydnu($e(dnyZnaky,di)) }
        }
        s dnySpoje = ..DnySpoje(plk)
        f gi=1:1:$l(skupiny,",") {
            s idSloupce = $p(skupiny,",",gi)
            s o = ..BuildOdjezd(currentPoradi, idSloupce, hod, min)
            s idxSloupce = 0, pokryvaObdobi = 0
            i +$g(defSloupce) {
                s idxSloupce = ..IndexSloupce(.defSloupce, idSloupce)
                s dnyZnaky = ..DenniZnackyProSloupec(plk, dnySpoje, $g(defSloupce(idxSloupce,"dny")))
                f di=1:1:$l(dnyZnaky) { s dnyLegenda($e(dnyZnaky,di)) = ..TextDneVTydnu($e(dnyZnaky,di)) }
                // spoj jedoucí celé období sloupce nemá u negativní značky co vysvětlovat (#13045)
                s pokryvaObdobi = (+$g(obdobiSloupcu(idxSloupce)) > 0) && (..JizdaVObdobi(smer, sp, .obdobiSloupcu, idxSloupce, $g(defSloupce(idxSloupce,"dny"))) = 2)
            }
            // pořadí anotací 1:1 legacy ZnkOm: dny → rozdíl trasy → omezení → bezbariér → ruční komentář.
            // KATEGORIE + spoj = pro TD filtr viditelnosti (#7955) - Caché generuje nadmnožinu.
            f di=1:1:$l(dnyZnaky) d ..AddAnotace(o, "specialni", $e(dnyZnaky,di), "", "", "tydenni-platnost", sp, vRozsahu)
            i pismRT '= "" d ..AddAnotace(o, "text", pismRT, "", "", "rozdil-trasy", sp, vRozsahu)
            i pismVarTx '= "" d ..AddAnotace(o, "text", pismVarTx, "", "", "varianta-trasy", sp, vRozsahu)
            i znak '= "" {
                // vlastní sloupce nesou potlačení značky samy (per-sloupec seznam + pokrytí období);
                // linkový poziční seznam MHD_NEZNZ123 platí jen v režimu „automaticky"
                i +$g(defSloupce) { s skryto = (typOmez = "nz") && (..ZnackaSkrytaDleDefinice(.defSloupce, idSloupce, znak) || pokryvaObdobi) }
                else { s skryto = (typOmez = "nz") && ..ZnackaSkrytaVeSloupci(nzNoShow, $p(skupiny,",",gi), znak) }
                i 'skryto {
                    i typOmez = "nz" s poznMap("byMarker", znak) = txt, poznMap("typ", znak) = "nz"
                    d ..AddAnotace(o, typOmez, znak, "", "", "omezeni", sp, vRozsahu)
                }
            }
            i inv && 'allBBP d ..AddAnotace(o, "specialni", "H", "", "", "bezbarier", sp, vRozsahu)   // u allBBP se marker netiskne, jen legenda
            i cbe d ..AddAnotace(o, "specialni", "F", "", "", "bezbarier", sp, vRozsahu)
            i kolo d ..AddAnotace(o, "specialni", ..ZnakKola(), "", "", "odjezd-kola", sp, vRozsahu)
            i mistenka d ..AddAnotace(o, "text", "R", "", "", "odjezd-mistenka", sp, vRozsahu)
            i pokrZnak '= "" d ..AddAnotace(o, "text", pokrZnak, "", "", "pokracovani", sp, vRozsahu)
            f indexZnaku=1:1:$l(komentarZnaky) { s komentarZnak = $e(komentarZnaky, indexZnaku)  i komentarZnak '= "" d ..AddAnotace(o, "text", komentarZnak, "", "", "rucni-komentar", sp, vRozsahu) }
            d odjezdy.%Push(o)
        }
    }
    q odjezdy
}

/// Má se negativní značka `nz` potlačit **u minut** ve sloupci `sloupec` (1-4)? Per-linkový seznam
/// `MHD_NEZNZ123` (4 sloty `~`, v každém čísla značek oddělená čárkou) - 1:1 legacy
/// `noNZ^JRapiLinPrtMhd1` ř.340. Slot N patří sloupci N (na rozdíl od `MHD_NENZ1_NENZ2`, kde má
/// legacy sloty prohozené).
///
/// ⚠️ Potlačuje se **jen značka**, spoj se tiskne dál. Legacy to tak má záměrně po chybě:
/// zakomentovaný blok (`JRapiLinPrtMhd1` ř.246-251) ukazuje, že se tím spoj dřív přeskakoval celý
/// a „NETISKLY SE ČASY". Skrytí celého spoje ze sloupce je jiné nastavení (`MHD_NENZ1_NENZ2`).
ClassMethod ZnackaSkrytaVeSloupci(cfg As %String, sloupec As %String, nz As %String) As %Boolean
{
    i (cfg = "") || (nz = "") || (sloupec = "") ret 0
    ret (","_$p(cfg, "~", sloupec)_",") [ (","_(+nz)_",")
}

/// Konfigurace bezbariérových atributů spoje (1:1 `JRapiLinPrtMhd` ř.317-318, `AllSpIsBBP` ř.953):
///   isinv = feature „invalida" povolená (`^JRSET("PS","LIMES")` obsahuje „invalida")
///   iscbe = feature „invalidaspomoc" (částečně bezbariér)
///   allBBP = všechny spoje obou směrů mají příznak hlava:15 → legenda „všechny spoje…", bez markeru u minut
ClassMethod BezbarierKonfig(dataDB As %String, sku As %String, lin As %String, ByRef isinv, ByRef iscbe, ByRef allBBP)
{
    // invariant per linka → cache (v GetData se NacistOdjezdy volá per stránka)
    i $d(^||TiskAd("bezbar",dataDB,sku,lin)) {
        s cfg = ^||TiskAd("bezbar",dataDB,sku,lin)
        s isinv = $lg(cfg,1), iscbe = $lg(cfg,2), allBBP = $lg(cfg,3)
        q
    }

    s isinv = 0, iscbe = 0
    s cnt = ""
    f {
        s cnt = $o(^[dataDB]JRSET("PS","LIMES",cnt))
        q:cnt=""
        s val = $g(^[dataDB]JRSET("PS","LIMES",cnt))
        i val [ "invalidaspomoc" s iscbe = 1
        i val [ "invalida" s isinv = 1
    }

    // všechny spoje bezbariérové? (každý spoj obou směrů má hlava:15)
    s allBBP = isinv, maSpoj = 0
    f sm="T","Z" {
        s sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,sm,sp))
            q:sp=""
            i '$isvalidnum(sp) continue
            s maSpoj = 1
            i '+$p($p($g(^[dataDB]JRL(sku,lin,sm,sp)),"#",1),":",15) s allBBP = 0  q
        }
        i 'allBBP q
    }
    i 'maSpoj s allBBP = 0

    s ^||TiskAd("bezbar",dataDB,sku,lin) = $lb(isinv,iscbe,allBBP)
}

/// Naplní `served(poradi)` zastávkami, kde staví aspoň jeden spoj daného směru (W-offset != "<").
/// Slouží k filtru projížděných zastávek ze StopListu (jako legacy).
/// Invariant per (linka×směr) → cache `^||TiskAd("served",…)`; v GetData se volá per stránka.
ClassMethod ObsluhovanePoradi(dataDB As %String, sku As %String, lin As %String, smer As %String, ByRef served)
{
    k served
    i $d(^||TiskAd("served",dataDB,sku,lin,smer)) {
        m served = ^||TiskAd("served",dataDB,sku,lin,smer)
        q
    }
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s cisloTrasy = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
        s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
        f i=1:1:$l(wTrasa,"#") {
            s seg = $p(wTrasa,"#",i)
            s pp = $p(seg,":",1), cas = $p(seg,":",2)
            i (pp '= "") && (cas '= "<") && (cas '= "") && (cas '= "|") s served(pp) = ""   // `|` = spoj nestaví (jako `<`)
        }
    }
    i $d(served) m ^||TiskAd("served",dataDB,sku,lin,smer) = served     // prázdné necachovat (merge z undef = <UNDEFINED>)
}

/// Naplní `served(poradi)` zastávkami, kde staví **aspoň jeden spoj, který staví i na vývěskové
/// zastávce** (`currentPoradi`) - tedy zastávkami dosažitelnými odtud.
///
/// Proti `ObsluhovanePoradi` (stačí, že tam staví kdokoli ze směru) je to zúžení. Zastávku, kam se
/// z téhle vývěsky nedá dojet, nemá cestující jak použít: v seznamu zabírala řádek, kde je `<`
/// v sloupci časů i ve všech variantách. Požadavek TRANSDEV #7959 („aby zastávka nebyla v seznamu,
/// pokud není aktuální") - směrově globální filtr z 30. 7. ten školní scénář nepokryl a dokončení
/// se odložilo, protože se nedařilo najít testovací linku. Doloženo na `CB_CSADAD` lince `0330530`
/// (Střítež,žel.st.točna). **MHD tuhle sémantiku má odjakživa** (`ObsluhaBitmap`), takže se tím
/// agendy zároveň srovnávají.
///
/// ⚠️ Důsledek: seznam zastávek se **liší mezi vývěskami téže linky** - je to cena za to, že každá
/// vývěska mluví jen o tom, co z ní jede (a přesně to zákazník žádal).
///
/// Invariant per (linka×směr×vývěska) → cache `^||TiskAd("servedOd",…)`.
ClassMethod ObsluhovanePoradiOd(dataDB As %String, sku As %String, lin As %String, smer As %String, currentPoradi As %String, ByRef served)
{
    k served
    // Bez vývěskové zastávky není od čeho měřit (sonda, starší volání) → původní směrový filtr.
    i currentPoradi = "" {
        d ..ObsluhovanePoradi(dataDB, sku, lin, smer, .served)
        q
    }
    i $d(^||TiskAd("servedOd",dataDB,sku,lin,smer,currentPoradi)) {
        m served = ^||TiskAd("servedOd",dataDB,sku,lin,smer,currentPoradi)
        q
    }
    k trasyOdtud
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s ct = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
        i ct = "" continue
        i $d(trasyOdtud(ct)) continue                    // trasu už známe (W-mapa je per trasa)
        k wMapS
        d ..WMapa(dataDB, sku, lin, ct, .wMapS)
        i ..CasHodnota($g(wMapS(currentPoradi))) = "" { s trasyOdtud(ct) = 0  continue }   // odtud nejede
        s trasyOdtud(ct) = 1
        s pp = ""
        f {
            s pp = $o(wMapS(pp))
            q:pp=""
            i '$isvalidnum(pp) continue
            i ..CasHodnota($g(wMapS(pp))) '= "" s served(pp) = ""
        }
    }
    i $d(served) m ^||TiskAd("servedOd",dataDB,sku,lin,smer,currentPoradi) = served
}

/// Značka pokračování spoje pro daný spoj, nebo prázdno (#13093 bod 3).
///
/// Zdroj `^JRL(sku,lin,smer,spoj,34)`, tvar `u@<náš spoj>:<linka>:<spoj>:<cíl>:…` - značka `u@` je
/// táž „u", kterou pro pokračování používá legacy (`JRapiLinPrtWin` ř.1561). **Zastávka v záznamu je
/// cíl** (konečná pokračujícího spoje), ověřeno 5. 8.: spoj 251 linky 0773165 má v záznamu
/// Otrokovice,Štěrkoviště a trasa jeho pokračování tam končí, kdežto ostatní končí na žel.st.
/// Nemusíme tedy dohledávat trasu pokračujícího spoje - a tím padá i to, že pokračující linka
/// může ležet v jiné skupině linek.
///
/// ⚠️ **Nepoužívat uzel 32** - to je návaznost (`m@…`, jiný tvar, nese i vlaky), ne pokračování.
///
/// Odjezdy se stejným **(linka, cíl)** dostanou tutéž značku `u1`, `u2`… - klient výslovně chce
/// neřešit číslo spoje. Číslování je per stránka v pořadí prvního výskytu (stabilní a reprodukovatelné,
/// stejný princip jako písmena rozdílů tras). Číslice se dává vždy, i u jediného cíle.
ClassMethod PokracovaniZnak(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String, ByRef legenda, ByRef poradi) As %String
{
    s zapis = $g(^[dataDB]JRL(sku,lin,smer,sp,34))   // 34 = pokračuje (NE 32, to je návaznost)
    i zapis = "" ret ""
    s rec = $p(zapis, "@", 2)
    s pLin = $p(rec, ":", 2), pCil = $p(rec, ":", 4)
    i (pLin = "") || (pCil = "") ret ""
    s klic = pLin _ "|" _ pCil
    i $d(poradi(klic)) ret poradi(klic)
    s cislo = $g(poradi) + 1, poradi = cislo
    s znak = "u" _ cislo
    s poradi(klic) = znak
    s legenda(znak) = ..TextPokracovani(pLin, pCil)
    ret znak
}

/// Text vysvětlivky pokračování: „pokračuje do zast. <cíl> (l. <trojčíslí>)".
/// Klient chce **poslední trojčíslí** linky, ne celé číslo („l. 304", ne „l. 824532").
ClassMethod TextPokracovani(linka As %String, cil As %String) As %String
{
    s trojcisli = $s($l(linka) > 3: $e(linka, $l(linka) - 2, $l(linka)), 1: linka)
    ret "pokračuje do zast. " _ cil _ " (l. " _ trojcisli _ ")"
}

/// Znak, kterým se u odjezdu kreslí **přeprava jízdních kol** (#13093 bod 2).
///
/// „K" je **náš marker, ne písmeno číselníku** - kola jsou atribut spoje a číselník atributů pro ně
/// znak nemá. Na glyf kola ho přeloží `StopScheduleElementBase.SymbolGlyph` („K" → `L`, 0x4C);
/// posílat rovnou `L` nelze, to je v číselníku vlak a přeložilo by se na lokomotivu.
/// Pozice ověřená 3. 8. vytažením obrysů z `Fonts/TimeTable2.ttf`, ne odhadem podle písmene.
/// Místenkování „R" překlad nepotřebuje: kreslí se jako **prostý text** (tak ho zapisuje i zákazník).
ClassMethod ZnakKola() As %String
{
    ret "K"
}

/// Textové poznámky z vlastností linky (#13093 bod 1). Zdroj `^JRL(sku,lin,"C",key,idx)`, kategorie
/// dle klíče: 1 = tarifní, 3 = všeobecné, 4 = radiobus; **klíč 2 se přeskakuje** (legacy dtto, viz
/// `LinkovyJrAd.PridatPoznamkyLinky`).
///
/// **Záznam = jedna poznámka**, ne slepený blok jako legacy - jinak nejde oddělit výluka od ostatních
/// textů téže kategorie (KOVED má ve „všeobecných" tři výluky i „R - k jízdence je možné zakoupit
/// místenku", měřeno 3. 8.).
ClassMethod PoznamkyLinky(dataDB As %String, sku As %String, lin As %String, poznamky As %DynamicArray)
{
    s key = ""
    f {
        s key = $o(^[dataDB]JRL(sku,lin,"C",key))
        q:key=""
        i key = 2 continue
        s kategorie = $case(key, 1:"poznamka-linky-tarifni", 4:"poznamka-linky-radiobus", :"poznamka-linky-vseobecne")
        s idx = ""
        f {
            s idx = $o(^[dataDB]JRL(sku,lin,"C",key,idx))
            q:idx=""
            s txt = ..TextPoznamkyLinky($g(^[dataDB]JRL(sku,lin,"C",key,idx)))
            continue:txt=""
            s kat = $s(..JeVyluka(txt):"vyluka", 1:kategorie)
            d poznamky.%Push(..BuildPoznamka("text", "", txt, "", "", kat))
        }
    }
}

/// Značky, které si zákazník vysvětluje **sám** v poznámkách u linky (zjištěno 4. 8. na vývěsce).
/// Konvence: poznámka začíná „<znak> - …" - KOVED má „R - k jízdence je možné zakoupit místenku
/// i pro přepravu jízdních kol na https://rezervace.idzk.cz". Jeho text je bohatší než náš
/// generovaný, takže **vyhrává on** a naši vysvětlivku pro tentýž znak zahodíme; jinak vývěska
/// vysvětluje totéž dvakrát pod sebou.
///
/// Záměrně platí **jen na atributy odjezdu** (kola, místenka). Rozdílové poznámky tras mají písmena
/// `A`, `B`, `C`… **přiřazená námi za běhu** - kdyby je zákazníkova poznámka „A - …" umlčela, zmizí
/// vysvětlivka, na kterou se odkazuje značka u minuty, a vývěska přestane být čitelná.
ClassMethod ZnakyVysvetleneLinkou(dataDB As %String, sku As %String, lin As %String, ByRef znaky)
{
    s key = ""
    f {
        s key = $o(^[dataDB]JRL(sku,lin,"C",key))
        q:key=""
        i key = 2 continue
        s idx = ""
        f {
            s idx = $o(^[dataDB]JRL(sku,lin,"C",key,idx))
            q:idx=""
            s txt = ..TextPoznamkyLinky($g(^[dataDB]JRL(sku,lin,"C",key,idx)))
            s znak = ..ZnakVysvetlivky(txt)
            i znak '= "" { s znaky(znak) = 1 }
        }
    }
}

/// Vysvětluje tato poznámka linky nějakou značku, a kterou? Vrátí znak, nebo prázdno.
/// Konvence „<znak> - text": jednoznakové první slovo a pomlčka za ním. Pomlčka je záměrně
/// **povinná** - bez ní by „V úseku…" vypadalo jako vysvětlivka značky „V".
ClassMethod ZnakVysvetlivky(txt As %String) As %String
{
    s prvniSlovo = $p(txt, " ", 1)
    i $l(prvniSlovo) '= 1 ret ""
    i $p(txt, " ", 2) '= "-" ret ""
    ret prvniSlovo
}

/// Text poznámky linky pro vývěsku: legacy značka `$$` (TrNz) pryč, `#` je oddělovač odstavců a na
/// vývěsce z něj děláme mezeru, protože legenda text zalamuje sama.
ClassMethod TextPoznamkyLinky(txt As %String) As %String
{
    s t = $replace($replace(txt, "$$", ""), "#", " ")
    ret $zstrip(t, "<>W")
}

/// Je to výluková poznámka? Konvence zákazníka: text začíná „Výluka" - tak to posílají i do CIS
/// (166 z 800 linek KOVED, měřeno 3. 8.). Přesná shoda je zaručená cesta, malá/velká varianta bonus.
ClassMethod JeVyluka(txt As %String) As %Boolean
{
    s prefix = $e(txt, 1, 6)
    ret (prefix = "Výluka") || ($zcvt(prefix, "L") = "výluka")
}

/// Spojené výlukové texty linky do jednoho pole (#13088 - klient žádal „databázovou položku, kterou
/// by mohli používat v návrháři", protože si je tiskne ve zvláštním poli). Oddělovač = nový řádek,
/// aby šly položit jako víceřádkový text. Prázdno = linka žádnou výluku nemá.
ClassMethod VylukoveTexty(dataDB As %String, sku As %String, lin As %String) As %String
{
    s vysledek = ""
    s key = ""
    f {
        s key = $o(^[dataDB]JRL(sku,lin,"C",key))
        q:key=""
        i key = 2 continue
        s idx = ""
        f {
            s idx = $o(^[dataDB]JRL(sku,lin,"C",key,idx))
            q:idx=""
            s txt = ..TextPoznamkyLinky($g(^[dataDB]JRL(sku,lin,"C",key,idx)))
            continue:txt=""
            continue:'..JeVyluka(txt)
            s vysledek = vysledek_$s(vysledek="":"", 1:$c(13,10))_txt
        }
    }
    ret vysledek
}

/// Sloupek zastávky z trasy linky - J-věta `#`-segment **12** ve tvaru `sloupekTam:sloupekZpet`
/// (legacy `JRapiLin` ř.700 `dta14`), vybraný dle směru. Je to **odkaz do číselníku sloupků
/// zastávky** (`^JRZ(czst,"Sloupek",id)` = číslo~GPS~popis směru), tedy jiný údaj než označník
/// (segment 3), který je volný text. Zákazníci, kteří sloupky evidují, tisknou tento údaj.
/// Per-spoj přebití (W pozice 13) tento krok ještě nedělá - viz roadmapa `B35b`.
ClassMethod SloupekZTrasy(veta As %String, jeTam As %Boolean) As %String
{
    ret $p($p(veta, "#", $p(..PoziceStanoviste("SL"), "~", 2)), ":", $s(jeTam:1, 1:2))
}

/// Nástupiště zastávky posbíraná ze SPOJŮ směru - legacy pravidlo „nástupiště ze spoje,
/// není-li, tak z linky" (`JRapiExp10` ř.1222-1226: `trs(8)`, fallback J-věta `#3` dle směru).
/// ⚠️ `trs(8)` = W-segment pozice **7** - čas/čekání z W pozice 2 se v legacy rozpadá na dvě
/// pole (`JRapiSp` ř.234-241), takže trs indexy jsou od W pozic posunuté o 1.
/// Vrací CSV distinct hodnot seřazených dle četnosti sestupně (shoda → menší hodnota),
/// první položka = nejčetnější. "" když žádný spoj nástupiště nemá. KOVED #13048 bod 4.
ClassMethod NastupisteSpoju(dataDB As %String, sku As %String, lin As %String, smer As %String, poradi As %String, zdroj As %String = "OZN") As %String
{
    i poradi = "" q ""
    i '$d(^||TiskAd("nastCsv",zdroj,dataDB,sku,lin,smer)) d ..NastupisteAgregace(dataDB, sku, lin, smer, zdroj)
    q $g(^||TiskAd("nastCsv",zdroj,dataDB,sku,lin,smer,poradi))
}

/// Pozice údaje „odkud spoj odjíždí" v datech podle zdroje: `OZN` = označník (W 7 / J-věta #3),
/// `SL` = sloupek (W 13 / J-věta #12). Vrací `wPozice~jSegment`. Jediné místo, kde jsou ta čísla -
/// agregace, W-mapa i fallback z trasy si je berou odsud, aby se nemohly rozejít.
ClassMethod PoziceStanoviste(zdroj As %String) As %String
{
    ret $s(zdroj = "SL" : "13~12", 1 : "7~3")
}

/// Popis sloupku z číselníku zastávky (`^JRZ(czst,"Sloupek",id)` `~`-pole 4, např. „směr Brno přes UH").
/// "" když zastávka číselník nemá nebo sloupek není vyplněný.
ClassMethod PopisSloupku(dataDB As %String, czst As %String, sloupek As %String) As %String
{
    i (czst = "") || (sloupek = "") ret ""
    // Sloupky má každá časová varianta zastávky vlastní (registr je tak ukládá); varianta bez nich
    // dědí hlavní záznam, jinak by k novému názvu patřil starý popis směru.
    d ..ZaznamZastavky(dataDB, czst, .od)
    i (od '= "") && $d(^[dataDB]JRZ(czst, "CasoveVariantyUdaju", od, "Sloupek")) {
        ret $p($g(^[dataDB]JRZ(czst, "CasoveVariantyUdaju", od, "Sloupek", sloupek)), "~", 4)
    }
    ret $p($g(^[dataDB]JRZ(czst, "Sloupek", sloupek)), "~", 4)
}

/// Agregace nástupišť per zastávka přes všechny spoje směru → cache `^||TiskAd("nastCsv",…)`
/// (invariant per linka×směr; v GetData se volá per stránka). Spoj v zastávce nestaví
/// (`<` / `|` / prázdné) → nepočítá se (stejná sémantika jako legacy `JRapiExp10` ř.272-275).
ClassMethod NastupisteAgregace(dataDB As %String, sku As %String, lin As %String, smer As %String, zdroj As %String = "OZN")
{
    k agg
    s jeTamSmer = (smer = "T")
    s pozice = ..PoziceStanoviste(zdroj)
    s wPoz = $p(pozice,"~",1), jSeg = $p(pozice,"~",2)
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s cisloTrasy = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
        i cisloTrasy = "" continue
        s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
        f i=1:1:$l(wTrasa,"#") {
            s seg = $p(wTrasa,"#",i)
            s pp = $p(seg,":",1), cas = $p(seg,":",2)
            i (pp = "") || (cas = "") || (cas = "<") || (cas = "|") continue
            s nast = $p(seg,":",wPoz)
            i nast = "" s nast = $p($p($g(^[dataDB]JRL(sku,lin,"J",pp)),"#",jSeg),":",$s(jeTamSmer:1,1:2))
            i nast = "" continue
            s agg(pp,nast) = $g(agg(pp,nast)) + 1
        }
    }

    s ^||TiskAd("nastCsv",zdroj,dataDB,sku,lin,smer) = 1     // marker „spočítáno" (i když nic nevyšlo)
    s pp = ""
    f {
        s pp = $o(agg(pp)) q:pp=""
        k cetnosti m cetnosti = agg(pp)
        s ^||TiskAd("nastCsv",zdroj,dataDB,sku,lin,smer,pp) = ..NastupisteCsv(.cetnosti)
    }
}

/// Čisté řazení: `cetnosti(nastupiste) = počet spojů` → CSV sestupně dle četnosti,
/// shoda četností → menší hodnota dřív (numerická kolace subscriptů). "" pro prázdný vstup.
ClassMethod NastupisteCsv(ByRef cetnosti) As %String
{
    k dleCetnosti
    s nast = ""
    f {
        s nast = $o(cetnosti(nast)) q:nast=""
        s dleCetnosti(-cetnosti(nast),nast) = ""
    }
    s csv = ""
    s c = ""
    f {
        s c = $o(dleCetnosti(c)) q:c=""
        s nast = ""
        f {
            s nast = $o(dleCetnosti(c,nast)) q:nast=""
            s csv = csv_$s(csv="":"",1:",")_nast
        }
    }
    q csv
}

/// W-mapa `poradi → cas` pro danou trasu, cachovaná per (linka × `cisloTrasy`) (`^||TiskAd("wmap",…)`).
/// Trasa je sdílená napříč spoji i stránkami → parsuje se tak jen jednou (dřív lineární `$p` sken
/// W-stringu při každém lookupu/spoji/stránce). `cas` může být číslo, `<` (projíždí) nebo "" (mimo).
/// ⚠️ `cisloTrasy` je číslo lokální v rámci linky (různé linky mají trasu „1") → linka MUSÍ být
/// v klíči cache. Bez ní si linky mapy navzájem přebíjejí a vznikají falešné rozdíly tras, jakmile
/// jeden proces čte víc linek (tisk jede jednu, ale sondy a dávkové čtení ne).
ClassMethod WMapa(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String, ByRef wMap)
{
    k wMap
    i cisloTrasy = "" q                                  // spoj bez trasy → prázdná mapa (^|| nebere prázdný subscript)
    i $d(^||TiskAd("wmap",dataDB,sku,lin,cisloTrasy)) { m wMap = ^||TiskAd("wmap",dataDB,sku,lin,cisloTrasy)  q }
    s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
    f i=1:1:$l(wTrasa,"#") {
        s seg = $p(wTrasa,"#",i)
        s p = $p(seg,":",1)
        i p = "" continue
        s wMap(p) = $p(seg,":",2)
        // atributy spoje v zastávce (legacy arrsp("trs",Tc,6), JRapiSp.mac:239) - „V" = jen výstup
        i $p(seg,":",5) '= "" s wMap(p,"atr") = $p(seg,":",5)
    }
    i $d(wMap) m ^||TiskAd("wmap",dataDB,sku,lin,cisloTrasy) = wMap   // prázdnou trasu necachovat (merge z undef = <UNDEFINED>)
}

/// W-mapa `poradi → nástupiště` pro danou trasu, cachovaná per (linka × `cisloTrasy`)
/// (`^||TiskAd("wnast",…)`; linka v klíči je povinná - viz `WMapa`).
/// Nástupiště = W pole 7 (⚠️ legacy `arrsp("trs",tc,8)` - indexy posunuté o +1, JRapiSp ř.234-241).
/// Bez J-fallbacku - ten je per zastávka×směr a řeší ho volající (fallback nerozlišuje varianty).
ClassMethod WNastupisteMapa(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String, ByRef nMap, zdroj As %String = "OZN")
{
    k nMap
    i cisloTrasy = "" q                                  // spoj bez trasy → prázdná mapa (^|| nebere prázdný subscript)
    i $d(^||TiskAd("wnast",zdroj,dataDB,sku,lin,cisloTrasy)) { m nMap = ^||TiskAd("wnast",zdroj,dataDB,sku,lin,cisloTrasy)  q }
    s wPoz = $p(..PoziceStanoviste(zdroj),"~",1)
    s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
    f i=1:1:$l(wTrasa,"#") {
        s seg = $p(wTrasa,"#",i)
        s p = $p(seg,":",1)
        i p '= "" s nMap(p) = $p(seg,":",wPoz)
    }
    i $d(nMap) m ^||TiskAd("wnast",zdroj,dataDB,sku,lin,cisloTrasy) = nMap
}

/// Výchozí sada písmen **variantních sloupců** - vzestupně a disjunktně proti sadě rozdílů tras
/// (`SadaRozdiluDefault`) i proti ostatním značkám u minuty.
///
/// ⚠️ Obě řady se do 3. 9. kryly: varianty brala zadrátovaná abeceda `A, B, C, D…`, rozdíly
/// `BCEFGHIJOQRWeijqsvwxz`, takže od druhé varianty výš mělo **jedno písmeno u minuty dva významy**
/// a legenda ho vysvětlila cizím textem (spoj variantou „E" si čtenář spároval s „jede jen do
/// zast. Babice"). Na vývěsce pro cestující je to dezinformace, ne kosmetika.
///
/// Vyškrtané je vše, co u minuty jako značka už je: sada rozdílů **včetně malých písmen** (velikost
/// se nerozlišuje, „V" vedle „v" ve výšce písma odjezdů nikdo nerozezná), `R` místenka, `K` kola,
/// `u` pokračující spoj, `H`/`F` bezbariér. `A` v sadě rozdílů není, proto zůstává první -
/// linka se dvěma variantami se tím nezmění a řady se rozejdou až u třetí (rozhodnutí Jan 2. 9.).
Parameter SadaVariantDefault = "ADLMNPTY";

/// Výchozí sada písmen **rozdílů tras**. 1:1 legacy (`GetZnk^JRapiLinPrtMhd`, `JRapiLinPrtMhd.mac:539`)
/// a záměrně bez jediné změny - je to parita se starým modulem.
Parameter SadaRozdiluDefault = "BCEFGHIJOQRWeijqsvwxz";

/// Sada písmen variantních sloupců pro linku: **prioritně z nastavení u linky**, jinak výchozí.
/// U linky ji drží „Volné značky pro minutové trasy spojů" (EDISON → linka → Nastavení k tisku →
/// Odjezdník MHD, wire klíč `ODJEZDNIK_ZNK_CAS_PRUBEHY_TRAS`).
///
/// ⚠️ Bere se **doslova, bez filtrování kolizí** (rozhodnutí Jan 2. 9.): kdo si napíše „ABCDE",
/// dostane to i s kolizí proti rozdílům tras. Je to jeho volba a tiše mu do ní sahat je horší;
/// východisko je, že nastavitelné jsou **obě** sady, takže lze přesunout tu druhou.
///
/// ⚠️ Vyplněné pole zapne písmena i ve **starém modulu** - ten pro ně nemá žádný výchozí obsah
/// (`JRapiLinPrtMhd1.mac:27`, „UVWXYZ" je jen komentář), takže dnes variantní značky netiskne vůbec.
/// Paritu neporušuje ani jedna volba, ale kdo pole vyplní kvůli nám, uvidí písmena i ve staré sestavě.
ClassMethod SadaVariant(dataDB As %String, sku As %String, lin As %String) As %String
{
    s sada = $g(^[dataDB]JRL(sku,lin,"PATCH","mhd","par",2))
    i sada = "" s sada = ..#SadaVariantDefault
    q sada
}

/// Písmeno variantního sloupce (1-based pořadí sloupce) ze zadané sady; `prvniBez` → první sloupec
/// (hlavní varianta) bez písmene a sada běží od druhého. Přes délku sady se písmena **zdvojují**
/// (`DD`, `LL`) přes <method>GetZnk</method> - stejně jako u rozdílů tras a jako v legacy.
/// Prázdná sada → prázdné písmeno (radši nic než značka, kterou nejde vysvětlit).
ClassMethod PismenoVarianty(poradiSloupce As %Integer, prvniBez As %Boolean = 1, sada As %String = "") As %String
{
    i sada = "" s sada = ..#SadaVariantDefault
    s idx = poradiSloupce - $s(prvniBez:1, 1:0)
    i idx <= 0 q ""
    q ..GetZnk(sada, idx)
}

/// Vrátí `cisloTrasy` **hlavní (nejčastější)** trasy mezi spoji stavícími na vývěskové zastávce
/// (`currentPoradi`). Základ pro časy jízdy ve StopListu (jeden sloupec, jako MHD `TrasaMin1`).
/// Hlavní = nejvíc spojů (1:1 legacy „označím trasu s největším počtem spojů" `JRapiLinPrtMhd` ř.108-127)
/// - vyhne se tomu, aby expresní spoj projíždějící uzly (`<`) určoval časy. "" když nikdo nestaví.
ClassMethod ZakladniCisloTrasy(dataDB As %String, sku As %String, lin As %String, smer As %String, currentPoradi As %String) As %String
{
    s jeTam = (smer '= "Z")
    k pocet, pokryti, delka, zamitnuto
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s ct = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
        i ct = "" continue
        i $d(zamitnuto(ct)) continue
        i $d(pocet(ct)) { s pocet(ct) = pocet(ct) + 1  continue }   // trasu už známe (W-mapa je per trasa), jen spoje
        d ..WMapa(dataDB, sku, lin, ct, .wMap)
        s c = $g(wMap(currentPoradi))
        i (c = "") || (c = "<") || ('$isvalidnum(c)) { s zamitnuto(ct) = ""  continue }   // na vývěsce nestaví
        s pocet(ct) = 1
        // Pokrytí = kolik zastávek trasa obslouží OD vývěsky dál (= kolik řádků seznamu vyplní),
        // délka = nejvzdálenější kumulativní čas. Zastávky před vývěskou se nepočítají - sloupec
        // se plní od ní dolů, takže trasa s hromadou zastávek před ní by nepomohla.
        s n = 0, maxT = 0, pp = ""
        f {
            s pp = $o(wMap(pp))
            q:pp=""
            i '$isvalidnum(pp) continue
            i '..JeVUsekuVariant(pp, currentPoradi, jeTam) continue
            i ..CasHodnota($g(wMap(pp))) = "" continue
            s n = n + 1
            s t = ..CasJizdyZMapy(.wMap, currentPoradi, pp)
            i t > maxT s maxT = t
        }
        s pokryti(ct) = n, delka(ct) = maxT
    }
    q ..VyberZaklTrasu(.pokryti, .delka, .pocet)
}

/// Vybere trasu pro **jediný** sloupec časů: největší pokrytí seznamu, při shodě delší jízda,
/// pak víc spojů, pak nejnižší číslo trasy (determinismus).
///
/// ⚠️ **Nejčetnější trasa je špatné kritérium** - nejvíc spojů má často zkrácená trasa, takže sloupec
/// skončil v polovině seznamu a zbytek zůstal prázdný (hlásil TRANSDEV, #12843). Starý modul bere
/// v automatickém režimu **nejdelší** spočítaný průběh (`JRapiLinPrtMhd1.mac:330` řadí průběhy od
/// největšího a jedničku dá do sloupce „Časy"), takže tohle je oprava odchylky, ne nová volba.
/// Pokrytí měříme přímo (počet obsloužených zastávek), legacy ho aproximuje délkou textu průběhu.
///
/// Variantní sloupce jedou podle **téhož pravidla** (dosah, při shodě delší jízda) - jen tam mají
/// členové varianty dosah shodný z definice, takže rozhoduje `DelkaProfilu`. Jedno pravidlo pro obě
/// rodiny sloupců je záměr, ne shoda: obojí má být skutečný průběh jednoho spoje.
ClassMethod VyberZaklTrasu(ByRef pokryti, ByRef delka, ByRef pocet) As %String
{
    s baseCt = "", bestN = -1, bestT = -1, bestP = -1, ct = ""
    f {
        s ct = $o(pokryti(ct), 1, n)
        q:ct=""
        s t = +$g(delka(ct)), p = +$g(pocet(ct))
        s lepsi = $s(n > bestN:1, (n = bestN) && (t > bestT):1, (n = bestN) && (t = bestT) && (p > bestP):1, 1:0)
        i lepsi s baseCt = ct, bestN = n, bestT = t, bestP = p
    }
    q baseCt
}

/// Numerická hodnota času z W-segmentu. Pobytová zastávka má formát `příjezd/čekání` (např. `33/4`)
/// → vrací **odjezd = příjezd + čekání** (1:1 legacy `Cas+Ceka` / `Cas+$P(Cas,"/",2)`). Běžná `27` → 27.
/// "" když spoj zastávku neobsluhuje: mimo trasu (prázdné), projíždí (`<`), nebo `|` (zastávka na
/// trase, ale spoj v ní nestaví - legacy `JRapiExp10` ř.272-275 přeskakuje `<`/`|`/prázdné shodně;
/// dřívější `+"|"` = 0 tisklo falešný čas 0).
ClassMethod CasHodnota(cas As %String) As %String
{
    i (cas = "") || (cas = "<") || (cas = "|") q ""
    q +$p(cas,"/",1) + +$p(cas,"/",2)        // "33/4"→37, "27"→27 (piece2 prázdné = +0)
}

/// Odjíždí spoj z vývěskové zastávky? Ne, když tu nestaví, když tu **končí** (`tcDo` = 2. pole
/// hlavičky spoje) nebo když tu má příznak **jen výstup** (`V`). 1:1 legacy `JRapiLinPrtMhd1.mac`
/// ř.194 (`I Tc=TcDo Q`) a ř.201 (`I AtrSpTc["V" q`).
///
/// ⚠️ Do 25. 9. se kontrolovalo jen „nestaví" a vývěska tiskla i odjezd, se kterým cestující nikam
/// nedojede: na konečné spoje (TRANSDEV #12843, Nitra linka 32 Kmeťova) a v zastávce pro výstup,
/// která je v datech častá (VS_CSAD skoro 30 000 zastávek spojů, obvykle ta před konečnou).
ClassMethod OdjizdiZde(ByRef wMap, currentPoradi As %String, tcDo As %String) As %Boolean
{
    i ..CasHodnota($g(wMap(currentPoradi))) = "" q 0
    i (tcDo '= "") && (+tcDo = +currentPoradi) q 0
    i $g(wMap(currentPoradi,"atr")) [ "V" q 0
    q 1
}

/// Kumulativní doba jízdy (min) ze zastávky `fromPoradi` na `poradi` z W-mapy (`poradi → cas`).
/// = `cas(poradi) - cas(fromPoradi)` (reference od vývěskové zastávky, 1:1 legacy TrasaMin / MHD).
/// 0 když některá zastávka není na trase / spoj projíždí (`<`).
ClassMethod CasJizdyZMapy(ByRef wMap, fromPoradi As %String, poradi As %String) As %Integer
{
    s cFrom = ..CasHodnota($g(wMap(fromPoradi)))
    s cTo   = ..CasHodnota($g(wMap(poradi)))
    i (cFrom = "") || (cTo = "") q 0
    q cTo - cFrom
}

/// Hodnota sloupce „Čas jízdy" pro jednu zastávku - **tři stavy jako `TRASA_MIN`**: číslo (základní
/// trasa tu staví), „<" (projíždí bez stání) nebo prázdno (mimo základní trasu). Dřív bylo prázdno
/// i pro projíždění, takže z jediného sloupce nebylo vidět, že tudy spoj vůbec jede - legacy tam „<"
/// má (`JRapiLinPrtMhd1.mac` ř.276-277 slévá `|` a `<`).
///
/// Projetá zastávka (před vývěskovou) zůstává na 0: StopList ji ztlumí a jestli se u ní čas vypíše,
/// řídí volba „tisk projetých" nad variantními sloupci.
ClassMethod CasJizdySloupce(ByRef wMap, currentPoradi As %String, poradi As %String, jeProjeta As %Boolean = 0) As %String
{
    i jeProjeta q 0
    s vv = $g(wMap(poradi))
    i (vv = "<") || (vv = "|") q "<"
    i ..CasHodnota(vv) = "" q ""
    q ..CasJizdyZMapy(.wMap, currentPoradi, poradi)
}

/// Jak dlouho průběh jede: nejvzdálenější kumulativní čas od vývěskové zastávky dál. Kritérium pro
/// výběr průběhu varianty - dosah (počet obsloužených zastávek) mají členové varianty shodný, protože
/// právě ten je definuje, takže rozhoduje čas. Legacy „nechám průběh s delším časem"
/// (`JRapiLinPrtMhd1.mac` ř.290-294).
ClassMethod DelkaProfilu(ByRef wMap, currentPoradi As %String, ByRef servedPoradi, jeTam As %Boolean) As %Integer
{
    s maxT = 0, pp = ""
    f {
        s pp = $o(servedPoradi(pp))
        q:pp=""
        i '$isvalidnum(pp) continue
        i '..JeVUsekuVariant(pp, currentPoradi, jeTam) continue
        i ..CasHodnota($g(wMap(pp))) = "" continue
        s t = ..CasJizdyZMapy(.wMap, currentPoradi, pp)
        i t > maxT s maxT = t
    }
    q maxT
}

/// Vrátí sloupce dnů (1-based indexy, čárkou oddělené) pro spoj - 1:1 s `KamPrt` logikou
/// `JRapiLinPrtMhd1` ř.30-152. Závisí na FTL módu, tarifní platnosti `plk`, negativní značce `cnz`
/// a NZ konfiguraci linky `nzCfg` (= `MHD_NZ1_NZ2|MHD_NENZ1_NENZ2`).
/// Prázdninové dělení (FTL 1/3): pracovní spoj → sloupec "mimo prázdniny"/"o prázdninách" dle CNZ
/// porovnané s NZFTL1 (nejede o prázd) / NZFTL2 (jede o prázd); bez NZ konfigurace jdou do obou.
ClassMethod ZarazeniSpoje(ftl As %String, plk As %String, cnz As %String, nzCfg As %String) As %String
{
    s nzftl1   = $p($p(nzCfg,"|",1),"~",2)              // NZ "nejede o prázdninách"
    s nzftl2   = $p($p(nzCfg,"|",1),"~",1)              // NZ "jede o prázdninách"
    s nenzftl1 = ","_$p($p(nzCfg,"|",2),"~",2)_","      // NZ nevypisovat ve sloupci "mimo"
    s nenzftl2 = ","_$p($p(nzCfg,"|",2),"~",1)_","      // NZ nevypisovat ve sloupci "o prázd"
    s prac     = (plk="")||(plk=".")
    s p15x     = (plk["1")||(plk["2")||(plk["3")||(plk["4")||(plk["5")||(plk["X")

    i ftl = 2 {
        s kam = "0~0"
        i prac { s kam = "1~1" }
        else {
            i p15x s $p(kam,"~",1) = 1
            i (plk["6")||(plk["7")||(plk["+") s $p(kam,"~",2) = 1
        }
        q ..BitmapNaSkupiny(kam)
    }
    i (ftl = 1) || (ftl = 3) {
        s kam = $s(ftl=3:"0~0~0~0", 1:"0~0~0")
        i prac {
            i cnz = nzftl1 { s $p(kam,"~",1) = 1 }
            elseif cnz = nzftl2 { s $p(kam,"~",2) = 1 }
            else {
                s $p(kam,"~",1) = 1
                s $p(kam,"~",2) = 1
                i (cnz'="") && (nenzftl1[(","_cnz_",")) s $p(kam,"~",1) = 0
                i (cnz'="") && (nenzftl2[(","_cnz_",")) s $p(kam,"~",2) = 0
            }
            // pracovní spoj jede i o víkendu (poslední sloupec/sloupce)
            i ftl = 1 { s $p(kam,"~",3) = 1 }
            else { s $p(kam,"~",3) = 1, $p(kam,"~",4) = 1 }
        }
        else {
            i p15x {
                i cnz = nzftl1 { s $p(kam,"~",1) = 1 }
                elseif cnz = nzftl2 { s $p(kam,"~",2) = 1 }
                else {
                    s $p(kam,"~",1) = 1
                    s $p(kam,"~",2) = 1
                    i (cnz'="") && (nenzftl1[(","_cnz_",")) s $p(kam,"~",1) = 0
                    i (cnz'="") && (nenzftl2[(","_cnz_",")) s $p(kam,"~",2) = 0
                }
            }
            i ftl = 1 {
                i (plk["6")||(plk["7")||(plk["+") s $p(kam,"~",3) = 1
            }
            else {
                i plk["6" s $p(kam,"~",3) = 1
                i (plk["7")||(plk["+") s $p(kam,"~",4) = 1
            }
        }
        q ..BitmapNaSkupiny(kam)
    }
    // FTL = 0 (výchozí): Prac | So | Ne+svátky
    s kam = "0~0~0"
    i prac { s kam = "1~1~1" }
    else {
        i p15x s $p(kam,"~",1) = 1
        i plk["6" s $p(kam,"~",2) = 1
        i (plk["7")||(plk["+") s $p(kam,"~",3) = 1
    }
    q ..BitmapNaSkupiny(kam)
}

/// Převede `KamPrt` bitmapu ("1~0~1") na čárkou oddělené 1-based indexy nastavených sloupců ("1,3").
ClassMethod BitmapNaSkupiny(kamPrt As %String) As %String
{
    s g = ""
    f i=1:1:$l(kamPrt,"~") i +$p(kamPrt,"~",i) s g = g_i_","
    q $e(g,1,*-1)
}



/// Sloupce skupin dnů z vlastní definice - tvar 1:1 s NacistSkupinyDnu (ID/NADPIS/NADPIS2/PORADI).
ClassMethod NacistSkupinyDnuDleDefinice(ByRef defArr) As %DynamicArray
{
    s skupiny = ##class(%DynamicArray).%New()
    f i=1:1:+$g(defArr) {
        d skupiny.%Push(..BuildSkupinaDnu(defArr(i,"id"), defArr(i,"nadpis"), $g(defArr(i,"nadpis2")), i))
    }
    q skupiny
}



/// Zařazení spoje do vlastních sloupců (druhý režim vedle ZarazeniSpoje). Kaskáda podmínek:
/// dny (průnik s týdenní platností) → značky „jen s těmito" (CNZ musí být v seznamu; spoj bez CNZ
/// nesedí) → značky „bez těchto" (CNZ v seznamu vyřazuje) → **období** (spoj v něm musí jet aspoň
/// jeden den). Nevyplněná podmínka neomezuje; bez podkladu z ISY neomezuje ani období.
/// Vrací CSV id sloupců ("" = spoj nespadl nikam → hlásí se v NEZARAZENE_SPOJE).
ClassMethod ZarazeniSpojeDleDefinice(ByRef defArr, plk As %String, cnz As %String, smer As %String = "", sp As %String = "", ByRef obdobiSloupcu) As %String
{
    s dnySpoje = ..DnySpoje(plk)
    s out = ""
    f i=1:1:+$g(defArr) {
        i '..SedneDoSloupceDny(dnySpoje, defArr(i,"dny")) continue
        i (defArr(i,"jen") '= "") && ((cnz = "") || (defArr(i,"jen") '[ (","_(+cnz)_","))) continue
        i (defArr(i,"bez") '= "") && (cnz '= "") && (defArr(i,"bez") [ (","_(+cnz)_",")) continue
        i ..JizdaVObdobi(smer, sp, .obdobiSloupcu, i, defArr(i,"dny")) = 0 continue
        s out = out_$s(out="":"", 1:",")_defArr(i,"id")
    }
    q out
}

/// Index sloupce v definici podle jeho id (anotace pracují s id, období s pořadím). 0 = nenalezeno.
ClassMethod IndexSloupce(ByRef defArr, id As %String) As %Integer
{
    f i=1:1:+$g(defArr) {
        i defArr(i,"id") = id ret i
    }
    q 0
}

/// Potlačení značky u minut ve vlastním sloupci - obdoba ZnackaSkrytaVeSloupci, ale seznam
/// nese sloupec sám v definici (per-sloupcová náhrada pozičního linkového MHD_NEZNZ123).
ClassMethod ZnackaSkrytaDleDefinice(ByRef defArr, sloupecId As %String, nz As %String) As %Boolean
{
    i (nz = "") || (sloupecId = "") q 0
    s cfg = $g(defArr("nevyp",sloupecId))
    i cfg = "" q 0
    q (cfg [ (","_(+nz)_","))
}

/// Denní značky spoje v konkrétním vlastním sloupci.
///
/// Obecné pravidlo: **značka se tiskne, jen když spoj nepokrývá celý sloupec** - co platí pro celý
/// sloupec, říká jeho nadpis. Tisknou se **jen dny, které sloupec sám pokrývá**, takže se do sloupce
/// nemůže dostat značka dne, který v něm vůbec není (to byla chyba opravená v `B29`).
///
/// ⚠️ Čtyři kombinace, které umí i režim „automaticky" (`12345` / `6` / `7+` / `67+`), si drží
/// **dnešní chování** - rozhodnutí Jan 10. 8. Obecné pravidlo by ve sloupci „Neděle a svátky"
/// označkovalo 6 269 ze 7 295 spojů (86 %) a ve slitém víkendu 4 386 z 8 213 (53 %); značka na
/// většině řádků nerozlišuje nic a je hlavně příznakem, že sloupec neodpovídá datům (v CB_CSADAD
/// skoro žádný nedělní spoj o svátcích nejede). Kdo chce nedělní a sváteční provoz odlišit,
/// rozdělí si sloupec - vlastní sloupce to od etapy 1 umožňují.
ClassMethod DenniZnackyProSloupec(plk As %String, dnySpoje As %String, sloupecDny As %String) As %String
{
    i sloupecDny = "" q ""                                      // sloupec neomezuje - není vůči čemu značku vztáhnout
    // kombinace známé z dnešních rozvržení - beze změny („X" = pracovní dny podle vyhlášky, což je
    // od `B82` tvar, který posílají presety; kalendářní „12345" se chová stejně, liší se jen svátky)
    i (sloupecDny = "12345") || (sloupecDny = "X") q ..DenniZnacky(0, plk)   // FTL 0 vrací jen znaky 1-5
    i (sloupecDny = "6") || (sloupecDny = "7+") || (sloupecDny = "+") q ""   // oddělený víkendový sloupec: nadpis to říká
    i (sloupecDny = "67+") || (sloupecDny = "6+") q $tr(..DenniZnacky(2, plk), "12345")   // slitý víkend dle dnešního pravidla
    // vlastní kombinace (uživatel si dny slil sám) - obecné pravidlo
    i (dnySpoje = "") || (dnySpoje = "-") q ""
    // „X" ve sloupci se vůči značkám chová jako 1-5; samotné „X" značka není, tu nese den v týdnu
    i sloupecDny [ "X" s sloupecDny = $tr(sloupecDny,"X")_"12345"
    s pokryva = 1
    f i=1:1:$l(sloupecDny) {
        i dnySpoje '[ $e(sloupecDny,i) s pokryva = 0
    }
    i pokryva q ""
    s znacky = ""
    f i=1:1:$l(sloupecDny) {
        s dch = $e(sloupecDny,i)
        i dnySpoje [ dch s znacky = znacky_dch
    }
    q znacky
}

/// Vzory vlastních sloupců = čtyři dnešní pevná rozvržení, postavená z konfigurace linky.
/// **Jediný zdroj pravdy**: klient je nestaví sám, bere je z hodnoty `PRESETY_SLOUPCU`, a paritní
/// test měří proti témuž kódu - parita tak hlídá to, co klient reálně pošle, ne ruční kopii pravidel.
/// Klíče vzorů jsou stabilní (visí na nich volba v editoru), popisky 1:1 s parametrem
/// „Rozdělení sloupců".
ClassMethod PresetySloupcu(dataDB As %String, sku As %String, lin As %String) As %String
{
    s nz    = $g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NZ1_NZ2"))
    s nenz  = $g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NENZ1_NENZ2"))
    s neznz = $g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NEZNZ123"))
    // 1. pozice NZ = „jede o prázdninách", 2. = „nejede"; poziční seznamy skrývání spojů mají
    // legacy sloty prohozené (viz ZarazeniSpoje)
    s mimoBez   = ..SpojSeznamyZnacek($p(nz,"~",1), $p(nenz,"~",2))
    s oPrazdBez = ..SpojSeznamyZnacek($p(nz,"~",2), $p(nenz,"~",1))

    s out = ##class(%DynamicObject).%New()
    d out.%Set("PRAC_SO_NE", ..PresetSloupce($lb($lb("1","Pracovní dny","X",""), $lb("2","Soboty","6",""), $lb("3","Neděle a svátky","+","")), neznz))
    d out.%Set("PRAC_VIKEND", ..PresetSloupce($lb($lb("1","Pracovní dny","X",""), $lb("2","Soboty, neděle a svátky","6+","")), neznz))
    d out.%Set("PRAZDNINY_VIKEND", ..PresetSloupce($lb($lb("1","Pracovní dny mimo prázdniny","X",mimoBez), $lb("2","Pracovní dny o prázdninách","X",oPrazdBez), $lb("3","Soboty, neděle a svátky","6+","")), neznz))
    d out.%Set("PRAZDNINY_SO_NE", ..PresetSloupce($lb($lb("1","Pracovní dny mimo prázdniny","X",mimoBez), $lb("2","Pracovní dny o prázdninách","X",oPrazdBez), $lb("3","Soboty","6",""), $lb("4","Neděle a svátky","+","")), neznz))
    q out.%ToJSON()
}

/// Sloupce jednoho vzoru; `neznz` = poziční seznam „nevypisovat značku" (slot N patří N-tému sloupci).
ClassMethod PresetSloupce(sloupce As %List, neznz As %String) As %DynamicArray
{
    s arr = ##class(%DynamicArray).%New()
    f i=1:1:$ll(sloupce) {
        s def = $li(sloupce, i)
        s sl = ##class(%DynamicObject).%New()
        d sl.%Set("id", $li(def,1))
        d sl.%Set("nadpis", $li(def,2))
        d sl.%Set("dny", $li(def,3))
        d sl.%Set("znackyBez", $li(def,4))
        d sl.%Set("nevypisovatZnacky", $zstrip($p(neznz,"~",i),"<>W"))
        d arr.%Push(sl)
    }
    q arr
}

/// Spojení dvou CSV seznamů čísel značek do jednoho (bez duplicit, prázdné se přeskočí).
ClassMethod SpojSeznamyZnacek(a As %String, b As %String) As %String
{
    s out = ""
    f i=1:1:2 {
        s seznam = $s(i=1:$g(a), 1:$g(b))
        f j=1:1:$l(seznam,",") {
            s cislo = $zstrip($p(seznam,",",j),"<>W")
            i cislo = "" continue
            i (","_out_",") [ (","_cislo_",") continue
            s out = out_$s(out="":"", 1:",")_cislo
        }
    }
    q out
}

/// Rozsahy období sloupce z uživatelského textu („14.2.-18.2., 1.7.-31.8." nebo „1.7.2026-31.8.2026").
/// Datum **bez roku** se opakuje každý rok (šablona pak přežije překlopení jízdního řádku), datum
/// **s rokem** platí jednou. Nerozpoznaný rozsah se zahodí - tvar hlídá editor při OK.
/// Vrací počet, rozsahy(k) = $lb(odText, doText).
ClassMethod ParseObdobi(text As %String, ByRef rozsahy) As %Integer
{
    k rozsahy
    s rozsahy = 0
    i $g(text) = "" q 0
    s n = 0
    f i=1:1:$l(text,",") {
        s cast = $zstrip($p(text,",",i), "<>W")
        i cast = "" continue
        s odText = $zstrip($p(cast,"-",1), "<>W")
        s doText = $zstrip($p(cast,"-",2), "<>W")
        i doText = "" s doText = odText
        i ('..JeDatumTvar(odText)) || ('..JeDatumTvar(doText)) continue
        s n = n + 1
        s rozsahy(n) = $lb(odText, doText)
    }
    s rozsahy = n
    q n
}

/// Je text datum ve tvaru „d.m", „d.m." nebo „d.m.rrrr"? Rok je nepovinný (= každý rok).
ClassMethod JeDatumTvar(text As %String) As %Boolean
{
    s text = $g(text)
    i text = "" q 0
    s den = $p(text,".",1), mesic = $p(text,".",2), rok = $p(text,".",3)
    i ('$isvalidnum(den)) || ('$isvalidnum(mesic)) q 0
    i (+den < 1) || (+den > 31) || (+mesic < 1) || (+mesic > 12) q 0
    i rok '= "" {
        i '$isvalidnum(rok) q 0
        i (+rok < 1900) || (+rok > 2999) q 0
    }
    q 1
}

/// Platnost jízdního řádku linky jako $lb(hrlOd, hrlDo) - `PATCH,"Plati"` ve tvaru „datumOd~datumDo:…".
/// Prázdná složka = neomezeno.
ClassMethod PlatnostLinky(dataDB As %String, sku As %String, lin As %String) As %String
{
    s val = $p($g(^[dataDB]JRL(sku,lin,"PATCH","Plati")), ":", 1)
    s datumOd = $p(val,"~",1), datumDo = $p(val,"~",2)
    s hrlOd = "", hrlDo = ""
    i ..JeDatumTvar(datumOd) s hrlOd = ..DatumNaHorolog(datumOd, $p(datumOd,".",3))
    i ..JeDatumTvar(datumDo) s hrlDo = ..DatumNaHorolog(datumDo, $p(datumDo,".",3))
    q $lb(hrlOd, hrlDo)
}

/// „d.m" + rok → horolog; "" když datum v daném roce neexistuje (29.2. v nepřestupném roce).
ClassMethod DatumNaHorolog(text As %String, rok As %String) As %String
{
    i $g(rok) = "" q ""
    s den = +$p(text,".",1), mesic = +$p(text,".",2)
    s hrl = ""
    try { s hrl = ##class(ALVA.GENERALAPI.Date).DateToHor(den_"."_mesic_"."_rok) } catch (ex) { s hrl = "" }
    q hrl
}

/// Období všech vlastních sloupců převedené na horolog + souhrnný rozsah pro jeden dotaz do ISY.
/// Datum bez roku se materializuje pro každý rok, který protíná platnost jízdního řádku (rozsah
/// přes Nový rok pokračuje do dalšího roku). Rozsahy mimo platnost se zahazují - sloupec, kterému
/// takhle nezbude nic, se hlásí uživateli, protože jinak zmlkne bez vysvětlení.
///   obdobiSloupcu(i)        = počet rozsahů sloupce i (0 = neomezuje)
///   obdobiSloupcu(i,k)      = $lb(hrlOd, hrlDo)
///   obdobiSloupcu("rozsah") = $lb(spanOd, spanDo) přes všechny sloupce ("" = žádné období)
///   obdobiSloupcu("mimo")   = nadpisy sloupců, jejichž období leží mimo platnost jízdního řádku
ClassMethod ObdobiSloupcu(dataDB As %String, sku As %String, lin As %String, ByRef defArr, ByRef obdobiSloupcu)
{
    k obdobiSloupcu
    s obdobiSloupcu("rozsah") = $lb("", "")
    s obdobiSloupcu("mimo") = ""
    s platnost = ..PlatnostLinky(dataDB, sku, lin)
    s hrlPlatiOd = $lg(platnost,1), hrlPlatiDo = $lg(platnost,2)
    s dnesniRok = $p(##class(ALVA.GENERALAPI.Date).horToDate($h), ".", 3)
    s rokOd = $s(hrlPlatiOd'="":$p(##class(ALVA.GENERALAPI.Date).horToDate(hrlPlatiOd),".",3), 1:dnesniRok)
    s rokDo = $s(hrlPlatiDo'="":$p(##class(ALVA.GENERALAPI.Date).horToDate(hrlPlatiDo),".",3), 1:dnesniRok + 1)
    s spanOd = "", spanDo = ""
    f i=1:1:+$g(defArr) {
        s obdobiSloupcu(i) = 0
        k rozsahy
        i '..ParseObdobi($g(defArr(i,"obdobi")), .rozsahy) continue
        s obdobiSloupcu(i,"zadano") = 1
        s n = 0
        f ri=1:1:+$g(rozsahy) {
            s odText = $lg(rozsahy(ri),1), doText = $lg(rozsahy(ri),2)
            s maRok = ($p(odText,".",3) '= "") || ($p(doText,".",3) '= "")
            i maRok {
                s rok = $s($p(odText,".",3)'="":$p(odText,".",3), 1:$p(doText,".",3))
                s hrlOd = ..DatumNaHorolog(odText, $s($p(odText,".",3)'="":$p(odText,".",3), 1:rok))
                s hrlDo = ..DatumNaHorolog(doText, $s($p(doText,".",3)'="":$p(doText,".",3), 1:rok))
                i (hrlOd '= "") && (hrlDo '= "") && (hrlDo < hrlOd) s hrlDo = ..DatumNaHorolog(doText, rok + 1)
                d ..PridatRozsah(hrlOd, hrlDo, hrlPlatiOd, hrlPlatiDo, .n, .obdobiSloupcu, i, .spanOd, .spanDo)
                continue
            }
            // bez roku - opakuje se; rok před platností kvůli rozsahu přesahujícímu Nový rok
            f rok = (rokOd - 1) : 1 : rokDo {
                s hrlOd = ..DatumNaHorolog(odText, rok)
                s hrlDo = ..DatumNaHorolog(doText, rok)
                i (hrlOd '= "") && (hrlDo '= "") && (hrlDo < hrlOd) s hrlDo = ..DatumNaHorolog(doText, rok + 1)
                d ..PridatRozsah(hrlOd, hrlDo, hrlPlatiOd, hrlPlatiDo, .n, .obdobiSloupcu, i, .spanOd, .spanDo)
            }
        }
        s obdobiSloupcu(i) = n
        i n = 0 {
            s nadpis = $g(defArr(i,"nadpis"))
            i nadpis = "" s nadpis = "sloupec "_i
            s obdobiSloupcu("mimo") = obdobiSloupcu("mimo")_$s(obdobiSloupcu("mimo")="":"", 1:", ")_nadpis
        }
    }
    s obdobiSloupcu("rozsah") = $lb(spanOd, spanDo)
}

/// Přidá rozsah do období sloupce, když protíná platnost jízdního řádku (a ořízne ho na ni).
/// Pomocná metoda `ObdobiSloupcu` - vlastní try/catch nepotřebuje, volá se jen z API metody.
ClassMethod PridatRozsah(hrlOd As %String, hrlDo As %String, hrlPlatiOd As %String, hrlPlatiDo As %String, ByRef n, ByRef obdobiSloupcu, i As %Integer, ByRef spanOd, ByRef spanDo)
{
    i (hrlOd = "") || (hrlDo = "") q
    i (hrlPlatiOd '= "") && (hrlOd < hrlPlatiOd) s hrlOd = hrlPlatiOd
    i (hrlPlatiDo '= "") && (hrlDo > hrlPlatiDo) s hrlDo = hrlPlatiDo
    i hrlDo < hrlOd q                                   // rozsah leží mimo platnost jízdního řádku
    s n = n + 1
    s obdobiSloupcu(i,n) = $lb(hrlOd, hrlDo)
    i (spanOd = "") || (hrlOd < spanOd) s spanOd = hrlOd
    i (spanDo = "") || (hrlDo > spanDo) s spanDo = hrlDo
}

/// Denní bitmapy „kdy spoj skutečně jede" pro celou linku. Vyhodnocení (datumová omezení, svátky,
/// sudé/liché týdny, platnost linky) drží ISY - `LinkyStatistika.jede`; my ho voláme **jednou na
/// request** a výsledek necháme v `^||TiskAd`, protože GetData staví N stránek z téže linky.
/// Linku předáváme globálem: v našich klíčích je už v indexovém tvaru a přes `id` by ji
/// `cisloLinkyDoIndexu` převedla podruhé.
ClassMethod PripravitJede(ByRef sys As %String, dataDB As %String, sku As %String, lin As %String)
{
    k defArr
    i '..ParseDefiniceSloupcu(.defArr) q
    k obdobiSloupcu
    d ..ObdobiSloupcu(dataDB, sku, lin, .defArr, .obdobiSloupcu)
    s hrlOd = $lg(obdobiSloupcu("rozsah"),1), hrlDo = $lg(obdobiSloupcu("rozsah"),2)
    i (hrlOd = "") || (hrlDo = "") q                    // žádný sloupec období neomezuje
    k arr
    s arr("PAR","horlogOdDo") = $lb(hrlOd, hrlDo)
    s arr("PAR","platnostLin") = 1
    s arr("GLB") = $name(^||TiskAd("jede","raw"))
    d ##class(ALVA.ISY.ISYLinky.API.LinkyStatistika).jede(.sys, $lb(sku,"",""), $name(^[dataDB]JRL(sku,lin)), .arr)
    s ^||TiskAd("jede","od") = hrlOd
    // stát pro dopravní kalendář (0 CZ / 1 SK) - kalendář `jede` naplnil pro oba, svátky se liší
    s ^||TiskAd("jede","stat") = +$g(^[dataDB]JRSET("COME","REC"))
}

/// Patří den do sloupce? Sloupec bez dnů neomezuje. Svátek patří jen do sloupce, který svátek
/// vyjmenoval - jinak by „Pracovní dny" tvrdily, že pokrývají i 1. květen.
/// Pomocná pro `JizdaVObdobi`; kalendář bere z toho, co naplnila `PripravitJede`.
ClassMethod DenPatriDoSloupce(hrl As %String, sloupecDny As %String) As %Boolean
{
    i sloupecDny = "" q 1
    s stat = +$g(^||TiskAd("jede","stat"))
    s jeSvatek = +$lg($g(^||TiskAd("jede","raw","CAL",hrl,stat)), 3)
    s denVTydnu = (hrl + 3 # 7) + 1
    // Svátek: patří do sloupce, který vyjmenoval "+"; do sloupce „X" (pracovní dny) nikdy, protože
    // pracovní dny svátky z definice nezahrnují; jinak je svátek pořád tím dnem v týdnu, kterým
    // padl - velikonoční neděle do sloupce „Ne" patří (`B81`), 1. květen do kalendářního „Po-Pá"
    // taky. Právě proto musí jít „pracovní dny" zadat jako `X`, ne jako `12345` (`B82`).
    i jeSvatek {
        i sloupecDny [ "+" q 1
        i sloupecDny [ "X" q 0
        q (sloupecDny [ denVTydnu)
    }
    // „+" pokrývá i běžnou neděli (viz `SedneDoSloupceDny`), „X" pracovní dny 1-5
    q (sloupecDny [ denVTydnu)
      || ((sloupecDny [ "X") && (denVTydnu < 6))
      || ((sloupecDny [ "+") && (denVTydnu = 7))
}

/// Jak spoj jede v období sloupce: 2 = každý den období, 1 = jen část, 0 = vůbec ani den,
/// -1 = neumíme rozhodnout (chybí podklad z ISY → chová se jako „neomezuje").
/// Sloupec bez období vrací 2 (nic neomezuje, značka se z téhle osy netiskne). Sloupec, jehož
/// období leží **mimo platnost jízdního řádku**, vrací 0 - do „Vánočního provozu" nepatří všechny
/// spoje jen proto, že jízdní řád Vánoce nepokrývá (hlásí se přes OBDOBI_MIMO_PLATNOST).
ClassMethod JizdaVObdobi(smer As %String, sp As %String, ByRef obdobiSloupcu, sloupecIdx As %Integer, sloupecDny As %String = "") As %Integer
{
    s pocet = +$g(obdobiSloupcu(sloupecIdx))
    i pocet = 0 q $s(+$g(obdobiSloupcu(sloupecIdx,"zadano")):0, 1:2)
    s hrlOdBitmapy = $g(^||TiskAd("jede","od"))
    s smerId = $s(smer="Z":1, 1:0)
    s val = $g(^||TiskAd("jede","raw","DTA",smerId,sp))
    i (hrlOdBitmapy = "") || (val = "") q -1
    s bitmapa = $lg(val,3)
    i bitmapa = "" q -1
    s jede = 0, nejede = 0
    f k=1:1:pocet {
        s hrlOd = $lg(obdobiSloupcu(sloupecIdx,k),1), hrlDo = $lg(obdobiSloupcu(sloupecIdx,k),2)
        f hrl = hrlOd:1:hrlDo {
            s poz = hrl - hrlOdBitmapy + 1
            i (poz < 1) || (poz > $l(bitmapa)) continue
            // dny mimo sloupec se nepočítají: pracovní spoj o víkendu nejede, takže by „jede celé
            // období" nesplnil nikdy a potlačení značky by se nezapnulo (nález Jan 10. 8.)
            i '..DenPatriDoSloupce(hrl, sloupecDny) continue
            i +$e(bitmapa, poz) { s jede = 1 } else { s nejede = 1 }
        }
    }
    i 'jede q 0
    q $s(nejede:1, 1:2)
}

/// Rozdíly tras - pro každý spoj daného směru porovná jeho trasu se vzorovou (základní) trasou
/// a sestaví poznámku vyjmenovávající rozdílové zastávky. 1:1 reimplementace `OmTrasa^JRapiLinPrtMhd`
/// (AD). Gated `AutoPozn` (PATCH,"mhd","not"). Markery z písmenné sady (default "BCEFGHIJOQRWeijqsvwxz"
/// nebo PATCH,"mhd","notchar"), počítané **per aktuální zastávku** (legacy scope Smer×Tc), dedup textů.
///   pismenoSpoj(sp) = písmeno přiřazené spoji (anotace u minuty)
///   legenda(písmeno) = text do legendy ("neobsluhuje zast. X" / "jede přes…" / "jede jen do…" / "pokračuje…")
ClassMethod RozdilyTras(dataDB As %String, sku As %String, lin As %String, smer As %String, currentPoradi As %String, ByRef pismenoSpoj, ByRef legenda)
{
    k pismenoSpoj, legenda
    i currentPoradi = "" q
    i '+$g(^[dataDB]JRL(sku,lin,"PATCH","mhd","not")) q     // AutoPozn vypnuto → bez rozdílů tras

    s krok = $s(smer="Z":-1, 1:1)

    // vzorová trasa = základní trasa (J-věta #10 dta12), fallback tištěná (#7 dta9), fallback union obsluhy
    s vzor = ..TrasaBitmapaZPole(dataDB, sku, lin, smer, 10)
    i vzor = "" s vzor = ..TrasaBitmapaZPole(dataDB, sku, lin, smer, 7)
    i vzor = "" {
        k served  d ..ObsluhovanePoradi(dataDB, sku, lin, smer, .served)
        s p = ""  f { s p = $o(served(p))  q:p=""  i $isvalidnum(p) s $e(vzor,p) = 1 }
    }
    i vzor = "" q

    // MTarTr = poslední pozice vzorové trasy (T) / první (Z)
    s mTarTr = ""
    i smer = "T" s mTarTr = $l(vzor)
    i smer = "Z" f tmp=1:1:$l(vzor) { i +$e(vzor,tmp) s mTarTr = tmp  q }

    // Písmenná sada rozdílů tras - prioritně z nastavení linky („Volné značky"), jinak výchozí
    // (1:1 legacy, začíná „B"). Počítáno per zastávku, dedup textů. Sada variantních sloupců je
    // jiná a záměrně disjunktní - viz SadaVariantDefault.
    s oznTr = $g(^[dataDB]JRL(sku,lin,"PATCH","mhd","notchar"))
    i oznTr = "" s oznTr = ..#SadaRozdiluDefault
    s pocMarker = 0

    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue

        s cisloTrasy = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
        s spojTr = ..SpojBitmapa(dataDB, sku, lin, cisloTrasy)
        i spojTr = "" continue
        i spojTr = vzor continue              // spoj jede vzorovou trasou → bez poznámky
        i '+$e(spojTr, currentPoradi) continue // spoj zde nestaví → na této stránce neřešíme

        // TcDo = konečná spoje ve směru jízdy (poslední obsluhovaná pozice)
        s tcDo = ""
        i smer = "T" s tcDo = $l(spojTr)
        i smer = "Z" f tmp=1:1:$l(spojTr) { i +$e(spojTr,tmp) s tcDo = tmp  q }
        i tcDo = "" continue

        s (jede, nejede, jedeDo, pokrac, pruJezd) = ""

        // "jede jen do" - spoj končí dříve než vzorová trasa
        s tcDOk = ""
        i ((tcDo < mTarTr) && (smer = "T")) || ((tcDo > mTarTr) && (smer = "Z")) {
            s jedeDo = ..KonecnaNazev(dataDB, sku, lin, spojTr, smer, tcDo)
            s tcDOk = tcDo
        }

        f tmpTc=currentPoradi:krok:tcDo {
            i tmpTc = currentPoradi continue
            i tcDOk = tmpTc continue
            i +$e(vzor,tmpTc) = +$e(spojTr,tmpTc) continue
            s naz = ..NazZPoradi(dataDB, sku, lin, tmpTc)
            i (smer = "T") && (tmpTc > mTarTr) && (+$e(spojTr,tmpTc)) {
                s $p(pokrac,", ",$l(pokrac,", ")+1) = naz
                i tmpTc '= tcDo s $p(pruJezd,", ",$l(pruJezd,", ")+1) = naz   // průjezd = bez konečné (ta je v „jede až do")
                continue
            }
            i (smer = "Z") && (tmpTc < mTarTr) && (+$e(spojTr,tmpTc)) {
                s $p(pokrac,", ",$l(pokrac,", ")+1) = naz
                i tmpTc '= tcDo s $p(pruJezd,", ",$l(pruJezd,", ")+1) = naz
                continue
            }
            i +$e(spojTr,tmpTc)  s $p(jede,", ",$l(jede,", ")+1) = naz
            i '+$e(spojTr,tmpTc) s $p(nejede,", ",$l(nejede,", ")+1) = naz
        }

        // strip vedoucího prázdného prvku (legacy $P(...,2,...))
        s jede    = $p(jede,    ", ", 2, $l(jede,    ", "))
        s nejede  = $p(nejede,  ", ", 2, $l(nejede,  ", "))
        s pokrac  = $p(pokrac,  ", ", 2, $l(pokrac,  ", "))
        s pruJezd = $p(pruJezd, ", ", 2, $l(pruJezd, ", "))

        // Prodloužení za základní trasu (#7947) - JEDINÁ záměrná odchylka od legacy znění.
        // Legacy říká „pokračuje přes <mezizastávky>, <konečná>"; klient to označil za matoucí, protože
        // chce vědět KAM spoj dojede. Vypisujeme tedy cíl + průjezd („jede až do zast. X přes Y").
        // Průjezd tam musí zůstat: bez něj splynou skupiny spojů, které se liší jen cestou (změřeno
        // na CB_GWBUS 501/0000001/T - legacy 3 skupiny, bez průjezdu 2).
        s vetaCil = ""
        i pokrac '= "" {
            s cilNaz = ..KonecnaNazev(dataDB, sku, lin, spojTr, smer, tcDo)
            i cilNaz '= "" {                       // bez známého cíle větu nepsat (jinak „jede až do zast. ")
                s vetaCil = ..TextJedeAzDo()_cilNaz
                i pruJezd '= "" s vetaCil = vetaCil_..TextPres()_pruJezd
            }
        }

        // Sestavení textu. U prodloužení jde cíl DOPŘEDU (#7947): je to informace, kvůli které tiket
        // vznikl, a legenda ořezává konec věty. Bez prodloužení zůstává pořadí i oddělovače 1:1 legacy
        // (jede přes → neobsluhuje → jede jen do; „ , " před „neobsluhuje" je legacy tvar, ne překlep).
        // „jede přes" se u prodloužení NEVYNECHÁVÁ - nese jinou informaci (kudy jede navíc) než cíl.
        s txt = vetaCil
        i jede '= "" {
            i txt = "" {
                s txt = ..TextJedePres()_jede
            } else {
                s $p(txt,", ",$l(txt,", ")+1) = ..TextJedePres()_jede
            }
        }
        i nejede '= "" {
            i txt = "" {
                s txt = ..TextNeobsluhuje()_nejede
            } else {
                s $p(txt," , ",$l(txt," , ")+1) = ..TextNeobsluhuje()_nejede
            }
        }
        i jedeDo '= "" {
            i txt = "" {
                s txt = ..TextJedeJenDo()_jedeDo
            } else {
                s $p(txt,", ",$l(txt,", ")+1) = ..TextJedeJenDo()_jedeDo
            }
        }
        i txt = "" continue

        // marker - dedup stejných textů v rámci této zastávky, jinak další písmeno ze sady
        i $d(markerProText(txt)) {
            s znk = markerProText(txt)
        } else {
            s pocMarker = pocMarker + 1
            s znk = ..GetZnk(oznTr, pocMarker)
            s markerProText(txt) = znk
            s legenda(znk) = txt
        }
        s pismenoSpoj(sp) = znk
    }
}

/// Bitmapa obsluhovaných pozic spoje z jeho trasy (cachovaná W-mapa per `cisloTrasy`).
/// `$e(bmp,poradi)=1` kde spoj staví. "" = prázdná trasa.
/// Nestaví = `<` (projíždí), `|` (zastávka na trase, ale spoj v ní nestaví) nebo prázdno (mimo trasu) -
/// stejná trojice jako `CasHodnota` a `ObsluhovanePoradi`. `|` tu dřív chybělo a rozdíly tras z toho
/// dělaly falešné „jede přes zast. X" plus slévaly skupiny spojů, které se lišily právě jen `|`
/// (změřeno proti starému modulu na CB_GWBUS 501/0000001/T: legacy 3 skupiny, my 2).
ClassMethod SpojBitmapa(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String) As %String
{
    d ..WMapa(dataDB, sku, lin, cisloTrasy, .wMap)
    s bmp = "", p = ""
    f {
        s p = $o(wMap(p), 1, cas)
        q:p=""
        i ($isvalidnum(p)) && (cas '= "<") && (cas '= "") && (cas '= "|") s $e(bmp,p) = 1
    }
    q bmp
}

/// Bitmapa trasy z předpisového pole J-věty (segIdx = 7 tištěná trasa / 10 základní trasa),
/// per pozice, dle směru (`:`-piece 1 = Tam, 2 = Zpět). `$e(bmp,poradi)=1` kde je příznak nastaven.
ClassMethod TrasaBitmapaZPole(dataDB As %String, sku As %String, lin As %String, smer As %String, segIdx As %Integer) As %String
{
    s dir = $s(smer="Z":2, 1:1)
    s bmp = "", poradi = ""
    f {
        s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi))
        q:poradi=""
        i '$isvalidnum(poradi) continue
        s flag = $p($p($g(^[dataDB]JRL(sku,lin,"J",poradi)),"#",segIdx),":",dir)
        i +flag s $e(bmp,poradi) = 1
    }
    q bmp
}

/// Atributy zastávky = znaky z **trasy linky** (J-věta `#1:2`) ∪ znaky z **registru zastávek**
/// (`^JRZ(czst)`, `$`-pole 8). Legacy čte obojí - změřeno 30. 7.: „x = na znamení" vypisuje jak
/// u `CB_CSADAD` 0320680 (kde je jen v trase), tak u `CB_GWBUS` 0000001 (kde je jen v registru).
/// Bez registru nám na vývěsce chyběla informace, že zastávka je na znamení.
/// Pořadí: nejdřív trasa, pak znaky z registru, které v trase nejsou (dedup po znacích).
/// Registr je per zastávka (nezávisle na směru i lince) - to je správná sémantika „tato zastávka je
/// na znamení". Dopad měřen: v CB_CSADAD je pole registru prázdné u všech 5728 zastávek → beze změny.
ClassMethod AtributyZastavky(dataDB As %String, czst As %String, atributyTrasy As %String) As %String
{
    i czst = "" ret atributyTrasy
    s atributyRegistru = $p(..ZaznamZastavky(dataDB, czst),"$",8)
    i atributyRegistru = "" ret atributyTrasy
    s vysledek = atributyTrasy
    f i=1:1:$l(atributyRegistru) {
        s znak = $e(atributyRegistru,i)
        i znak = "" continue
        i vysledek [ znak continue
        s vysledek = vysledek _ znak
    }
    ret vysledek
}

/// Tarifní zóny zastávky, `;`-oddělené (1:1 legacy `JRapiExp10` blok „trasa").
/// Přednost má zóna z **trasy linky** (J-věta, 9. `#`-segment - `JRapiLin` dta11; čárka uvnitř je
/// oddělovač zón, ne součást hodnoty). Když ji trasa nemá, vezme se z **registru zastávek**
/// (`^JRZ` `$`-pole 11 = `zóna~hranice IDS~další zóny`; druhá zóna se přidá jen u hraniční
/// zastávky). Hodnota `-` = zóny netisknout. `zonaBezPismen` odstraní z kódu písmena.
ClassMethod ZonyZastavky(dataDB As %String, veta As %String, czst As %String, zonaBezPismen As %Boolean = 0) As %String
{
    s zonyTrasy = $tr($p(veta,"#",9), ",", ";")
    i zonyTrasy = "", czst '= "" {
        s poleRegistru = $p(..ZaznamZastavky(dataDB, czst),"$",11)
        s zonaRegistru = $p(poleRegistru,"~",1)
        // Hraniční zastávka (příznak) leží ve dvou zónách - druhá je v témže poli.
        i +$p(poleRegistru,"~",2) {
            s druhaZona = $p(poleRegistru,"~",3)
            i druhaZona '= "" s zonaRegistru = $s(zonaRegistru="":druhaZona, 1:zonaRegistru_";"_druhaZona)
        }
        s zonyTrasy = zonaRegistru
    }
    i zonyTrasy = "-" ret ""                     // „-" u linky = zóny se netisknou
    s vysledek = ""
    f idx=1:1:$l(zonyTrasy,";") {
        s zona = $p(zonyTrasy,";",idx)
        i zonaBezPismen s zona = ..ZonaBezPismenVKodu(zona)
        i zona = "" continue
        s vysledek = vysledek_$s(vysledek="":"",1:";")_zona
    }
    ret vysledek
}

/// Odstraní z kódu zóny písmena (1:1 legacy `delZnk^JRapiLinPrtPS2`). Písmeno v kódu nese
/// **přepravní systém** (`JRapi4hand`: ODIS → „O", IDSOK → „I"), ne číslo zóny - do tisku
/// patří jen číslo z číselníku. Záměrně jen ASCII, jako legacy.
ClassMethod ZonaBezPismenVKodu(zona As %String) As %String
{
    ret $tr(zona, "QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm", "")
}

/// Má se z kódu zóny odstranit písmeno přepravního systému? Globální nastavení
/// `^JRSET("Definice","ZonaIdsDelZnk")`, přebité nastavením tisku linky
/// `^JRL(sku,lin,"PATCH","ZonaIdsDelZnk")` - prázdné/0 = dle programu, 1 = nechat, 2 = odstranit
/// (1:1 legacy `JRapiExp10` + `JRapiLin1` dta77). V JŘ to je u linky pole „Zóny - tisk a export
/// do JDF bez písmen".
ClassMethod ZonaBezPismen(dataDB As %String, sku As %String, lin As %String) As %Boolean
{
    s nastaveni = +$g(^[dataDB]JRSET("Definice","ZonaIdsDelZnk"))
    s perLinka = +$g(^[dataDB]JRL(sku,lin,"PATCH","ZonaIdsDelZnk"))
    i perLinka s nastaveni = perLinka - 1
    ret ''nastaveni
}

/// Název zastávky podle pořadí v trase (přes czst z J-věty `#1:1`).
ClassMethod NazZPoradi(dataDB As %String, sku As %String, lin As %String, poradi As %String) As %String
{
    s czst = $p($p($g(^[dataDB]JRL(sku,lin,"J",poradi)),"#",1),":",1)
    i czst = "" q ""
    q ..NazevZastavky(dataDB, czst)
}

/// Název konečné zastávky spoje pro věty „jede jen do / jede až do zast. X". Primárně pozice `tcDo`;
/// když tam zastávka pojmenovaná není (W-trasa spoje může nést pozici, kterou J-trasa nepojmenovává),
/// vrátí nejbližší pojmenovanou obsluhovanou pozici proti směru jízdy. Bez toho vznikla věta bez cíle
/// („jede až do zast. " s prázdným koncem). "" jen když spoj nemá pojmenovanou zastávku vůbec - pak
/// se věta nepíše.
ClassMethod KonecnaNazev(dataDB As %String, sku As %String, lin As %String, spojBitmapa As %String, smer As %String, tcDo As %String) As %String
{
    s naz = ..NazZPoradi(dataDB, sku, lin, tcDo)
    i naz '= "" ret naz
    i smer = "Z" {
        f p = tcDo + 1 : 1 : $l(spojBitmapa) {
            i '+$e(spojBitmapa, p) continue
            s naz = ..NazZPoradi(dataDB, sku, lin, p)
            i naz '= "" ret naz
        }
    } else {
        f p = tcDo - 1 : -1 : 1 {
            i '+$e(spojBitmapa, p) continue
            s naz = ..NazZPoradi(dataDB, sku, lin, p)
            i naz '= "" ret naz
        }
    }
    ret ""
}

/// Vygeneruje písmeno markeru z písmenné sady; když dojdou písmena (cnt > délka), zdvojí je
/// (B…z, pak BB…zz). 1:1 `GetZnk^JRapiLinPrtMhd`.
ClassMethod GetZnk(txt As %String, cnt As %Integer) As %String
{
    s len = $l(txt), ml = 0
    f { q:cnt'>len  s cnt = cnt-len, ml = ml+1 }
    s znk = $e(txt,cnt), one = znk
    f i=1:1:ml s znk = znk_one
    q znk
}

/// Vrátí marker (pořadové číslo) pro text poznámky; deduplikuje (stejný text → stejný marker).
ClassMethod ZiskejMarker(ByRef poznMap, txt As %String) As %String
{
    i $d(poznMap("byText", txt)) q poznMap("byText", txt)
    s n = $g(poznMap("cnt")) + 1
    s poznMap("cnt") = n
    s poznMap("byText", txt) = n
    s poznMap("byMarker", n) = txt
    q n
}

/// Zahodí z legendy rozdílů tras písmena, která nejsou u žádného odjezdu **této stránky**.
/// Bez toho zůstane na rozdělené vývěsce (per označník) vysvětlivka ke značce, jejíž spoj odjíždí
/// z jiného označníku, tedy je na jiné stránce - cestující čte vysvětlení k něčemu, co nevidí.
/// Přiřazení písmen zůstává nedotčené (nepřečíslovává se), takže táž značka znamená na všech
/// stránkách téže zastávky totéž.
ClassMethod ProrezatLegenduRozdilu(odjezdy As %DynamicArray, ByRef legenda)
{
    i '$d(legenda) q
    k pouzite
    s itOdj = odjezdy.%GetIterator()
    while itOdj.%GetNext(.klicOdj, .odjezd) {
        continue:'$isobject(odjezd)
        s itAnot = odjezd.%Get("ANOTACE").%GetIterator()
        while itAnot.%GetNext(.klicAnot, .anotace) {
            continue:'$isobject(anotace)
            continue:anotace.%Get("KATEGORIE")'="rozdil-trasy"
            s znak = anotace.%Get("ZNAK")
            i znak '= "" s pouzite(znak) = ""
        }
    }
    s znak = ""
    f {
        s znak = $o(legenda(znak))
        q:znak=""
        i '$d(pouzite(znak)) k legenda(znak)
    }
}

/// Sestaví legendu: atributy zastávek + atributy spoje (bezbariér) + rozdíly tras + "nejede/jede".
/// Pořadí 1:1 gold (`ALVA.TISKMHD.Data`): speciální znaky → rozdíly/text → omezení.
ClassMethod SestavPoznamky(ByRef poznMap, ByRef atrSeen, ByRef rozdilLegenda, ByRef bezbarLegenda, ByRef komentareLegenda, ByRef dnyLegenda, allBBP As %Boolean = 0, ByRef prestupSeen, ByRef odjAtrLegenda, ByRef znakyLinky, ByRef pokrLegenda) As %DynamicArray
{
    s poznamky = ##class(%DynamicArray).%New()
    // týdenní platnost - kroužkové denní značky (číslice 1-7): „⑤ = jede v pátek" (specialni → font kroužkuje)
    s dch = ""
    f {
        s dch = $o(dnyLegenda(dch))
        q:dch=""
        d poznamky.%Push(..BuildPoznamka("specialni", dch, dnyLegenda(dch), "", "", "tydenni-platnost"))
    }
    // atributy zastávek - jeden řádek legendy na každý viděný znak (znak → text z číselníku)
    s znak = ""
    f {
        s znak = $o(atrSeen(znak))
        q:znak=""
        d poznamky.%Push(..BuildPoznamka("specialni", znak, atrSeen(znak), "", "", "atribut-zastavky"))
    }
    // atributy u odjezdu (kola, místenkování) - hodnota je `typ~text~kategorie`, protože kolo je
    // piktogram a „R" prostý text, a každý má vlastní kategorii (zapínají se zvlášť).
    s znakAtr = ""
    f {
        s znakAtr = $o(odjAtrLegenda(znakAtr))
        q:znakAtr=""
        continue:$d(znakyLinky(znakAtr))     // zákazník si tuto značku vysvětluje vlastní poznámkou
        s zapis = odjAtrLegenda(znakAtr)
        d poznamky.%Push(..BuildPoznamka($p(zapis,"~",1), znakAtr, $p(zapis,"~",2), "", "", $p(zapis,"~",3)))
    }
    // pokračující spoje - značky u1, u2… přidělil server per stránka (viz PokracovaniZnak).
    // Kreslí se textem (víceznakové), kategorie `pokracovani` = filtr-tag pro šablonu.
    s znakPokr = ""
    f {
        s znakPokr = $o(pokrLegenda(znakPokr))
        q:znakPokr=""
        d poznamky.%Push(..BuildPoznamka("text", znakPokr, pokrLegenda(znakPokr), "", "", "pokracovani"))
    }
    // přestupy - vlastní kategorie, aby si je šablona bez značky přestupu mohla schovat.
    // Text z číselníku je jen „autobus" / „vlak", pro legendu z toho udělá větu „přestup na …".
    s znak = ""
    f {
        s znak = $o(prestupSeen(znak))
        q:znak=""
        d poznamky.%Push(..BuildPoznamka("specialni", znak, ..TextPrestupu()_prestupSeen(znak), "", "", "prestup"))
    }
    // atributy spoje - bezbariér (H) / částečně bezbariér (F). Při allBBP je „H" agregátní řádek
    // („všechny spoje…") → kategorie „bezbarier-agregat" (TD filtr ho umí skrýt, #7161 D6).
    s zb = ""
    f {
        s zb = $o(bezbarLegenda(zb))
        q:zb=""
        s katBezbar = $s((zb="H") && allBBP : "bezbarier-agregat", 1 : "bezbarier")
        d poznamky.%Push(..BuildPoznamka("specialni", zb, bezbarLegenda(zb), "", "", katBezbar))
    }
    // rozdíly tras - písmeno → text ("neobsluhuje zast. X" / "jede přes…" / "jede jen do…" / "pokračuje…")
    s pism = ""
    f {
        s pism = $o(rozdilLegenda(pism))
        q:pism=""
        d poznamky.%Push(..BuildPoznamka("text", pism, rozdilLegenda(pism), "", "", "rozdil-trasy"))
    }
    // ruční komentáře spoje (tab MHD: Komentáře) - znak → text z SeT_SpeC
    s komentarZnak = ""
    f {
        s komentarZnak = $o(komentareLegenda(komentarZnak))
        q:komentarZnak=""
        d poznamky.%Push(..BuildPoznamka("text", komentarZnak, komentareLegenda(komentarZnak), "", "", "rucni-komentar"))
    }
    // datumová omezení (nejede/jede) - marker = číslo NZ (cnz), typ „nz" (invertovaně) pokud pochází
    // z cnz, jinak „text" (sekvenční fallback). Text per-spoj (SestavTextOmezeni).
    s n = ""
    f {
        s n = $o(poznMap("byMarker", n))
        q:n=""
        s typPozn = $g(poznMap("typ", n), "text")
        d poznamky.%Push(..BuildPoznamka(typPozn, n, poznMap("byMarker", n), "", "", "omezeni"))
    }
    q poznamky
}

/// „Spoje, nepoznámkovat" (tab MHD) - potlačí ruční komentář spoje v zadaném rozsahu tarifních čísel.
/// 1:1 legacy `noPoz^JRapiLinPrtMhd1` (ř.371-381): rozsah = odTarifniCislo~doTarifniCislo; směr dle
/// parity spoje (lichý = tam, vzestupně; sudý = zpět, sestupně); prázdné doTarifniCislo → konec trasy spoje.
ClassMethod NepoznamkovatSpoj(dataDB As %String, sku As %String, lin As %String, spojId As %String, tarifniCislo As %String) As %Boolean
{
    s zaznam = $g(^[dataDB]JRL(sku,lin,"PATCH","MHDSPNPZN",spojId))
    i zaznam = "" q 0
    s odTarifniCislo = +$p(zaznam,"~",1)
    s doTarifniCislo = +$p(zaznam,"~",2)
    i 'doTarifniCislo {
        s smerSpoje = $s(spojId#2:"T", 1:"Z")
        s doTarifniCislo = $p($p($g(^[dataDB]JRL(sku,lin,smerSpoje,spojId)),"#",1),":",2)
    }
    i (spojId#2)  i ('(tarifniCislo<odTarifniCislo)) && ('(tarifniCislo>doTarifniCislo)) q 1
    i '(spojId#2) i ('(tarifniCislo>odTarifniCislo)) && ('(tarifniCislo<doTarifniCislo)) q 1
    q 0
}

/// Atributy zastávky, které nesou **informaci pro cestujícího** - tisknou se jako značka u zastávky
/// a řádek legendy: na znamení a jeho varianty (`x`,`y`,`z`), metro A/B/C (`e`,`f`,`g`), letadlo (`O`),
/// loď (`D`), clo (`C`), paragrafy (`G`,`a`,`b`,`c`).
///
/// Zbytek číselníku 16 jsou legacy **tiskové direktivy** (tučně `T`, podtržení `p`/`P`/`o`, jen nástup
/// `N` / jen výstup `V`, s příjezdem a odjezdem `J`, tisk stanoviště `S`, odjezdová `>` / příjezdová `<`
/// / nácestná `E` / cílová pro tablo `H`) - jako značka se tisknout NESMÍ, jinak na vývěsce vyskáčou
/// nesmysly (na AD datech jsou jich tisíce: `T` 2724×, `E` 2111×, `P` 843× jen u KOVED).
///
/// Přestupy `A`/`L` jdou vlastní cestou - řídí je přepínač na prvku, viz `JePrestupovyAtribut`.
///
/// Legacy vývěska (`UVOD^JRapiLinPrtMhd` ř.189-190, `JRapiLinPrtMhd1` ř.458-472) řeší **jen `x`**;
/// zbytek je nad rámec legacy, na výslovné přání KOVED (#13048 bod 1 - „další atributy, co umožňuje
/// CIS"). V datech AD firem se dnes žádný z doplněných znaků kromě `C` (1×) nevyskytuje, takže
/// změna je pro stávající vývěsky neviditelná a je to pojistka do budoucna.
/// ⚠️ Klient nakreslí, co pošleme (přepínač „Atributy" je zapnutý) - **nedoplněný znak tiše zmizí**,
/// a přesně to byla podstata #13048 bodu 1.
ClassMethod JeViditelnyAtribut(znak As %String) As %Boolean
{
    ret ",x,y,z,e,f,g,O,D,C,G,a,b,c," [ (","_znak_",")
}

/// Přestupové atributy zastávky (přestup na jiný druh dopravy) - nad rámec legacy StopListu.
/// V1: vlak „L" + autobus „A". Symbol u zastávky řídí přepínač „Přestupy" na prvku StopList (default vyp, opt-in).
ClassMethod JePrestupovyAtribut(znak As %String) As %Boolean
{
    q (znak = "A") || (znak = "L")
}

/// Text atributu zastávky dle znaku - 1:1 s číselníkem `arrCis(16)^JRapiCis`.
ClassMethod TextAtributu(znak As %String) As %String
{
    s m("P") = "podtrženo s příjezdem a odjezdem"
    s m("T") = "tučně"
    s m("p") = "podtrženě"
    s m("N") = "jen nástup"
    s m("V") = "jen výstup"
    s m("x") = "na znamení"
    s m("y") = "na znamení od 20:00"
    s m("z") = "na znamení v prac. dny od 22:00, o víkendech celodenně"
    s m("A") = "autobus"
    s m("L") = "vlak"
    s m("e") = "Metro A"
    s m("f") = "Metro B"
    s m("g") = "Metro C"
    s m("O") = "letadlo"
    s m("D") = "loď"
    s m("C") = "clo"
    s m("J") = "s příjezdem a odjezdem"
    s m("o") = "podtrženo nad i pod"
    s m("S") = "tisk stanoviště"
    s m("G") = "paragraf"
    s m("a") = "paragraf 1"
    s m("b") = "paragraf 2"
    s m("c") = "paragraf 3"
    s m(">") = "odjezdová zastávka"
    s m("<") = "příjezdová zastávka"
    s m("E") = "nácestná zastávka"
    s m("H") = "cílová zastávka pro tablo"

    // SK varianty jen u znaků, které se na vývěsce reálně tisknou (`JeViditelnyAtribut` + přestupy).
    // Tiskové direktivy (tučně, podtrženě, jen nástup…) se jako značka nekreslí, takže je nepřekládáme.
    // „na znamenie" je znění z legacy (`MODE=1`), zbytek drží stejný vzor.
    i ..Jazyk() = "sk" {
        s s("x") = "na znamenie"
        s s("y") = "na znamenie od 20:00"
        s s("z") = "na znamenie v pracovné dni od 22:00, cez víkendy celodenne"
        s s("A") = "autobus"
        s s("L") = "vlak"
        s s("e") = "Metro A"
        s s("f") = "Metro B"
        s s("g") = "Metro C"
        s s("O") = "lietadlo"
        s s("D") = "loď"
        s s("C") = "clo"
        s s("G") = "paragraf"
        s s("a") = "paragraf 1"
        s s("b") = "paragraf 2"
        s s("c") = "paragraf 3"
        i $d(s(znak)) q s(znak)
    }
    q $g(m(znak))
}

/// Sestaví text poznámky z datumových omezení spoje (omzId 11-15, 22), 1:1 s legacy `txtNZ^JRapiNZUtl`.
/// Spoj může mít víc omezení → spojí je do jedné poznámky čárkou v pořadí 11,12,13,14,15,22.
ClassMethod SestavTextOmezeni(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String) As %String
{
    s txt = ""
    f omzId = 11,12,13,14,15,22 {
        s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
        i omz = "" continue
        s datumy = ..FormatDatumyOmezeni(omz)
        i datumy = "" continue
        i txt '= "" s txt = txt_", "
        s txt = txt_..PrefixOmezeni(omzId)_datumy
    }
    q txt
}

/// Leží zastávka `poradi` v úseku, podle kterého se rozlišují varianty tras? Tedy od vývěskové
/// zastávky (včetně) dál ve směru jízdy - 1:1 legacy smyčka `f mytc=Tc:Krok:TcDo`
/// (`JRapiLinPrtMhd1.mac:274`).
///
/// ⚠️ Rozdíl trasy PŘED vývěskovou zastávkou nesmí zakládat vlastní variantu: časy se počítají jako
/// rozdíl proti vývěskové zastávce, takže dva spoje se zajezdkou v první polovině linky dají ve druhé
/// polovině dva sloupce s IDENTICKÝMI čísly (hlásil TRANSDEV, #12843). Zpětný směr jde v pořadích
/// sestupně, proto se srovnává obráceně.
ClassMethod JeVUsekuVariant(poradi As %String, currentPoradi As %String, jeTam As %Boolean) As %Boolean
{
    q $s(jeTam:(poradi '< currentPoradi), 1:(poradi '> currentPoradi))
}

/// `B157`: jede varianta `a` jen **souvislý úsek** varianty `b`?
///
/// Signatura nese per zastávku „S" (staví), „<" (projíždí) nebo prázdno (mimo trasu) - projíždí
/// i mimo trasu znamená pro cestujícího totéž („tady nenastoupíš"), takže se rozlišuje jen „S".
/// Delší signatura než `b` úsek nedá: chybějící kousek se čte jako prázdno, tedy ne „S".
///
/// Dvě podmínky. Kde staví `a`, musí stavět i `b` - a mezi **první a poslední** zastávkou `a`
/// nesmí být žádná, kde staví `b` a `a` ne.
///
/// ⚠️ **Druhá podmínka je celý smysl `B157`** (zadání TRANSDEV 16. 9., opravuje `B142`). Bez ní
/// platí pouhá podmnožina, a tou je i varianta, která uprostřed trasy **zajíždí jinam** a část
/// zastávek vynechá. Ta se slila do delší, takže z vývěsky zmizelo, že tam spoj nejede - zákazník
/// to hlásil jako „prestali sa rozlišovať varianty, kde uprostred trasy je zachádzka". Sám uznal,
/// že požadavek zadal špatně; cílem bylo skrýt varianty jedoucí jen část trasy (od-do).
ClassMethod JeUsekemVarianty(a As %String, b As %String) As %Boolean
{
    s prvni = 0, posledni = 0
    f i=1:1:$l(a,"~") {
        i $p(a,"~",i) '= "S" continue
        i $p(b,"~",i) '= "S" ret 0
        s:'prvni prvni = i
        s posledni = i
    }
    // Varianta, která nikde nestaví, není úsek ničeho - slít ji by zahodilo sloupec beze stopy.
    i 'prvni ret 0
    f i=prvni:1:posledni {
        i ($p(b,"~",i) = "S") && ($p(a,"~",i) '= "S") ret 0
    }
    ret 1
}

/// Je varianta `a` podmnožinou `b`? **Slévání to už neřídí** (od `B157` rozhoduje
/// <method>JeUsekemVarianty</method>) - zůstává jen proto, aby šel v testu doložit rozdíl mezi
/// oběma pravidly: zacházka uprostřed trasy je podmnožinou, ale úsekem ne.
ClassMethod JePodmnozinaProSrovnani(a As %String, b As %String) As %Boolean
{
    f i=1:1:$l(a,"~") {
        i $p(a,"~",i) '= "S" continue
        i $p(b,"~",i) '= "S" ret 0
    }
    ret 1
}

/// Počet zastávek, kde varianta staví. Velikost varianty pro výběr největší nadmnožiny (`B142`) -
/// na ní stojí to, že se řetězec A ⊆ B ⊆ C vyřeší jedním průchodem.
ClassMethod PocetZastaveni(sig As %String) As %Integer
{
    s n = 0
    f i=1:1:$l(sig,"~") {
        i $p(sig,"~",i) = "S" s n = n + 1
    }
    ret n
}

/// Prefix textu pro daný typ omezení (1:1 s `txtNZ^JRapiNZUtl`).
ClassMethod PrefixOmezeni(omzId As %String) As %String
{
    // SK sloveso „premáva" je z legacy („premáva len do zast. …"), ne vymyšlené.
    i ..Jazyk() = "sk" {
        q $case(omzId, 11:"premáva len ", 12:"premáva aj ", 13:"premáva ", 14:"premáva len v su. ", 15:"premáva len v li. ", 22:"nepremáva ", :"")
    }
    q $case(omzId, 11:"jede jen ", 12:"jede také ", 13:"jede ", 14:"jede jen v su. ", 15:"jede jen v li. ", 22:"nejede ", :"")
}

/// Text do legendy pro kroužkovou denní značku týdenní platnosti (dny 1-7, svátek `+`).
/// Přednost má zákaznický číselník (`NactiTextyZnacek`), znění níže je záloha pro instalaci,
/// kde číselník značku nemá.
///
/// ⚠️ `+` je podle vyhlášky 122/2014 **neděle i státem uznané svátky**, ne jen svátek - stejné
/// znění nese výchozí číselník legacy (`JRapiExp32:489`) i náš linkový JŘ (`LinkovyJrAd.TextPlatnosti`).
ClassMethod TextDneVTydnu(dch As %String) As %String
{
    s txt = $s(dch = "":"", 1:$g(^||TiskAd("znacka", dch)))
    i txt '= "" ret txt
    i ..Jazyk() = "sk" {
        q $case(dch, "1":"premáva v pondelok", "2":"premáva v utorok", "3":"premáva v stredu", "4":"premáva vo štvrtok", "5":"premáva v piatok", "6":"premáva v sobotu", "7":"premáva v nedeľu", "+":"premáva v nedeľu a v štátom uznaný deň pracovného pokoja", :"")
    }
    q $case(dch, "1":"jede v pondělí", "2":"jede v úterý", "3":"jede ve středu", "4":"jede ve čtvrtek", "5":"jede v pátek", "6":"jede v sobotu", "7":"jede v neděli", "+":"jede v neděli a ve státem uznané svátky", :"")
}

/// Denní značky týdenní platnosti pro odjezd - závisí na **rozvržení sloupců dnů** (FTL),
/// 1:1 legacy `JRapiLinPrtMhd1` (ř.80-83 pro FTL 0, ř.112-114 pro FTL 1, ř.128-130 pro FTL 2,
/// ř.151-152 pro FTL 3). Značka smí nést jen informaci, kterou **nedává nadpis sloupce**:
///   - sobota i neděle mají vlastní sloupec (FTL 0/3) → pro 6, 7 a `+` se značka netiskne vůbec
///   - víkend je v jednom sloupci (FTL 1/2) → značka rozlišuje uvnitř sloupce, ale jen když spoj
///     nejede i v ostatní víkendové dny („6" jen bez 7 a bez `+`, „7" a `+` jen bez 6)
/// Konkrétní pracovní dny (1-5) se značkují vždy - sloupec pracovních dnů je nerozlišuje.
/// Skupinový vzor `X` a prázdná/`.` platnost značku nemají (celá skupina).
ClassMethod DenniZnacky(ftl As %String, plk As %String) As %String
{
    s znacky = ""
    f di=1:1:$l(plk) {
        s dch = $e(plk, di)
        i "12345" '[ dch continue
        s znacky = znacky_dch
    }
    i (ftl = 0) || (ftl = 3) q znacky
    i (plk["6") && (plk'["7") && (plk'["+") s znacky = znacky_"6"
    i (plk'["6") && (plk["7") s znacky = znacky_"7"
    i (plk'["6") && (plk["+") s znacky = znacky_"+"
    q znacky
}

/// Datumová část omezení - 1:1 formát zastávkového JŘ (gold `JRapiLinPrtPS3`): datumy přes `UpravDatum`
/// (strip nul ze dne/měsíce, 2místný rok), rozsah `:` → `od D1 do D2`, jednotlivé datum = `D1`,
/// položky (`#`) spojené `, `. Vedoucí prefix `k@`/`L@` se zahodí.
ClassMethod FormatDatumyOmezeni(omz As %String) As %String
{
    s data = $s(omz["@":$p(omz,"@",2), 1:omz)
    s casti = ""
    f i=1:1:$l(data,"#") {
        s item = $p(data,"#",i)
        i item = "" continue
        s od = ..UpravDatum($p(item,":",1)), do = ..UpravDatum($p(item,":",2))
        i do '= "" { s cast = "od "_od_" do "_do }
        else       { s cast = od }
        i casti '= "" s casti = casti_", "
        s casti = casti_cast
    }
    q casti
}

/// Úprava datumu pro tisk (1:1 `UprDat^JRapiLinPrtPS3`): strip vedoucích nul ze dne a měsíce; rok
/// 2místně (zastávkový JŘ ukazuje „25.12.22", ne „25.12.2022"). "" → "".
ClassMethod UpravDatum(dat As %String) As %String
{
    i dat = "" q ""
    s dd = +$p(dat,".",1), mm = +$p(dat,".",2), rr = $p(dat,".",3)
    i $l(rr) > 2 s rr = $e(rr,*-1,*)        // 2022 → 22
    q dd_"."_mm_"."_rr
}

/// Najde první linku v zadané firmě (první skupina × první linka s uzlem "I").
/// Vrací ByRef sku + lin (prázdné, pokud firma nemá v ^JRL žádnou linku).
ClassMethod PrvniLinka(dataDB As %String, ByRef sku As %String, ByRef lin As %String)
{
    s sku = $o(^[dataDB]JRL(""))
    s lin = ""
    q:sku=""
    f {
        s lin = $o(^[dataDB]JRL(sku,lin))
        q:lin=""
        i $d(^[dataDB]JRL(sku,lin,"I")) quit
    }
}

/// Normalizuje číslo linky na formát uložený v ^JRL (7 znaků, zleva doplněno nulami).
ClassMethod NormalizLinku(lin As %String) As %String
{
    i lin = "" q ""
    i $l(lin) < 7 q $e("0000000", 1, 7 - $l(lin)) _ lin
    q lin
}

/// Sestaví název linky z částí ^JRL(sku,lin,"I",1..N) spojených pomlčkou.
ClassMethod NazevLinky(dataDB As %String, sku As %String, lin As %String) As %String
{
    s nazev = "", i = ""
    f {
        s i = $o(^[dataDB]JRL(sku,lin,"I",i))
        q:i=""
        i '$isvalidnum(i) continue       // přeskoč ne-numerické (nemělo by nastat)
        i i'>0 continue                  // přeskoč konfig uzel "0" (mapování spojek názvu)
        s cast = $g(^[dataDB]JRL(sku,lin,"I",i))
        i cast="" continue               // přeskoč prázdnou část, ale pokračuj
        i nazev '= "" s nazev = nazev _ "-"
        s nazev = nazev _ cast
    }
    q nazev
}

/// POPIS_DOPRAVCE = **každý dopravce ve vlastním bloku** - název, sídlo, ulice a telefon, každé na
/// svém řádku; bloky odděluje prázdný řádek. Sesbírá distinct dopravce (fa:pr z `#`-pole 2 spoje)
/// přes oba směry a resolvuje je přes <see cref="NazevDopravce"/>.
///
/// ⚠️ Do 31. 8. tu bylo "Zajišťuje: <dopravci>" v jednom odstavci (1:1 s `sTIT`/`gDopMHD^JRapiLinPrtMhd`)
/// a u komentáře stálo, že tvar měnit nelze kvůli zlatým odtiskům a existujícím šablonám. **Rozhodnutí
/// Jan 31. 8. (#13158) to přebilo**: zákazník uvození nechce a adresu ano. Zlaté odtisky se přerazily,
/// existující šablony se nezměnily (mění se obsah pole, ne jeho název).
ClassMethod PopisDopravce(dataDB As %String, sku As %String, lin As %String) As %String
{
    k dop
    f smer="T","Z" {
        s sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
            q:sp=""
            i '$isvalidnum(sp) continue
            s carriers = $p($g(^[dataDB]JRL(sku,lin,smer,sp)), "#", 2)
            f ci=1:1:$l(carriers,"~") {
                s slot = $p(carriers,"~",ci)
                i slot = "" continue
                s fa = $p(slot,":",1)
                i fa = "" continue
                s dop(fa, $p(slot,":",2)) = ""
            }
        }
    }
    k seenName
    s txt = "", fa = ""
    f {
        s fa = $o(dop(fa))
        q:fa=""
        s pr = ""
        f {
            s pr = $o(dop(fa,pr))
            q:pr=""
            s nazev = ..NazevDopravce(dataDB, fa, pr, 1)
            i nazev = "" continue
            i $d(seenName(nazev)) continue
            s seenName(nazev) = ""
            // Prázdný řádek mezi dopravci - jinak by nešlo poznat, kde končí adresa jednoho
            // a začíná jméno druhého. U jediného dopravce se neprojeví.
            i txt '= "" s txt = txt_$c(10)_$c(10)
            s txt = txt_nazev
        }
    }
    q txt
}

/// DOPRAVCI_SOUHRNNY_JR = každý dopravce (firma × provoz) na **vlastním řádku**, bez uvození, ve tvaru
/// „Linka &lt;licenční číslo&gt; | &lt;dopravce&gt;, spoje č. &lt;rozsahy&gt;" (KOVED #13008).
/// Pojmenované podle účelu: skládá hlavičku **souhrnného jízdního řádu**, kde jednu vývěskovou linku
/// obsluhuje víc dopravců pod různými licencemi. Na obyčejné lince funguje stejně (jedna licence,
/// jedna skupina), takže se dá použít i tam.
///
/// Oproti `POPIS_DOPRAVCE` (jeden odstavec s „Zajišťuje: ") je to samostatné pole - staré nemůže
/// změnit tvar, visí na něm zlaté odtisky i existující šablony.
///
/// Řádky se seskupují podle **(licenční linka → dopravce → provoz)**, takže souhrnná linka vyrobí
/// řádek per licenci; licenční číslo dává <see cref="LicencniLinkaSpoje"/>.
/// ⚠️ Souhrnná větev je ověřená jen syntetickými vstupy - v žádné testovací firmě souhrnná linka
/// není, první reálné potvrzení přijde od zákazníka.
ClassMethod DopravciSouhrnnyJr(dataDB As %String, sku As %String, lin As %String, vypsatSpoje As %Boolean = 1) As %String
{
    s cisloLinky = +lin
    k dop, dopSpoje
    f smer="T","Z" {
        s sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
            q:sp=""
            i '$isvalidnum(sp) continue
            s spojRec = $g(^[dataDB]JRL(sku,lin,smer,sp))
            s licencniLinka = ..LicencniLinkaSpoje(spojRec, cisloLinky)
            s carriers = $p(spojRec, "#", 2)
            f ci=1:1:$l(carriers,"~") {
                s slot = $p(carriers,"~",ci)
                i slot = "" continue
                s fa = $p(slot,":",1)
                i fa = "" continue
                s pr = $p(slot,":",2)
                s dop(licencniLinka, fa, pr) = ""
                s dopSpoje(licencniLinka, fa, pr, +sp) = ""
            }
        }
    }

    k videno
    s vysledek = "", licLin = ""
    f {
        s licLin = $o(dop(licLin))
        q:licLin=""
        s fa = ""
        f {
            s fa = $o(dop(licLin,fa))
            q:fa=""
            s pr = ""
            f {
                s pr = $o(dop(licLin,fa,pr))
                q:pr=""
                s nazev = ..NazevDopravce(dataDB, fa, pr)
                i nazev = "" continue
                s radek = "Linka "_licLin_" | "_nazev
                i vypsatSpoje {
                    // Referencí nelze předat uzel s indexy - přelít do lokálního pole (merge jen když uzel je).
                    k spojeDop
                    i $d(dopSpoje(licLin, fa, pr)) m spojeDop = dopSpoje(licLin, fa, pr)
                    s rozsahy = ..RozsahyCisel(.spojeDop)
                    i rozsahy '= "" s radek = radek_", spoje č. "_rozsahy
                }
                i $d(videno(radek)) continue   // tentýž dopravce i provoz v obou směrech = jeden řádek
                s videno(radek) = ""
                i vysledek '= "" s vysledek = vysledek_$c(10)
                s vysledek = vysledek_radek
            }
        }
    }
    ret vysledek
}

/// Licenční číslo linky, pod kterou spoj jede. U **souhrnného JŘ** spoje pocházejí z několika
/// licenčních linek a každý si nese „Zdroj spoje" (`#`-pole 3 = `index linky~spoj`); index je číslo
/// linky doplněné nulami na 7 znaků, takže číslo je `$e(index,2,7)` (1:1 legacy `cnvIndLin^JRapiFce`).
/// Bez zdroje (obyčejná linka) platí číslo té linky, na které stojíme.
ClassMethod LicencniLinkaSpoje(spojRec As %String, cisloLinky As %String) As %String
{
    s zdrojIndex = $p($p(spojRec, "#", 3), "~", 1)
    i zdrojIndex = "" ret cisloLinky
    s cislo = +$e(zdrojIndex, 2, 7)
    ret $s(cislo:cislo, 1:cisloLinky)
}

/// Čísla (subscripty pole) stlačená do rozsahů: „1-31, 201-204". Dvojice se vypíše jako „1, 2" -
/// „1-2" se čte hůř než výčet. Vstup je pole s **číselnými** subscripty, iteruje se vzestupně.
ClassMethod RozsahyCisel(ByRef cisla) As %String
{
    s vysledek = "", zacatek = "", predchozi = "", c = ""
    f {
        s c = $o(cisla(c))
        q:c=""
        i '$isvalidnum(c) continue
        i zacatek = "" s zacatek = c, predchozi = c  continue
        i c = (predchozi + 1) s predchozi = c  continue
        s vysledek = vysledek_$s(vysledek="":"", 1:", ")_..RozsahText(zacatek, predchozi)
        s zacatek = c, predchozi = c
    }
    i zacatek '= "" s vysledek = vysledek_$s(vysledek="":"", 1:", ")_..RozsahText(zacatek, predchozi)
    ret vysledek
}

/// Jeden úsek výčtu: samostatné číslo, dvojice („1, 2") nebo rozsah („1-31").
ClassMethod RozsahText(odC As %String, doC As %String) As %String
{
    i odC = doC ret odC
    i doC = (odC + 1) ret odC_", "_doC
    ret odC_"-"_doC
}

/// Text dopravce (fa,pr) z `^JRSET("DOP",...)` - 1:1 s `gDopMHD`/PzaJ assembly `^JRapiLinPrtMhd`:
/// 1) je-li nastaven zkrácený MHD název (per-provoz $6 → firemní $14), použij ho SAMOSTATNĚ.
/// 2) jinak plný tvar `<obchodní název / sídlo firmy>, <sídlo provozovny>,,tel:<telefon provozovny>`.
/// <var>poRadcich</var>=1 vrátí dopravce **na víc řádcích** (název, sídlo, ulice, telefon) - tvar pro
/// `POPIS_DOPRAVCE` na vývěsce. Výchozí 0 = dosavadní jednořádkový tvar; drží ho `DOPRAVCI_SOUHRNNY_JR`,
/// který skládá vlastní řádky sám.
///
/// ⚠️ Zkrácený název **nesmí adresu potlačit**: do 31. 8. se při jeho vyplnění vracel sám a adresa
/// se pak neukázala nikdy, i když v datech byla - právě proto ji zákazník na vývěsce neviděl
/// (#13158). Nově zkracuje jen **jméno**, zbytek řádků přibude tak jako tak.
ClassMethod NazevDopravce(dataDB As %String, fa As %String, pr As %String, poRadcich As %Boolean = 0) As %String
{
    s firma = $g(^[dataDB]JRSET("DOP",fa))
    s provoz = $s(pr'="":$g(^[dataDB]JRSET("DOP",fa,pr)), 1:"")

    // Jméno: zkrácený MHD název (provozovny, pak firmy), jinak obchodní název
    s jmeno = ""
    i provoz '= "" {
        s mhd = $p(provoz,"~",6)
        i $tr(mhd," ","") '= "" s jmeno = mhd
    }
    i jmeno = "" {
        s mhd = $p(firma,"~",14)
        i $tr(mhd," ","") '= "" s jmeno = mhd
    }
    s maZkratku = (jmeno '= "")
    i jmeno = "" {
        s jmeno = $p(firma,"~",7)
        i jmeno = "" s jmeno = $p(firma,"~",1)
    }

    // Adresa provozovny (gDopFaPr: $1 sídlo, $2 ulice, $3 tel)
    s sidlo = $p(provoz,"~",1), ulice = $p(provoz,"~",2), tel = $p(provoz,"~",3)

    // Záloha z firmy **po položkách**, ne všechno nebo nic (gDopFa: $2 ulice, $3 telefon,
    // $10 sídlo i s PSČ a obcí). Provozovna bývá organizační jednotka bez ulice - „Oblast Písek"
    // má jen název a telefon, kdežto firma má „Hálkova 781/9, 370 04, České Budějovice"
    // (linka 108360, CB_CSADAD, nález Jan 31. 8.). Kdyby se čekalo, až bude provozovna prázdná
    // celá, adresa by se u takového dopravce neukázala nikdy.
    i ulice = "" s ulice = $s($p(firma,"~",10)'="":$p(firma,"~",10), 1:$p(firma,"~",2))
    i tel = ""   s tel = $p(firma,"~",3)

    i 'poRadcich {
        // Jednořádkový tvar 1:1 jako dosud - zkrácený název se vrací samotný.
        i maZkratku q jmeno
        s text = jmeno
        i sidlo '= "" s text = text_", "_sidlo
        i tel '= ""   s text = text_","_ulice_",tel:"_tel   // slot ulice mezi sídlem a telefonem (prázdná → ,,tel:)
        q text
    }

    s text = jmeno
    i sidlo '= "" s text = text_$c(10)_sidlo
    i ulice '= "" s text = text_$c(10)_ulice
    i tel '= ""   s text = text_$c(10)_"tel: "_tel
    q text
}

/// Údaje jednoho dopravce a jeho provozovny **po jednotlivých polích**, ne slepené do bloku.
/// Vrací je v `udaje` (ByRef): NAZEV, ADRESA, TELEFON, EMAIL, ICO a PROVOZOVNA_*.
///
/// ⚠️ **Číselník sám není atomický** a tohle je hranice, za kterou pole rozebrat nejde
/// (ověřeno na CB_CSADAD 1. 9.):
/// - Adresa firmy nese ulici, PSČ i obec v **jednom** poli - dělit ji parsováním by u tvarů jako
///   „č.p. 138, 569 07 Radiměř" vyrábělo nesmysly.
/// - Adresa firmy je v číselníku **dvakrát** (pole 2 a 10) a ⚠️ **nejsou vždy totožné**: u ČSAD
///   Jindřichův Hradec obě nesou „U Nádraží 694/II, 377 14 Jindřichův Hradec", ale u ČSAD AUTOBUSY
///   České Budějovice má pole 2 jen „Hálkova 781/9" a plnou adresu s PSČ a obcí nese teprve pole 10.
///   Bere se proto **pole 10** a pole 2 je záloha - shodně s <see cref="NazevDopravce"/>. Obráceně
///   by na vývěsce zmizelo PSČ a obec (odhaleno ověřením voláním 1. 9., ne úvahou).
/// - Telefon provozovny bývá slitý s webem a e-mailem („384 371 922, …, www…, info@…"). Vrací se,
///   jak je; kdo chce čistý telefon, vezme telefon firmy.
///
/// ⚠️ **Pole „zkrácený název pro MHD" se sem záměrně NEDOSTANE.** U ČSAD Jindřichův Hradec obsahuje
/// celý text včetně telefonů, takže jako jméno slouží špatně - a právě odsud pramení duplicita
/// telefonů v <see cref="NazevDopravce"/>, na kterou zákazník narazil (#13158).
ClassMethod DopravceUdaje(dataDB As %String, fa As %String, pr As %String, ByRef udaje As %String)
{
    k udaje
    s firma = $g(^[dataDB]JRSET("DOP",fa))
    s provoz = $s(pr'="":$g(^[dataDB]JRSET("DOP",fa,pr)), 1:"")

    // Obchodní název (7) má přednost před názvem (1) - u kontrolovaných dopravců jsou shodné,
    // ale obchodní tvar je ten, který patří na vývěsku.
    s udaje("NAZEV")   = $s($p(firma,"~",7)'="":$p(firma,"~",7), 1:$p(firma,"~",1))
    s udaje("ADRESA")  = $s($p(firma,"~",10)'="":$p(firma,"~",10), 1:$p(firma,"~",2))
    s udaje("TELEFON") = $p(firma,"~",3)
    s udaje("ICO")     = $p(firma,"~",5)
    s udaje("EMAIL")   = $p(firma,"~",13)
    // Fax se vědomě nevystavuje (rozhodnutí Jan 1. 9.) - na vývěsce nemá co dělat. Pole 4 číselníku
    // ho drží dál, takže se dá kdykoli přidat.

    // Provozovna: 1 je její NÁZEV („provozna Příbram"), ne sídlo - komentář u NazevDopravce
    // tvrdil opak. 4 je místo („J.Hradec", „Dačice").
    s udaje("PROVOZOVNA_NAZEV")   = $p(provoz,"~",1)
    s udaje("PROVOZOVNA_ADRESA")  = $p(provoz,"~",2)
    s udaje("PROVOZOVNA_TELEFON") = $p(provoz,"~",3)
    s udaje("PROVOZOVNA_MISTO")   = $p(provoz,"~",4)
    q
}

/// Skalární pole prvního dopravce linky (NAZEV_DOPRAVCE, ADRESA_DOPRAVCE…). Bere první řádek
/// kolekce, takže se s ní nemůže rozejít; u linky bez dopravce zapíše prázdno, aby pole ve schématu
/// existovalo vždy (chybějící klíč by v šabloně vypadal jako neznámé pole).
ClassMethod DopravceSkalary(dataDB As %String, sku As %String, lin As %String, vals As %DynamicObject)
{
    s prvni = ..BuildDopravci(dataDB, sku, lin).%Get(0)
    d vals.%Set("NAZEV_DOPRAVCE",   $s($isobject(prvni):prvni.NAZEV,   1:""))
    d vals.%Set("ADRESA_DOPRAVCE",  $s($isobject(prvni):prvni.ADRESA,  1:""))
    d vals.%Set("TELEFON_DOPRAVCE", $s($isobject(prvni):prvni.TELEFON, 1:""))
    d vals.%Set("EMAIL_DOPRAVCE",   $s($isobject(prvni):prvni.EMAIL,   1:""))
    d vals.%Set("ICO_DOPRAVCE",     $s($isobject(prvni):prvni.ICO,     1:""))
    q
}

/// Kolekce DOPRAVCI - **řádek na každou kombinaci dopravce a provozovny, která na lince jezdí**.
/// Páry (firma, provoz) sbírá z `#`-pole 2 spojů obou směrů, stejně jako <see cref="PopisDopravce"/>,
/// takže se v ní neobjeví dopravce, který linku neobsluhuje.
///
/// Šablona si z ní v tabulce z dat vybere jen sloupce, které chce - tím se řeší duplicita adres,
/// na kterou zákazník narazil: nikdo mu neslepí adresu firmy s adresou provozovny za něj (#13158).
ClassMethod BuildDopravci(dataDB As %String, sku As %String, lin As %String) As %DynamicArray
{
    s arr = ##class(%DynamicArray).%New()
    k dop
    f smer="T","Z" {
        s sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
            q:sp=""
            i '$isvalidnum(sp) continue
            s carriers = $p($g(^[dataDB]JRL(sku,lin,smer,sp)), "#", 2)
            f ci=1:1:$l(carriers,"~") {
                s slot = $p(carriers,"~",ci)
                i slot = "" continue
                s fa = $p(slot,":",1)
                i fa = "" continue
                s dop(fa, $p(slot,":",2)) = ""
            }
        }
    }

    s fa = ""
    f {
        s fa = $o(dop(fa))
        q:fa=""
        s pr = ""
        f {
            s pr = $o(dop(fa,pr))
            q:pr=""
            k udaje
            d ..DopravceUdaje(dataDB, fa, pr, .udaje)
            // Dopravce bez jména je prázdný záznam číselníku - řádek by na vývěsce jen zabral místo.
            i $g(udaje("NAZEV")) = "" continue
            s r = ##class(%DynamicObject).%New()
            s kl = ""
            f {
                s kl = $o(udaje(kl))
                q:kl=""
                d r.%Set(kl, udaje(kl))
            }
            d arr.%Push(r)
        }
    }
    q arr
}

/// Vrátí název zastávky z ^JRZ(czst) - odbav/MHD název s obcí ($7, formát "obec,místo").
/// Legacy tiskne přesně toto pole (zkrácená obec "Fr. Lázně,..." místo plné "Františkovy Lázně,...").
/// Fallback na $2, pokud $7 chybí (nevyplněný odbav název).
ClassMethod NazevZastavky(dataDB As %String, czst As %String) As %String
{
    i czst = "" q ""                 // prázdný subscript (^JRZ("")) = <SUBSCRIPT>
    s rec = ..ZaznamZastavky(dataDB, czst)
    // Pole 7 = „upravený název pro MHD" (legacy JRapiZst dta13). Má přednost, ale parametrem
    // sestavy ho lze vypnout - KOVED ho u některých zastávek má a na vývěsce chce CIS název.
    s nazev = ""
    i ..PouzitNazevProMhd() s nazev = $p(rec, "$", 7)
    i nazev = "" s nazev = $p(rec, "$", 2)
    q ..FormatNazvuZastavky(nazev, $p(rec, "$", 3), ..TvarNazvuZastavky())
}

/// Tvar názvu zastávky z parametru sestavy - per požadavek uložený do `^||TiskAd` (nastavuje
/// `GetData`/`GetMockData`, ať se nemusí protahovat všemi voláními jména).
ClassMethod TvarNazvuZastavky() As %String
{
    ret $g(^||TiskAd("nazev","tvar"))
}

/// Má se použít „upravený název pro MHD" z registru zastávek? Výchozí ano = dosavadní chování.
ClassMethod PouzitNazevProMhd() As %Boolean
{
    ret $g(^||TiskAd("nazev","mhd"), 1)
}

/// Má se u dopravce vypsat výčet spojů? Parametr sestavy, výchozí ano (tak to má zákazník na vzoru).
ClassMethod VypsatSpojeDopravce() As %Boolean
{
    ret ($zcvt($g(^||ML("IN","SPOJE_U_DOPRAVCE")),"L") '= "false")
}

/// Přečte nastavení názvů zastávek z parametrů sestavy do `^||TiskAd`. Volá se po úklidu cache
/// na začátku požadavku, než se začnou skládat jména.
/// Jazyk textů značek pro tento request = kultura šablony (klient posílá `JAZYK` = dvoumístný kód).
/// Prázdné / neznámé → čeština. Drží se v `^||TiskAd` jako ostatní per-request invarianty, aby se
/// nemusel protahovat všemi signaturami textových helperů.
///
/// ⚠️ **Jen texty značek** (#12843: zákazník má správně slovenské piktogramy, ale české popisky).
/// Nadpisy sloupců dnů ani nadpisy prvků sem nepatří - ty si uživatel přepisuje v šabloně.
ClassMethod NactiJazyk()
{
    s kod = $zcvt($p($g(^||ML("IN","JAZYK")),"-",1),"L")
    s ^||TiskAd("jazyk") = $s(kod = "sk":"sk", 1:"cs")
}

/// Jazyk textů tohoto requestu (`cs` / `sk`). Bez `NactiJazyk` (starší klient, sonda) → `cs`.
ClassMethod Jazyk() As %String
{
    ret $g(^||TiskAd("jazyk"), "cs")
}

/// Texty denních značek z číselníku `^JRSET("NCOME",rec,znak)` - zákazník si je v EDISONu přepisuje
/// a starý modul je odtud bere taky (`JRapiLinPrtMhd1:113`), stejně jako náš linkový JŘ.
/// Načte se jednou per request, `TextDneVTydnu` pak čte tuhle cache.
///
/// Záznam se volí podle jazyka sestavy (`NactiJazyk`), ne podle instalace - firma může mít
/// obě jazykové varianty vyplněné a texty značek se řídí kulturou šablony (#12843).
ClassMethod NactiTextyZnacek(dataDB As %String)
{
    s rec = $s(..Jazyk() = "sk":"*SK", 1:"CZ")
    f i=1:1:8 {
        s znak = $p("1,2,3,4,5,6,7,+", ",", i)
        s txt = $g(^[dataDB]JRSET("NCOME", rec, znak))
        i txt '= "" s ^||TiskAd("znacka", znak) = txt
    }
}

/// Bezbariérovost spoje. SK znění „všetky spoje s bezbariérovým prístupom do vozidla" je z jeho
/// starého tisku (#12843), ne vymyšlené.
ClassMethod TextBezbarier(vsechny As %Boolean) As %String
{
    i ..Jazyk() = "sk" {
        ret $s(vsechny:"všetky spoje s bezbariérovým prístupom do vozidla", 1:"spoj má bezbariérovo prístupné vozidlo")
    }
    ret $s(vsechny:"všechny spoje mají bezbariérově přístupné vozidlo", 1:"spoj má bezbariérově přístupné vozidlo")
}

ClassMethod TextCastecneBezbarier() As %String
{
    i ..Jazyk() = "sk" ret "spoj má čiastočne bezbariérovo prístupné vozidlo, potrebná pomoc sprievodcu"
    ret "spoj má částečne bezbariérově přístupné vozidlo, nutná dopomoc průvodce"
}

ClassMethod TextKola() As %String
{
    i ..Jazyk() = "sk" ret "spoj prepravuje bicykle"
    ret "spoj přepravuje jízdní kola"
}

/// SK znění z legacy (`MODE=1`: „cestovný lístok s miestenkou je možné zakúpiť").
ClassMethod TextMistenky() As %String
{
    i ..Jazyk() = "sk" ret "cestovný lístok s miestenkou je možné zakúpiť"
    ret "k jízdence je možné zakoupit místenku"
}

/// Předložková část věty o přestupu; za ni se lepí druh dopravy z číselníku („autobus" / „vlak").
ClassMethod TextPrestupu() As %String
{
    i ..Jazyk() = "sk" ret "prestup na "
    ret "přestup na "
}

ClassMethod TextSouhrnuNaZnameni() As %String
{
    i ..Jazyk() = "sk" ret "všetky zastávky linky sú na znamenie"
    ret "všechny zastávky linky jsou na znamení"
}

/// Věty o rozdílu trasy spoje vůči vzorové trase. SK znění je 1:1 legacy `OmTrasa^JRapiLinPrtMhd`
/// (`MODE=1`): „premáva cez" / „nepremáva cez" / „premáva len do". Slovo „pokračuje" legacy ve SK
/// nechává - je slovensky správné, mění se jen předložka („cez"), viz `TextPokracovani`/`TextPres`.
ClassMethod TextJedePres() As %String
{
    i ..Jazyk() = "sk" ret "premáva cez zast. "
    ret "jede přes zast. "
}

ClassMethod TextNeobsluhuje() As %String
{
    i ..Jazyk() = "sk" ret "nepremáva cez zast. "
    ret "neobsluhuje zast. "
}

ClassMethod TextJedeJenDo() As %String
{
    i ..Jazyk() = "sk" ret "premáva len do zast. "
    ret "jede jen do zast. "
}

/// Prodloužení za konec vzorové trasy (#7947) - větu legacy nemá, takže SK drží jeho vzor („premáva").
ClassMethod TextJedeAzDo() As %String
{
    i ..Jazyk() = "sk" ret "premáva až do zast. "
    ret "jede až do zast. "
}

/// Předložka mezi cílem a výčtem průjezdů („… přes X, Y").
ClassMethod TextPres() As %String
{
    i ..Jazyk() = "sk" ret " cez "
    ret " přes "
}

ClassMethod NactiNastaveniNazvu()
{
    s ^||TiskAd("nazev","tvar") = $g(^||ML("IN","TVAR_NAZVU_ZASTAVKY"))
    // Tolerantně jako ostatní bool parametry (starší klient posílal „True"); prázdné = zapnuto.
    s ^||TiskAd("nazev","mhd") = ($zcvt($g(^||ML("IN","NAZEV_PRO_MHD")),"L") '= "false")
}

/// "Číselníkový" (CIS) název zastávky z ^JRZ `$2` - zkrácený, s `,,` zachovaným (1:1 legacy POPIS_ZASTAVKA_CIS dta20).
ClassMethod NazevZastavkyCis(dataDB As %String, czst As %String) As %String
{
    i czst = "" q ""          // prázdný czst → prázdný subscript (^JRZ("")) = <SUBSCRIPT>; ošetřit
    q $p(..ZaznamZastavky(dataDB, czst), "$", 2)
}

/// Záznam zastávky z registru k „platí od" linky, pro kterou se stránka staví (`B188`, časové
/// varianty) - datum nastaví `BuildStrankaZastavky`. Mimo sestavu stránky (bez data) platí
/// dnešek jako v legacy `gZst`. `variantaOd` = začátek vybrané varianty (sloupky).
ClassMethod ZaznamZastavky(dataDB As %String, czst As %String, ByRef variantaOd As %String = "") As %String
{
    ret ..ZaznamZastavkyKDatu(dataDB, czst, $g(^||TiskAd("zastavky","hrl")), .variantaOd)
}

/// Číslo zastávky dle celostátního registru (CIS) - `^JRZ(czst)` `$`-pole **9** (legacy `JRapiZst`
/// ř.150 `dta14`). KOVED #13088.
/// ⚠️ Není to `czst`, i když se to na některých datech shoduje (firma s číslováním přebraným z CIS).
/// ⚠️ Šestimístný údaj v `$`-poli 5 (`419940`) je **číslo pro strojky** = CIS × koeficient
/// (`JRapiZst` ř.327), ne CIS kód - nepoužívat.
ClassMethod CisloZastavkyCis(dataDB As %String, czst As %String) As %String
{
    i czst = "" ret ""        // prázdný subscript → <SUBSCRIPT>
    ret $p(..ZaznamZastavky(dataDB, czst), "$", 9)
}

/// Vrátí ukázková data pro live preview v designeru.
///
/// **Vědomé rozhodnutí (2026-07-11, audit C-e - won't-do):** mock = živý `GetData` nad první
/// linkou aktuální firmy, NE zmrazený statický vzorek. Zákazníci AD data mají vždy, výkon není
/// problém a živý mock nemůže driftovat od kontraktu (nová pole dostane automaticky).
/// Nedeterminismus náhledu per firma je akceptovaný. Fallback pro firmu bez linek níže.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd                       // cache per-linka/per-směr invariantů - fresh per request
        d ..NactiNastaveniNazvu()
        d ..NactiJazyk()
        d ..NactiTextyZnacek(dataDB)

        // Živý náhled z první linky aktuální firmy (session dataDB) - zrcadlí reálný GetData.
        d ..PrvniLinka(dataDB, .sku, .lin)
        s page = $s(lin'="":..BuildStrankaZastavky(dataDB, sku, lin, , , , IdFa), 1:"")

        // Fallback - když firma nemá v ^JRL žádnou linku, ať designer nespadne.
        i page = "" {
            s page = ##class(%DynamicObject).%New()
            s v = ##class(%DynamicObject).%New()
            d v.%Set("POPIS_LINKY", "(žádná linka v ^JRL pro firmu "_dataDB_")")
            d page.%Set("values", v)
            s r = ##class(%DynamicObject).%New()
            d r.%Set("ZASTAVKY", ##class(%DynamicArray).%New())
            d r.%Set("SKUPINY_DNU", ##class(%DynamicArray).%New())
            d r.%Set("ODJEZDY", ##class(%DynamicArray).%New())
            d r.%Set("POZNAMKY", ##class(%DynamicArray).%New())
            d r.%Set("DOPRAVCI", ##class(%DynamicArray).%New())
            d page.%Set("rows", r)
        }

        s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Standardní JŘ schema (duplikováno z ZastavkoveJrMhd - stejný výstupní formát)
/// ═══════════════════════════════════════════════════════════════════════════════
ClassMethod AddStandardJRFields(fields As %DynamicArray)
{
    // Pořadí je seskupené podle toho, čeho se pole týká (linka → dopravce → platnost → zastávka →
    // poznámky), ne podle toho, kdy přibylo. Zastávková pole byla do 31. 8. rozházená mezi ostatní
    // a uživatel je v seznamu hledal po jednom (nález Jan 31. 8.).

    // ── linka ──
    d ..AddField(fields, "CISLO_LINKY",            "Číslo linky (dle licence)", "number")
    d ..AddField(fields, "LOKALNI_OZNACENI_LINKY", "Veřejné označení linky", "text")
    d ..AddField(fields, "TITULEK",                "Titulek sestavy",        "text")
    d ..AddField(fields, "POPIS_LINKY",            "Popis linky",            "text")
    d ..AddField(fields, "TYP_JR",                 "Typ jízdního řádu",      "text")
    d ..AddField(fields, "BARVA_LINKY",            "Barva linky",            "color")
    d ..AddField(fields, "BARVA_LINKY_2",          "Barva linky 2",          "color")

    // ── dopravce ──
    d ..AddField(fields, "POPIS_DOPRAVCE",         "Popis dopravce",         "text")
    d ..AddField(fields, "DOPRAVCI_SOUHRNNY_JR",   "Dopravci souhrnného JŘ", "text")
    // Údaje po jednotlivých polích - aby si šablona složila jen to, co chce, a adresa nevyšla
    // dvakrát (#13158). Nesou PRVNÍHO dopravce linky; víc dopravců je v kolekci Dopravci.
    d ..AddField(fields, "NAZEV_DOPRAVCE",         "Název dopravce",         "text")
    d ..AddField(fields, "ADRESA_DOPRAVCE",        "Adresa dopravce",        "text")
    d ..AddField(fields, "TELEFON_DOPRAVCE",       "Telefon dopravce",       "text")
    d ..AddField(fields, "EMAIL_DOPRAVCE",         "E-mail dopravce",        "text")
    d ..AddField(fields, "ICO_DOPRAVCE",           "IČO dopravce",           "text")

    // ── platnost ──
    // Bez „Popisu platnosti" (Jan 23. 10.): větu si šablona složí z dat od-do. GetData ho posílá dál
    // kvůli šablonám, které ho už mají; v nabídce není, aby se nepoužíval dál.
    d ..AddField(fields, "DATUM_PLATNOSTI_OD",     "Datum platnosti od",     "date")
    d ..AddField(fields, "DATUM_PLATNOSTI_DO",     "Datum platnosti do",     "date")

    // ── zastávka, na které vývěska visí, a směr ──
    d ..AddField(fields, "POPIS_ZASTAVKA",         "Aktuální zastávka",      "text")
    d ..AddField(fields, "POPIS_ZASTAVKA_CIS",     "Aktuální zastávka (CIS)","text")
    d ..AddField(fields, "CISLO_ZASTAVKA",         "Číslo zastávky",         "text")
    d ..AddField(fields, "CISLO_ZASTAVKA_CIS",     "CIS číslo zastávky",     "text")
    d ..AddField(fields, "NASTUPISTE",             "Označník",               "text")
    d ..AddField(fields, "SLOUPEK",                "Sloupek",                "text")
    d ..AddField(fields, "SLOUPEK_POPIS",          "Popis sloupku",          "text")
    d ..AddField(fields, "SMER",                   "Směr (T/Z)",             "text")
    d ..AddField(fields, "KONECNA_ZASTAVKA",       "Konečná zastávka",       "text")
    d ..AddField(fields, "KONECNA_ZASTAVKA_CIS",   "Konečná zastávka (CIS)", "text")
    d ..AddField(fields, "CISLO_ZASTAVKA_VYCHOZI_CIS", "CIS číslo výchozí zastávky", "text")
    d ..AddField(fields, "CISLO_ZASTAVKA_CILOVA_CIS",  "CIS číslo cílové zastávky",  "text")

    // ── poznámky a doplňky ──
    d ..AddField(fields, "POZNAMKA_POD_DOPRAVCE",  "Poznámka pod dopravce",  "text")
    d ..AddField(fields, "POZNAMKA_NAD_LINKU",     "Poznámka nad linku",     "text")
    d ..AddField(fields, "INFORMACE",              "Informace",              "text")
    d ..AddField(fields, "VYLUKOVE_TEXTY",         "Výlukové texty",         "text")
    d ..AddField(fields, "QR_KOD",                 "QR kód URL",             "text")
    // _CIS varianty (celostátní názvy) jsou AD-specifické - MHD je zatím neprodukuje.
    // VARIANTY_TRAS (pole hlaviček variantních časových sloupců) je wire-only pole ve values -
    // pole (ne skalár), konzumuje ho LineTable/StopList, ne plochý binding → ve schématu nefiguruje.
}

ClassMethod AddStandardJRCollections(collections As %DynamicArray)
{
    // Pole = skalární klíče, které reálně produkují Build* metody v GetData (1:1 s ostrými daty).
    // ZONY (skalární pole) se vystavuje - plochá buňka ho zobrazí spojené („1/2", GetString join).
    // Objektová vnořená pole (ATRIBUTY {ZNAK,POPIS}, POZNAMKY_ZASTAVKY, ANOTACE) se NEvystavují -
    // plochá tabulka je neumí; spotřebovávají je specializované elementy (StopList/DepartureGrid).

    // ZASTAVKY (BuildZastavka)
    s colZastavky = ##class(%DynamicObject).%New()
    s colZastavky.name          = "Zastávky"
    s colZastavky.collectionKey = "ZASTAVKY"
    s colZastavkyFields = ##class(%DynamicArray).%New()
    d ..AddField(colZastavkyFields, "NAZEV_ZASTAVKY",    "Název zastávky",    "text")
    d ..AddField(colZastavkyFields, "CAS_JIZDY",         "Čas jízdy",         "number")
    d ..AddField(colZastavkyFields, "PORADI",            "Pořadí",            "number")
    d ..AddField(colZastavkyFields, "AKTUALNI_ZASTAVKA", "Aktuální zastávka", "boolean")
    d ..AddField(colZastavkyFields, "ZONY",              "Zóny",              "text")
    d ..AddField(colZastavkyFields, "NASTUPISTE",        "Označník",          "text")
    d ..AddField(colZastavkyFields, "NASTUPISTE_SPOJU",  "Označník (dle spojů)", "text")
    d ..AddField(colZastavkyFields, "SLOUPEK",           "Sloupek",           "text")
    d ..AddField(colZastavkyFields, "SLOUPEK_SPOJU",     "Sloupek (dle spojů)", "text")
    d ..AddField(colZastavkyFields, "SLOUPEK_POPIS",     "Popis sloupku",     "text")
    d ..AddField(colZastavkyFields, "CISLO_ZASTAVKA_CIS", "CIS číslo zastávky", "text")
    // Pozn.: TC (tarifní číslo) v AD záměrně nevystaveno - nese jen J-klíč (= interní ID),
    // ne reálné tarifní číslo; MHD TC (skutečné číslo zastávky) vystaveno zůstává.
    // TRASA_MIN (pole variantních časů jízdy v každém řádku) je wire-only - pole (ne skalár),
    // konzumuje ho StopList (variantní časové sloupce), plochá tabulka ne → ve schématu nefiguruje.
    s colZastavky.fields = colZastavkyFields
    d collections.%Push(colZastavky)

    // SKUPINY_DNU (BuildSkupinaDnu)
    s colSkupiny = ##class(%DynamicObject).%New()
    s colSkupiny.name          = "Skupiny dnů"
    s colSkupiny.collectionKey = "SKUPINY_DNU"
    s colSkupinyFields = ##class(%DynamicArray).%New()
    // ID (join/pivot klíč) zůstává ve wire, ale NEvystavuje se jako sloupec.
    d ..AddField(colSkupinyFields, "NADPIS",  "Nadpis skupiny",   "text")
    d ..AddField(colSkupinyFields, "NADPIS2", "Nadpis skupiny 2", "text")
    d ..AddField(colSkupinyFields, "PORADI",  "Pořadí",           "number")
    s colSkupiny.fields = colSkupinyFields
    d collections.%Push(colSkupiny)

    // ODJEZDY (BuildOdjezd)
    s colOdjezdy = ##class(%DynamicObject).%New()
    s colOdjezdy.name          = "Odjezdy"
    s colOdjezdy.collectionKey = "ODJEZDY"
    s colOdjezdyFields = ##class(%DynamicArray).%New()
    // Ploché user-facing pole. ZASTAVKA_ID/SKUPINA_DNU_ID (join/pivot klíče) + FORE_COLOR/BACK_COLOR
    // (prezentační nápovědy, čte je jen render) zůstávají ve wire, ale NEvystavují se jako sloupce.
    d ..AddField(colOdjezdyFields, "HODINA", "Hodina", "number")
    d ..AddField(colOdjezdyFields, "MINUTA", "Minuta", "number")
    s colOdjezdy.fields = colOdjezdyFields
    d collections.%Push(colOdjezdy)

    // POZNAMKY (BuildPoznamka)
    s colPoznamky = ##class(%DynamicObject).%New()
    s colPoznamky.name          = "Poznámky"
    s colPoznamky.collectionKey = "POZNAMKY"
    s colPoznamkyFields = ##class(%DynamicArray).%New()
    // Ploché user-facing pole (legenda = znak + text). KATEGORIE (interní filtr-tag) + FORE_COLOR/
    // BACK_COLOR (prezentační nápovědy) zůstávají ve wire, ale NEvystavují se jako sloupce.
    d ..AddField(colPoznamkyFields, "TYP",  "Typ poznámky",  "text")
    d ..AddField(colPoznamkyFields, "ZNAK", "Znak",          "text")
    d ..AddField(colPoznamkyFields, "TEXT", "Text poznámky", "text")
    s colPoznamky.fields = colPoznamkyFields
    d collections.%Push(colPoznamky)

    // ⚠️ DOPRAVCI (BuildDopravci) se ZÁMĚRNĚ NEVYSTAVUJE - data na drátě jsou, ale uživatel je
    // v nabídce polí nenajde (rozhodnutí Jan 1. 9., rozdělaná práce v backlogu).
    //
    // Důvod: řádek na (dopravce × provozovna) **nutně opakuje údaje firmy**. Tři provozovny téhož
    // dopravce daly třikrát pod sebou týž telefon a adresu firmy - a tabulka z dat s tím neumí nic
    // udělat, protože nemá deduplikaci. Původní záměr byl zbavit se duplicity ze slepeného
    // POPIS_DOPRAVCE, takže takhle by vyměnila jednu duplicitu za druhou. Navíc to není ve své
    // podstatě tabulka, ale hierarchie „dopravce → jeho provozovny".
    //
    // Kód i wire zůstávají živé, ať se na tom dá stavět; skalární pole dopravce (NAZEV_DOPRAVCE…)
    // fungují a vystavená jsou. Než se kolekce zapne, musí být jasné, čím se ta hierarchie kreslí.
}

ClassMethod AddStandardJRParameters(arr As %DynamicArray)
{
    // `group` = vizuální seskupení v liště parametrů / dialogu (aditivní klíč - starší klient ignoruje).
    // Konvence: skupina = UI název elementu, který parametr ovlivňuje („Seznam zastávek" /
    // „Minuty odjezdů" / „Poznámky"); parametry pro celou šablonu → „Obecné".
    // ⚠️ Skupina je jen vizuální - persistují se názvy a hodnoty parametrů, ne jejich skupina,
    // takže přejmenování skupiny uloženou volbu uživatele nezahodí (na rozdíl od labelů voleb).
    // „Obecné" je první schválně - je to nastavení celé sestavy, ne jednoho prvku.

    // ⚠️ Výchozí „Zkrácený" = dosavadní chování - jiná výchozí hodnota by pohnula zlatými odtisky.
    d arr.%Push(..ParametrTvaruNazvuZastavky("Zkrácený"))

    d ..AddBoolParam(arr, "NAZEV_PRO_MHD", "Použít název pro MHD", "true", "Obecné", "Zastávka může mít v číselníku vedle názvu podle CIS ještě upravený název pro MHD; ten se tiskne přednostně. Vypnutím se použije název podle CIS.", 1)
    d ..AddBoolParam(arr, "SPOJE_U_DOPRAVCE", "Výčet spojů u dopravce", "true", "Obecné", "V poli Dopravci souhrnného JŘ se za dopravce dopíše, které spoje jezdí (čísla stlačená do rozsahů). Netýká se pole Popis dopravce.", 1)

    // `B161`: výchozí pořadí je parita se starým tiskem (`JRapiLinPrtMhd1.mac:397-398`, zastávka
    // vnější smyčka, směr vnitřní) - obě vývěsky téže zastávky jdou za sebou, takže se vytisknou
    // a pověsí jedním výjezdem. TRANSDEV chce projít trasu tam a pak zpět; místo přepsání logiky
    // je to volba, protože tvar pořadí se za měsíc měnil potřetí (rozhodnutí Jan 18. 9.).
    s pPorStr = ##class(%DynamicObject).%New()
    s pPorStr.name         = "PORADI_STRANEK"
    s pPorStr.label        = "Pořadí stránek"
    s pPorStr.type         = "enum"
    s pPorStr.group        = "Obecné"
    s pPorStr.serverSide   = 1
    s pPorStr.defaultValue = "Po zastávkách (oba směry za sebou)"
    s pPorStr.description  = "V jakém pořadí jdou vývěsky v exportu. Po zastávkách = linka se projde jednou a u každé zastávky jsou obě její vývěsky za sebou (tak to dělá i starý tisk). Po směrech = nejdřív celý směr Tam od první zastávky k poslední, pak celý směr Zpět od poslední k první, takže se trasa projde tam a zase zpátky."
    s optsPS = ##class(%DynamicArray).%New()
    d optsPS.%Push("Po zastávkách (oba směry za sebou)")
    d optsPS.%Push("Po směrech (tam vzestupně, zpět sestupně)")
    s pPorStr.options = optsPS
    d arr.%Push(pPorStr)

    d ..AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true",  "Seznam zastávek")
    d ..AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false", "Seznam zastávek")
    // #8828: vývěsková zastávka obsluhovaná z více označníků → samostatná stránka per označník
    // (odjezdy filtrované na spoje z toho označníku). Default vyp = jedna vývěska, odjezdy smíchané.
    // Label zrcadlí volbu elementu „Řádek pro každý označník" - stránka × řádek = dvě roviny téže věci
    d ..AddBoolParam(arr, "ROZDELIT_PER_OZNACNIK", "Samostatná stránka pro každé stanoviště", "false", "Seznam zastávek", "Zastávka obsluhovaná z více stanovišť se vytiskne jako samostatná vývěska pro každé z nich, jen s odjezdy spojů, které z něj jedou. V titulku stránky pak je i jeho číslo. Které číslo to je, řídí volba Číslo stanoviště podle.", 1)

    // KOVED #13088: označník a sloupek jsou dvě různá pole (volný text z trasy vs. číselník sloupků
    // zastávky) a zákazníci, kteří evidují sloupky, chtějí logiku stanovišť nad nimi. Jeden přepínač
    // pro všechno - režimy více hodnot, řádky, dělení stránek i titulek; prvek se jím řídí a vlastní
    // kopii nemá (dvě místa pro totéž = past odklizená v B41).
    // ⚠️ Výchozí „Označník" = dosavadní chování; jiný default by pohnul vývěskou zákazníkovi,
    // který dělení stránek už používá.
    s pZdrojSt = ##class(%DynamicObject).%New()
    s pZdrojSt.name         = "ZDROJ_STANOVISTE"
    s pZdrojSt.label        = "Číslo stanoviště podle"
    s pZdrojSt.type         = "enum"
    s pZdrojSt.group        = "Seznam zastávek"
    s pZdrojSt.serverSide   = 1
    s pZdrojSt.defaultValue = "Označník"
    s pZdrojSt.description  = "Který údaj z jízdního řádu se bere jako číslo stanoviště: Označník (pole nástupiště z trasy linky) nebo Sloupek (číslo z číselníku sloupků zastávky). Volba řídí Více stanovišť, samostatné stránky i titulek stránky. Sloupce Označník a Sloupek jdou tisknout oba nezávisle."
    s optsZS = ##class(%DynamicArray).%New()
    d optsZS.%Push("Označník")
    d optsZS.%Push("Sloupek")
    s pZdrojSt.options = optsZS
    d arr.%Push(pZdrojSt)

    // Varianty tras (KOVED #13048 bod 5) - týkají se variantních sloupců seznamu zastávek.
    // Varianta = sled zastávek (legacy dedup), časy sloupce = průběh jedné skutečné trasy.
    // ⚠️ Písmena variant a písmena rozdílových poznámek jsou dvě NEZÁVISLÉ řady a obě začínají od
    // „A", takže se na jedné minutě můžou potkat dvě stejná písmena v jiném významu. Vědomý
    // kompromis (rozhodnutí Jan 13. 8.) - řeší se, až na to někdo narazí. Starý modul se tomu vyhnul
    // vlastní řadou od konce abecedy („UVWXYZ"), jenže tu bere z nastavení u linky, které nemá
    // vyplněné ani jedna z 325 prověřených linek - takže značky netiskne vůbec.
    s pRazV = ##class(%DynamicObject).%New()
    s pRazV.name         = "RAZENI_VARIANT"
    s pRazV.label        = "Řazení variant tras"
    s pRazV.type         = "enum"
    s pRazV.group        = "Seznam zastávek"
    s pRazV.serverSide   = 1
    s pRazV.defaultValue = "Dle četnosti ze zastávky"
    s pRazV.description  = "Pořadí variantních časových sloupců v seznamu zastávek: nejčastější varianta z dané zastávky první, stabilní pořadí dle spojů (stejné napříč stránkami), nebo od nejrychlejší varianty k nejpomalejší."
    s optsRV = ##class(%DynamicArray).%New()
    d optsRV.%Push("Dle četnosti ze zastávky")
    d optsRV.%Push("Stabilní (pořadí spojů)")
    // Popisky voleb jsou po vydání ZAMČENÉ (persistuje se zobrazovaný text), proto „nejkratší první"
    // rovnou v popisku - směr řazení už nepůjde upřesnit přejmenováním.
    d optsRV.%Push("Dle délky jízdy (nejkratší první)")
    d optsRV.%Push("Dle délky jízdy (nejdelší první)")
    s pRazV.options = optsRV
    d ..JenSeSloupcemVariant(pRazV)
    d arr.%Push(pRazV)

    // `B181`: hned pod řazením, protože ořezává podle něj.
    d ..AddNumberParam(arr, "MAX_SLOUPCU_VARIANT", "Nejvýš sloupců variant tras", "", "Seznam zastávek", "Kolik sloupců variant tras se nejvýš vytiskne. Prázdné = všechny. Vezmou se první podle řazení variant, ostatní se netisknou - jejich spoje pak ve sloupcích nemají svou trasu ani písmeno varianty. Zúží to seznam zastávek za cenu té informace.", 1)
    d ..JenSeSloupcemVariant(arr.%Get(arr.%Size() - 1))

    // `B142`: vědomá výměna „méně sloupců za méně informace", proto výchozí vypnuto. Popisek i
    // nápověda musí říct, CO to schová - jinak si to zapne někdo, kdo variantní sloupce nasadil
    // právě kvůli detailu (KOVED #13048).
    d ..AddBoolParam(arr, "SLUCOVAT_PODMNOZINY", "Slučovat varianty, které jedou jen část jiné", "false", "Seznam zastávek", "Varianta, která jede jen souvislý úsek jiné trasy (od - do), dostane sloupec té delší. Ubere to sloupce na lince, kde část spojů jezdí jen kus trasy. Varianta, která uprostřed zajíždí jinam a část zastávek vynechá, si sloupec nechá - jinak by z vývěsky zmizelo, že tam spoj nejede.", 1)
    d ..JenSeSloupcemVariant(arr.%Get(arr.%Size() - 1))

    // ⚠️ Následující dvě volby musí zůstat vedle sebe a v tomhle pořadí - obě jsou o písmenech
    // variant a slučování mezi ně nepatří (nález Jan 14. 9.).
    // Zapínání písmen variant se 17. 8. zrušilo (bez nich nešlo u minuty poznat, který časový
    // sloupec pro ni platí) a 11. 9. se na žádost TRANSDEV vrátilo: vývěska, která varianty
    // vysvětluje jinde, je nepotřebuje. Hodnotu aplikuje KLIENT (vyprázdní záhlaví sloupců
    // a zhasne anotaci u minuty), takže do allowlistu server-side parametrů nepatří - server
    // písmena posílá dál a nic se kvůli té volbě znovu nenačítá.
    d ..AddBoolParam(arr, "PISMENA_VARIANT", "Písmena variant tras", "true", "Seznam zastávek", "Označení variantních sloupců jízdních dob - v záhlaví sloupců i u minut odjezdů. Vypnutím sloupce s časy zůstanou, jen bez písmen. Netýká se písmen rozdílů tras, ta se vypínají u linky.")
    d ..JenSeSloupcemVariant(arr.%Get(arr.%Size() - 1))

    s pPrvB = ##class(%DynamicObject).%New()
    s pPrvB.name         = "PRVNI_BEZ_PISMENE"
    s pPrvB.label        = "První varianta bez písmene"
    s pPrvB.type         = "bool"
    s pPrvB.defaultValue = "true"
    s pPrvB.group        = "Seznam zastávek"
    s pPrvB.serverSide   = 1
    s pPrvB.description  = "Nejčastější varianta zůstane bez písmene (hlavní trasa), odchylky se značí od A. Vypnuto = písmena už od první varianty."
    // Bez písmen variant tato volba nemá co ovlivnit - schovat ji je míň matoucí než ji nechat
    // svítit jako mrtvé zaškrtávátko.
    s pPrvB.visibleWhen  = "PISMENA_VARIANT=true"
    d ..JenSeSloupcemVariant(pPrvB)
    d arr.%Push(pPrvB)

    // Rozdělení sloupců typů provozu - override FTL (auto = dle linky MHD_TYP_R). 4 módy = kombinace
    // {prázdninový split prac. dnů} × {sloučení víkendu}; reuse existujícího NacistSkupinyDnu/ZarazeniSpoje.
    s pRozd = ##class(%DynamicObject).%New()
    s pRozd.name         = "ROZDELENI_SLOUPCU"
    // Název jako u linkového AD (Jan 23. 10.); do té doby „Rozdělení sloupců (typy provozu)" - typy
    // provozu jsou pojem z MHD, v AD se spoje dělí podle dnů platnosti.
    s pRozd.label        = "Rozdělení sloupců dle platnosti"
    s pRozd.type         = "enum"
    s pRozd.group        = "Minuty odjezdů"
    s pRozd.serverSide   = 1
    s pRozd.description  = "Podle čeho se spoje rozdělí do sloupců minut odjezdů. Automaticky = podle nastavení u linky v jízdních řádech. Vlastní rozložení se nastavuje u prvku Minuty odjezdů - tady se jen ukáže, že ho šablona má."
    s pRozd.defaultValue = "Automaticky (dle linky)"
    s optsR = ##class(%DynamicArray).%New()
    d optsR.%Push("Automaticky (dle linky)")
    d optsR.%Push("Pracovní / Sobota / Neděle+svátky")
    d optsR.%Push("Pracovní / víkend (So+Ne+svátky)")
    d optsR.%Push("Prázdniny: Prac. mimo+o prázd. / víkend")
    d optsR.%Push("Prázdniny: Prac. mimo+o prázd. / Sobota / Neděle+svátky")
    // Ukazatel, ne rozvržení: klient ji nastaví sám, když si uživatel sloupce upravil tak, že
    // neodpovídají žádnému vzoru. Server ji nezná a spadne do „dle linky" - na výsledku nezáleží,
    // protože s vlastními sloupci stejně přijde DEFINICE_SLOUPCU a ta FTL přebíjí.
    d optsR.%Push("Vlastní rozložení")
    s pRozd.options = optsR
    d arr.%Push(pRozd)

    s param = ##class(%DynamicObject).%New()
    s param.name         = "CASOVA_OSA"
    s param.label        = "Časová osa odjezdů"
    s param.type         = "enum"
    s param.group        = "Minuty odjezdů"
    s param.defaultValue = "Jen hodiny s odjezdy"
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("Jen hodiny s odjezdy")
    d opts.%Push("Souvislá od první do poslední")
    d opts.%Push("Vlastní rozsah")
    s param.options = opts
    d arr.%Push(param)

    s pOd = ##class(%DynamicObject).%New()
    s pOd.name         = "HODINY_OD"
    s pOd.label        = "Hodiny od"
    s pOd.type         = "number"
    s pOd.group        = "Minuty odjezdů"
    s pOd.defaultValue = "4"
    s pOd.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pOd)

    s pDo = ##class(%DynamicObject).%New()
    s pDo.name         = "HODINY_DO"
    s pDo.label        = "Hodiny do"
    s pDo.type         = "number"
    s pDo.group        = "Minuty odjezdů"
    s pDo.defaultValue = "23"
    s pDo.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pDo)

    d ..AddBoolParam(arr, "ROZTAHNOUT_NA_VYSKU", "Roztáhnout na výšku", "false", "Minuty odjezdů")

    d ..AddBoolParam(arr, "SOUHRN_NA_ZNAMENI", "Souhrn ""všechny zastávky na znamení"" místo × u každé", "false", "Poznámky", , 1)

    // Rozsah „Spoje, nepoznámkovat" definuje legacy data (per spoj + TČ); tato volba určuje, CO se
    // v tom rozsahu v tisku schová. TD-aplikované (filtr nad nadmnožinou). Default = legacy.
    s pNepoz = ##class(%DynamicObject).%New()
    s pNepoz.name         = "SCHOVAT_V_ROZSAHU"
    s pNepoz.label        = "V rozsahu nepoznámkovat schovat"
    s pNepoz.type         = "enum"
    s pNepoz.group        = "Poznámky"
    s pNepoz.defaultValue = "Ruční komentáře"
    s optsNepoz = ##class(%DynamicArray).%New()
    d optsNepoz.%Push("Ruční komentáře")
    d optsNepoz.%Push("Automatické (rozdíly tras)")
    d optsNepoz.%Push("Obojí")
    s pNepoz.options = optsNepoz
    d arr.%Push(pNepoz)
}

/// Parametr variant tras má účinek jen se sloupcem Varianty tras v seznamu zastávek: bez něj se
/// varianty netisknou ani u minut. Klient takový parametr schová (rozhodnutí Jan 5. 10.); klíč
/// `visibleWhenColumn` je aditivní, starší klient ho ignoruje a parametr ukáže jako dřív.
ClassMethod JenSeSloupcemVariant(param As %DynamicObject)
{
    d param.%Set("visibleWhenColumn", "CAS_JIZDY_VARIANTY")
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Build* helpery výstupního JSON (duplikováno z ZastavkoveJrMhd - stejný formát)
/// ═══════════════════════════════════════════════════════════════════════════════
/// `casJizdy` má tři stavy jako `TRASA_MIN`: číslo, „<" (základní trasa projíždí bez stání) nebo
/// prázdno (mimo základní trasu).
ClassMethod BuildZastavka(id As %String, popis As %String, tc As %String = "", casJizdy As %String = 0, poradi As %Integer = 1, jeAktualni As %Boolean = 0, naZnameni As %Boolean = 0, odsazeni As %Integer = 0) As %DynamicObject
{
    s zastavka = ##class(%DynamicObject).%New()
    // Čitelná field ID (SLOVO_SLOVO) - sdílený kontrakt sloupců StopList↔DynamicTable.
    // TC (tarifní/interní číslo) vědomě nevystaveno; poziční potřebu pokrývá PORADI.
    d zastavka.%Set("ID", id)                          // interní join klíč (ODJEZDY.ZASTAVKA_ID), ne ve schématu
    d zastavka.%Set("NAZEV_ZASTAVKY", popis)
    d zastavka.%Set("CAS_JIZDY", casJizdy)
    d zastavka.%Set("PORADI", poradi)
    d zastavka.%Set("AKTUALNI_ZASTAVKA", $s(jeAktualni:"true", 1:"false"))
    d zastavka.%Set("NA_ZNAMENI", $s(naZnameni:"true", 1:"false"))
    d zastavka.%Set("ODSAZENI", odsazeni)
    d zastavka.%Set("ZONY", ##class(%DynamicArray).%New())
    d zastavka.%Set("ATRIBUTY", ##class(%DynamicArray).%New())
    d zastavka.%Set("POZNAMKY_ZASTAVKY", ##class(%DynamicArray).%New())
    q zastavka
}

ClassMethod AddZona(zastavka As %DynamicObject, zona As %String)
{
    d zastavka.%Get("ZONY").%Push(zona)
}

ClassMethod AddAtribut(zastavka As %DynamicObject, znak As %String, popis As %String = "")
{
    s atribut = ##class(%DynamicObject).%New()
    d atribut.%Set("ZNAK", znak)
    d atribut.%Set("POPIS", popis)
    d zastavka.%Get("ATRIBUTY").%Push(atribut)
}

ClassMethod AddPoznamkaZastavky(zastavka As %DynamicObject, znak As %String, typ As %String = "text", text As %String = "")
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("TEXT", text)
    d zastavka.%Get("POZNAMKY_ZASTAVKY").%Push(poznamka)
}

ClassMethod BuildSkupinaDnu(id As %String, nadpis As %String, nadpis2 As %String = "", poradi As %Integer = 1) As %DynamicObject
{
    s skupina = ##class(%DynamicObject).%New()
    d skupina.%Set("ID", id)
    d skupina.%Set("NADPIS", nadpis)
    d skupina.%Set("NADPIS2", nadpis2)
    d skupina.%Set("PORADI", poradi)
    q skupina
}

ClassMethod BuildOdjezd(zastavkaId As %String, skupinaDnuId As %String, hodina As %Integer, minuta As %Integer) As %DynamicObject
{
    s odjezd = ##class(%DynamicObject).%New()
    d odjezd.%Set("ZASTAVKA_ID", zastavkaId)
    d odjezd.%Set("SKUPINA_DNU_ID", skupinaDnuId)
    d odjezd.%Set("HODINA", hodina)
    d odjezd.%Set("MINUTA", minuta)
    d odjezd.%Set("FORE_COLOR", "")
    d odjezd.%Set("BACK_COLOR", "")
    d odjezd.%Set("ANOTACE", ##class(%DynamicArray).%New())
    q odjezd
}

/// Přidá anotaci (horní index) k odjezdu. `kategorie` + `spoj` + `vRozsahu` slouží TD filtru
/// viditelnosti (#7955): TD podle nich rozhodne, co schovat. Caché generuje vždy nadmnožinu, filtr je
/// render-time v TD. Kategorie: rozdil-trasy / omezeni / bezbarier / rucni-komentar.
/// vRozsahu = anotace spadá do rozsahu „Spoje, nepoznámkovat" (jen tam se schování aplikuje).
ClassMethod AddAnotace(odjezd As %DynamicObject, typ As %String, znak As %String, foreColor As %String = "", backColor As %String = "", kategorie As %String = "", spoj As %String = "", vRozsahu As %Boolean = 0)
{
    s anotace = ##class(%DynamicObject).%New()
    d anotace.%Set("TYP", typ)
    d anotace.%Set("ZNAK", znak)
    d anotace.%Set("FORE_COLOR", foreColor)
    d anotace.%Set("BACK_COLOR", backColor)
    d anotace.%Set("KATEGORIE", kategorie)
    d anotace.%Set("SPOJ", spoj)
    d anotace.%Set("V_ROZSAHU_NEPOZNAMKOVAT", vRozsahu)
    d odjezd.%Get("ANOTACE").%Push(anotace)
}

/// `kategorie` (atribut-zastavky / bezbarier / rozdil-trasy / rucni-komentar / omezeni / smerove)
/// umožní TD filtru prořezat legendu jen v relevantní kategorii (např. „x - na znamení" se nesmí
/// smazat jen proto, že nemá anotaci u minut).
ClassMethod BuildPoznamka(typ As %String, znak As %String, text As %String, foreColor As %String = "", backColor As %String = "", kategorie As %String = "") As %DynamicObject
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TEXT", text)
    d poznamka.%Set("FORE_COLOR", foreColor)
    d poznamka.%Set("BACK_COLOR", backColor)
    d poznamka.%Set("KATEGORIE", kategorie)
    q poznamka
}

/// Konvertuje 8-bit integer (0-255) na dvouznakovou velkou hex reprezentaci.
ClassMethod IntToHex2(n As %Integer) As %String [ Private ]
{
    s digits = "0123456789ABCDEF"
    i n < 0 s n = 0
    i n > 255 s n = 255
    q $e(digits, (n\16) + 1) _ $e(digits, (n#16) + 1)
}

/// Barva přiřazená lince v JŘ modulu, jako "#AARRGGBB" pro SkiaSharp; prázdno, když linka barvu
/// nastavenou nemá (šablona pak použije bílou). Modul ji drží jako "R;G;B".
///
/// `poradi` = kolikátá barva linky (1 = primární). JŘ umí od 3. 8. 2026 **víc barev na linku**
/// (#12997, Petr Šmíd) - klient je používá jako „barva textu" a „barva pozadí", ale samotný modul
/// jim význam nedává, je to jen seřazený seznam. Uloženo v `^JRL(sku,lin,"PATCH","BARVA")`, starý
/// tvar (jedna barva jako text) i nový (`$lb`) normalizuje `Linky.BarvyLinkySeznam`, takže tady se
/// nic neparsuje. Nenastavená n-tá barva = prázdno.
///
/// Volání je obalené záměrně: třída patří JŘ modulu (ISYJR2), takže na serveru, kde modul není,
/// se barva prostě nenaplní a tisk jde dál - vývěska kvůli barvě spadnout nesmí.
ClassMethod BarvaLinky(IdFa As %String = "", dataDB As %String = "", sku As %String = "", lin As %String = "", poradi As %Integer = 1) As %String
{
    i (IdFa = "") || (sku = "") || (lin = "") ret ""
    s rgb = ""
    try {
        s rgb = ##class(ALVA.ISY.ISYJR2.API.Linky).GetBarvaLinky(IdFa, dataDB, sku, lin, poradi)
    }
    catch ex { s rgb = "" }
    ret ..RgbToHex(rgb)
}

ClassMethod RgbToHex(rgb As %String) As %String
{
    i rgb = "" q ""
    s r = +$p(rgb,";",1)
    s g = +$p(rgb,";",2)
    s b = +$p(rgb,";",3)
    q "#FF"_..IntToHex2(r)_..IntToHex2(g)_..IntToHex2(b)
}

/// Konvertuje číselný kód speciálního znaku na Unicode string.
ClassMethod ConvertSpecialniZnak(cislo As %String) As %String
{
    i cislo = 1 q "Po"
    i cislo = 2 q "Út"
    i cislo = 3 q "St"
    i cislo = 4 q "Čt"
    i cislo = 5 q "Pá"
    i cislo = 6 q "So"
    i cislo = 7 q "Ne"
    i +cislo > 31 q $c(cislo)
    q cislo
}

}
