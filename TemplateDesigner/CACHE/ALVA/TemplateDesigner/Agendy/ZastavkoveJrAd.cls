Include GENERAL

/// Agenda: **Zastávkový JŘ z dat autobusové dopravy (AD)** = hlavní JŘ modul / meziměstská
/// + příměstská doprava (legacy JRMAIN). Sourozenec `ZastavkoveJrMhd` — stejný výstup
/// (zastávková vývěska: StopList + DepartureGrid + NotesLegend), jiný datový zdroj.
///
/// Datový zdroj (legacy globály, viz TD `cache-database-linky.md`):
/// - `^[dataDB]JRL(sku,lin,...)` — linka, trasa ("J"), spoje ("T"/"Z"), trasy spojů ("W"), PATCH
/// - `^[dataDB]JRZ(czst,...)`    — zastávky (názvy, zóny, GPS, časové varianty)
/// - `^[dataDB]JRSET(...)`       — číselníky (zóny, NZ, nadpisy sloupců)
///
/// Reimplementace legacy větve `ALVA.TISKMHD.Data.LoadData` s `VolanoZ=JRMAIN`
/// (→ `WinTiskMhd^JRapiExp10`). Legacy výstup lze vygenerovat jako zlatou referenci přes
/// `cache_query ALVA.TISKMHD.Data/LoadData` (viz `cache-database-linky.md` §9).
///
/// **Stav:** GetData fázový engine hotový — trasa, spoje, FTL sloupce (skupiny dnů), odjezdy,
/// atributy zastávek, datumová omezení (jede/nejede), dopravce, titulek/informace, **rozdíly tras**
/// (1:1 `OmTrasa^JRapiLinPrtMhd`: "neobsluhuje zast. X" / "jede přes…" / "pokračuje…", marker od "B").
/// Ověřeno 1:1 proti zlaté referenci (0365001: marker "B" = "neobsluhuje zast. Lesní hřbitov").
/// **Zbývá pro úplné 1:1:** atributy spoje (např. bezbariér "72") na minutách + legendě, adresa
/// provozovny v POPIS_DOPRAVCE, časy jízdy ve StopListu (TrasaMin), ověřit filtr atributů zastávek
/// A/L. Detail `cache-database-linky.md` §10.
///
/// Sdílené Build*/AddStandard* helpery jsou zatím **duplikované** z `ZastavkoveJrMhd`
/// (stejný výstupní formát). Extrakce do společného helperu/baseclass až podle výsledků
/// (ne předem) — viz dohoda v projektových poznámkách.
Class ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaCode = "ZASTAVKOVY_JR_AD";

Parameter AgendaLabel = "Zastávkový JŘ (AD)";

Parameter InstanceParamName = "LINKA_ID";

Parameter ElementKind = "STOP";

Parameter InstanceLabel = "linka";

Parameter InstanceLabelPlural = "linky";

/// Vrátí DataSchema — shodné se zastávkovým JŘ MHD (stejný výstupní formát).
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
    q $$$OK
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
    q $$$OK
}

/// Vrátí definici Fixed šablony.
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s key = $g(^||ML("IN", "key"))

    if key = "ZAHLAVI_LINKY" {
        d ..WriteTemplate2Col([
            ["Linka",       "{CISLO_LINKY}"],
            ["Označení",    "{LOKALNI_OZNACENI_LINKY}"],
            ["Směr",        "{POPIS_SMER}"],
            ["Zastávka",    "{POPIS_ZASTAVKA}"],
            ["Platnost",    "{POPIS_PLATNOSTI}"]
        ])
    }
    elseif key = "PLATNOST_DOPRAVCE" {
        d ..WriteTemplate2Col([
            ["Platnost",  "{POPIS_PLATNOSTI}"],
            ["Dopravce",  "{POPIS_DOPRAVCE}"],
            ["Typ JR",    "{TYP_JR}"]
        ])
    }

    q $$$OK
}

/// Vrátí parametry sestavy. Instance AD je určená skupinou linek + linkou (^JRL(sku,lin)).
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    $$$FillParams

    s arr = ##class(%DynamicArray).%New()

    // Linka se vybírá v Instance Pickeru (ne jako parametr sestavy) — viz GetAvailableInstances.
    // SKUPINA_LINEK_ID je zatím fixní (default = první skupina v ^JRL, shodně s pickerem i GetData);
    // až bude Návrhář spouštěn z JŘ/MHD, pošle se skupina (příp. výchozí linka) jako launch kontext.
    d ..AddStandardJRParameters(arr)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q $$$OK
}

