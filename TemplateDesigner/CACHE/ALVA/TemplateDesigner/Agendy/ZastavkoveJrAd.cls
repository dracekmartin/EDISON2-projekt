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
/// **Stav: Fáze 1 (skeleton)** — GetSchema/GetMockData/GetTableTemplates/GetParameters hotové,
/// GetData je stub (Fáze 2 = fázový engine čtoucí ^JRL).
///
/// Sdílené Build*/AddStandard* helpery jsou zatím **duplikované** z `ZastavkoveJrMhd`
/// (stejný výstupní formát). Extrakce do společného helperu/baseclass až podle výsledků
/// (ne předem) — viz dohoda v projektových poznámkách.
Class ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaCode = "ZASTAVKOVY_JR_AD";

Parameter AgendaLabel = "Zastávkový JŘ (AD)";

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

    // Výchozí linka = první v aktuální firmě → auto-load preview funguje bez ohledu na firmu.
    d ..PrvniLinka(dataDB, .defSku, .defLin)
    i defSku = "" s defSku = 1

    s arr = ##class(%DynamicArray).%New()

    // Vstupní parametry — identifikace linky v ^JRL (povinné pro GetData)
    s pSku = ##class(%DynamicObject).%New()
    s pSku.name = "SKUPINA_LINEK_ID"
    s pSku.label = "Skupina linek"
    s pSku.type = "string"
    s pSku.defaultValue = defSku
    d arr.%Push(pSku)

    s pLin = ##class(%DynamicObject).%New()
    s pLin.name = "LINKA_ID"
    s pLin.label = "Linka"
    s pLin.type = "string"
    s pLin.defaultValue = defLin
    d arr.%Push(pLin)

    d ..AddStandardJRParameters(arr)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q $$$OK
}

/// Vrátí seznam linek pro Instance Picker (umožní proklikávání stránek v preview).
/// IN["search"] = substring filtr (case-insensitive), IN["limit"] = max (default 50),
/// IN["SKUPINA_LINEK_ID"] = skupina linek (default první v ^JRL).
/// OUT["1"] = JSON pole { "value": lin, "display": "číslo — název", "subtitle": "Platnost ..." }.
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
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
            s display = +lin
            i nazev '= "" s display = (+lin)_" — "_nazev
            i (search '= "") && ('$f($zcvt(display,"L"), search)) continue

            s plati = $g(^[dataDB]JRL(sku,lin,"PATCH","Plati"))
            s subtitle = ""
            i $p(plati,"~",1) '= "" {
                s subtitle = "Platnost od "_$p(plati,"~",1)
                i $p(plati,"~",2) '= "" s subtitle = subtitle_" do "_$p(plati,"~",2)
            }

            s obj = ##class(%DynamicObject).%New()
            d obj.%Set("value", lin)
            d obj.%Set("display", display)
            i subtitle '= "" d obj.%Set("subtitle", subtitle)
            d arr.%Push(obj)

            s count = count + 1
            q:count>=limit
        }

        s ^||ML("OUT","1") = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Vrátí data pro tisk: 1 stránka per zastávka × směr.
