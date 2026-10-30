Include GENERAL

/// Čtení dat AD (jízdní řády linkové dopravy, `^JRL`), společné všem agendám nad daty AD - dnes
/// vývěsce AD, linkovému JŘ AD a rozdílovému JŘ (`B56` S5 až S8, `architektonicka-rozhodnuti-agendy.md` R8):
/// linka a hlavička linky, dopravce po polích, platnost jízdního řádu, výběr linek a text datumových omezení spojů.
/// Datovou databázi a linku dostává parametry, do pracovního globálu agendy nesahá (`cache-api.md`
/// pravidlo 6). Do 30. 10. měla každá agenda vlastní kopii.
/// Výběr skupin linek a linek (`GetAvailableSkupiny`, `GetAvailableInstances`) agendy AD jen přesměrují
/// sem, aby se okno výběru linek chovalo ve všech stejně - i v budoucí relaci nad AD.
Class ALVA.TemplateDesigner.Agendy.Jr.DataAd Extends %RegisteredObject
{

/// Skupiny linek pro filtr výběru linek - první subscript ^JRL(sku). Název skupiny je 1. `~`-piece
/// hodnoty ^JRL(sku) (např. "Vertikála k 1.1. - ŘÁDNÉ~01.01.2024~..."). OUT["1"] = [{ value, label }].
/// Bez téhle metody vrátí BaseAgenda prázdno, dialog přepínač skryje a uživatel zůstane trvale
/// na první skupině (nahlásil Jan 26. 8.).
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

/// Seznam linek pro výběr linek (umožní proklikávání stránek v náhledu).
/// IN["search"] = substring filtr (case-insensitive), IN["limit"] = max (default 50),
/// IN["SKUPINA_LINEK_ID"] = skupina linek (default první v ^JRL).
/// OUT["1"] = JSON pole { "value": lin, "cislo", "nazev", "display": "číslo - název", "subtitle": "Platnost ..." }.
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

            s platnost = ..PlatnostLinky(dataDB, sku, lin)
            s subtitle = ""
            i $lg(platnost,1) '= "" {
                s subtitle = "Platnost od "_$lg(platnost,1)
                i $lg(platnost,2) '= "" s subtitle = subtitle_" do "_$lg(platnost,2)
            }

            s obj = ##class(%DynamicObject).%New()
            // ⚠️ `cislo` a `nazev` musí být ve výstupu, i když je nese `display`: výběr instancí
            // je vypisuje jako sloupce, takže bez nich vyjde seznam **prázdný** (nahlásil Jan 24. 8.).
            // "string" type - .NET čte přes GetString(); bez toho číselné cislo/value → JSON number → chyba.
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

/// Název linky z částí ^JRL(sku,lin,"I",1..N) spojených pomlčkou.
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