/// Vrátí seznam linek pro Instance Picker (umožní proklikávání stránek v preview).
/// IN["search"] = substring filtr (case-insensitive), IN["limit"] = max (default 50),
/// Skupiny linek pro filtr Instance Pickeru — první subscript ^JRL(sku). Název skupiny je 1. `~`-piece
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
/// OUT["1"] = JSON pole { "value": lin, "display": "číslo — název", "subtitle": "Platnost ..." }.
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
            i nazev '= "" s display = cislo_" — "_nazev
            i (search '= "") && ('$f($zcvt(display,"L"), search)) continue

            s plati = $g(^[dataDB]JRL(sku,lin,"PATCH","Plati"))
            s subtitle = ""
            i $p(plati,"~",1) '= "" {
                s subtitle = "Platnost od "_$p(plati,"~",1)
                i $p(plati,"~",2) '= "" s subtitle = subtitle_" do "_$p(plati,"~",2)
            }

            s obj = ##class(%DynamicObject).%New()
            // "string" type — .NET čte přes GetString(); bez toho číselné cislo/value → JSON number → chyba
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
    q $$$OK
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
/// Zlatá reference výstupu: `cache-database-linky.md` §9.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd                       // cache per-linka/per-směr invariantů (served, bezbariér) — fresh per request

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        // Default = první skupina v ^JRL — shodně s GetAvailableInstances. Picker posílá jen
        // LINKA_ID, takže GetData musí číst tutéž skupinu, kterou picker listoval.
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        // LINKA_ID nebo INSTANCE_ID (alias z Instance Pickeru) — co přijde
        s lin = ..NormalizLinku($g(^||ML("IN","LINKA_ID")))
        i lin = "" s lin = ..NormalizLinku($g(^||ML("IN","INSTANCE_ID")))
        i lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))
        i '$d(^[dataDB]JRL(sku,lin,"I")) $$$ThrowStatus($$$ERROR($$$GeneralError, "Linka "_sku_"/"_lin_" neexistuje v ^JRL"))

        // Multi-page: jedna stránka per zastávka × směr (Tam + Zpět). Aktuální zastávka =
        // ze které se počítají odjezdy; StopList ukazuje úsek od ní ve směru jízdy.
        f smer = "T", "Z" {
            s poradi = ""
            f {
                s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi))
                q:poradi=""
                i '$isvalidnum(poradi) continue
                i $p($g(^[dataDB]JRL(sku,lin,"J",poradi)),":",1) = "" continue   // bod bez zastávky
                s page = ..BuildStrankaZastavky(dataDB, sku, lin, poradi, smer)
                i page '= "" s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
            }
        }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Sestaví JSON stránku zastávkového JŘ pro jednu linku — sdíleno mezi GetData (živě ze session
/// firmy) a GetMockData (hardcoded CB_GWBUS pro live preview).
/// dataDB = data namespace; sku/lin = identifikace linky v ^JRL. Vrací %DynamicObject, nebo ""
/// když linka neexistuje.
/// smer = "T" (Tam, pořadí nahoru k poslední zastávce) nebo "Z" (Zpět, pořadí dolů k první).
/// currentPoradi = pořadí aktuální zastávky (prázdné = první). Vrací %DynamicObject nebo "".
ClassMethod BuildStrankaZastavky(dataDB As %String, sku As %String, lin As %String, currentPoradi As %String = "", smer As %String = "T") As %DynamicObject
{
    i '$d(^[dataDB]JRL(sku,lin,"I")) q ""

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
    s lokalni = $p(setMhd,"~",1)
    i lokalni = "" s lokalni = $e(lin,5,7)                       // fallback = znaky 5-7 čísla linky
    s titulek = $p(setMhd,"~",2)
    i titulek = "" s titulek = "Městská hromadná doprava..."     // legacy MHD default
    s informace = $replace($p(setMhd,"~",5),"<br>",$c(13,10))    // cnvTxt: <br> → newline

    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            +lin)
    d vals.%Set("LOKALNI_OZNACENI_LINKY", lokalni)
    d vals.%Set("TITULEK",                titulek)
    d vals.%Set("POPIS_LINKY",            ..NazevLinky(dataDB, sku, lin))
    d vals.%Set("POPIS_DOPRAVCE",         ..PopisDopravce(dataDB, sku, lin))
    s plati = $g(^[dataDB]JRL(sku,lin,"PATCH","Plati"))
    s platiOd = $p(plati,"~",1)
    s platiDo = $p(plati,"~",2)
    d vals.%Set("POPIS_PLATNOSTI",        $s(platiOd'="":"Platí od "_platiOd, 1:""))
    d vals.%Set("DATUM_PLATNOSTI_OD",     platiOd)
    d vals.%Set("DATUM_PLATNOSTI_DO",     platiDo)
    // Typ JR (AD-N2) — PATCH,"OBJ" (gPrpLin dta3). Znění razítka 1:1 legacy JRapiLinPrtPS3.
    // Pozn.: zastávková větev (JRapiLinPrtMhd) OBJ nečte → razítko nestaví automaticky;
    // TYP_JR je volitelný placeholder pro autora šablony (PS/knižní feature).
    d vals.%Set("TYP_JR", $case(+$g(^[dataDB]JRL(sku,lin,"PATCH","OBJ")),1:"VÝLUKOVÝ",2:"návrh jízdního řádu",:""))
    d vals.%Set("QR_KOD",                 $g(^[dataDB]JRL(sku,lin,"PATCH","QR_CODE")))
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  $g(^[dataDB]JRL(sku,lin,"PATCH","PoznPodDopr")))
    // Poznámka nad linku (AD-N1) — legacy gPrpLin dta4 = PATCH,"Head"
    d vals.%Set("POZNAMKA_NAD_LINKU",     $g(^[dataDB]JRL(sku,lin,"PATCH","Head")))
    d vals.%Set("INFORMACE",              informace)
    // NEJEDOU_SPOJE je legacy misnomer — nese NÁSTUPIŠTĚ vývěskové zastávky (J-věta #3 = nastTam:nastZpet,
    // dta23 v JRapiExp10). Per směr (T=tam, Z=zpět). 1:1 gold.
    s nastupiste = $p($p($g(^[dataDB]JRL(sku,lin,"J",currentPoradi)),"#",3),":",$s(smer="Z":2,1:1))
    d vals.%Set("NEJEDOU_SPOJE",          nastupiste)

    // ── trasa → zastávky (CELÁ trasa ve směru jízdy, vč. projetých; zóny + na znamení) ──
    // Tam: vzestupně 1→N. Zpět: sestupně N→1. Aktuální = JE_AKTUALNI; projeté mají casJizdy=0 a bez
    // atributů (jako MHD). StopList je defaultně skryje; „tisk projetých" je zobrazí ztlumeně.
    s zastavky = ##class(%DynamicArray).%New()
    s aktualniNazev = "", aktualniCzst = "", posledniNazev = "", posledniCzst = ""
    k atrSeen                                                   // atributy zastávek viděné na stránce (znak → text) pro legendu
    k servedPoradi
    d ..ObsluhovanePoradi(dataDB, sku, lin, smer, .servedPoradi)   // filtr projížděných zastávek
    s jeTam = (smer = "T")

    // Konečná směru = za currentPoradi není žádná obsluhovaná zastávka → jen příjezdy, board se netiskne
    // (1:1 legacy — přeskakuje pouze poslední zastávku směru, ne per-spoj konečné).
    s jeDal = 0, pp = ""
    f {
        s pp = $o(servedPoradi(pp))
        q:pp=""
        i jeTam, (pp > currentPoradi) s jeDal = 1 quit
        i 'jeTam, (pp < currentPoradi) s jeDal = 1 quit
    }
    i 'jeDal q ""

    // základní trasa pro časy jízdy (TrasaMin) — reprezentativní trasa spoje stavícího na vývěskové
    // zastávce; W-mapu rozparsujeme jednou a v cyklu zastávek z ní jen čteme
    s zaklCt = ..ZakladniCisloTrasy(dataDB, sku, lin, smer, currentPoradi)
    d ..WMapa(dataDB, sku, lin, zaklCt, .zaklWMap)

    // ── varianty časů tras (dynamické „TrasaMin") ─────────────────────────────
    // Seskup spoje obsluhující vývěskovou zastávku podle časového profilu (shodný profil = jeden
    // sloupec). N = počet distinct profilů — NE fixních 15 jako legacy.
    // Signatura = jen ZOBRAZENÉ zastávky (servedPoradi): per-zastávka kumulativní čas / „<" (projíždí) /
    // prázdno (mimo trasu). Přes celou W-mapu by dvě varianty lišící se MIMO tuto vývěsku daly dva
    // identicky vypadající sloupce — signatura přes servedPoradi je sloučí (na vývěsce nerozlišitelné).
    k variantSig, variantCt, variantRepSpoj
    s poradiVar = 0, spV = ""
    f {
        s spV = $o(^[dataDB]JRL(sku,lin,smer,spV))
        q:spV=""
        i '$isvalidnum(spV) continue
        s ctV = $p($p($g(^[dataDB]JRL(sku,lin,smer,spV)),"#",1),":",3)
        i ctV = "" continue
        d ..WMapa(dataDB, sku, lin, ctV, .wMapV)
        s cAktV = $g(wMapV(currentPoradi))
        i (cAktV = "") || (cAktV = "<") continue     // spoj zde nestaví → nedává časový sloupec
        s sigV = "", ppV = ""
        f {
            s ppV = $o(servedPoradi(ppV))  q:ppV=""
            i '$isvalidnum(ppV) continue
            s vv = $g(wMapV(ppV))
            s $p(sigV,"~",ppV) = $s((vv="")||(vv="<"):vv, 1:..CasJizdyZMapy(.wMapV, currentPoradi, ppV))
        }
        i '$d(variantSig(sigV)) {
            s poradiVar = poradiVar + 1
            s variantSig(sigV) = poradiVar
            s variantCt(poradiVar) = ctV
            s variantRepSpoj(poradiVar) = spV
        }
    }

    // Emituj VŠECHNY zastávky směru (vč. projetých, jako MHD) — StopList je filtruje dle „tisk projetých".
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
        s atributy  = $p($p(veta,":",2),"#",1)              // dta2 — atributy zastávky (znaky číselníku 16)
        s zona      = $p($p(veta,":",5),"#",2)               // "#zóna#..."
        s rawNazev = ..NazevZastavky(dataDB, czst)
        s jeAkt = (poradi = currentPoradi)
        s casJizdy = $s(jeProjeta:0, 1:..CasJizdyZMapy(.zaklWMap, currentPoradi, poradi))   // projetá → 0 (StopList ji ztlumí); jinak kumulativní min od vývěskové
        s cnt = cnt + 1
        // „na znamení" se tiskne přes atribut „x" (níže) — legacy UVOD tiskne jen „x". NaZnameni=0,
        // ať StopList nepřidá ještě syntetický SignalStopSymbol „×" (jinak duplicita „× x").
        s z = ..BuildZastavka(poradi, rawNazev, poradi, casJizdy, cnt, jeAkt, 0)
        i zona '= "" d ..AddZona(z, zona)
        // atributy zastávky → symbol u názvu + řádek legendy (jen neprojeté + významové znaky; analogicky MHD)
        i 'jeProjeta {
            f ai=1:1:$l(atributy) {
                s znak = $e(atributy, ai)
                i '..JeViditelnyAtribut(znak) continue
                s txtAtr = ..TextAtributu(znak)
                d ..AddAtribut(z, znak, txtAtr)
                s atrSeen(znak) = txtAtr
            }
        }
        // varianty časů (dynamické TrasaMin) — hodnota per varianta pro tuto zastávku
        s trasaMin = ##class(%DynamicArray).%New()
        f viX=1:1:poradiVar {
            d ..WMapa(dataDB, sku, lin, $g(variantCt(viX)), .wMapX)
            s vvX = $g(wMapX(poradi))
            i (vvX = "") || (vvX = "<") { d trasaMin.%Push(vvX) }
            else { d trasaMin.%Push(..CasJizdyZMapy(.wMapX, currentPoradi, poradi)) }
        }
        d z.%Set("TRASA_MIN", trasaMin)
        d zastavky.%Push(z)
        i jeAkt s aktualniNazev = rawNazev, aktualniCzst = czst
        s posledniNazev = rawNazev, posledniCzst = czst
    }

    d vals.%Set("POPIS_ZASTAVKA",      aktualniNazev)
    d vals.%Set("CISLO_ZASTAVKA",      aktualniCzst)
    d vals.%Set("POPIS_ZASTAVKA_CIS",  ..NazevZastavkyCis(dataDB, aktualniCzst))
    d vals.%Set("POPIS_SMER",          posledniNazev)
    d vals.%Set("POPIS_SMER_CIS",      ..NazevZastavkyCis(dataDB, posledniCzst))

    // FTL = formát sloupců mřížky (PATCH MHD_TYP_R); nzCfg = NZ konfig pro prázdninové dělení (MHD_NZ1_NZ2|MHD_NENZ1_NENZ2)
    s ftl   = +$g(^[dataDB]JRL(sku,lin,"PATCH","MHD_TYP_R"))
    // Override FTL z parametru „Rozdělení sloupců" (prázdné/auto = dle linky). Mapuje label → FTL mód;
    // engine (NacistSkupinyDnu + ZarazeniSpoje) zůstává beze změny.
    s rozdSl = $g(^||ML("IN","ROZDELENI_SLOUPCU"))
    s ftl = $case(rozdSl, "Pracovní / Sobota / Neděle+svátky":0, "Pracovní / víkend (So+Ne+svátky)":2, "Prázdniny: Prac. mimo+o prázd. / víkend":1, "Prázdniny: Prac. mimo+o prázd. / Sobota / Neděle+svátky":3, :ftl)
    s nzCfg = $g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NZ1_NZ2"))_"|"_$g(^[dataDB]JRL(sku,lin,"PATCH","MHD_NENZ1_NENZ2"))

    // ── odjezdy + poznámky (omezení + rozdíly tras + atributy spoje → anotace + legenda) ────
    k poznMap, pismenoSpoj, rozdilLegenda, bezbarLegenda, komentareLegenda
    d ..RozdilyTras(dataDB, sku, lin, smer, currentPoradi, .pismenoSpoj, .rozdilLegenda)
    // hlavičky variantních časových sloupců:
    //   písmeno rozdílu trasy (RozdilyTras, reprezentativní spoj); stejné písmeno u víc variant
    //   (liší se jen časem, ne zastávkami) → rozlišit „B1"/„B2"; varianta bez písmene → „T1"/„T2"…
    // 1. průchod: spočítej výskyty každého písmene (kolize → číslovat)
    k pismCount, pismSeq
    f viY=1:1:poradiVar {
        s pismV = $g(pismenoSpoj($g(variantRepSpoj(viY))))
        i pismV '= "" s pismCount(pismV) = $g(pismCount(pismV)) + 1
    }
    s variantyTras = ##class(%DynamicArray).%New()
    s fallbackVar = 0
    f viY=1:1:poradiVar {
        s pismV = $g(pismenoSpoj($g(variantRepSpoj(viY))))
        i pismV = "" {
            s fallbackVar = fallbackVar + 1
            s hdr = "T" _ fallbackVar
        } elseif $g(pismCount(pismV)) > 1 {
            s pismSeq(pismV) = $g(pismSeq(pismV)) + 1
            s hdr = pismV _ pismSeq(pismV)
        } else {
            s hdr = pismV
        }
        d variantyTras.%Push(hdr)
    }
    d vals.%Set("VARIANTY_TRAS", variantyTras)
    s odjezdy = ..NacistOdjezdy(dataDB, sku, lin, smer, currentPoradi, ftl, nzCfg, .poznMap, .pismenoSpoj, .bezbarLegenda, .komentareLegenda)
    i odjezdy.%Size() = 0 q ""        // zastávka bez odjezdů v daném směru (konečná) — stránku negenerovat
    d ..BezbarierKonfig(dataDB, sku, lin, .isinv, .iscbe, .allBBP)   // allBBP → agregátní legenda bezbariér (cache)
    s poznamky = ..SestavPoznamky(.poznMap, .atrSeen, .rozdilLegenda, .bezbarLegenda, .komentareLegenda, allBBP)

    // směrové poznámky linky (tab MHD: poznámky tam/zpět/vše) — 1:1 legacy JRapiLinPrtMhd1 ř.440-447.
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
    d rows.%Set("SKUPINY_DNU", ..NacistSkupinyDnu(ftl))
    d rows.%Set("ODJEZDY", odjezdy)
    d rows.%Set("POZNAMKY", poznamky)
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
/// `<` na zastávce = spoj zde nestaví → přeskočit. Zařazení do sloupců dnů dle FTL+Plk+CNZ (`ZarazeniSpoje`).
ClassMethod NacistOdjezdy(dataDB As %String, sku As %String, lin As %String, smer As %String, currentPoradi As %String, ftl As %String, nzCfg As %String, ByRef poznMap, ByRef pismenoSpoj, ByRef bezbarLegenda, ByRef komentareLegenda) As %DynamicArray
{
    s odjezdy = ##class(%DynamicArray).%New()
    i currentPoradi = "" q odjezdy

    // bezbariér konfig (per linka): isinv/iscbe = feature povolená, allBBP = všechny spoje bezbar.
    d ..BezbarierKonfig(dataDB, sku, lin, .isinv, .iscbe, .allBBP)

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
        s offset = ..CasHodnota($g(wMap(currentPoradi)))
        i offset = "" continue            // spoj zde nestaví (`<`) / zastávka mimo trasu spoje

        s celkem = odjezd + offset
        s hod = (celkem\60)#24, min = celkem#60

        // anotace z datumových omezení spoje (omzId 11-15, 22) — sdílená přes všechny skupiny spoje.
        // Marker = číslo negativní značky (cnz) + typ „nz" (invertovaně), 1:1 legacy gold (anotace
        // „2$<cnz>", legenda „2#<cnz>#<text>"; ověřeno KV_DP i CB_CSADAD). Text bere per-spoj omzId
        // (SestavTextOmezeni) — InPlatDef se nečte (drží zastaralý seed datumů). cnz prázdné → fallback
        // na sekvenční číslo + typ „text" (zachová staré chování pro spoj bez NZ).
        s znak = "", typOmez = "text"
        s txt = ..SestavTextOmezeni(dataDB, sku, lin, smer, sp)
        i txt '= "" {
            i cnz '= "" {
                s znak = cnz, typOmez = "nz"
                s poznMap("byMarker", cnz) = txt, poznMap("typ", cnz) = "nz"
            } else {
                s znak = ..ZiskejMarker(.poznMap, txt)
            }
        }

        // marker rozdílu trasy (z RozdilyTras) — u minuty jako první, dle legacy ZnkOm=ZnkTc_…
        s pismRT = $g(pismenoSpoj(sp))

        // atributy spoje — bezbariér (inv = hlava:15) / částečně (cbe = hlava:22); SpecZnak 72=H / 70=F
        s inv = (isinv && +$p(hlava,":",15))
        s cbe = (iscbe && +$p(hlava,":",22))
        i inv s bezbarLegenda("H") = $s(allBBP:"všechny spoje mají bezbariérově přístupné vozidlo", 1:"spoj má bezbariérově přístupné vozidlo")
        i cbe s bezbarLegenda("F") = "spoj má částečne bezbariérově přístupné vozidlo, nutná dopomoc průvodce"

        // ruční komentáře spoje (tab MHD: Komentáře = SeT_SpeC přiřazené spoji přes MhD_SpeC).
        // V rozsahu „Spoje, nepoznámkovat" (MHDSPNPZN) se anotace jen OZNAČÍ (vRozsahu) — co se reálně
        // schová (ruční / rozdíly tras / obojí) rozhoduje TD filtr dle nastavení šablony. Caché emituje
        // nadmnožinu (nedropuje), aby šly volit všechny varianty.
        s vRozsahu = +..NepoznamkovatSpoj(dataDB, sku, lin, sp, currentPoradi)
        s komentarZnaky = $g(^[dataDB]JRL(sku,lin,smer,sp,"MhD_SpeC"))
        f indexZnaku=1:1:$l(komentarZnaky) {
            s komentarZnak = $e(komentarZnaky, indexZnaku)
            i komentarZnak '= "" s komentareLegenda(komentarZnak) = $g(^[dataDB]JRL(sku,lin,"MhD","SeT_SpeC",komentarZnak))
        }

        // spoj může patřit do více sloupců dnů (FTL layout + prázdninové dělení dle CNZ)
        s skupiny = ..ZarazeniSpoje(ftl, plk, cnz, nzCfg)
        i skupiny = "" continue           // KamPrt = 0 (nerozpoznané Plk) → spoj se netiskne (jako legacy)
        f gi=1:1:$l(skupiny,",") {
            s o = ..BuildOdjezd(currentPoradi, $p(skupiny,",",gi), hod, min)
            // pořadí anotací 1:1 legacy ZnkOm: rozdíl trasy → omezení → bezbariér → ruční komentář.
            // KATEGORIE + spoj = pro TD filtr viditelnosti (#7955) — Caché generuje nadmnožinu.
            i pismRT '= "" d ..AddAnotace(o, "text", pismRT, "", "", "rozdil-trasy", sp, vRozsahu)
            i znak  '= "" d ..AddAnotace(o, typOmez, znak, "", "", "omezeni", sp, vRozsahu)
            i inv && 'allBBP d ..AddAnotace(o, "specialni", "H", "", "", "bezbarier", sp, vRozsahu)   // u allBBP se marker netiskne, jen legenda
            i cbe d ..AddAnotace(o, "specialni", "F", "", "", "bezbarier", sp, vRozsahu)
            f indexZnaku=1:1:$l(komentarZnaky) { s komentarZnak = $e(komentarZnaky, indexZnaku)  i komentarZnak '= "" d ..AddAnotace(o, "text", komentarZnak, "", "", "rucni-komentar", sp, vRozsahu) }
            d odjezdy.%Push(o)
        }
    }
    q odjezdy
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
            i (pp '= "") && (cas '= "<") && (cas '= "") s served(pp) = ""
        }
    }
    i $d(served) m ^||TiskAd("served",dataDB,sku,lin,smer) = served     // prázdné necachovat (merge z undef = <UNDEFINED>)
}