///
/// **Fáze 2 (TODO):** reimplementovat legacy `WinTiskMhd^JRapiExp10` — fázový engine:
///   1. Načíst linku + trasu (^JRL(sku,lin,"J"), "I", PATCH)
///   2. Načíst spoje (^JRL(sku,lin,"T"/"Z",sp)) + trasy spojů ("W") + omezení (,22 / ,32)
///   3. Odvodit sloupce (skupiny dnů) z FTL (MHD_TYP_R) + Plk + CNZ
///   4. Promítnout odjezdy per zastávka × sloupec, anotace + NZ
///   5. Sestavit poznámky (legenda)
///   6. Iterovat zastávky × směry → OUT[$i] = JSON stránka (values + rows ZASTAVKY/ODJEZDY/POZNAMKY)
/// Zlatá reference výstupu: `cache-database-linky.md` §9.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = 1
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
    d vals.%Set("TYP_JR",                 "")
    d vals.%Set("QR_KOD",                 $g(^[dataDB]JRL(sku,lin,"PATCH","QR_CODE")))
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  $g(^[dataDB]JRL(sku,lin,"PATCH","PoznPodDopr")))
    d vals.%Set("INFORMACE",              informace)
    d vals.%Set("NEJEDOU_SPOJE",          "")

    // ── trasa → zastávky (úsek od aktuální zastávky ve směru jízdy; zóny + na znamení) ──
    // Tam: vzestupně od aktuální k poslední. Zpět: sestupně od aktuální k první (pořadí 1).
    s zastavky = ##class(%DynamicArray).%New()
    s aktualniNazev = "", aktualniCzst = "", posledniNazev = "", maNaZnameni = 0
    k servedPoradi
    d ..ObsluhovanePoradi(dataDB, sku, lin, smer, .servedPoradi)   // filtr projížděných zastávek
    s jeTam = (smer = "T")
    s poradi = $s(jeTam:"", 1:currentPoradi + 1)
    s cnt = 0
    f {
        i jeTam { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi)) }
        else    { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi), -1) }
        q:poradi=""
        i '$isvalidnum(poradi) continue
        i jeTam,(poradi < currentPoradi) continue           // Tam: jen od aktuální zastávky nahoru
        s veta = $g(^[dataDB]JRL(sku,lin,"J",poradi))
        s czst = $p(veta,":",1)
        i czst="" continue
        i '$d(servedPoradi(poradi)) continue                // jen obsluhované zastávky (jako legacy)
        s atributy  = $p($p(veta,":",2),"#",1)              // znaky před prvním "#"
        s zona      = $p($p(veta,":",5),"#",2)               // "#zóna#..."
        s naZnameni = (atributy["x")
        i naZnameni s maNaZnameni = 1
        s rawNazev = ..NazevZastavky(dataDB, czst)
        s jeAkt = (poradi = currentPoradi)
        s cnt = cnt + 1
        s z = ..BuildZastavka(poradi, rawNazev, poradi, 0, cnt, jeAkt, naZnameni)
        i zona '= "" d ..AddZona(z, zona)
        d zastavky.%Push(z)
        i jeAkt s aktualniNazev = rawNazev, aktualniCzst = czst
        s posledniNazev = rawNazev
    }

    d vals.%Set("POPIS_ZASTAVKA",  aktualniNazev)
    d vals.%Set("CISLO_ZASTAVKA",  aktualniCzst)
    d vals.%Set("POPIS_SMER",      posledniNazev)

    // ── odjezdy + poznámky (omezení "nejede" → anotace + legenda) ────
    k poznMap
    s odjezdy = ..NacistOdjezdy(dataDB, sku, lin, smer, currentPoradi, .poznMap)
    i odjezdy.%Size() = 0 q ""        // zastávka bez odjezdů v daném směru (konečná) — stránku negenerovat
    s poznamky = ..SestavPoznamky(.poznMap, maNaZnameni)

    // ── sestavení stránky ───────────────────────────────────────────
    s out = ##class(%DynamicObject).%New()
    d out.%Set("values", vals)
    s rows = ##class(%DynamicObject).%New()
    d rows.%Set("ZASTAVKY", zastavky)
    d rows.%Set("SKUPINY_DNU", ..NacistSkupinyDnu())
    d rows.%Set("ODJEZDY", odjezdy)
    d rows.%Set("POZNAMKY", poznamky)
    d out.%Set("rows", rows)
    q out
}

/// Vrátí výchozí skupiny dnů (FTL=0: Pracovní dny / Soboty / Neděle a svátky).
/// Increment 3: odvození podle MHD_TYP_R (FTL módy) + NZ prázdninové dělení.
ClassMethod NacistSkupinyDnu() As %DynamicArray
{
    s skupiny = ##class(%DynamicArray).%New()
    d skupiny.%Push(..BuildSkupinaDnu("1", "Pracovní dny", "", 1))
    d skupiny.%Push(..BuildSkupinaDnu("2", "Soboty", "", 2))
    d skupiny.%Push(..BuildSkupinaDnu("3", "Neděle a svátky", "", 3))
    q skupiny
}

