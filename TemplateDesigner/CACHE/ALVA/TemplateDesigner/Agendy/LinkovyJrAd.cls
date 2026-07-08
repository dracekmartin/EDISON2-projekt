Include GENERAL

/// Agenda: **Linkový JŘ z dat autobusové dopravy (AD)** — celá linka v jednom tisku:
/// svislý seznam zastávek (tarifní čísla) × vodorovné sloupce jednotlivých SPOJŮ s časy v buňkách.
/// Legacy ekvivalent `ALVA.TISKJR` (klient) + `mkPLinWin^JRapiLinPrtWin` (JRMAIN, skutečný engine).
/// 
/// Datový zdroj (stejný jako `ZastavkoveJrAd`):
/// - `^[dataDB]JRL(sku,lin,...)` — linka ("I"), trasa ("J"), spoje ("T"/"Z"), trasy spojů ("W"),
///   km sloupce (PATCH,"KmsSmer"), návaznosti (spoj,31/32), omezení (spoj,11-15/22), PATCH metadata
/// - `^[dataDB]JRZ(czst,...)`    — zastávky (názvy)
/// - `^[dataDB]JRSET(...)`       — dopravci ("DOP"), texty legend ("NCOME"/"STALCOME"),
///   nadpisy skupin dnů ("Definice","WinMHD",rec,"DSC"), formát datumů ("Definice","TiskDtm")
/// 
/// ── Wire formát (čistě JSON, R3) — 1 stránka per (linka × směr) ──────────────────
/// **Sémantický tvar** (data = CO, element = JAK): element `LineTableElement` si matici
/// [zastávka × spoj] skládá sám; Caché neposílá layout (žádný legacy `idobj` model).
/// 
///   values: CISLO_LINKY, POPIS_LINKY, POZNAMKA_NAD_LINKU, POZNAMKA_POD_DOPRAVCE, POPIS_DOPRAVCE,
///           PLATNOST_OD/DO, POPIS_PLATNOSTI, TYP_JR, QR_KOD, SMER ("T"/"Z"), POPIS_SMER,
///           VYCHOZI_ZASTAVKA
///   rows.ZASTAVKY:    [{ PORADI, TYP ("zastavka" hlavní/odjezdový řádek | "prijezd" vložený
///                        příjezdový u zastávek s atributem P/J), CZST, POPIS, TC, ZONA, MHD,
///                        KM:[per km-sloupec směru], ATRIBUTY:[znaky], NASTUPISTE }]
///                     — v pořadí směru jízdy (T vzestupně, Z sestupně)
///   rows.SPOJE:       [{ CISLO_SPOJE, SKUPINA, NZ, PLATNOST:[znaky], CASY:[zarovnané na ZASTAVKY:
///                        "HH:MM" | "" (mimo trasu) | "|" (projíždí) | "<" (jede jinudy)],
///                        PRIPOJE:[{ TYP ("vycka"/"navazuje"), PORADI, ZNAK, TEXT, CAS, LINKA,
///                        SPOJ, RADEK (0/1 — legacy „typ výstupu": tisknout jako vložený řádek) }] }]
///                     — řazené dle legacy mkPoradiSp (odjezd z výchozí + bublinové doseřazení
///                       per tarifní číslo); spoj patřící do víc skupin dnů emitován vícekrát
///   rows.SKUPINY_DNU: [{ ID, NADPIS }] dle parametru DELENI_SLOUPCU (texty WinMHD DSC číselník)
///   rows.POZNAMKY:    [{ DRUH (1-10 legacy číselník druhů), TYP ("text"/"znak"/"nz"), ZNAK, TEXT }]
///                     — pořadí druhů pevné (1,2,3,7,8,9,10; 4/5/6 zatím neemitovány)
/// 
/// ── Vědomé mezery vůči mkPLinWin (viz analýza linkovy-knizni-jr-implementace §11) ──
/// - bez 2D bin-packingu (`Strih`), měřicích řetězců a padding sloupců — layout řeší TD
/// - bez turnusů, obousměrného tisku, souhrnných JŘ (řádek zdrojové linky)
/// - poznámky druhů 4 (poznámka pro spoj, omz41/42), 5 (atributy spoje) a 6 (místenkování)
///   čekají na mapování gSpLin dta→raw pieces; pořadí poznámek pevné (legacy konfigurovatelné)
/// - dělení do skupin dnů jen dle Plk znaků (legacy u spojů „jede vždy" ověřuje kalendář
///   mkStaLinSp — spoj bez týdenní platnosti jde do všech skupin, jako legacy fallback HD 10183)
/// - symbolový font (roadmap 42b): ZNAK hodnoty jsou legacy klíče (X, |, <, m, u…), ne glyfy
/// 
/// Gold oracle: `ALVA.TISKJR.Data/LoadData` s `VOLANO_Z~JRMAIN`; multi-linkový diff harness
/// (scratchpad LineTableHarness, režim diff) porovnává spoje/časy/km/zóny/poznámky napříč firmami.
Class ALVA.TemplateDesigner.Agendy.LinkovyJrAd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaCode As %String = "LINKOVY_JR_AD";

Parameter AgendaLabel As %String = "Linkový JŘ (AD)";

Parameter InstanceParamName As %String = "LINKA_ID";

Parameter ElementKind As %String = "LINE";

Parameter InstanceLabel As %String = "linka";

Parameter InstanceLabelPlural As %String = "linky";

/// Vrátí DataSchema — skaláry hlavičky linky + kolekce ZASTAVKY/SPOJE/SKUPINY_DNU/POZNAMKY.
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    $$$FillParams

    s def = ##class(%DynamicObject).%New()

    s fields = ##class(%DynamicArray).%New()
    d ..AddField(fields, "CISLO_LINKY",           "Číslo linky",              "number")
    d ..AddField(fields, "POPIS_LINKY",           "Název linky",              "text")
    d ..AddField(fields, "POZNAMKA_NAD_LINKU",    "Poznámka nad linku",       "text")
    d ..AddField(fields, "POZNAMKA_POD_DOPRAVCE", "Poznámka pod dopravce",    "text")
    d ..AddField(fields, "POPIS_DOPRAVCE",        "Dopravce",                 "text")
    d ..AddField(fields, "PLATNOST_OD",           "Platnost od",              "text")
    d ..AddField(fields, "PLATNOST_DO",           "Platnost do",              "text")
    d ..AddField(fields, "POPIS_PLATNOSTI",       "Popis platnosti",          "text")
    d ..AddField(fields, "TYP_JR",                "Typ JŘ (razítko)",         "text")
    d ..AddField(fields, "QR_KOD",                "QR kód",                   "text")
    d ..AddField(fields, "SMER",                  "Směr (T/Z)",               "text")
    d ..AddField(fields, "POPIS_SMER",            "Konečná směru",            "text")
    d ..AddField(fields, "VYCHOZI_ZASTAVKA",      "Výchozí zastávka",         "text")
    s def.fields = fields

    s collections = ##class(%DynamicArray).%New()

    s g = ##class(%DynamicObject).%New()
    s g.name = "Zastávky", g.collectionKey = "ZASTAVKY"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "PORADI",     "Pořadí (Tč)",        "number")
    d ..AddField(gf, "TYP",        "Typ řádku",          "text")
    d ..AddField(gf, "CZST",       "Číslo zastávky",     "number")
    d ..AddField(gf, "POPIS",      "Název zastávky",     "text")
    d ..AddField(gf, "TC",         "Tarifní číslo",      "number")
    d ..AddField(gf, "ZONA",       "Zóna",               "text")
    d ..AddField(gf, "MHD",        "MHD/IDS",            "text")
    d ..AddField(gf, "NASTUPISTE", "Nástupiště",         "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Spoje", g.collectionKey = "SPOJE"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "CISLO_SPOJE", "Číslo spoje",       "number")
    d ..AddField(gf, "SKUPINA",     "Skupina dnů",       "number")
    d ..AddField(gf, "NZ",          "Negativní značka",  "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Skupiny dnů", g.collectionKey = "SKUPINY_DNU"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "ID",     "ID skupiny", "number")
    d ..AddField(gf, "NADPIS", "Nadpis",     "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Poznámky", g.collectionKey = "POZNAMKY"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "DRUH", "Druh (1-10)",       "number")
    d ..AddField(gf, "TYP",  "Typ uvození",       "text")
    d ..AddField(gf, "ZNAK", "Znak",              "text")
    d ..AddField(gf, "TEXT", "Text",              "text")
    s g.fields = gf
    d collections.%Push(g)

    d def.%Set("fieldCollections", collections)

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    q $$$OK
}

