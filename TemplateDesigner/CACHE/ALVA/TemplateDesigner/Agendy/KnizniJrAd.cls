Include GENERAL

/// Agenda: **Knižní / linkový JŘ z dat autobusové dopravy (AD)** — celá linka v jednom tisku:
/// svislý seznam zastávek (tarifní čísla) × vodorovné sloupce jednotlivých SPOJŮ s časy v buňkách.
/// Legacy ekvivalent `ALVA.TISKJR` (klient) + `mkPLinWin^JRapiLinPrtWin` (JRMAIN, skutečný engine).
///
/// Datový zdroj (stejný jako `ZastavkoveJrAd`):
/// - `^[dataDB]JRL(sku,lin,...)` — linka ("I"), trasa ("J"), spoje ("T"/"Z"), trasy spojů ("W"),
///   km sloupce ("K" / PATCH,"KmsSmer"), návaznosti (spoj,31/32), PATCH metadata
/// - `^[dataDB]JRZ(czst,...)`    — zastávky (názvy)
/// - `^[dataDB]JRSET("DOP",...)` — dopravci
///
/// ── Wire formát (čistě JSON, R3) — 1 stránka per (linka × směr) ──────────────────
/// **Sémantický tvar** (data = CO, element = JAK): element `LineTableElement` si matici
/// [zastávka × spoj] skládá sám; Caché neposílá layout (žádné buňkové matice legacy `idobj` modelu).
///
///   values: CISLO_LINKY, POPIS_LINKY, POZNAMKA_NAD_LINKU, POZNAMKA_POD_DOPRAVCE, POPIS_DOPRAVCE,
///           PLATNOST_OD/DO, POPIS_PLATNOSTI, TYP_JR, QR_KOD, SMER ("T"/"Z"), POPIS_SMER,
///           VYCHOZI_ZASTAVKA
///   rows.ZASTAVKY:    [{ PORADI, CZST, POPIS, TC, ZONA, KM:[per km-sloupec směru], ATRIBUTY:[znaky],
///                        NASTUPISTE }] — v pořadí směru jízdy (T vzestupně, Z sestupně)
///   rows.SPOJE:       [{ CISLO_SPOJE, SKUPINA, PLATNOST:[znaky], NZ, CASY:[zarovnané na ZASTAVKY:
///                        "HH:MM" | "|" (projíždí) | "<" (jede jinudy)], PRIPOJE:[{ TYP ("vycka"/
///                        "navazuje"), PORADI, ZNAK, TEXT, CAS, LINKA, SPOJ }] }]
///                     — řazené (SKUPINA, odjezd); spoj patřící do víc skupin dnů je emitován
///                       vícekrát (jednou per skupina), jako legacy
///   rows.SKUPINY_DNU: [{ ID, NADPIS }] dle parametru DELENI_SLOUPCU
///   rows.POZNAMKY:    [{ DRUH (1-10 dle legacy číselníku druhů), TYP ("text"/"znak"/"nz"),
///                        ZNAK, TEXT }] — pořadí druhů pevné (1,2,3,7,8,9,10)
///
/// ── První řez (vědomé mezery vůči mkPLinWin, viz analýza linkovy-knizni-jr-implementace) ──
/// - bez 2D bin-packingu (`Strih`), měřicích řetězců a prázdných padding sloupců — layout řeší TD
/// - bez turnusů, obousměrného tisku, MHD sloupce (J-věta dta6 mapování neověřeno)
/// - bez rozdvojení řádku příjezd/odjezd (atributy p/o/J/P/T) a symbolů trasy před názvem
/// - poznámky druhů 4/5/6 (poznámky pro spoj, atributy BEV..., místenkování) zatím neemitovány
/// - pořadí poznámek pevné (legacy má konfigurovatelné `PoznamkyPoradiDruh`)
/// - texty druhu 1 (týdenní platnost) hardcoded (legacy čte ^JRSET("NCOME"))
/// - řazení spojů dle odjezdu z výchozí (legacy `mkPoradiSp^JRapi` může být jemnější)
///
/// Gold oracle: `cache_query ALVA.TISKJR.Data/LoadData` s `VOLANO_Z~JRMAIN` (viz analýza §4;
/// ověřeno na CB_GWBUS 1/0320113 — dump ve scratchpadu oracle_0320113.txt).
///
/// Sdílené helpery (NormalizLinku, WMapa, SestavTextOmezeni, Nazev*...) jsou zatím **duplikované**
/// ze `ZastavkoveJrAd` (dohoda: extrakce až podle výsledků, ne předem).
Class ALVA.TemplateDesigner.Agendy.KnizniJrAd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaCode As %String = "KNIZNI_JR_AD";