/// W-mapa `poradi → cas` pro danou trasu, cachovaná per `cisloTrasy` (`^||TiskAd("wmap",…)`).
/// Trasa je sdílená napříč spoji i stránkami → parsuje se tak jen jednou (dřív lineární `$p` sken
/// W-stringu při každém lookupu/spoji/stránce). `cas` může být číslo, `<` (projíždí) nebo "" (mimo).
ClassMethod WMapa(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String, ByRef wMap)
{
    k wMap
    i cisloTrasy = "" q                                  // spoj bez trasy → prázdná mapa (^|| nebere prázdný subscript)
    i $d(^||TiskAd("wmap",cisloTrasy)) { m wMap = ^||TiskAd("wmap",cisloTrasy)  q }
    s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
    f i=1:1:$l(wTrasa,"#") {
        s seg = $p(wTrasa,"#",i)
        s p = $p(seg,":",1)
        i p '= "" s wMap(p) = $p(seg,":",2)
    }
    i $d(wMap) m ^||TiskAd("wmap",cisloTrasy) = wMap     // prázdnou trasu necachovat (merge z undef = <UNDEFINED>)
}

/// Vrátí `cisloTrasy` **hlavní (nejčastější)** trasy mezi spoji stavícími na vývěskové zastávce
/// (`currentPoradi`). Základ pro časy jízdy ve StopListu (jeden sloupec, jako MHD `TrasaMin1`).
/// Hlavní = nejvíc spojů (1:1 legacy „označím trasu s největším počtem spojů" `JRapiLinPrtMhd` ř.108-127)
/// — vyhne se tomu, aby expresní spoj projíždějící uzly (`<`) určoval časy. "" když nikdo nestaví.
ClassMethod ZakladniCisloTrasy(dataDB As %String, sku As %String, lin As %String, smer As %String, currentPoradi As %String) As %String
{
    k pocet
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s ct = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
        i ct = "" continue
        d ..WMapa(dataDB, sku, lin, ct, .wMap)
        s c = $g(wMap(currentPoradi))
        i (c '= "") && (c '= "<") && ($isvalidnum(c)) s pocet(ct) = $g(pocet(ct)) + 1   // spoj této trasy staví na vývěsce
    }
    // nejčastější trasa = hlavní
    s baseCt = "", maxN = 0, ct = ""
    f {
        s ct = $o(pocet(ct), 1, n)
        q:ct=""
        i n > maxN s maxN = n, baseCt = ct
    }
    q baseCt
}