/// Číslo linky ve tvaru, pod kterým je uložené v ^JRL (7 znaků, zleva doplněno nulami).
/// Do 30. 10. `NormalizLinku` v každé agendě AD.
ClassMethod FormatCisloLinky(lin As %String) As %String
{
    i lin = "" q ""
    i $l(lin) < 7 q $e("0000000", 1, 7 - $l(lin)) _ lin
    q lin
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

/// Údaje hlavičky linky AD, které posílají všechny agendy nad AD stejně: číslo podle licence (číslo),
/// veřejné označení (text), název, poznámky nad linkou a pod dopravcem, údaje prvního dopravce po polích,
/// data platnosti, typ JŘ a QR kód. Co si agenda skládá po svém (titulek, popis dopravce, hotová věta
/// platnosti pro starší šablony), přidá sama za ně. Hodnoty jsou z dat, bez hotových vět a náhradních
/// textů (`cache-api.md` §Data, ne hotové věty). Do 30. 10. si hlavičku skládala každá agenda zvlášť (`B56` S7).
ClassMethod AddHlavickaLinky(dataDB As %String, sku As %String, lin As %String, vals As %DynamicObject)
{
    d vals.%Set("CISLO_LINKY",            +lin, "number")
    d vals.%Set("LOKALNI_OZNACENI_LINKY", ..LokalniOznaceniLinky(dataDB, sku, lin), "string")
    d vals.%Set("POPIS_LINKY",            ..NazevLinky(dataDB, sku, lin))
    // Poznámka nad linku - legacy gPrpLin dta4 = PATCH,"Head"
    d vals.%Set("POZNAMKA_NAD_LINKU",     $g(^[dataDB]JRL(sku,lin,"PATCH","Head")))
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  $g(^[dataDB]JRL(sku,lin,"PATCH","PoznPodDopr")))
    d ..DopravceSkalary(dataDB, sku, lin, vals)
    s platnost = ..PlatnostLinky(dataDB, sku, lin)
    d vals.%Set("DATUM_PLATNOSTI_OD",     $lg(platnost,1))
    d vals.%Set("DATUM_PLATNOSTI_DO",     $lg(platnost,2))
    d vals.%Set("TYP_JR",                 ..TypJrLinky(dataDB, sku, lin))
    d vals.%Set("QR_KOD",                 $g(^[dataDB]JRL(sku,lin,"PATCH","QR_CODE")))
}

/// Má směr aspoň jeden spoj? Do 30. 10. v linkovém JŘ AD a vývěska AD ji volala přes něj (`B56` S12).
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

/// Barva přiřazená lince v JŘ modulu, jako "#AARRGGBB" pro SkiaSharp; prázdno, když linka barvu
/// nastavenou nemá (šablona pak použije bílou). Modul ji drží jako "R;G;B". Do 30. 10. ve vývěsce AD
/// a linkový JŘ AD ji volal přes ni (`B56` S12).
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
    ret ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).RgbToHex(rgb)
}