/// Spočítá odjezdy z aktuální zastávky pro daný směr.
/// Pro každý spoj: odjezd = Odjezd spoje + offset času na aktuální zastávce (z trasy spoje "W").
/// `<` na zastávce = spoj zde nestaví → přeskočit. Zařazení do skupiny dnů dle Plk (FTL=0).
/// Increment 3: plné FTL/Plk/CNZ odvození. Increment 4: anotace (NZ, omezení).
ClassMethod NacistOdjezdy(dataDB As %String, sku As %String, lin As %String, smer As %String, currentPoradi As %String, ByRef poznMap) As %DynamicArray
{
    s odjezdy = ##class(%DynamicArray).%New()
    i currentPoradi = "" q odjezdy

    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue

        s hlava      = $p($g(^[dataDB]JRL(sku,lin,smer,sp)), "#", 1)
        s cisloTrasy = $p(hlava, ":", 3)
        s odjezd     = +$p(hlava, ":", 4)
        s plk        = $p(hlava, ":", 7)

        // offset času na aktuální zastávce z trasy spoje
        s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
        s offset = ..WCasNaZastavce(wTrasa, currentPoradi)
        i offset = "<" continue           // spoj zde nestaví (projíždí)
        i offset = "" continue            // zastávka mimo trasu spoje
        i '$isvalidnum(offset) continue

        s celkem = odjezd + offset
        s hod = (celkem\60)#24, min = celkem#60

        // anotace z datumových omezení spoje (omzId 11-15, 22) — sdílená přes všechny skupiny spoje
        s znak = ""
        s txt = ..SestavTextOmezeni(dataDB, sku, lin, smer, sp)
        i txt '= "" s znak = ..ZiskejMarker(.poznMap, txt)

        // spoj může patřit do více skupin dnů (např. Plk "6+" → soboty i neděle/svátky)
        s skupiny = ..SkupinyZPlk(plk)
        f gi=1:1:$l(skupiny,",") {
            s o = ..BuildOdjezd(currentPoradi, $p(skupiny,",",gi), hod, min)
            i znak '= "" d ..AddAnotace(o, "text", znak)
            d odjezdy.%Push(o)
        }
    }
    q odjezdy
}

/// Naplní `served(poradi)` zastávkami, kde staví aspoň jeden spoj daného směru (W-offset != "<").
/// Slouží k filtru projížděných zastávek ze StopListu (jako legacy).
ClassMethod ObsluhovanePoradi(dataDB As %String, sku As %String, lin As %String, smer As %String, ByRef served)
{
    k served
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
}

/// Vrátí čas (minuty od počátku spoje) na zastávce daného pořadí z trasy spoje "W".
/// Formát "W": #-segmenty `poradi:cas:km:...`; `<` = spoj zde nestaví. "" = nenalezeno.
ClassMethod WCasNaZastavce(wTrasa As %String, poradi As %String) As %String
{
    f i=1:1:$l(wTrasa,"#") {
        s seg = $p(wTrasa,"#",i)
        i $p(seg,":",1) = poradi ret $p(seg,":",2)
    }
    q ""
}