/// Numerická hodnota času z W-segmentu. Pobytová zastávka má formát `příjezd/čekání` (např. `33/4`)
/// → vrací **odjezd = příjezd + čekání** (1:1 legacy `Cas+Ceka` / `Cas+$P(Cas,"/",2)`). Běžná `27` → 27.
/// "" když mimo trasu / projíždí (`<`).
ClassMethod CasHodnota(cas As %String) As %String
{
    i (cas = "") || (cas = "<") q ""
    q +$p(cas,"/",1) + +$p(cas,"/",2)        // "33/4"→37, "27"→27 (piece2 prázdné = +0)
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

/// Vrátí sloupce dnů (1-based indexy, čárkou oddělené) pro spoj — 1:1 s `KamPrt` logikou
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

/// Rozdíly tras — pro každý spoj daného směru porovná jeho trasu se vzorovou (základní) trasou
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

    // písmenná sada — default začíná "B" (proto marker "B"); počítáno per zastávku, dedup textů
    s oznTr = $g(^[dataDB]JRL(sku,lin,"PATCH","mhd","notchar"))
    i oznTr = "" s oznTr = "BCEFGHIJOQRWeijqsvwxz"
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

        s (jede, nejede, jedeDo, pokrac) = ""

        // "jede jen do" — spoj končí dříve než vzorová trasa
        s tcDOk = ""
        i ((tcDo < mTarTr) && (smer = "T")) || ((tcDo > mTarTr) && (smer = "Z")) {
            s jedeDo = ..NazZPoradi(dataDB, sku, lin, tcDo)
            s tcDOk = tcDo
        }

        f tmpTc=currentPoradi:krok:tcDo {
            i tmpTc = currentPoradi continue
            i tcDOk = tmpTc continue
            i +$e(vzor,tmpTc) = +$e(spojTr,tmpTc) continue
            s naz = ..NazZPoradi(dataDB, sku, lin, tmpTc)
            i (smer = "T") && (tmpTc > mTarTr) && (+$e(spojTr,tmpTc)) {
                s $p(pokrac,", ",$l(pokrac,", ")+1) = naz
                continue
            }
            i (smer = "Z") && (tmpTc < mTarTr) && (+$e(spojTr,tmpTc)) {
                s $p(pokrac,", ",$l(pokrac,", ")+1) = naz
                continue
            }
            i +$e(spojTr,tmpTc)  s $p(jede,", ",$l(jede,", ")+1) = naz
            i '+$e(spojTr,tmpTc) s $p(nejede,", ",$l(nejede,", ")+1) = naz
        }

        // strip vedoucího prázdného prvku (legacy $P(...,2,...))
        s jede   = $p(jede,   ", ", 2, $l(jede,   ", "))
        s nejede = $p(nejede, ", ", 2, $l(nejede, ", "))
        s pokrac = $p(pokrac, ", ", 2, $l(pokrac, ", "))

        // sestavení textu (pořadí: jede → neobsluhuje → jede jen do → jede až do)
        s txt = ""
        i jede '= "" s txt = "jede přes zast. "_jede
        i nejede '= "" {
            i txt = "" {
                s txt = "neobsluhuje zast. "_nejede
            } else {
                s $p(txt," , ",$l(txt," , ")+1) = "neobsluhuje zast. "_nejede
            }
        }
        i jedeDo '= "" {
            i txt = "" {
                s txt = "jede jen do zast. "_jedeDo
            } else {
                s $p(txt,", ",$l(txt,", ")+1) = "jede jen do zast. "_jedeDo
            }
        }
        // Prodloužení za základní trasu (#7947): místo legacy „pokračuje přes <mezizastávky>"
        // vypíšeme cíl spoje „jede až do zast. <konečná>" (klient chce vědět KAM dojede, ne kudy).
        // Záměrná odchylka od legacy znění — legacy „pokračuje" bylo matoucí.
        i pokrac '= "" {
            s cilNaz = ..NazZPoradi(dataDB, sku, lin, tcDo)
            i txt = "" {
                s txt = "jede až do zast. "_cilNaz
            } else {
                s $p(txt,", ",$l(txt,", ")+1) = "jede až do zast. "_cilNaz
            }
        }
        i txt = "" continue

        // marker — dedup stejných textů v rámci této zastávky, jinak další písmeno ze sady
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
/// `$e(bmp,poradi)=1` kde spoj staví (cas != "<" a != ""). "" = prázdná trasa.
ClassMethod SpojBitmapa(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String) As %String
{
    d ..WMapa(dataDB, sku, lin, cisloTrasy, .wMap)
    s bmp = "", p = ""
    f {
        s p = $o(wMap(p), 1, cas)
        q:p=""
        i ($isvalidnum(p)) && (cas '= "<") && (cas '= "") s $e(bmp,p) = 1
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

/// Název zastávky podle pořadí v trase (přes czst z J-věty `#1:1`).
ClassMethod NazZPoradi(dataDB As %String, sku As %String, lin As %String, poradi As %String) As %String
{
    s czst = $p($p($g(^[dataDB]JRL(sku,lin,"J",poradi)),"#",1),":",1)
    i czst = "" q ""
    q ..NazevZastavky(dataDB, czst)
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

/// Sestaví legendu: atributy zastávek + atributy spoje (bezbariér) + rozdíly tras + "nejede/jede".
/// Pořadí 1:1 gold (`ALVA.TISKMHD.Data`): speciální znaky → rozdíly/text → omezení.
ClassMethod SestavPoznamky(ByRef poznMap, ByRef atrSeen, ByRef rozdilLegenda, ByRef bezbarLegenda, ByRef komentareLegenda, allBBP As %Boolean = 0) As %DynamicArray
{
    s poznamky = ##class(%DynamicArray).%New()
    // atributy zastávek — jeden řádek legendy na každý viděný znak (znak → text z číselníku)
    s znak = ""
    f {
        s znak = $o(atrSeen(znak))
        q:znak=""
        d poznamky.%Push(..BuildPoznamka("specialni", znak, atrSeen(znak), "", "", "atribut-zastavky"))
    }
    // atributy spoje — bezbariér (H) / částečně bezbariér (F). Při allBBP je „H" agregátní řádek
    // („všechny spoje…") → kategorie „bezbarier-agregat" (TD filtr ho umí skrýt, #7161 D6).
    s zb = ""
    f {
        s zb = $o(bezbarLegenda(zb))
        q:zb=""
        s katBezbar = $s((zb="H") && allBBP : "bezbarier-agregat", 1 : "bezbarier")
        d poznamky.%Push(..BuildPoznamka("specialni", zb, bezbarLegenda(zb), "", "", katBezbar))
    }
    // rozdíly tras — písmeno → text ("neobsluhuje zast. X" / "jede přes…" / "jede jen do…" / "pokračuje…")
    s pism = ""
    f {
        s pism = $o(rozdilLegenda(pism))
        q:pism=""
        d poznamky.%Push(..BuildPoznamka("text", pism, rozdilLegenda(pism), "", "", "rozdil-trasy"))
    }
    // ruční komentáře spoje (tab MHD: Komentáře) — znak → text z SeT_SpeC
    s komentarZnak = ""
    f {
        s komentarZnak = $o(komentareLegenda(komentarZnak))
        q:komentarZnak=""
        d poznamky.%Push(..BuildPoznamka("text", komentarZnak, komentareLegenda(komentarZnak), "", "", "rucni-komentar"))
    }
    // datumová omezení (nejede/jede) — marker = číslo NZ (cnz), typ „nz" (invertovaně) pokud pochází
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

/// „Spoje, nepoznámkovat" (tab MHD) — potlačí ruční komentář spoje v zadaném rozsahu tarifních čísel.
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

/// Atributy zastávky tištěné v zastávkovém JŘ. Legacy `UVOD^JRapiLinPrtMhd` (ř.189-190) promítá
/// z dta2 do StopListu **jen „na znamení" (znak `x`)** — ostatní znaky číselníku 16 (A=autobus,
/// L=vlak, metro, nástup/výstup, formátovací…) se v TOMTO výstupu NEtisknou (ověřeno gold 0365001:
/// Vlakové nádraží má L=vlak, U autobusového nádraží A=autobus, ale gold je v zastávkách neukazuje).
ClassMethod JeViditelnyAtribut(znak As %String) As %Boolean
{
    q znak = "x"
}

/// Text atributu zastávky dle znaku — 1:1 s číselníkem `arrCis(16)^JRapiCis`.
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

/// Prefix textu pro daný typ omezení (1:1 s `txtNZ^JRapiNZUtl`).
ClassMethod PrefixOmezeni(omzId As %String) As %String
{
    q $case(omzId, 11:"jede jen ", 12:"jede také ", 13:"jede ", 14:"jede jen v su. ", 15:"jede jen v li. ", 22:"nejede ", :"")
}

/// Datumová část omezení — 1:1 formát zastávkového JŘ (gold `JRapiLinPrtPS3`): datumy přes `UpravDatum`
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

/// POPIS_DOPRAVCE = "Zajišťuje: <dopravci>" (1:1 s `sTIT`/`gDopMHD^JRapiLinPrtMhd`).
/// Sesbírá distinct dopravce (fa:pr z `#`-pole 2 spoje) přes oba směry, resolvuje název, spojí čárkou.
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
            s nazev = ..NazevDopravce(dataDB, fa, pr)
            i nazev = "" continue
            i $d(seenName(nazev)) continue
            s seenName(nazev) = ""
            i txt '= "" s txt = txt_", "
            s txt = txt_nazev
        }
    }
    i txt = "" q ""
    q "Zajišťuje: "_txt
}

/// Text dopravce (fa,pr) z `^JRSET("DOP",...)` — 1:1 s `gDopMHD`/PzaJ assembly `^JRapiLinPrtMhd`:
/// 1) je-li nastaven zkrácený MHD název (per-provoz $6 → firemní $14), použij ho SAMOSTATNĚ.
/// 2) jinak plný tvar `<obchodní název / sídlo firmy>, <sídlo provozovny>,,tel:<telefon provozovny>`.
ClassMethod NazevDopravce(dataDB As %String, fa As %String, pr As %String) As %String
{
    // 1) zkrácený MHD název (samostatně, bez adresy)
    i pr '= "" {
        s mhd = $p($g(^[dataDB]JRSET("DOP",fa,pr)),"~",6)
        i $tr(mhd," ","") '= "" q mhd
    }
    s firma = $g(^[dataDB]JRSET("DOP",fa))
    s mhd = $p(firma,"~",14)
    i $tr(mhd," ","") '= "" q mhd

    // 2) plný tvar: název firmy + sídlo provozovny + ulice + telefon (gDopFaPr: $1 sídlo, $2 ulice, $3 tel)
    s jmeno = $p(firma,"~",7)
    i jmeno = "" s jmeno = $p(firma,"~",1)
    s sidlo = "", ulice = "", tel = ""
    i pr '= "" {
        s provoz = $g(^[dataDB]JRSET("DOP",fa,pr))
        s sidlo = $p(provoz,"~",1)
        s ulice = $p(provoz,"~",2)
        s tel   = $p(provoz,"~",3)
    }
    s text = jmeno
    i sidlo '= "" s text = text_", "_sidlo
    i tel '= ""   s text = text_","_ulice_",tel:"_tel     // slot ulice mezi sídlem a telefonem (prázdná → ,,tel:)
    q text
}