/// Veřejné (lokální) označení linky AD: první část MHD nastavení linky (`SeT_MhD`), bez něj znaky
/// 5-7 čísla linky (1:1 `sTIT^JRapiLinPrtMhd1`). `lin` je sedmimístné číslo linky (<method>FormatCisloLinky</method>).
/// Je to text, ne číslo - v MHD má část linek označení s písmeny.
ClassMethod LokalniOznaceniLinky(dataDB As %String, sku As %String, lin As %String) As %String
{
    s lokalni = $p($g(^[dataDB]JRL(sku,lin,"MhD","SeT_MhD")),"~",1)
    q $s(lokalni '= "":lokalni, 1:$e(lin,5,7))
}

/// Razítko typu JŘ linky AD podle `PATCH,"OBJ"` (1 výluka, 2 návrh). Texty jako u MHD
/// (`Jr.DataMhd.TypJrLinky`), aby razítko vypadalo ve všech agendách stejně; starý postskriptový
/// tisk AD psal u návrhu „návrh jízdního řádu" (rozhodnutí Jan 23. 10.: jednotně „NÁVRH").
/// Vědomá výjimka z pravidla „data, ne hotové texty" (Jan 30. 10.): ze dvou kódů by si šablona text
/// složila jen dvěma prvky s podmínkou viditelnosti.
ClassMethod TypJrLinky(dataDB As %String, sku As %String, lin As %String) As %String
{
    q $case(+$g(^[dataDB]JRL(sku,lin,"PATCH","OBJ")), 1:"VÝLUKOVÝ", 2:"NÁVRH", :"")
}

/// Skalární pole prvního dopravce linky (NAZEV_DOPRAVCE, ADRESA_DOPRAVCE…) z <method>BuildDopravci</method>;
/// u linky bez dopravce zapíše prázdno, aby pole ve schématu existovalo vždy (chybějící klíč by
/// v šabloně vypadal jako neznámé pole). MHD posílá tytéž klíče (`Jr.DataMhd.DopravceSkalary`).
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

/// Dopravci linky - **řádek na každou kombinaci dopravce a provozovny, která na lince jezdí**.
/// Páry (firma, provoz) sbírá z `#`-pole 2 spojů obou směrů, stejně jako popis dopravce v agendách,
/// takže se v ní neobjeví dopravce, který linku neobsluhuje. Bere z ní jen <method>DopravceSkalary</method>
/// první řádek; jako kolekce se do dat neposílá (provozovny vyhozené 4. 9., `B112`).
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
///   Bere se proto **pole 10** a pole 2 je záloha - shodně s `ZastavkoveJrAd.NazevDopravce`. Obráceně
///   by na vývěsce zmizelo PSČ a obec (odhaleno ověřením voláním 1. 9., ne úvahou).
/// - Telefon provozovny bývá slitý s webem a e-mailem („384 371 922, …, www…, info@…"). Vrací se,
///   jak je; kdo chce čistý telefon, vezme telefon firmy.
///
/// ⚠️ **Pole „zkrácený název pro MHD" se sem záměrně NEDOSTANE.** U ČSAD Jindřichův Hradec obsahuje
/// celý text včetně telefonů, takže jako jméno slouží špatně - a právě odsud pramení duplicita
/// telefonů v `ZastavkoveJrAd.NazevDopravce`, na kterou zákazník narazil (#13158).
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

/// Platnost jízdního řádu linky jako $lb(od, do) v textu, jak je v datech (`PATCH,"Plati"` = „od~do:…",
/// datum `dd.mm.rrrr`, prázdné „do" = bez konce). Příponu za dvojtečkou odřízne jako starý tisk
/// (`gLinHrlOd^JRapiLinUtl`). Do 30. 10. ji odřízla jen dvě ze šesti míst, která platnost četla - jinak
/// by skončila v datu „do"; v datech ji zatím nemá žádná linka (sonda `DuplicityAgendProbe.PlatnostSPriponou`).
ClassMethod PlatnostLinky(dataDB As %String, sku As %String, lin As %String) As %List
{
    i (sku = "") || (lin = "") q $lb("", "")
    s plati = $p($g(^[dataDB]JRL(sku,lin,"PATCH","Plati")), ":", 1)
    q $lb($p(plati,"~",1), $p(plati,"~",2))
}

/// Platnost linky jako $lb(horologOd, horologDo); prázdné, když datum chybí nebo neplatí.
/// Tři dřívější převody (`ALVA.GENERALAPI.Date`, `$zdateh`, vlastní kontrola tvaru) vyšly na všech
/// 193 050 datech platnosti šesti testovacích firem stejně (sonda `DuplicityAgendProbe.PrevodyPlatnosti`).
ClassMethod HorologPlatnostiLinky(dataDB As %String, sku As %String, lin As %String) As %List
{
    s platnost = ..PlatnostLinky(dataDB, sku, lin)
    s datumOd = $lg(platnost,1), datumDo = $lg(platnost,2), horologOd = "", horologDo = ""
    try { s:datumOd'="" horologOd = ##class(ALVA.GENERALAPI.Date).DateToHor(datumOd) } catch (ex) { s horologOd = "" }
    try { s:datumDo'="" horologDo = ##class(ALVA.GENERALAPI.Date).DateToHor(datumDo) } catch (ex) { s horologDo = "" }
    q $lb(horologOd, horologDo)
}

/// Text poznámky z datumových omezení spoje (`^JRL(sku,lin,smer,sp,omzId)`) jako starý zastávkový i linkový
/// tisk (`gComNZLiSp`, `JRapiNZUtl2.mac:110-131`): na začátku věta o lichých / sudých týdnech
/// (<method>TextTydnuPlatnosti</method>), pak omezení spojená čárkou v pořadí 13, 12, 11, 14, 15, 22 -
/// základní období „jede", pak „jede také", „jede jen", nakonec „nejede". `rec` = jazyk číselníku
/// („CZ", „*SK"), `tiskDtm` = režim roku (<method>TiskDtm</method>).
/// Omezení 14 a 15 („jede jen v sudých / lichých týdnech") je věta i bez data, ostatní bez data nic neřeknou.
/// Vývěska AD je do 30. 10. bez data vynechávala; takové není žádné z 319 na testovacích firmách
/// (sonda `DuplicityAgendProbe.OmezeniBezDat`).
ClassMethod TextOmezeni(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String, rec As %String, tiskDtm As %Integer) As %String
{
    s txt = ..TextTydnuPlatnosti(dataDB, rec, $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",8))
    f omzId = 13,12,11,14,15,22 {
        s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
        i omz = "" continue
        s datumy = ..FormatDatumyOmezeni(omz, tiskDtm)
        i (datumy = "") && (omzId '= 14) && (omzId '= 15) continue
        i txt '= "" s txt = txt_", "
        s txt = txt_..PredlozkaOmezeni(dataDB, rec, omzId)_$s(datumy'="":" "_datumy, 1:"")
    }
    q txt
}

/// Věta o platnosti spoje AD „jen v sudých / lichých týdnech" (`dlouhodoba` = hlava:8, 4 sudé, 5 liché)
/// z číselníku firmy (NCOME SU/LI), jinak české znění. Stojí na začátku textu omezení - 1:1 legacy
/// `gComNZLiSp` (`JRapiNZUtl2.mac:125-128`), který volá starý zastávkový i linkový tisk AD.
/// Zastávkový JŘ AD ji do 26. 10. vynechával (`B194` T3a).
ClassMethod TextTydnuPlatnosti(dataDB As %String, rec As %String, dlouhodoba As %String) As %String
{
    i (dlouhodoba '= 4) && (dlouhodoba '= 5) q ""
    q ..PredlozkaOmezeni(dataDB, rec, $s(dlouhodoba=4:14, 1:15))
}

/// Předložka typu omezení AD (`omzId`): 11 jede jen, 12 jede také, 13 jede, 22 nejede, 14 / 15 jede
/// jen v sudých / lichých týdnech. Z číselníku firmy `^JRSET("NCOME",rec,…)` (14 / 15 pod klíči SU / LI),
/// jako starý zastávkový i linkový tisk (`gComNZOmz`, `JRapiNZUtl2.mac:146-153`) - zákazník si znění
/// může přepsat, `rec` = „CZ" nebo „*SK". Bez číselníku (nebo `dataDB` prázdné) standardní znění.
/// Do 26. 10. měl Zastávkový JŘ AD předložky napevno, u sudých týdnů zkratkou „jede jen v su."
/// a ve slovenštině „premáva aj" (`B194` T3d).
ClassMethod PredlozkaOmezeni(dataDB As %String, rec As %String, omzId As %String) As %String
{
    s txt = ""
    i dataDB '= "" s txt = $g(^[dataDB]JRSET("NCOME",rec,$case(omzId, 14:"SU", 15:"LI", :omzId)))
    q $s(txt '= "":txt, 1:..PredlozkaOmezeniStandard(rec, omzId))
}

/// Standardní znění předložek omezení - totéž, co mají v číselníku všechny měřené firmy
/// (sonda `LinkovyJrNastaveniProbe.PredlozkyOmezeni`, 26. 10.).
ClassMethod PredlozkaOmezeniStandard(rec As %String, omzId As %String) As %String
{
    i rec = "*SK" q $case(omzId, 11:"premáva len", 12:"premáva tiež", 13:"premáva", 14:"premáva len v párnych týždňoch", 15:"premáva len v nepárnych týždňoch", 22:"nepremáva", :"")
    q $case(omzId, 11:"jede jen", 12:"jede také", 13:"jede", 14:"jede jen v sudých týdnech", 15:"jede jen v lichých týdnech", 22:"nejede", :"")
}

/// Režim tisku roku v datech omezení AD (legacy TiskDtm): nastavení u linky, jinak u firmy.
/// 0 = rok jak je v datech (celý), 1 = rok dvoumístně, 2 = rozsahy bez roku v „od", 3 = bez roku.
/// Firma bez nastavení = 0, jako starý tisk - data jsou uložená s celým rokem, takže výchozí tvar
/// je jednoznačný. Až se nastavení přesune nebo zruší, mění se jen tady.
/// ⚠️ U linky je režim uložený **o 1 výš** (0 = nenastaveno), u firmy bez posunu - jako starý tisk
/// (`JRapiNZUtl2.mac:186-190`). Do 26. 10. se u linky nic neodečítalo a 5 tisíc linek s „rokem
/// dvoumístně" tisklo data bez roku (`B194a` C9). Zastávkový JŘ AD režim do 26. 10. nečetl vůbec
/// a tiskl rok vždy dvoumístně, ač starý zastávkový tisk se jím řídí (`B194` T3c).
ClassMethod TiskDtm(dataDB As %String, sku As %String, lin As %String) As %Integer
{
    s v = +$g(^[dataDB]JRL(sku,lin,"PATCH","TiskDtm"))
    q $s(v > 0:v - 1, 1:+$g(^[dataDB]JRSET("Definice","TiskDtm")))
}

/// Datumová část omezení AD jako starý tisk (`JRapiNZUtl2.mac` gComOddO :197-222): položky (`#`)
/// spojené ", ", rozsah `od:do` → "od D1 do D2"; shodné nebo chybějící „do" = jedno datum. Datumy
/// dle režimu `TiskDtm` (viz <method>FormatDatumOmezeni</method>).
///
/// Vědomě jinak: starý linkový tisk spojuje data čárkou bez mezery („24.12.22,31.12.22", mezeru má
/// jen před „od"). Čárka s mezerou je tvar starého zastávkového tisku - obě agendy AD tak píšou totéž
/// a dlouhý výčet se líp čte (`B194` T3b, rozhodnutí Jan 26. 10.).
ClassMethod FormatDatumyOmezeni(omz As %String, tiskDtm As %Integer) As %String
{
    s data = $s(omz["@":$p(omz,"@",2), 1:omz)
    s casti = ""
    f i=1:1:$l(data,"#") {
        s item = $p(data,"#",i)
        i item = "" continue
        s od = ..FormatDatumOmezeni($p(item,":",1), tiskDtm), do = ..FormatDatumOmezeni($p(item,":",2), tiskDtm)
        i (do = "") || (do = od) { s cast = od }
        else {
            // režim 2: „od" bez roku, jen když je rok stejný jako u „do" (do 26. 10. vždy a u obou)
            i (tiskDtm = 2) && ($p(od,".",3) = $p(do,".",3)) s od = $p(od,".",1,2)_"."
            s cast = "od "_od_" do "_do
        }
        i casti '= "" s casti = casti_", "
        s casti = casti_cast
    }
    q casti
}

/// Jedno datum položky omezení: den a měsíc vždy bez úvodních nul a „d.m" s tečkou - starý tisk tak čistí
/// data s nulou navíc v každém režimu. Režim 1 = rok dvoumístně, 3 = bez roku, 0 a 2 = rok jak je.
/// Do 30. 10. `BaseAgenda.UpravDatum`.
ClassMethod FormatDatumOmezeni(dat As %String, tiskDtm As %Integer) As %String
{
    i dat = "" q ""
    i dat [ "." { f idx=1:1:2 { s $p(dat,".",idx) = +$p(dat,".",idx) } }
    i dat ? 1.2N1"."1.2N s dat = dat_"."
    i tiskDtm = 3 q $p(dat,".",1,2)_"."
    i tiskDtm = 1 {
        s rr = $p(dat,".",3)
        i $l(rr) > 2 s $p(dat,".",3) = $e(rr,*-1,*)
    }
    q dat
}

}