/// Vrátí skupiny dnů (čárkou oddělené) pro spoj dle tarifní platnosti Plk (FTL=0).
/// Spoj může patřit do VÍCE skupin: 1-5/X/prázdné → Pracovní(1); 6 → Soboty(2); 7/+ → Neděle a svátky(3).
/// Plné FTL 1-3 + prázdninové dělení (MHD_TYP_R/CNZ) zatím neimplementováno (chybí grid-linka k ověření).
ClassMethod SkupinyZPlk(plk As %String) As %String
{
    s g = ""
    i (plk["1")||(plk["2")||(plk["3")||(plk["4")||(plk["5")||(plk["X")||(plk="")||(plk=".") s g = g_"1,"
    i plk["6" s g = g_"2,"
    i (plk["7")||(plk["+") s g = g_"3,"
    i g = "" s g = "1,"
    q $e(g,1,*-1)
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

/// Sestaví legendu: na-znamení (× pokud nějaká zastávka) + nasbírané "nejede" poznámky (v pořadí).
ClassMethod SestavPoznamky(ByRef poznMap, maNaZnameni As %Boolean) As %DynamicArray
{
    s poznamky = ##class(%DynamicArray).%New()
    i maNaZnameni d poznamky.%Push(..BuildPoznamka("specialni", "x", "zastávka pouze na znamení"))
    s n = ""
    f {
        s n = $o(poznMap("byMarker", n))
        q:n=""
        d poznamky.%Push(..BuildPoznamka("text", n, poznMap("byMarker", n)))
    }
    q poznamky
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

/// Datumová část omezení (1:1 s `TODDO^JRapiNZUtl`): datumy verbatim VČETNĚ roku, `:`→rozsah `D1-D2`,
/// jednotlivé datum = `D1`, položky (`#`) spojené čárkou bez mezery. Vedoucí prefix `k@`/`L@` se zahodí.
ClassMethod FormatDatumyOmezeni(omz As %String) As %String
{
    s data = $s(omz["@":$p(omz,"@",2), 1:omz)
    s casti = ""
    f i=1:1:$l(data,"#") {
        s item = $p(data,"#",i)
        i item = "" continue
        s od = $p(item,":",1), do = $p(item,":",2)
        i do '= "" { s cast = od_"-"_do }
        else       { s cast = od }
        i casti '= "" s casti = casti_","
        s casti = casti_cast
    }
    q casti
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

/// Název dopravce (fa,pr) z `^JRSET("DOP",...)` — fallback chain dle `gDopMHD`/`gDopFa^JRapiDop`:
/// per-provoz zkrácený MHD ($6) → firemní zkrácený MHD ($14) → obchodní název ($7) → sídlo/název ($1).
ClassMethod NazevDopravce(dataDB As %String, fa As %String, pr As %String) As %String
{
    i pr '= "" {
        s nazev = $p($g(^[dataDB]JRSET("DOP",fa,pr)),"~",6)
        i $tr(nazev," ","") '= "" q nazev
    }
    s firma = $g(^[dataDB]JRSET("DOP",fa))
    s nazev = $p(firma,"~",14)
    i $tr(nazev," ","") '= "" q nazev
    s nazev = $p(firma,"~",7)
    i nazev '= "" q nazev
    q $p(firma,"~",1)
}

/// Vrátí název zastávky z ^JRZ(czst) — odbav/MHD název s obcí ($7, formát "obec,místo").
/// Legacy tiskne přesně toto pole (zkrácená obec "Fr. Lázně,..." místo plné "Františkovy Lázně,...").
/// Fallback na $2, pokud $7 chybí (nevyplněný odbav název).
ClassMethod NazevZastavky(dataDB As %String, czst As %String) As %String
{
    s rec = $g(^[dataDB]JRZ(czst))
    s nazev = $p(rec, "$", 7)
    i nazev = "" s nazev = $p(rec, "$", 2)
    q nazev
}

/// Vrátí ukázková data pro live preview v designeru.
///
/// **DEV (Fáze 2):** zrcadlí živý `GetData` nad jednou hardcoded linkou (CB_GWBUS / 0320113),
/// aby designer preview ukazoval reálný stav rebuildu — jak přibývají increments (odjezdy,
/// sloupce…), preview se plní živými daty z `^JRL`.
/// TODO: před produkčním nasazením vrátit lehký statický vzorek nezávislý na firmě/datech.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    $$$FillParams

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
    q $$$OK
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
    d ..AddField(fields, "INFORMACE",              "Informace",              "text")
    d ..AddField(fields, "NEJEDOU_SPOJE",          "Nejedou spoje",          "text")
}

ClassMethod AddStandardJRCollections(collections As %DynamicArray)
{
    // ZASTAVKY
    s colZastavky = ##class(%DynamicObject).%New()
    s colZastavky.name          = "Zastávky"
    s colZastavky.collectionKey = "ZASTAVKY"
    s colZastavkyFields = ##class(%DynamicArray).%New()
    d ..AddField(colZastavkyFields, "ID",                "ID zastávky",         "text")
    d ..AddField(colZastavkyFields, "POPIS",             "Název zastávky",      "text")
    d ..AddField(colZastavkyFields, "TC",                "Tarifní číslo",       "text")
    d ..AddField(colZastavkyFields, "CAS_JIZDY",         "Čas jízdy",           "number")
    d ..AddField(colZastavkyFields, "NA_ZNAMENI",        "Na znamení",          "boolean")
    s colZastavky.fields = colZastavkyFields
    d collections.%Push(colZastavky)

    // SKUPINY_DNU
    s colSkupiny = ##class(%DynamicObject).%New()
    s colSkupiny.name          = "Skupiny dnů"
    s colSkupiny.collectionKey = "SKUPINY_DNU"
    s colSkupinyFields = ##class(%DynamicArray).%New()
    d ..AddField(colSkupinyFields, "ID",      "ID skupiny",     "text")
    d ..AddField(colSkupinyFields, "NADPIS",  "Nadpis skupiny", "text")
    d ..AddField(colSkupinyFields, "NADPIS2", "Nadpis 2",       "text")
    s colSkupiny.fields = colSkupinyFields
    d collections.%Push(colSkupiny)

    // ODJEZDY
    s colOdjezdy = ##class(%DynamicObject).%New()
    s colOdjezdy.name          = "Odjezdy"
    s colOdjezdy.collectionKey = "ODJEZDY"
    s colOdjezdyFields = ##class(%DynamicArray).%New()
    d ..AddField(colOdjezdyFields, "ZASTAVKA_ID",     "ID zastávky",       "text")
    d ..AddField(colOdjezdyFields, "SKUPINA_DNU_ID",  "ID skupiny dnů",    "text")
    d ..AddField(colOdjezdyFields, "HODINA",          "Hodina",            "number")
    d ..AddField(colOdjezdyFields, "MINUTA",          "Minuta",            "number")
    s colOdjezdy.fields = colOdjezdyFields
    d collections.%Push(colOdjezdy)

    // POZNAMKY
    s colPoznamky = ##class(%DynamicObject).%New()
    s colPoznamky.name          = "Poznámky"
    s colPoznamky.collectionKey = "POZNAMKY"
    s colPoznamkyFields = ##class(%DynamicArray).%New()
    d ..AddField(colPoznamkyFields, "TYP",  "Typ poznámky", "text")
    d ..AddField(colPoznamkyFields, "ZNAK", "Znak",          "text")
    d ..AddField(colPoznamkyFields, "TEXT", "Text poznámky",  "text")
    s colPoznamky.fields = colPoznamkyFields
    d collections.%Push(colPoznamky)
}

ClassMethod AddStandardJRParameters(arr As %DynamicArray)
{
    d ..AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true")
    d ..AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false")

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
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Build* helpery výstupního JSON (duplikováno z ZastavkoveJrMhd — stejný formát)
/// ═══════════════════════════════════════════════════════════════════════════════
ClassMethod BuildZastavka(id As %String, popis As %String, tc As %String = "", casJizdy As %Integer = 0, poradi As %Integer = 1, jeAktualni As %Boolean = 0, naZnameni As %Boolean = 0, odsazeni As %Integer = 0) As %DynamicObject
{
    s zastavka = ##class(%DynamicObject).%New()
    d zastavka.%Set("ID", id)
    d zastavka.%Set("POPIS", popis)
    d zastavka.%Set("TC", tc)
    d zastavka.%Set("CAS_JIZDY", casJizdy)
    d zastavka.%Set("PORADI", poradi)
    d zastavka.%Set("JE_AKTUALNI", $s(jeAktualni:"true", 1:"false"))
    d zastavka.%Set("NA_ZNAMENI", $s(naZnameni:"true", 1:"false"))
    d zastavka.%Set("ODSAZENI", odsazeni)
    d zastavka.%Set("ZAKLADNI_TRASA", "true")
    d zastavka.%Set("ZONY", ##class(%DynamicArray).%New())
    d zastavka.%Set("PRUBEHY", ##class(%DynamicArray).%New())
    d zastavka.%Set("ATRIBUTY", ##class(%DynamicArray).%New())
    d zastavka.%Set("POZNAMKY_ZASTAVKY", ##class(%DynamicArray).%New())
    q zastavka
}

ClassMethod AddZona(zastavka As %DynamicObject, zona As %String)
{
    d zastavka.%Get("ZONY").%Push(zona)
}

ClassMethod AddPrubeh(zastavka As %DynamicObject, prubeh As %String)
{
    d zastavka.%Get("PRUBEHY").%Push(prubeh)
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

ClassMethod AddAnotace(odjezd As %DynamicObject, typ As %String, znak As %String, foreColor As %String = "", backColor As %String = "")
{
    s anotace = ##class(%DynamicObject).%New()
    d anotace.%Set("TYP", typ)
    d anotace.%Set("ZNAK", znak)
    d anotace.%Set("FORE_COLOR", foreColor)
    d anotace.%Set("BACK_COLOR", backColor)
    d odjezd.%Get("ANOTACE").%Push(anotace)
}

ClassMethod BuildPoznamka(typ As %String, znak As %String, text As %String, foreColor As %String = "", backColor As %String = "") As %DynamicObject
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TEXT", text)
    d poznamka.%Set("FORE_COLOR", foreColor)
    d poznamka.%Set("BACK_COLOR", backColor)
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