/// Vrátí název zastávky z ^JRZ(czst) — odbav/MHD název s obcí ($7, formát "obec,místo").
/// Legacy tiskne přesně toto pole (zkrácená obec "Fr. Lázně,..." místo plné "Františkovy Lázně,...").
/// Fallback na $2, pokud $7 chybí (nevyplněný odbav název).
ClassMethod NazevZastavky(dataDB As %String, czst As %String) As %String
{
    s rec = $g(^[dataDB]JRZ(czst))
    s nazev = $p(rec, "$", 7)
    i nazev = "" s nazev = $p(rec, "$", 2)
    q $replace(nazev, ",,", ",")     // display název: "obec,,místo" → "obec,místo" (legacy collapse; _CIS pole ,, zachovává)
}

/// "Číselníkový" (CIS) název zastávky z ^JRZ `$2` — zkrácený, s `,,` zachovaným (1:1 legacy POPIS_ZASTAVKA_CIS dta20).
ClassMethod NazevZastavkyCis(dataDB As %String, czst As %String) As %String
{
    i czst = "" q ""          // prázdný czst → prázdný subscript (^JRZ("")) = <SUBSCRIPT>; ošetřit
    q $p($g(^[dataDB]JRZ(czst)), "$", 2)
}

/// Vrátí ukázková data pro live preview v designeru.
///
/// **DEV (Fáze 2):** zrcadlí živý `GetData` nad jednou hardcoded linkou (CB_GWBUS / 0320113),
/// aby designer preview ukazoval reálný stav rebuildu — jak přibývají increments (odjezdy,
/// sloupce…), preview se plní živými daty z `^JRL`.
/// TODO: před produkčním nasazením vrátit lehký statický vzorek nezávislý na firmě/datech.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd                       // cache per-linka/per-směr invariantů — fresh per request

        // DEV: živý náhled z první linky aktuální firmy (session dataDB) — zrcadlí reálný GetData.
        d ..PrvniLinka(dataDB, .sku, .lin)
        s page = $s(lin'="":..BuildStrankaZastavky(dataDB, sku, lin), 1:"")

        // Fallback — když firma nemá v ^JRL žádnou linku, ať designer nespadne.
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
            d page.%Set("rows", r)
        }

        s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Standardní JŘ schema (duplikováno z ZastavkoveJrMhd — stejný výstupní formát)