/// Fixed šablony tabulek — knižní JŘ žádné nemá (layout drží LineTableElement).
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
{
    s ^||ML("OUT", $i(^||ML("OUT"))) = "[]"
    q $$$OK
}

/// Fixed šablony tabulek nejsou — vrací prázdnou definici.
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s ^||ML("OUT", $i(^||ML("OUT"))) = "{}"
    q $$$OK
}

/// Parametry sestavy. Linka se vybírá v Instance Pickeru (GetAvailableInstances).
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    $$$FillParams

    s arr = ##class(%DynamicArray).%New()

    // Dělení sloupců spojů podle platnosti (legacy in("A",2,"arr",3) = 0/1/2)
    s p = ##class(%DynamicObject).%New()
    s p.name = "DELENI_SLOUPCU"
    s p.label = "Dělení spojů dle platnosti"
    s p.type = "enum"
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("Bez rozlišení")
    d opts.%Push("Pracovní dny / víkend+svátky")
    d opts.%Push("Pracovní dny / Soboty / Neděle+svátky")
    d p.%Set("options", opts)
    s p.defaultValue = "Pracovní dny / Soboty / Neděle+svátky"
    d arr.%Push(p)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q $$$OK
}

/// Vrátí seznam linek pro Instance Picker (shodné se ZastavkoveJrAd — stejný zdroj ^JRL).
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s search = $zcvt($g(^||ML("IN","search")), "L")
        s limit  = +$g(^||ML("IN","limit"))
        i 'limit s limit = 50

        s sku = +$g(^||ML("IN","SKUPINA_LINEK_ID"))
        i 'sku s sku = $o(^[dataDB]JRL(""))

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

/// Vrátí data pro tisk: 1 stránka per (linka × směr) — Tam, pak Zpět.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizni                    // per-request cache (W-mapy)

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s lin = ..NormalizLinku($g(^||ML("IN","LINKA_ID")))
        i lin = "" s lin = ..NormalizLinku($g(^||ML("IN","INSTANCE_ID")))
        i lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))
        i '$d(^[dataDB]JRL(sku,lin,"I")) $$$ThrowStatus($$$ERROR($$$GeneralError, "Linka "_sku_"/"_lin_" neexistuje v ^JRL"))

        s deleni = ..DeleniZParametru($g(^||ML("IN","DELENI_SLOUPCU")))

        f smer = "T", "Z" {
            s page = ..BuildStrankaLinky(dataDB, sku, lin, smer, deleni)
            i page '= "" s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
        }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Ukázková data pro live preview — živě z první linky aktuální firmy (DEV vzor).
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizni

        d ..PrvniLinka(dataDB, .sku, .lin)
        s page = ""
        i lin '= "" s page = ..BuildStrankaLinky(dataDB, sku, lin, "T", 2)

        i page = "" {
            s page = ##class(%DynamicObject).%New()
            s v = ##class(%DynamicObject).%New()
            d v.%Set("POPIS_LINKY", "(žádná linka v ^JRL pro firmu "_dataDB_")")
            d page.%Set("values", v)
            s r = ##class(%DynamicObject).%New()
            d r.%Set("ZASTAVKY", ##class(%DynamicArray).%New())
            d r.%Set("SPOJE", ##class(%DynamicArray).%New())
            d r.%Set("SKUPINY_DNU", ##class(%DynamicArray).%New())
            d r.%Set("POZNAMKY", ##class(%DynamicArray).%New())
            d page.%Set("rows", r)
        }

        s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Mapování labelu parametru „Dělení spojů dle platnosti" na legacy mód 0/1/2.
ClassMethod DeleniZParametru(label As %String) As %Integer
{
    q $case(label, "Bez rozlišení":0, "Pracovní dny / víkend+svátky":1, :2)
}

/// Jazyková varianta textových číselníků (NCOME/STALCOME/WinMHD): "CZ", při SK instalaci "*SK".
ClassMethod Jazyk(dataDB As %String) As %String
{
    i $g(^[dataDB]JRSET("NCOME","CZ","X")) '= "" q "CZ"
    i $d(^[dataDB]JRSET("NCOME","*SK")) q "*SK"
    q "CZ"
}

/// Režim tisku datumů omezení (legacy TiskDtm): per-linka PATCH override, jinak globální Definice.
/// 0 = jak zadáno, 1 = rok 2místně, 2 = rozsahy bez roku v „od", 3 = bez roku.
ClassMethod TiskDtm(dataDB As %String, sku As %String, lin As %String) As %Integer
{
    s v = $g(^[dataDB]JRL(sku,lin,"PATCH","TiskDtm"))
    i v = "" s v = $g(^[dataDB]JRSET("Definice","TiskDtm"))
    q +v
}

/// Sestaví JSON stránku knižního JŘ pro jednu linku a směr. "" když směr nemá spoje.
ClassMethod BuildStrankaLinky(dataDB As %String, sku As %String, lin As %String, smer As %String, deleni As %Integer) As %DynamicObject
{
    i '$d(^[dataDB]JRL(sku,lin,"I")) q ""
    i '..MaSpoje(dataDB, sku, lin, smer) q ""

    s rec = ..Jazyk(dataDB)
    s tiskDtm = ..TiskDtm(dataDB, sku, lin)

    // ── hlavička linky ──────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            +lin)
    d vals.%Set("POPIS_LINKY",            ..NazevLinky(dataDB, sku, lin))
    d vals.%Set("POZNAMKA_NAD_LINKU",     $g(^[dataDB]JRL(sku,lin,"PATCH","Head")))
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  $g(^[dataDB]JRL(sku,lin,"PATCH","PoznPodDopr")))
    d vals.%Set("POPIS_DOPRAVCE",         ..PopisDopravce(dataDB, sku, lin))
    s plati = $g(^[dataDB]JRL(sku,lin,"PATCH","Plati"))
    s platiOd = $p(plati,"~",1), platiDo = $p(plati,"~",2)
    d vals.%Set("PLATNOST_OD",            platiOd)
    d vals.%Set("PLATNOST_DO",            platiDo)
    s popisPlatnosti = ""
    i platiOd '= "" {
        s popisPlatnosti = "Platí od "_platiOd
        i platiDo '= "" s popisPlatnosti = popisPlatnosti_" do "_platiDo
    }
    d vals.%Set("POPIS_PLATNOSTI",        popisPlatnosti)
    d vals.%Set("TYP_JR", $case(+$g(^[dataDB]JRL(sku,lin,"PATCH","OBJ")),1:"VÝLUKOVÝ",2:"návrh jízdního řádu",:""))
    d vals.%Set("QR_KOD",                 $g(^[dataDB]JRL(sku,lin,"PATCH","QR_CODE")))
    d vals.%Set("SMER",                   smer)

    // ── zastávkové řádky v pořadí směru (vč. zdvojení příjezd/odjezd u atributů P/J) ──
    // rowMeta(i) = $lb(poradi, typ) — typ "O" hlavní/odjezdový, "P" vložený příjezdový.
    s jeTam = (smer = "T")
    s zastavky = ##class(%DynamicArray).%New()
    k rowMeta
    s prvniNazev = "", posledniNazev = ""
    s pocetKmSloupcu = +$g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer))
    s maxKmVeta = ..MaxKmVeta(dataDB, sku, lin, smer, pocetKmSloupcu)
    s linIds = $g(^[dataDB]JRL(sku,lin,"PATCH","linIDS"))     // zóny se tisknou jen u IDS linek
    k atributyZobrazene                       // znaky atributů trasy viditelné na stránce (legenda druhu 7)
    k atributyZastavek                        // atributy z číselníku zastávek (^JRZ $8, CSV) — legenda druhu 7
    s maMhd = 0
    s idx = 0, poradi = ""
    f {
        i jeTam { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi)) }
        else    { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi), -1) }
        q:poradi=""
        i '$isvalidnum(poradi) continue
        s veta = $g(^[dataDB]JRL(sku,lin,"J",poradi))
        s czst = $p(veta,":",1)
        i czst = "" continue

        s atrZnaky = $p($p(veta,":",2),"#",1)
        s zdvojit = (atrZnaky [ "P") || (atrZnaky [ "J")

        // vložený příjezdový řádek (nad hlavním) — legacy insertrow = row-0.01
        i zdvojit {
            s idx = idx + 1
            s rowMeta(idx) = $lb(poradi, "P")
            d zastavky.%Push(..BuildZastavkaRadek(dataDB, sku, lin, smer, poradi, veta, "prijezd", pocetKmSloupcu, maxKmVeta, jeTam, linIds))
        }

        s idx = idx + 1
        s rowMeta(idx) = $lb(poradi, "O")
        s z = ..BuildZastavkaRadek(dataDB, sku, lin, smer, poradi, veta, "zastavka", pocetKmSloupcu, maxKmVeta, jeTam, linIds)
        d zastavky.%Push(z)

        f ai=1:1:$l(atrZnaky) s atributyZobrazene($e(atrZnaky,ai)) = ""
        // atributy z číselníku zastávek (^JRZ $8, CSV — např. "A,L" = přestup bus/vlak)
        s atrZst = $p($g(^[dataDB]JRZ(czst)),"$",8)
        f ai=1:1:$l(atrZst,",") {
            s znakZst = $p(atrZst,",",ai)
            i znakZst '= "" s atributyZastavek(znakZst) = ""
        }
        i $p(veta,"#",4) '= "" s maMhd = 1
        i prvniNazev = "" s prvniNazev = ..NazevZastavky(dataDB, czst)
        s posledniNazev = ..NazevZastavky(dataDB, czst)
    }
    i idx = 0 q ""

    d vals.%Set("VYCHOZI_ZASTAVKA", prvniNazev)
    d vals.%Set("POPIS_SMER",       posledniNazev)

    // ── spoje směru: legacy mkPoradiSp řazení + zařazení do skupin dnů ──
    d ..SeradSpoje(dataDB, sku, lin, smer, .serazeneSpoje, .pocetSpoju)

    s spoje = ##class(%DynamicArray).%New()
    k platnostZnakySeen, nzSeen, pripojePoznamky, markerSeen, atributSeen, mistenkaSeen, trasaZnakySeen
    f skupina = 1:1:$s(deleni=0:1, deleni=1:2, 1:3) {
        f si=1:1:pocetSpoju {
            s sp = serazeneSpoje(si)
            s plk = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",7)
            // spoj bez týdenní platnosti: legacy ověřuje skutečnou platnost kalendářem
            // (mkStaLinSp) — aproximace: odvodit dny ze zadaných datumů omezení (jede jen/také)
            i plk = "" s plk = ..PlatnostZDatumu(dataDB, sku, lin, smer, sp)
            s skupiny = ..ZarazeniSpojeKnizni(deleni, plk)
            i (","_skupiny_",") '[ (","_skupina_",") continue
            s spojObj = ..BuildSpoj(dataDB, sku, lin, smer, sp, skupina, idx, .rowMeta, rec, .platnostZnakySeen, .nzSeen, .pripojePoznamky, .markerSeen, .atributSeen, .mistenkaSeen, .trasaZnakySeen)
            i spojObj '= "" d spoje.%Push(spojObj)
        }
    }
    i spoje.%Size() = 0 q ""

    // ── poznámky (pevné pořadí druhů 1:1 MT3: platnosti, NZ, návaznosti — pokračuje/vyčká/
    //    navazuje, atributy, místenkování, stálé značky, poznámky linky) ──
    s poznamky = ##class(%DynamicArray).%New()
    d ..PridatPoznamkyPlatnosti(dataDB, rec, poznamky, .platnostZnakySeen)
    d ..PridatPoznamkyNZ(dataDB, sku, lin, smer, rec, tiskDtm, poznamky, .nzSeen)
    f poradiDruhu3 = 1:1:3 {
        s seq = ""
        f {
            s seq = $o(pripojePoznamky(poradiDruhu3, seq))
            q:seq=""
            d poznamky.%Push(pripojePoznamky(poradiDruhu3, seq))
        }
    }
    d ..PridatPoznamkyAtributu(poznamky, .atributSeen)
    d ..PridatPoznamkyMistenkovani(poznamky, .mistenkaSeen)
    d ..PridatStaleZnacky(dataDB, rec, poznamky, .markerSeen, .atributyZobrazene, .atributyZastavek, .trasaZnakySeen, maMhd)
    d ..PridatPoznamkyLinky(dataDB, sku, lin, poznamky)

    // ── sestavení stránky ───────────────────────────────────────────
    s out = ##class(%DynamicObject).%New()
    d out.%Set("values", vals)
    s rows = ##class(%DynamicObject).%New()
    d rows.%Set("ZASTAVKY", zastavky)
    d rows.%Set("SPOJE", spoje)
    d rows.%Set("SKUPINY_DNU", ..BuildSkupinyDnu(dataDB, rec, deleni))
    d rows.%Set("POZNAMKY", poznamky)
    d out.%Set("rows", rows)
    q out
}