Parameter AgendaLabel As %String = "Knižní JŘ (AD)";

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
    d ..AddField(gf, "CZST",       "Číslo zastávky",     "number")
    d ..AddField(gf, "POPIS",      "Název zastávky",     "text")
    d ..AddField(gf, "TC",         "Tarifní číslo",      "number")
    d ..AddField(gf, "ZONA",       "Zóna",               "text")
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
///
/// Fázový tok (zrcadlí datovou podstatu mkPLinWin bez layoutu):
///   1. hlavička linky (PATCH, "I", dopravci)
///   2. zastávky v pořadí směru (J-věty: czst, zóna, atributy, nástupiště) + km sloupce
///   3. spoje směru (hlava: trasa/odjezd/Plk; #7 = CNZ) → časy per zastávka z W-mapy,
///      zařazení do skupin dnů dle parametru, návaznosti (,31/,32)
///   4. poznámky druhů 1/2/3/7/8/9/10 (pevné pořadí)
ClassMethod GetData(ByRef sys As %String) As %Status
{
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

/// Vrátí ukázková data pro live preview — živě z první linky aktuální firmy (DEV vzor
/// shodný se ZastavkoveJrAd; TODO před produkcí statický vzorek).
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
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
    q $$$OK
}

/// Mapování labelu parametru „Dělení spojů dle platnosti" na legacy mód 0/1/2.
ClassMethod DeleniZParametru(label As %String) As %Integer
{
    q $case(label, "Bez rozlišení":0, "Pracovní dny / víkend+svátky":1, :2)
}