/// ═══════════════════════════════════════════════════════════════════════════════
ClassMethod AddStandardJRFields(fields As %DynamicArray)
{
    d ..AddField(fields, "CISLO_LINKY",            "Číslo linky",            "number")
    d ..AddField(fields, "LOKALNI_OZNACENI_LINKY", "Označení linky",         "text")
    d ..AddField(fields, "TITULEK",                "Titulek sestavy",        "text")
    d ..AddField(fields, "POPIS_LINKY",            "Popis linky",            "text")
    d ..AddField(fields, "POPIS_DOPRAVCE",         "Popis dopravce",         "text")
    d ..AddField(fields, "POPIS_PLATNOSTI",        "Popis platnosti",        "text")
    d ..AddField(fields, "DATUM_PLATNOSTI_OD",     "Datum platnosti od",     "date")
    d ..AddField(fields, "DATUM_PLATNOSTI_DO",     "Datum platnosti do",     "date")
    d ..AddField(fields, "POPIS_SMER",             "Popis směru",            "text")
    d ..AddField(fields, "TYP_JR",                 "Typ jízdního řádu",      "text")
    d ..AddField(fields, "QR_KOD",                 "QR kód URL",             "text")
    d ..AddField(fields, "POPIS_ZASTAVKA",         "Aktuální zastávka",      "text")
    d ..AddField(fields, "CISLO_ZASTAVKA",         "Číslo zastávky",         "text")
    d ..AddField(fields, "POZNAMKA_POD_DOPRAVCE",  "Poznámka pod dopravce",  "text")
    d ..AddField(fields, "POZNAMKA_NAD_LINKU",     "Poznámka nad linku",     "text")
    d ..AddField(fields, "INFORMACE",              "Informace",              "text")
    d ..AddField(fields, "NEJEDOU_SPOJE",          "Nástupiště",             "text")
}