/// JSON objekt jednoho zastávkového řádku (hlavní "zastavka" nebo vložený "prijezd").
ClassMethod BuildZastavkaRadek(dataDB As %String, sku As %String, lin As %String, smer As %String, poradi As %String, veta As %String, typ As %String, pocetKmSloupcu As %Integer, maxKmVeta As %String, jeTam As %Boolean, linIds As %String = "") As %DynamicObject
{
    s czst = $p(veta,":",1)
    s z = ##class(%DynamicObject).%New()
    d z.%Set("PORADI",     +poradi)
    d z.%Set("TYP",        typ)
    d z.%Set("CZST",       czst)
    d z.%Set("POPIS",      ..NazevZastavky(dataDB, czst))
    d z.%Set("TC",         +poradi)
    // zóna: tiskne se jen u IDS linek (PATCH linIDS); prázdná → "-" (1:1 legacy gInfoTc)
    s zona = $p($p(veta,":",5),"#",2)
    s zona = $s(linIds="":"", zona="":"-", 1:zona)
    d z.%Set("ZONA",       zona)
    // MHD/IDS text z kódu v "#"-poli 4 J-věty (legacy gTrsLin dta,6 → gTrsLinDta2ToText)
    d z.%Set("MHD",        ..MhdText($p(veta,"#",4)))
    d z.%Set("NASTUPISTE", $p($p(veta,"#",3),":",$s(jeTam:1,1:2)))

    s atributy = ##class(%DynamicArray).%New()
    s atrZnaky = $p($p(veta,":",2),"#",1)
    f ai=1:1:$l(atrZnaky) d atributy.%Push($e(atrZnaky,ai))
    d z.%Set("ATRIBUTY", atributy)

    // km sloupce směru; -1 → "<" (jede jinudy); Zpět: km = maxKm - km (legacy KmSloupce/MaxKm)
    s km = ##class(%DynamicArray).%New()
    f sl=1:1:pocetKmSloupcu {
        s kmVal = $p($g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer,sl)),":",poradi)
        i kmVal = -1 { d km.%Push("<") }
        elseif kmVal = "" { d km.%Push("") }
        elseif 'jeTam { d km.%Push($p(maxKmVeta,":",sl) - kmVal) }
        else { d km.%Push(+kmVal) }
    }
    d z.%Set("KM", km)
    q z
}