/// Sestaví JSON stránku knižního JŘ pro jednu linku a směr. "" když směr nemá spoje.
ClassMethod BuildStrankaLinky(dataDB As %String, sku As %String, lin As %String, smer As %String, deleni As %Integer) As %DynamicObject
{
    i '$d(^[dataDB]JRL(sku,lin,"I")) q ""
    i '..MaSpoje(dataDB, sku, lin, smer) q ""

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

    // ── zastávky v pořadí směru + mapa poradi → index v poli (pro zarovnání CASY) ──
    s jeTam = (smer = "T")
    s zastavky = ##class(%DynamicArray).%New()
    k poradiNaIndex, poradiList
    s prvniNazev = "", posledniNazev = ""
    s pocetKmSloupcu = +$g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer))
    s maxKmVeta = ..MaxKmVeta(dataDB, sku, lin, smer, pocetKmSloupcu)
    s idx = 0, poradi = ""
    f {
        i jeTam { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi)) }
        else    { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi), -1) }
        q:poradi=""
        i '$isvalidnum(poradi) continue
        s veta = $g(^[dataDB]JRL(sku,lin,"J",poradi))
        s czst = $p(veta,":",1)
        i czst = "" continue

        s z = ##class(%DynamicObject).%New()
        d z.%Set("PORADI",     +poradi)
        d z.%Set("CZST",       czst)
        d z.%Set("POPIS",      ..NazevZastavky(dataDB, czst))
        d z.%Set("TC",         +poradi)
        d z.%Set("ZONA",       $p($p(veta,":",5),"#",2))
        d z.%Set("NASTUPISTE", $p($p(veta,"#",3),":",$s(jeTam:1,1:2)))

        // atributy zastávky (J-věta dta2) — surové znaky, interpretace/filtr na elementu
        s atributy = ##class(%DynamicArray).%New()
        s atrZnaky = $p($p(veta,":",2),"#",1)
        f ai=1:1:$l(atrZnaky) d atributy.%Push($e(atrZnaky,ai))
        d z.%Set("ATRIBUTY", atributy)

        // km sloupce směru (PATCH,"KmsSmer",smer,sl; hodnota per tarifní číslo, -1 → "<" jede jinudy;
        // směr Zpět: km = maxKm - km, 1:1 legacy KmSloupce)
        s km = ##class(%DynamicArray).%New()
        f sl=1:1:pocetKmSloupcu {
            s kmVal = $p($g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer,sl)),":",poradi)
            i kmVal = -1 { d km.%Push("<") }
            elseif kmVal = "" { d km.%Push("") }
            elseif 'jeTam { d km.%Push($p(maxKmVeta,":",sl) - kmVal) }
            else { d km.%Push(+kmVal) }
        }
        d z.%Set("KM", km)

        d zastavky.%Push(z)
        s idx = idx + 1
        s poradiNaIndex(poradi) = idx, poradiList(idx) = poradi
        i prvniNazev = "" s prvniNazev = ..NazevZastavky(dataDB, czst)
        s posledniNazev = ..NazevZastavky(dataDB, czst)
    }
    i idx = 0 q ""

    d vals.%Set("VYCHOZI_ZASTAVKA", prvniNazev)
    d vals.%Set("POPIS_SMER",       posledniNazev)

    // ── spoje směru: seřadit dle (skupina, odjezd), časy zarovnané na zastávky ──
    k razeni                              // razeni(skupina, odjezd, sp) = ""
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s hlava = $p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1)
        s odjezd = +$p(hlava,":",4)
        s plk = $p(hlava,":",7)
        s skupiny = ..ZarazeniSpojeKnizni(deleni, plk)
        f gi=1:1:$l(skupiny,",") s razeni($p(skupiny,",",gi), odjezd, sp) = ""
    }

    s spoje = ##class(%DynamicArray).%New()
    k platnostZnakySeen, nzSeen, pripojePoznamky, markerSeen
    s pocetPoznamekPripoju = 0
    s skupina = ""
    f {
        s skupina = $o(razeni(skupina))
        q:skupina=""
        s odjezd = ""
        f {
            s odjezd = $o(razeni(skupina, odjezd))
            q:odjezd=""
            s sp = ""
            f {
                s sp = $o(razeni(skupina, odjezd, sp))
                q:sp=""
                s spojObj = ..BuildSpoj(dataDB, sku, lin, smer, sp, skupina, idx, .poradiList, .platnostZnakySeen, .nzSeen, .pripojePoznamky, .pocetPoznamekPripoju, .markerSeen)
                i spojObj '= "" d spoje.%Push(spojObj)
            }
        }
    }
    i spoje.%Size() = 0 q ""

    // ── poznámky (pevné pořadí druhů: 1 platnosti, 2 NZ, 3 návaznosti, 7 stálé značky, 8-10 linka) ──
    s poznamky = ##class(%DynamicArray).%New()
    d ..PridatPoznamkyPlatnosti(poznamky, .platnostZnakySeen)
    d ..PridatPoznamkyNZ(dataDB, sku, lin, smer, poznamky, .nzSeen)
    f pi=1:1:pocetPoznamekPripoju d poznamky.%Push(pripojePoznamky(pi))
    d ..PridatStaleZnacky(poznamky, .markerSeen)
    d ..PridatPoznamkyLinky(dataDB, sku, lin, poznamky)

    // ── sestavení stránky ───────────────────────────────────────────
    s out = ##class(%DynamicObject).%New()
    d out.%Set("values", vals)
    s rows = ##class(%DynamicObject).%New()
    d rows.%Set("ZASTAVKY", zastavky)
    d rows.%Set("SPOJE", spoje)
    d rows.%Set("SKUPINY_DNU", ..BuildSkupinyDnu(deleni))
    d rows.%Set("POZNAMKY", poznamky)
    d out.%Set("rows", rows)
    q out
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

/// Max km per km-sloupec (":"-věta) pro přepočet směru Zpět (legacy `MaxKm`: km zpět = max - km;
/// hodnoty -1 a prázdné se ignorují).
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