ClassMethod AddStandardJRCollections(collections As %DynamicArray)
{
    // Pole = skalární klíče, které reálně produkují Build* metody v GetData (1:1 s ostrými daty).
    // ZONY (skalární pole) se vystavuje — plochá buňka ho zobrazí spojené („1/2", GetString join).
    // Objektová vnořená pole (ATRIBUTY {ZNAK,POPIS}, POZNAMKY_ZASTAVKY, ANOTACE) se NEvystavují —
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
    // Pozn.: TC (tarifní číslo) v AD záměrně nevystaveno — nese jen J-klíč (= interní ID),
    // ne reálné tarifní číslo; MHD TC (skutečné číslo zastávky) vystaveno zůstává.
    s colZastavky.fields = colZastavkyFields
    d collections.%Push(colZastavky)

    // SKUPINY_DNU (BuildSkupinaDnu)
    s colSkupiny = ##class(%DynamicObject).%New()
    s colSkupiny.name          = "Skupiny dnů"
    s colSkupiny.collectionKey = "SKUPINY_DNU"
    s colSkupinyFields = ##class(%DynamicArray).%New()
    d ..AddField(colSkupinyFields, "ID",     "ID skupiny",     "text")
    d ..AddField(colSkupinyFields, "NADPIS", "Nadpis skupiny", "text")
    d ..AddField(colSkupinyFields, "PORADI", "Pořadí",         "number")
    s colSkupiny.fields = colSkupinyFields
    d collections.%Push(colSkupiny)

    // ODJEZDY (BuildOdjezd)
    s colOdjezdy = ##class(%DynamicObject).%New()
    s colOdjezdy.name          = "Odjezdy"
    s colOdjezdy.collectionKey = "ODJEZDY"
    s colOdjezdyFields = ##class(%DynamicArray).%New()
    d ..AddField(colOdjezdyFields, "ZASTAVKA_ID",             "ID zastávky",          "text")
    d ..AddField(colOdjezdyFields, "SKUPINA_DNU_ID",          "ID skupiny dnů",       "text")
    d ..AddField(colOdjezdyFields, "HODINA",                  "Hodina",               "number")
    d ..AddField(colOdjezdyFields, "MINUTA",                  "Minuta",               "number")
    d ..AddField(colOdjezdyFields, "ZKRACENE_OZNACENI_LINKY", "Zkrácené ozn. linky",  "text")
    d ..AddField(colOdjezdyFields, "FORE_COLOR",              "Barva textu",          "text")
    d ..AddField(colOdjezdyFields, "BACK_COLOR",              "Barva pozadí",         "text")
    s colOdjezdy.fields = colOdjezdyFields
    d collections.%Push(colOdjezdy)

    // POZNAMKY (BuildPoznamka)
    s colPoznamky = ##class(%DynamicObject).%New()
    s colPoznamky.name          = "Poznámky"
    s colPoznamky.collectionKey = "POZNAMKY"
    s colPoznamkyFields = ##class(%DynamicArray).%New()
    d ..AddField(colPoznamkyFields, "TYP",        "Typ poznámky", "text")
    d ..AddField(colPoznamkyFields, "KATEGORIE",  "Kategorie",    "text")
    d ..AddField(colPoznamkyFields, "ZNAK",       "Znak",         "text")
    d ..AddField(colPoznamkyFields, "TEXT",       "Text poznámky", "text")
    d ..AddField(colPoznamkyFields, "FORE_COLOR", "Barva textu",  "text")
    d ..AddField(colPoznamkyFields, "BACK_COLOR", "Barva pozadí", "text")
    s colPoznamky.fields = colPoznamkyFields
    d collections.%Push(colPoznamky)
}