/// MHD kód → zobrazovaný text (1:1 legacy gTrsLinDta2ToText; bez IDP-P/Z overridů z nastavení).
ClassMethod MhdText(val As %String) As %String
{
    i val = "" q ""
    i ",1,2,3,4,5,6,7,8,9," [ (","_val_",") q "MHD"
    i val [ "MHD" q "MHD"
    i val [ "I" q "IDS"
    i val [ "P" q "IDP-P"
    i val [ "Z" q "IDP-Z"
    i val [ "B" q "BID"
    q ""
}

/// Má směr aspoň jeden spoj?
ClassMethod MaSpoje(dataDB As %String, sku As %String, lin As %String, smer As %String) As %Boolean
{
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i $isvalidnum(sp) q
    }
    q (sp '= "")
}

/// Max km per km-sloupec (":"-věta) pro přepočet směru Zpět (legacy MaxKm; -1 a prázdné ignoruje).
ClassMethod MaxKmVeta(dataDB As %String, sku As %String, lin As %String, smer As %String, pocetKmSloupcu As %Integer) As %String
{
    s veta = ""
    f sl=1:1:pocetKmSloupcu {
        s kmVeta = $g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer,sl))
        s mymax = 0
        f i=1:1:$l(kmVeta,":") {
            s v = $p(kmVeta,":",i)
            i (v '= "") && (v '= -1) && (v > mymax) s mymax = v
        }
        s $p(veta,":",sl) = mymax
    }
    q veta
}

/// Seřadí spoje směru 1:1 s legacy mkPoradiSp^JRapiSpUtl1: primárně dle času odjezdu z výchozí
/// zastávky spoje, pak bublinové doseřazení porovnáním časů v jednotlivých tarifních číslech
/// (v pořadí směru jízdy). Vrací serazeneSpoje(1..n)=sp.
ClassMethod SeradSpoje(dataDB As %String, sku As %String, lin As %String, smer As %String, ByRef serazeneSpoje, ByRef pocetSpoju)
{
    k serazeneSpoje
    s pocetSpoju = 0

    // primární pořadí: odjezd (hlava:4), při shodě číslo spoje
    k razeni
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s odjezd = +$p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",4)
        s razeni(odjezd, sp) = ""
    }
    s odjezd = ""
    f {
        s odjezd = $o(razeni(odjezd))
        q:odjezd=""
        s sp = ""
        f {
            s sp = $o(razeni(odjezd, sp))
            q:sp=""
            s pocetSpoju = pocetSpoju + 1
            s serazeneSpoje(pocetSpoju) = sp
        }
    }
    i pocetSpoju < 2 q

    // časová mapa spoje: cas(sp, poradi) = odjezd + W offset (jen numerické)
    f i=1:1:pocetSpoju {
        s sp = serazeneSpoje(i)
        s hlava = $p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1)
        s odj = +$p(hlava,":",4)
        d ..WMapa(dataDB, sku, lin, $p(hlava,":",3), .wMap)
        s pp = ""
        f {
            s pp = $o(wMap(pp))
            q:pp=""
            s c = ..CasHodnota($g(wMap(pp)))
            i c '= "" s cas(sp, pp) = odj + c
        }
    }

    // bublinové doseřazení 1:1 legacy mkPoradiSp: fáze 1 přes odjezdová tč spojů (TcOd = hlava:1),
    // fáze 2 přes příjezdová (TcDo = hlava:2), tč v pořadí směru jízdy; v každém tč se sousedé
    // s definovaným časem prohodí, je-li dřívější spoj pozdější.
    s jeTam = (smer = "T")
    k tcOdSet, tcDoSet
    f i=1:1:pocetSpoju {
        s hlava = $p($g(^[dataDB]JRL(sku,lin,smer,serazeneSpoje(i))),"#",1)
        i $p(hlava,":",1) '= "" s tcOdSet(+$p(hlava,":",1)) = ""
        i $p(hlava,":",2) '= "" s tcDoSet(+$p(hlava,":",2)) = ""
    }
    f faze=1,2 {
        s tc = ""
        f {
            i faze=1 { s tc = $s(jeTam:$o(tcOdSet(tc)), 1:$o(tcOdSet(tc),-1)) }
            else     { s tc = $s(jeTam:$o(tcDoSet(tc)), 1:$o(tcDoSet(tc),-1)) }
            q:tc=""
            f pass=1:1:pocetSpoju {
                s zmena = 0
                f i=1:1:pocetSpoju-1 {
                    s spA = serazeneSpoje(i), spB = serazeneSpoje(i+1)
                    i '$d(cas(spA, tc)) continue
                    i '$d(cas(spB, tc)) continue
                    i cas(spA, tc) > cas(spB, tc) {
                        s serazeneSpoje(i) = spB, serazeneSpoje(i+1) = spA
                        s zmena = 1
                    }
                }
                q:'zmena
            }
        }
    }
}

/// Sestaví JSON objekt jednoho spoje (sloupec matice): řádek platnosti (plný klíčový systém
/// legacy Platnost/Spoj2Tb), NZ, časy per řádek, návaznosti.
ClassMethod BuildSpoj(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String, skupina As %Integer, pocetRadku As %Integer, ByRef rowMeta, rec As %String, ByRef platnostZnakySeen, ByRef nzSeen, ByRef pripojePoznamky, ByRef markerSeen, ByRef atributSeen, ByRef mistenkaSeen, ByRef trasaZnakySeen) As %DynamicObject
{
    s spoj = $g(^[dataDB]JRL(sku,lin,smer,sp))
    s hlava = $p(spoj,"#",1)
    s cisloTrasy = $p(hlava,":",3)

    // per-tč značky spoje z trasy (W segment ":"-piece 5, legacy gSpLin trs,6): N/V/x/G/a/b/c
    // → legenda druhu 7 (r/i přesunuty do atributů, jako legacy)
    s wTrasaVeta = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
    f wi=1:1:$l(wTrasaVeta,"#") {
        s wZnaky = $p($p(wTrasaVeta,"#",wi),":",5)
        f zi=1:1:$l(wZnaky) {
            s wZnak = $e(wZnaky,zi)
            i (wZnak '= "") && (wZnak '= "r") && (wZnak '= "i") s trasaZnakySeen(wZnak) = ""
        }
    }
    s odjezd = +$p(hlava,":",4)
    s plk = $p(hlava,":",7)
    s cnz = $p(spoj,"#",7)

    s o = ##class(%DynamicObject).%New()
    d o.%Set("CISLO_SPOJE", +sp)
    d o.%Set("SKUPINA", skupina)
    d o.%Set("NZ", cnz)

    // ── klíče řádku platnosti (1:1 legacy Platnost → Spoj2Tb pevné pořadí) ──
    k klice
    f i=1:1:$l(plk) {
        s znak = $e(plk,i)
        i "1234567X+" [ znak s klice(znak) = "", platnostZnakySeen(znak) = ""
    }
    // místenkování: hlava:6 ("R") + hlava:11 (typ) → R / þ(R1) / &(R2) / Ü(R3)
    s mistenka = $tr($p(hlava,":",6)_$p(hlava,":",11), " ", "")
    s mistZnak = $case(mistenka, "R":"R", "R1":$c(254), "R2":"&", "R3":$c(220), :"")
    i mistZnak '= "" s klice(mistZnak) = "", mistenkaSeen(mistZnak) = ""
    // pokračuje (omz34, typ výstupu ≠ 2) → klíč u (nebo p dle ^JRSET("Cis","comU34znk"))
    s omz34 = $g(^[dataDB]JRL(sku,lin,smer,sp,34))
    i (omz34 '= "") && ($p($p(omz34,"@",2),":",5) '= 2) {
        s klice($s(+$g(^[dataDB]JRSET("Cis","comU34znk")):"p", 1:"u")) = ""
    }
    // poznámka pro spoj/tč (omz41/42) → klíč p
    i ($d(^[dataDB]JRL(sku,lin,smer,sp,41))) || ($d(^[dataDB]JRL(sku,lin,smer,sp,42))) s klice("p") = ""
    // atributy spoje (gSpLin dta ↔ hlava pieces: POS=12, IDS=14, BEV=15, SAO=16, PCZ=17, OBC=18,
    // PJI=19, NKA=20, RBU=21, CBE=22, RBP=23, wifi=25, zasu=26)
    f ai=1:1:13 {
        s klic = $p("POS,IDS,BEV,SAO,PCZ,OBC,PJI,NKA,RBU,CBE,RBP,wifi,zasu", ",", ai)
        s piece = $p("12,14,15,16,17,18,19,20,21,22,23,25,26", ",", ai)
        i +$p(hlava,":",piece) {
            s klice(klic) = ""
            s atributSeen(klic, +sp) = ""
        }
    }
    // PLATNOST = klíče v pevném legacy pořadí (Spoj2Tb ř.3754)
    s platnost = ##class(%DynamicArray).%New()
    f ki=1:1:26 {
        s klic = $p("X,1,2,3,4,5,6,7,+,R,þ,&,Ü,u,p,BEV,PJI,SAO,NKA,RBU,CBE,RBP,PCZ,OBC,wifi,zasu", ",", ki)
        i $d(klice(klic)) d platnost.%Push(klic)
    }
    d o.%Set("PLATNOST", platnost)
    i cnz '= "" s nzSeen(cnz) = ""

    // hranice spoje pro zdvojené řádky: první/poslední obsluhované poradi (v pořadí směru)
    d ..WMapa(dataDB, sku, lin, cisloTrasy, .wMap)
    s jeTam = (smer = "T")
    s prvniPoradi = "", posledniPoradi = ""
    f i=1:1:pocetRadku {
        i $lg(rowMeta(i),2) '= "O" continue
        s pp = $lg(rowMeta(i),1)
        i ..CasHodnota($g(wMap(pp))) '= "" {
            i prvniPoradi = "" s prvniPoradi = pp
            s posledniPoradi = pp
        }
    }

    // časy per řádek: "" mimo trasu, "|" projíždí, "<" jede jinudy, jinak HH:MM.
    // Hlavní řádek = odjezd (příjezd+čekání); vložený "P" řádek = příjezd (bez čekání).
    // Kraje spoje u zdvojených zastávek: na první obsluhované chybí příjezd, na poslední odjezd
    // (1:1 legacy Spoj2Tb ořez casobefore/casonext).
    s casy = ##class(%DynamicArray).%New()
    f i=1:1:pocetRadku {
        s pp = $lg(rowMeta(i),1), typRadku = $lg(rowMeta(i),2)
        s cas = $g(wMap(pp))
        i cas = "<" { d casy.%Push("<")  s markerSeen("<") = "" }
        elseif cas = "|" { d casy.%Push("|")  s markerSeen("|") = "" }
        elseif cas = "" { d casy.%Push("") }
        elseif typRadku = "P" {
            i pp = prvniPoradi { d casy.%Push("") }
            else { d casy.%Push(..FormatCas(odjezd + +$p(cas,"/",1))) }
        }
        else {
            i (pp = posledniPoradi) && ($d(rowMeta(i-1)) && ($lg($g(rowMeta(i-1)),2) = "P") && ($lg($g(rowMeta(i-1)),1) = pp)) {
                d casy.%Push("")          // zdvojená konečná: odjezd se maže, zůstává příjezd
            }
            else { d casy.%Push(..FormatCas(odjezd + ..CasHodnota(cas))) }
        }
    }
    d o.%Set("CASY", casy)

    // návaznosti: omzId 31 = vyčká, 32 = navazuje. Věta = znak@sekce@sekce…, sekce = tc#…:
    //   omz31: tc#maxCekaniMin#conn…, conn = AVL:linka:spoj:typVystupu:smerOdkud:minPrijezdu:
    //          typZobrazeni:dopravce:min:smerKam:linkaProTisk
    //   omz32: tc#conn…, conn = linka:spoj:smerKam:typVystupu:prefCsad:AVL:tablo:minOdjezdu:
    //          typZobrazeni:scitatCenu:dopravce:linkaProTisk
    // typVystupu=2 (jen exp. strojek) → vůbec netisknout; typZobrazeni: 0=značkou u spoje,
    // 1=v přípojném řádku, 2=obojí (1:1 g3132/cnvPrtn31/32).
    s pripoje = ##class(%DynamicArray).%New()
    f omzId = 31, 32 {
        s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
        i omz = "" continue
        s typ = $s(omzId=31:"vycka", 1:"navazuje")
        s znak = $p(omz,"@",1)
        f sekceIdx=2:1:$l(omz,"@") {
            s sekce = $p(omz,"@",sekceIdx)
            i sekce = "" continue
            s tc = $p(sekce,"#",1)
            s prvniConn = $s(omzId=31:3, 1:2)
            s fraze = ""                       // agregát vět „spoje X linky Y…" pro poznámku sekce
            f connIdx=prvniConn:1:$l(sekce,"#") {
                s conn = $p(sekce,"#",connIdx)
                i conn = "" continue
                i omzId = 31 {
                    s linka2 = $p(conn,":",2), spoj2 = $p(conn,":",3)
                    s kam = $p(conn,":",5), casMin = +$p(conn,":",6)
                    s typVystupu = +$p(conn,":",4), typZobrazeni = +$p(conn,":",7)
                    s avl = $p(conn,":",1)
                }
                else {
                    s linka2 = $p(conn,":",1), spoj2 = $p(conn,":",2)
                    s kam = $p(conn,":",3), casMin = +$p(conn,":",8)
                    s typVystupu = +$p(conn,":",4), typZobrazeni = +$p(conn,":",9)
                    s avl = $p(conn,":",6)
                }
                i typVystupu = 2 continue                 // jen exportní strojek — na JŘ vůbec

                s pr = ##class(%DynamicObject).%New()
                d pr.%Set("TYP", typ)
                d pr.%Set("PORADI", +tc)
                d pr.%Set("ZNAK", znak)
                d pr.%Set("TEXT", kam)
                d pr.%Set("CAS", ..FormatCas(casMin))
                d pr.%Set("LINKA", linka2)
                d pr.%Set("SPOJ", spoj2)
                d pr.%Set("RADEK", $s((typZobrazeni=1)||(typZobrazeni=2):1, 1:0))
                d pripoje.%Push(pr)

                // fráze do agregované poznámky legendy (typZobrazeni 0 nebo 2)
                i (typZobrazeni=0) || (typZobrazeni=2) {
                    // AVL abeceda: A=autobus, V=vlak, L=loď (legacy $tr("AVL","ALD") před GSId)
                    i omzId = 31 {
                        // „spoje 25 linky 330510" / vlak: „vlaku 20880" (bez čísla spoje); [ z SMĚR]
                        s slovoAvl = $case(avl, "V":"vlaku", "L":"lodě", :"linky")
                        s f31 = $s(+spoj2:"spoje "_(+spoj2)_" ", 1:"")_slovoAvl_" "_(+linka2)
                        i kam '= "" s f31 = f31_" z "_kam
                        s fraze = fraze_$s(fraze="":"", 1:",")_f31
                    }
                    else {
                        // „spoj 24 linky 330530 do X" / vlak: „vlak 20875 do X" (NCOME 32 piece 6)
                        i avl = "V" { s f32 = "vlak "_(+linka2) }
                        elseif avl = "L" { s f32 = "lodní doprava "_(+linka2) }
                        else { s f32 = "spoj "_(+spoj2)_" linky "_(+linka2) }
                        i kam '= "" s f32 = f32_" do "_kam
                        s fraze = fraze_$s(fraze="":"", 1:", ")_f32
                    }
                }
            }
            // jedna poznámka druhu 3 per (spoj × tc) — 1:1 legacy agregace vět; dedup přes text
            // (spoj ve víc skupinách dnů se zpracovává vícekrát)
            i fraze '= "" {
                s nazevTc = ..NazevZastavkyPoradi(dataDB, sku, lin, tc)
                i omzId = 31 {
                    s txt = "spoj "_(+sp)_" vyčká v zastávce "_nazevTc_" příjezdu "_fraze
                    i +$p(sekce,"#",2) s txt = txt_" nejvýše "_(+$p(sekce,"#",2))_" minut"
                    s bucket = 2
                }
                else {
                    s txt = "na spoj "_(+sp)_" navazuje v zastávce "_nazevTc_" "_fraze
                    s bucket = 3
                }
                i '$d(pripojePoznamky("seen", txt)) {
                    s pripojePoznamky("seen", txt) = ""
                    s seq = $o(pripojePoznamky(bucket, ""), -1) + 1
                    s pripojePoznamky(bucket, seq) = ..BuildPoznamka(3, "znak", znak, txt)
                }
            }
        }
    }
    d o.%Set("PRIPOJE", pripoje)

    // pokračuje (omz34): „spoj N pokračuje na lince číslo L spojem číslo S do zastávky Z"
    // (1:1 NCOME 34 šablona; typ výstupu :5 = 2 → netisknout). Jen poznámka, žádný řádek matice.
    s omz34 = $g(^[dataDB]JRL(sku,lin,smer,sp,34))
    i (omz34 '= "") && ($p($p(omz34,"@",2),":",5) '= 2) {
        s data34 = $p(omz34,"@",2)
        s znak34 = $s(+$g(^[dataDB]JRSET("Cis","comU34znk")):"p", 1:"u")
        s txt = "spoj "_(+sp)_" pokračuje na lince číslo "_(+$p(data34,":",2))_" spojem číslo "_(+$p(data34,":",3))_" do zastávky "_$p(data34,":",4)
        i '$d(pripojePoznamky("seen", txt)) {
            s pripojePoznamky("seen", txt) = ""
            s seq = $o(pripojePoznamky(1, ""), -1) + 1
            s pripojePoznamky(1, seq) = ..BuildPoznamka(3, "znak", znak34, txt)
        }
    }

    q o
}