/// Sestaví JSON objekt jednoho spoje (sloupec matice): platnost, NZ, časy per zastávka, návaznosti.
/// Sbírá vedlejší produkty do ByRef polí pro legendu (platnostZnakySeen, nzSeen, pripojePoznamky, markerSeen).
ClassMethod BuildSpoj(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String, skupina As %Integer, pocetZastavek As %Integer, ByRef poradiList, ByRef platnostZnakySeen, ByRef nzSeen, ByRef pripojePoznamky, ByRef pocetPoznamekPripoju, ByRef markerSeen) As %DynamicObject
{
    s spoj = $g(^[dataDB]JRL(sku,lin,smer,sp))
    s hlava = $p(spoj,"#",1)
    s cisloTrasy = $p(hlava,":",3)
    s odjezd = +$p(hlava,":",4)
    s plk = $p(hlava,":",7)
    s cnz = $p(spoj,"#",7)

    s o = ##class(%DynamicObject).%New()
    d o.%Set("CISLO_SPOJE", +sp)
    d o.%Set("SKUPINA", skupina)
    d o.%Set("NZ", cnz)

    // znaky týdenní platnosti z Plk (jen 1-7/X/+, jako legacy filtr dta7)
    s platnost = ##class(%DynamicArray).%New()
    f i=1:1:$l(plk) {
        s znak = $e(plk,i)
        i "1234567X+" [ znak {
            d platnost.%Push(znak)
            s platnostZnakySeen(znak) = ""
        }
    }
    d o.%Set("PLATNOST", platnost)
    i cnz '= "" s nzSeen(cnz) = ""

    // časy per zastávka (zarovnané indexem na rows.ZASTAVKY). W-věta rozlišuje tři stavy
    // (ověřeno na trase 7 linky 0320113): číslo = staví (odjezd+offset), "|" = projíždí bez
    // zastavení, "<" = jede jinudy; prázdný/chybějící segment = mimo trasu → "<".
    d ..WMapa(dataDB, sku, lin, cisloTrasy, .wMap)
    s casy = ##class(%DynamicArray).%New()
    f i=1:1:pocetZastavek {
        s poradi = poradiList(i)
        s cas = $g(wMap(poradi))
        i (cas = "<") || (cas = "") { d casy.%Push("<")  s markerSeen("<") = "" }
        elseif cas = "|" { d casy.%Push("|")  s markerSeen("|") = "" }
        else {
            s celkem = odjezd + ..CasHodnota(cas)
            d casy.%Push(..FormatCas(celkem))
        }
    }
    d o.%Set("CASY", casy)

    // návaznosti: omzId 31 = vyčká, 32 = navazuje. Věta `znak@tc#linka:spoj2:text:...:AVL(:6)...:casMin(:8)`
    // (ověřeno na CB_GWBUS 0320113 sp5/sp11). Emituje i poznámku druhu 3 do legendy.
    s pripoje = ##class(%DynamicArray).%New()
    f omzId = 31, 32 {
        s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
        i omz = "" continue
        s typ = $s(omzId=31:"vycka", 1:"navazuje")
        s znak = $p(omz,"@",1)
        s zbytek = $p(omz,"@",2,$l(omz,"@"))
        s tc = $p(zbytek,"#",1)
        s payload = $p(zbytek,"#",2,$l(zbytek,"#"))
        s linka2 = $p(payload,":",1)
        s spoj2 = $p(payload,":",2)
        s text = $p(payload,":",3)
        s casMin = +$p(payload,":",8)

        s pr = ##class(%DynamicObject).%New()
        d pr.%Set("TYP", typ)
        d pr.%Set("PORADI", +tc)
        d pr.%Set("ZNAK", znak)
        d pr.%Set("TEXT", text)
        d pr.%Set("CAS", ..FormatCas(casMin))
        d pr.%Set("LINKA", linka2)
        d pr.%Set("SPOJ", spoj2)
        d pripoje.%Push(pr)

        // poznámka druhu 3 (text 1:1 vzor legacy: "na spoj N navazuje v zastávce X spoj M linky L do T")
        s sloveso = $s(omzId=31:"vyčká", 1:"navazuje")
        s nazevTc = ..NazevZastavkyPoradi(dataDB, sku, lin, tc)
        s pozn = ..BuildPoznamka(3, "znak", znak, "na spoj "_(+sp)_" "_sloveso_" v zastávce "_nazevTc_" spoj "_(+spoj2)_" linky "_(+linka2)_" do "_text)
        s pocetPoznamekPripoju = $i(pocetPoznamekPripoju)
        s pripojePoznamky(pocetPoznamekPripoju) = pozn
    }
    d o.%Set("PRIPOJE", pripoje)

    q o
}

/// Zařazení spoje do skupin dnů dle módu dělení a Plk (legacy in("A",2,"arr",3)):
///   0 → vždy "1"; 1 → PD (X,1-5) / víkend+svátky (6,7,+); 2 → PD / So (6) / Ne+svátky (7,+).
/// Spoj může patřit do víc skupin (vrací čárkou oddělený seznam). Bez rozpoznané platnosti
/// → všechny skupiny (legacy fallback „bez explicitní platnosti se tiskne všude", HD 10183).
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

/// Definice skupin dnů dle módu dělení (nadpisy standardní; legacy čte ^JRSET WinMHD DSC — gap).
ClassMethod BuildSkupinyDnu(deleni As %Integer) As %DynamicArray
{
    s arr = ##class(%DynamicArray).%New()
    i deleni = 0 {
        d arr.%Push(..BuildSkupina(1, ""))
    }
    elseif deleni = 1 {
        d arr.%Push(..BuildSkupina(1, "Pracovní dny"))
        d arr.%Push(..BuildSkupina(2, "Soboty, neděle a svátky"))
    }
    else {
        d arr.%Push(..BuildSkupina(1, "Pracovní dny"))
        d arr.%Push(..BuildSkupina(2, "Soboty"))
        d arr.%Push(..BuildSkupina(3, "Neděle a svátky"))
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
/// Texty hardcoded (legacy čte ^JRSET("NCOME") — gap prvního řezu).
ClassMethod PridatPoznamkyPlatnosti(poznamky As %DynamicArray, ByRef platnostZnakySeen)
{
    f i=1:1:$l("X123456 7+") {
        s znak = $e("X123456 7+",i)
        i znak = " " continue
        i '$d(platnostZnakySeen(znak)) continue
        d poznamky.%Push(..BuildPoznamka(1, "znak", znak, ..TextPlatnosti(znak)))
    }
}

/// Standardní text týdenní platnosti pro znak.
ClassMethod TextPlatnosti(znak As %String) As %String
{
    q $case(znak,
        "X":"jede v pracovních dnech",
        1:"jede v pondělí", 2:"jede v úterý", 3:"jede ve středu", 4:"jede ve čtvrtek",
        5:"jede v pátek", 6:"jede v sobotu", 7:"jede v neděli",
        "+":"jede ve státem uznané svátky", :"")
}

/// Poznámky druhu 2 — negativní značky: text z datumových omezení spojů nesoucích danou NZ.
/// (Legacy klíčuje legendu NZ znakem; text bere z omezení spoje — shodně SestavTextOmezeni.)
ClassMethod PridatPoznamkyNZ(dataDB As %String, sku As %String, lin As %String, smer As %String, poznamky As %DynamicArray, ByRef nzSeen)
{
    s znak = ""
    f {
        s znak = $o(nzSeen(znak))
        q:znak=""
        // najdi první spoj směru s touto NZ a vezmi jeho text omezení
        s txt = "", sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
            q:sp=""
            i '$isvalidnum(sp) continue
            i $p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",7) '= znak continue
            s txt = ..SestavTextOmezeni(dataDB, sku, lin, smer, sp)
            q:txt'=""
        }
        i txt '= "" d poznamky.%Push(..BuildPoznamka(2, "nz", znak, txt))
    }
}

/// Poznámky druhu 7 — stálé značky použité v matici ("|" projíždí, "<" jede jinudy).
ClassMethod PridatStaleZnacky(poznamky As %DynamicArray, ByRef markerSeen)
{
    i $d(markerSeen("|")) d poznamky.%Push(..BuildPoznamka(7, "znak", "|", "spoj příslušnou zastávkou projíždí"))
    i $d(markerSeen("<")) d poznamky.%Push(..BuildPoznamka(7, "znak", "<", "spoj jede po jiné trase"))
}

/// Poznámky linky (^JRL(sku,lin,"C",key,idx), numerický klíč: 1 = tarifní → druh 8,
/// 3 = všeobecné → druh 9, 4 = radiobus → druh 10; klíč 2 se přeskakuje jako legacy).
/// Řádky jednoho klíče se spojují do jedné poznámky ("#" na konci řádku = zalomení, jako gold).
ClassMethod PridatPoznamkyLinky(dataDB As %String, sku As %String, lin As %String, poznamky As %DynamicArray)
{
    f key = 1, 3, 4 {
        s druh = $case(key, 1:8, 3:9, :10)
        s txt = "", idx = ""
        f {
            s idx = $o(^[dataDB]JRL(sku,lin,"C",key,idx))
            q:idx=""
            s txt = txt_$g(^[dataDB]JRL(sku,lin,"C",key,idx))
        }
        i txt '= "" d poznamky.%Push(..BuildPoznamka(druh, "text", "", $replace(txt,"#",$c(10))))
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

/// Formát času HH:MM z minut od půlnoci (zarovnané na 2 číslice, hodiny mod 24 — jako $$$NT2).
ClassMethod FormatCas(minuty As %Integer) As %String
{
    s hod = (minuty\60)#24, min = minuty#60
    q $tr($j(hod,2)," ",0)_":"_$tr($j(min,2)," ",0)
}

/// Název zastávky podle pořadí (tarifního čísla) v trase linky.
ClassMethod NazevZastavkyPoradi(dataDB As %String, sku As %String, lin As %String, poradi As %String) As %String
{
    s czst = $p($g(^[dataDB]JRL(sku,lin,"J",poradi)),":",1)
    i czst = "" q ""
    q ..NazevZastavky(dataDB, czst)
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Helpery duplikované ze ZastavkoveJrAd (stejný zdroj dat; extrakce až podle výsledků)
/// ═══════════════════════════════════════════════════════════════════════════════

/// W-mapa `poradi → cas` pro danou trasu, cachovaná per `cisloTrasy` (`^||TiskKnizni("wmap",…)`).
/// `cas` může být číslo, `<` (projíždí) nebo "" (mimo trasu).
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
/// "" pro všechny nečasové markery ("<" jede jinudy, "|" projíždí, prázdno mimo trasu).
ClassMethod CasHodnota(cas As %String) As %String
{
    i (cas = "") || (cas = "<") || (cas = "|") q ""
    q +$p(cas,"/",1) + +$p(cas,"/",2)
}

/// Text poznámky z datumových omezení spoje (omzId 11-15, 22), 1:1 `txtNZ^JRapiNZUtl`.
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

/// Prefix textu pro daný typ omezení (1:1 `txtNZ^JRapiNZUtl`).
ClassMethod PrefixOmezeni(omzId As %String) As %String
{
    q $case(omzId, 11:"jede jen ", 12:"jede také ", 13:"jede ", 14:"jede jen v su. ", 15:"jede jen v li. ", 22:"nejede ", :"")
}

/// Datumová část omezení v knižním formátu (1:1 gold ALVA.TISKJR): datumy BEZ roku ("24.12."),
/// položky spojené ","; rozsahová položka " od D1 do D2" má úvodní mezeru (kromě první).
/// (Zastávkový JŘ formátuje jinak — rok 2místně, ", " oddělovač.)
ClassMethod FormatDatumyOmezeni(omz As %String) As %String
{
    s data = $s(omz["@":$p(omz,"@",2), 1:omz)
    s casti = ""
    f i=1:1:$l(data,"#") {
        s item = $p(data,"#",i)
        i item = "" continue
        s od = ..UpravDatum($p(item,":",1)), do = ..UpravDatum($p(item,":",2))
        i do '= "" { s cast = $s(casti="":"", 1:" ")_"od "_od_" do "_do }
        else       { s cast = od }
        i casti '= "" s casti = casti_","
        s casti = casti_cast
    }
    q casti
}

/// Úprava datumu pro knižní tisk: strip nul ze dne/měsíce, BEZ roku ("24.12.2024" → "24.12.").
ClassMethod UpravDatum(dat As %String) As %String
{
    i dat = "" q ""
    s dd = +$p(dat,".",1), mm = +$p(dat,".",2)
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

/// Dopravci linky (distinct fa:pr přes oba směry) — plný tvar, bez prefixu „Zajišťuje:"
/// (knižní JŘ tiskne dopravce jako samostatný řádek hlavičky, viz gold PREPRAVCE_NAZEV).
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