ClassMethod AddStandardJRParameters(arr As %DynamicArray)
{
    d ..AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true")
    d ..AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false")

    // Rozdělení sloupců typů provozu — override FTL (auto = dle linky MHD_TYP_R). 4 módy = kombinace
    // {prázdninový split prac. dnů} × {sloučení víkendu}; reuse existujícího NacistSkupinyDnu/ZarazeniSpoje.
    s pRozd = ##class(%DynamicObject).%New()
    s pRozd.name         = "ROZDELENI_SLOUPCU"
    s pRozd.label        = "Rozdělení sloupců (typy provozu)"
    s pRozd.type         = "enum"
    s pRozd.defaultValue = "Automaticky (dle linky)"
    s optsR = ##class(%DynamicArray).%New()
    d optsR.%Push("Automaticky (dle linky)")
    d optsR.%Push("Pracovní / Sobota / Neděle+svátky")
    d optsR.%Push("Pracovní / víkend (So+Ne+svátky)")
    d optsR.%Push("Prázdniny: Prac. mimo+o prázd. / víkend")
    d optsR.%Push("Prázdniny: Prac. mimo+o prázd. / Sobota / Neděle+svátky")
    s pRozd.options = optsR
    d arr.%Push(pRozd)

    s param = ##class(%DynamicObject).%New()
    s param.name         = "CASOVA_OSA"
    s param.label        = "Časová osa odjezdů"
    s param.type         = "enum"
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
    s pOd.defaultValue = "4"
    s pOd.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pOd)

    s pDo = ##class(%DynamicObject).%New()
    s pDo.name         = "HODINY_DO"
    s pDo.label        = "Hodiny do"
    s pDo.type         = "number"
    s pDo.defaultValue = "23"
    s pDo.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pDo)

    d ..AddBoolParam(arr, "ROZTAHNOUT_NA_VYSKU", "Roztáhnout na výšku", "false")

    // Rozsah „Spoje, nepoznámkovat" definuje legacy data (per spoj + TČ); tato volba určuje, CO se
    // v tom rozsahu v tisku schová. TD-aplikované (filtr nad nadmnožinou). Default = legacy.
    s pNepoz = ##class(%DynamicObject).%New()
    s pNepoz.name         = "SCHOVAT_V_ROZSAHU"
    s pNepoz.label        = "V rozsahu nepoznámkovat schovat"
    s pNepoz.type         = "enum"
    s pNepoz.defaultValue = "Ruční komentáře"
    s optsNepoz = ##class(%DynamicArray).%New()
    d optsNepoz.%Push("Ruční komentáře")
    d optsNepoz.%Push("Automatické (rozdíly tras)")
    d optsNepoz.%Push("Obojí")
    s pNepoz.options = optsNepoz
    d arr.%Push(pNepoz)

    // Skrýt agregátní poznámku „všechny spoje mají bezbariérově přístupné vozidlo" (#7161 D6).
    // TD-aplikované (filtr skryje legendu kategorie bezbarier-agregat). Default = zobrazit (legacy).
    d ..AddBoolParam(arr, "SCHOVAT_AGREGAT_BEZBARIER", "Skrýt souhrn ""všechny spoje bezbariérové""", "false")
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Build* helpery výstupního JSON (duplikováno z ZastavkoveJrMhd — stejný formát)
/// ═══════════════════════════════════════════════════════════════════════════════
ClassMethod BuildZastavka(id As %String, popis As %String, tc As %String = "", casJizdy As %Integer = 0, poradi As %Integer = 1, jeAktualni As %Boolean = 0, naZnameni As %Boolean = 0, odsazeni As %Integer = 0) As %DynamicObject
{
    s zastavka = ##class(%DynamicObject).%New()
    // Čitelná field ID (SLOVO_SLOVO) — sdílený kontrakt sloupců StopList↔DynamicTable.
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

ClassMethod BuildOdjezd(zastavkaId As %String, skupinaDnuId As %String, hodina As %Integer, minuta As %Integer, zkrOznLinky As %String = "") As %DynamicObject
{
    s odjezd = ##class(%DynamicObject).%New()
    d odjezd.%Set("ZASTAVKA_ID", zastavkaId)
    d odjezd.%Set("SKUPINA_DNU_ID", skupinaDnuId)
    d odjezd.%Set("HODINA", hodina)
    d odjezd.%Set("MINUTA", minuta)
    d odjezd.%Set("ZKRACENE_OZNACENI_LINKY", zkrOznLinky)
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
/// umožní TD filtru prořezat legendu jen v relevantní kategorii (např. „x — na znamení" se nesmí
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

/// Konvertuje RGB string "R;G;B" na SkiaSharp hex "#FFRRGGBB". Prázdný vstup → prázdný string.
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