/// Odvození platnostních znaků z datumů omezení „jede jen/také/jede" (omz 11/12/13) pro spoj
/// bez týdenní platnosti — aproximace legacy kalendáře (mkStaLinSp): den → 1-7, státní svátek
/// (vč. Velikonoc) → "+". Prázdný výsledek = fallback „jede všude".
ClassMethod PlatnostZDatumu(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String) As %String
{
    k klice
    f omzId = 11, 12, 13 {
        s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
        i omz = "" continue
        s data = $s(omz["@":$p(omz,"@",2), 1:omz)
        f i=1:1:$l(data,"#") {
            s item = $p(data,"#",i)
            i item = "" continue
            s od = $p(item,":",1), do = $p(item,":",2)
            i do = "" s do = od
            s dhOd = ..DatumNaDh(od), dhDo = ..DatumNaDh(do)
            i (dhOd = "") || (dhDo = "") continue
            i dhDo - dhOd > 400 s dhDo = dhOd + 400        // pojistka proti nesmyslným rozsahům
            f dh = dhOd:1:dhDo s klice(..DhNaKlic(dh)) = ""
        }
    }
    s vysledek = "", k = ""
    f {
        s k = $o(klice(k))
        q:k=""
        s vysledek = vysledek_k
    }
    q vysledek
}

/// Datum "d.m.rrrr" → $H počet dnů. "" při nevalidním vstupu.
ClassMethod DatumNaDh(dat As %String) As %String
{
    s dd = +$p(dat,".",1), mm = +$p(dat,".",2), rr = +$p(dat,".",3)
    i (dd < 1) || (mm < 1) || (rr < 1900) q ""
    s iso = rr_"-"_$tr($j(mm,2)," ",0)_"-"_$tr($j(dd,2)," ",0)
    q $zdateh(iso, 3, , , , , , , "")
}

/// $H den → platnostní klíče: den v týdnu 1(Po)-7(Ne); státní svátek přidá "+" (den nese oba —
/// 24.12. v sobotu patří do skupiny So i Ne+svátky, jako legacy kalendář).
ClassMethod DhNaKlic(dh As %Integer) As %String
{
    s datum = $zdate(dh, 3)                       // yyyy-mm-dd
    s rr = +$p(datum,"-",1), mm = +$p(datum,"-",2), dd = +$p(datum,"-",3)
    s dow = $zdate(dh, 10)                        // 0=Ne .. 6=So
    s klice = $s(dow=0:7, 1:dow)
    i ..JeSvatek(dd, mm, rr, dh) s klice = klice_"+"
    q klice
}

/// Český státní svátek (pevné + Velký pátek a Velikonoční pondělí dle computu).
ClassMethod JeSvatek(dd As %Integer, mm As %Integer, rr As %Integer, dh As %Integer) As %Boolean
{
    i ",1.1,1.5,8.5,5.7,6.7,28.9,28.10,17.11,24.12,25.12,26.12," [ (","_dd_"."_mm_",") q 1
    // Velikonoční neděle (Meeus/Jones/Butcher pro gregoriánský kalendář)
    s a = rr#19, b = rr\100, c = rr#100
    s d = b\4, e = b#4, f = (b+8)\25, g = (b-f+1)\3
    s h = ((19*a)+b-d-g+15)#30
    s i = c\4, k = c#4
    s l = (32+(2*e)+(2*i)-h-k)#7
    s m = (a+(11*h)+(22*l))\451
    s mesic = (h+l-(7*m)+114)\31
    s den = ((h+l-(7*m)+114)#31)+1
    s dhNedele = $zdateh(rr_"-"_$tr($j(mesic,2)," ",0)_"-"_$tr($j(den,2)," ",0), 3, , , , , , , "")
    i dhNedele = "" q 0
    q (dh = (dhNedele - 2)) || (dh = (dhNedele + 1))    // Velký pátek, Velikonoční pondělí
}

/// Zařazení spoje do skupin dnů dle módu dělení a Plk (legacy in("A",2,"arr",3)).
/// Spoj bez rozpoznané platnosti → všechny skupiny (legacy fallback HD 10183).
ClassMethod ZarazeniSpojeKnizni(deleni As %Integer, plk As %String) As %String
{
    i deleni = 0 q "1"

    s maPD = 0, maSo = 0, maNe = 0
    f i=1:1:$l(plk) {
        s znak = $e(plk,i)
        i "X12345" [ znak s maPD = 1
        i znak = 6 s maSo = 1
        i "7+" [ znak s maNe = 1
    }
    i ('maPD) && ('maSo) && ('maNe) q $s(deleni=1:"1,2", 1:"1,2,3")

    s skupiny = ""
    i deleni = 1 {
        i maPD s skupiny = "1"
        i maSo || maNe s skupiny = skupiny_$s(skupiny'="":",",1:"")_"2"
    }
    else {
        i maPD s skupiny = "1"
        i maSo s skupiny = skupiny_$s(skupiny'="":",",1:"")_"2"
        i maNe s skupiny = skupiny_$s(skupiny'="":",",1:"")_"3"
    }
    q skupiny
}

/// Definice skupin dnů — nadpisy z číselníku WinMHD DSC (^JRSET("Definice","WinMHD",rec,"DSC"),
/// piece 2 = pracovní dny, 3 = soboty, 4 = neděle a svátky, 7 = soboty+neděle+svátky).
ClassMethod BuildSkupinyDnu(dataDB As %String, rec As %String, deleni As %Integer) As %DynamicArray
{
    s dsc = $g(^[dataDB]JRSET("Definice","WinMHD",rec,"DSC"))
    s tPD = $p(dsc,"~",2)  i tPD = "" s tPD = "pracovní dny"
    s tSo = $p(dsc,"~",3)  i tSo = "" s tSo = "soboty"
    s tNe = $p(dsc,"~",4)  i tNe = "" s tNe = "neděle a svátky"
    s tVik = $p(dsc,"~",7) i tVik = "" s tVik = "soboty, neděle a svátky"

    s arr = ##class(%DynamicArray).%New()
    i deleni = 0 {
        d arr.%Push(..BuildSkupina(1, ""))
    }
    elseif deleni = 1 {
        d arr.%Push(..BuildSkupina(1, tPD))
        d arr.%Push(..BuildSkupina(2, tVik))
    }
    else {
        d arr.%Push(..BuildSkupina(1, tPD))
        d arr.%Push(..BuildSkupina(2, tSo))
        d arr.%Push(..BuildSkupina(3, tNe))
    }
    q arr
}

ClassMethod BuildSkupina(id As %Integer, nadpis As %String) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("ID", id)
    d o.%Set("NADPIS", nadpis)
    q o
}

/// Poznámky druhu 1 — týdenní platnosti (jen znaky použité na stránce, pořadí X,1-7,+).
/// Texty z ^JRSET("NCOME",rec,znak), fallback hardcoded.
ClassMethod PridatPoznamkyPlatnosti(dataDB As %String, rec As %String, poznamky As %DynamicArray, ByRef platnostZnakySeen)
{
    f i=1:1:10 {
        s znak = $p("X,1,2,3,4,5,6,7,+", ",", i)
        i znak = "" continue
        i '$d(platnostZnakySeen(znak)) continue
        s txt = $g(^[dataDB]JRSET("NCOME",rec,znak))
        i txt = "" s txt = ..TextPlatnosti(znak)
        d poznamky.%Push(..BuildPoznamka(1, "znak", znak, txt))
    }
}

/// Fallback text týdenní platnosti (když číselník NCOME chybí).
ClassMethod TextPlatnosti(znak As %String) As %String
{
    q $case(znak,
        "X":"jede v pracovních dnech",
        1:"jede v pondělí", 2:"jede v úterý", 3:"jede ve středu", 4:"jede ve čtvrtek",
        5:"jede v pátek", 6:"jede v sobotu", 7:"jede v neděli",
        "+":"jede v neděli a ve státem uznané svátky", :"")
}

/// Poznámky druhu 2 — negativní značky: text z datumových omezení spojů nesoucích danou NZ.
ClassMethod PridatPoznamkyNZ(dataDB As %String, sku As %String, lin As %String, smer As %String, rec As %String, tiskDtm As %Integer, poznamky As %DynamicArray, ByRef nzSeen)
{
    s znak = ""
    f {
        s znak = $o(nzSeen(znak))
        q:znak=""
        s txt = "", sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
            q:sp=""
            i '$isvalidnum(sp) continue
            i $p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",7) '= znak continue
            s txt = ..SestavTextOmezeni(dataDB, sku, lin, smer, sp, rec, tiskDtm)
            q:txt'=""
        }
        i txt '= "" d poznamky.%Push(..BuildPoznamka(2, "nz", znak, txt))
    }
}

/// Poznámky druhu 5 — atributy spojů (BEV, PJI, SAO…). Legacy v režimu „značka nad spojem"
/// (pozorovaný gold) tiskne jednu generickou větu bez výčtu spojů — značky u konkrétních spojů
/// nese řádek platnosti. Texty dle legacy gAtrNZLi (hardcoded v legacy kódu, ne číselník).
/// TODO: režim s výčtem spojů + gating nastavením PAtrSp/TZPA.
ClassMethod PridatPoznamkyAtributu(poznamky As %DynamicArray, ByRef atributSeen)
{
    f ki=1:1:13 {
        s klic = $p("BEV,IDS,NKA,OBC,PCZ,PJI,POS,RBU,SAO,CBE,RBP,wifi,zasu", ",", ki)
        i '$d(atributSeen(klic)) continue
        d poznamky.%Push(..BuildPoznamka(5, "znak", klic, "spoj "_..PopisAtributu(klic, 1)))
    }
}

/// Popis atributu spoje (CZ, dle legacy gAtrNZLi) — tvar dle počtu spojů (j.č. / mn.č.).
ClassMethod PopisAtributu(klic As %String, pocet As %Integer) As %String
{
    s jc = (pocet = 1)
    q $case(klic,
        "BEV":$s(jc:"jede s bezbariérově přístupným vozidlem", 1:"jedou s bezbariérově přístupným vozidlem"),
        "IDS":$s(jc:"jede v systému integrované dopravy", 1:"jedou v systému integrované dopravy"),
        "NKA":$s(jc:"jede nízkokapacitním autobusem", 1:"jedou nízkokapacitním autobusem"),
        "OBC":$s(jc:"jede s možností občerstvení", 1:"jedou s možností občerstvení"),
        "PCZ":$s(jc:"přepravuje cestovní zavazadla", 1:"přepravují cestovní zavazadla"),
        "PJI":$s(jc:"přepravuje jízdní kola", 1:"přepravují jízdní kola"),
        "POS":$s(jc:"přepravuje zásilky, bez doprovodu cestujícího", 1:"přepravují zásilky, bez doprovodu cestujícího"),
        "RBU":$s(jc:"jede jen na objednání", 1:"jedou jen na objednání"),
        "SAO":$s(jc:"má samoobslužný způsob odbavování cestujících", 1:"mají samoobslužný způsob odbavování cestujících"),
        "CBE":$s(jc:"jede s částečne bezbariérově přístupným vozidlem, nutná dopomoc průvodce", 1:"jedou s částečne bezbariérově přístupným vozidlem, nutná dopomoc průvodce"),
        "RBP":$s(jc:"jede jen podmínečně", 1:"jedou jen podmínečně"),
        "wifi":$s(jc:"jede s bezdrátovým připojením k internetu", 1:"jedou s bezdrátovým připojením k internetu"),
        "zasu":$s(jc:"jede s přípojkou 230 V", 1:"jedou s přípojkou 230 V"), :"")
}

/// Poznámky druhu 6 — místenkování (texty hardcoded CZ, 1:1 gMstNZLi).
ClassMethod PridatPoznamkyMistenkovani(poznamky As %DynamicArray, ByRef mistenkaSeen)
{
    i $d(mistenkaSeen("R"))     d poznamky.%Push(..BuildPoznamka(6, "znak", "R", "k jízdence je možné zakoupit místenku"))
    i $d(mistenkaSeen($c(254))) d poznamky.%Push(..BuildPoznamka(6, "znak", $c(254), "spoj je možné použít jen s místenkou"))
    i $d(mistenkaSeen("&"))     d poznamky.%Push(..BuildPoznamka(6, "znak", "&", "místenku je možné zakoupit v místenkových kancelářích AMS"))
    i $d(mistenkaSeen($c(220))) d poznamky.%Push(..BuildPoznamka(6, "znak", $c(220), "místenku je nutné zakoupit v místenkových kancelářích AMS"))
}

/// Poznámky druhu 7 — stálé značky: markery matice (| projíždí, < jede jinudy), MHD sloupec,
/// atributy zastávek/trasy viditelné na stránce. Texty z ^JRSET("STALCOME",rec,klíč);
/// klíče dle legacy gStzNZLi konverze (A→auto, L→vlak, x→x…).
ClassMethod PridatStaleZnacky(dataDB As %String, rec As %String, poznamky As %DynamicArray, ByRef markerSeen, ByRef atributyZobrazene, ByRef atributyZastavek, ByRef trasaZnakySeen, maMhd As %Boolean)
{
    k klice
    // markery z časových buněk
    i $d(markerSeen("|")) s klice("|") = ""
    i $d(markerSeen("<")) s klice("<") = ""
    // per-tč značky spojů z tras (W piece 5, 1:1 gStzNZLi spojová sekce)
    s znak = ""
    f {
        s znak = $o(trasaZnakySeen(znak))
        q:znak=""
        s klic = $case(znak, "V":"(", "N":")", "x":"x", "G":"G", "a":"GA", "b":"GB", "c":"GC", :"")
        i klic '= "" s klice(klic) = ""
    }
    // MHD sloupec → text „zastávka s možností přestupu na MHD"
    i maMhd s klice("MHD") = ""
    // atributy trasy (jen významové pro cestujícího; řídicí P/T/S/E/J/o/p se netisknou)
    s znak = ""
    f {
        s znak = $o(atributyZobrazene(znak))
        q:znak=""
        s klic = $case(znak, "A":"auto", "L":"vlak", "x":"x", "y":"y", "z":"z", "V":"(", "N":")", "D":"lod", "C":"CLO", :"")
        i klic '= "" s klice(klic) = ""
    }
    // atributy z číselníku zastávek (^JRZ $8, 1:1 gStzNZLi zastávková sekce)
    s znak = ""
    f {
        s znak = $o(atributyZastavek(znak))
        q:znak=""
        s klic = $case(znak, "A":"auto", "L":"vlak", "B":"zbez", "BW":"BWC", "W":"WC", "R":"RZ",
            "ZP":"ZP", "TP":"TP", "PR":"PR", "O":"O", "D":"lod", "e":"e", "f":"f", "g":"g", "x":"x", :"")
        i klic '= "" s klice(klic) = ""
    }
    s klic = ""
    f {
        s klic = $o(klice(klic))
        q:klic=""
        d ..PridatStalouZnacku(dataDB, rec, poznamky, klic)
    }
}

/// Přidá jednu stálou značku s textem ze STALCOME. Klíč textu = legacy klíč (auto/vlak/x/…),
/// zobrazovaný ZNAK = konvertovaný znak (auto→A, vlak→L…, jako legacy Komentar $case).
ClassMethod PridatStalouZnacku(dataDB As %String, rec As %String, poznamky As %DynamicArray, klic As %String)
{
    s txt = $g(^[dataDB]JRSET("STALCOME",rec,klic))
    i txt = "" q
    s znak = $case(klic, "auto":"A", "vlak":"L", "CLO":"C", "(":"V", ")":"N", "lod":"D", "GA":"a", "GB":"b", "GC":"c", :klic)
    d poznamky.%Push(..BuildPoznamka(7, $s(klic="MHD":"text",1:"znak"), znak, txt))
}

/// Poznámky linky (^JRL(sku,lin,"C",key,idx), klíč 2 se přeskakuje): 1 → druh 8 (tarifní),
/// 3 → druh 9 (všeobecné), 4 → druh 10 (radiobus), jiné → druh 9. Věta se čistí od "$$" (TrNz)
/// a nechává "#" jako oddělovač odstavců (1:1 legacy tvar).
ClassMethod PridatPoznamkyLinky(dataDB As %String, sku As %String, lin As %String, poznamky As %DynamicArray)
{
    s key = ""
    f {
        s key = $o(^[dataDB]JRL(sku,lin,"C",key))
        q:key=""
        i key = 2 continue
        s druh = $case(key, 1:8, 3:9, 4:10, :9)
        s txt = "", idx = ""
        f {
            s idx = $o(^[dataDB]JRL(sku,lin,"C",key,idx))
            q:idx=""
            s txt = txt_$g(^[dataDB]JRL(sku,lin,"C",key,idx))
        }
        s txt = $replace(txt, "$$", "")      // legacy TrNz odstraňuje "$$"
        // prázdné odstavce ("##", koncové "#") legacy tiskne jako mezeru mezi mřížkami
        s upraveny = ""
        f pi=1:1:$l(txt,"#") {
            s cast = $p(txt,"#",pi)
            i (cast = "") && (pi > 1) s cast = " "
            i pi > 1 s upraveny = upraveny_"#"
            s upraveny = upraveny_cast
        }
        s txt = upraveny
        i txt '= "" d poznamky.%Push(..BuildPoznamka(druh, "text", "", txt))
    }
}

/// JSON objekt jedné poznámky legendy.
ClassMethod BuildPoznamka(druh As %Integer, typ As %String, znak As %String, text As %String) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("DRUH", druh)
    d o.%Set("TYP", typ)
    d o.%Set("ZNAK", znak)
    d o.%Set("TEXT", text)
    q o
}

/// Formát času HH:MM z minut od půlnoci.
ClassMethod FormatCas(minuty As %Integer) As %String
{
    s hod = (minuty\60)#24, min = minuty#60
    q $tr($j(hod,2)," ",0)_":"_$tr($j(min,2)," ",0)
}

/// Název zastávky podle pořadí (tarifního čísla) v trase linky — CIS tvar s obcí
/// („České Budějovice,aut.nádr."), jak ho v návaznostech tiskne legacy (gZst/gTskNaZst).
ClassMethod NazevZastavkyPoradi(dataDB As %String, sku As %String, lin As %String, poradi As %String) As %String
{
    s czst = $p($g(^[dataDB]JRL(sku,lin,"J",poradi)),":",1)
    i czst = "" q ""
    s nazev = $p($g(^[dataDB]JRZ(czst)), "$", 2)
    i nazev = "" q ..NazevZastavky(dataDB, czst)
    q $replace(nazev, ",,", ",")
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Helpery sdílené se ZastavkoveJrAd vzorem (duplikované; extrakce až podle výsledků)
/// ═══════════════════════════════════════════════════════════════════════════════
/// W-mapa `poradi → cas`, cachovaná per `cisloTrasy`. `cas` = číslo | "<" jede jinudy |
/// "|" projíždí | "" mimo trasu (chybějící segment).
ClassMethod WMapa(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String, ByRef wMap)
{
    k wMap
    i cisloTrasy = "" q
    i $d(^||TiskKnizni("wmap",cisloTrasy)) { m wMap = ^||TiskKnizni("wmap",cisloTrasy)  q }
    s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
    f i=1:1:$l(wTrasa,"#") {
        s seg = $p(wTrasa,"#",i)
        s p = $p(seg,":",1)
        i p '= "" s wMap(p) = $p(seg,":",2)
    }
    i $d(wMap) m ^||TiskKnizni("wmap",cisloTrasy) = wMap
}

/// Numerická hodnota času z W-segmentu; pobytová zastávka `příjezd/čekání` → odjezd.
/// "" pro nečasové markery ("<", "|", prázdno).
ClassMethod CasHodnota(cas As %String) As %String
{
    i (cas = "") || (cas = "<") || (cas = "|") q ""
    q +$p(cas,"/",1) + +$p(cas,"/",2)
}

/// Text poznámky z datumových omezení spoje. Nejprve dlouhodobá platnost sudé/liché týdny
/// (hlava:8 = 4/5 → NCOME "SU"/"LI"), pak omzId v pořadí 13,12,11,14,15,22 (1:1 gComNZLiSp);
/// prefixy z ^JRSET("NCOME"); víc omezení spojeno ", ".
ClassMethod SestavTextOmezeni(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String, rec As %String, tiskDtm As %Integer) As %String
{
    s txt = ""
    s dlouhodoba = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",8)
    i (dlouhodoba = 4) || (dlouhodoba = 5) {
        s txt = $g(^[dataDB]JRSET("NCOME",rec,$s(dlouhodoba=4:"SU",1:"LI")))
        i txt = "" s txt = $s(dlouhodoba=4:"jede jen v sudých týdnech", 1:"jede jen v lichých týdnech")
    }
    f omzIdx=1:1:6 {
        s omzId = $p("13,12,11,14,15,22", ",", omzIdx)
        s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
        i omz = "" continue
        s nidom = $case(omzId, 14:"SU", 15:"LI", :omzId)
        s prefix = $g(^[dataDB]JRSET("NCOME",rec,nidom))
        i prefix = "" s prefix = $case(omzId, 11:"jede jen", 12:"jede také", 13:"jede", 14:"jede jen v sudých týdnech", 15:"jede jen v lichých týdnech", 22:"nejede", :"")
        s datumy = ..FormatDatumyOmezeni(omz, tiskDtm)
        i (datumy = "") && (omzId '= 14) && (omzId '= 15) continue
        i txt '= "" s txt = txt_", "
        s txt = txt_prefix_$s(datumy'="":" "_datumy, 1:"")
    }
    q txt
}

/// Datumová část omezení: položky (`#`) spojené ","; rozsah `od:do` → " od D1 do D2" (úvodní
/// mezera kromě první položky, 1:1 gold). Datumy dle režimu TiskDtm (viz ..TiskDtm).
ClassMethod FormatDatumyOmezeni(omz As %String, tiskDtm As %Integer) As %String
{
    s data = $s(omz["@":$p(omz,"@",2), 1:omz)
    s casti = ""
    f i=1:1:$l(data,"#") {
        s item = $p(data,"#",i)
        i item = "" continue
        s od = ..UpravDatum($p(item,":",1), tiskDtm), do = ..UpravDatum($p(item,":",2), tiskDtm)
        i do '= "" {
            i tiskDtm = 2 s od = ..UpravDatum($p(item,":",1), 3)      // režim 2: „od" část bez roku
            s cast = $s(casti="":"", 1:" ")_"od "_od_" do "_do
        }
        else { s cast = od }
        i casti '= "" s casti = casti_","
        s casti = casti_cast
    }
    q casti
}

/// Úprava datumu dle režimu TiskDtm: 0 = jak zadáno; 1 = strip nul + rok 2místně ("24.12.22");
/// 2/3 = bez roku ("24.12."). "" → "".
ClassMethod UpravDatum(dat As %String, tiskDtm As %Integer) As %String
{
    i dat = "" q ""
    i tiskDtm = 0 q dat
    s dd = +$p(dat,".",1), mm = +$p(dat,".",2), rr = $p(dat,".",3)
    i tiskDtm = 1 {
        i $l(rr) > 2 s rr = $e(rr,*-1,*)
        q dd_"."_mm_"."_rr
    }
    q dd_"."_mm_"."
}

/// Najde první linku firmy (první skupina × první linka s uzlem "I").
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

/// Normalizuje číslo linky na formát ^JRL (7 znaků, zleva nuly).
ClassMethod NormalizLinku(lin As %String) As %String
{
    i lin = "" q ""
    i $l(lin) < 7 q $e("0000000", 1, 7 - $l(lin)) _ lin
    q lin
}

/// Název linky z částí ^JRL(sku,lin,"I",1..N) spojených pomlčkou.
ClassMethod NazevLinky(dataDB As %String, sku As %String, lin As %String) As %String
{
    s nazev = "", i = ""
    f {
        s i = $o(^[dataDB]JRL(sku,lin,"I",i))
        q:i=""
        i '$isvalidnum(i) continue
        i i'>0 continue
        s cast = $g(^[dataDB]JRL(sku,lin,"I",i))
        i cast="" continue
        i nazev '= "" s nazev = nazev _ "-"
        s nazev = nazev _ cast
    }
    q nazev
}

/// Dopravci linky (distinct fa:pr přes oba směry) — plný tvar (knižní JŘ tiskne jako
/// samostatný řádek hlavičky, viz gold PREPRAVCE_NAZEV).
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
    q txt
}

/// Text dopravce (fa,pr) z `^JRSET("DOP",...)` — zkrácený MHD název, jinak plný tvar s adresou.
ClassMethod NazevDopravce(dataDB As %String, fa As %String, pr As %String) As %String
{
    i pr '= "" {
        s mhd = $p($g(^[dataDB]JRSET("DOP",fa,pr)),"~",6)
        i $tr(mhd," ","") '= "" q mhd
    }
    s firma = $g(^[dataDB]JRSET("DOP",fa))
    s mhd = $p(firma,"~",14)
    i $tr(mhd," ","") '= "" q mhd

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
    i tel '= ""   s text = text_","_ulice_",tel:"_tel
    q text
}

/// Název zastávky z ^JRZ(czst) — odbav/MHD název ($7), fallback CIS ($2).
ClassMethod NazevZastavky(dataDB As %String, czst As %String) As %String
{
    i czst = "" q ""
    s rec = $g(^[dataDB]JRZ(czst))
    s nazev = $p(rec, "$", 7)
    i nazev = "" s nazev = $p(rec, "$", 2)
    q $replace(nazev, ",,", ",")
}

}
