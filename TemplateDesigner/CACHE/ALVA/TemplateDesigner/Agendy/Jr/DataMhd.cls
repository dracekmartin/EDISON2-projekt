Include GENERAL

/// Čtení dat MHD (`ALVA.MHD.LSO`, `ALVA.MHD.ENUMS`), společné agendám nad daty MHD - dnes vývěsce MHD
/// a linkovému JŘ MHD (`B56` S12, `architektonicka-rozhodnuti-agendy.md` R8): výběr skupin linek a linek,
/// hlavička linky, dopravce a typ JŘ. S daty AD tvar nesdílí, ta mají svou třídu (`Jr.DataAd`).
/// Datovou databázi, firmu a linku dostává parametry, do pracovního globálu agendy nesahá (`cache-api.md`
/// pravidlo 6). Do 30. 10. je měla vývěska MHD a linkový JŘ MHD je volal přes ni.
Class ALVA.TemplateDesigner.Agendy.Jr.DataMhd Extends %RegisteredObject
{

/// Skupiny linek pro filtr výběru linek - ENUMS "SkupinyLinek" mimo zablokované.
/// OUT["1"] = JSON pole [{ "value": id, "label": název }].
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s arr = ##class(%DynamicArray).%New()
        s id = ""
        f {
            s id = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id))
            q:id=""
            i +$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id,"Blocked")) continue

            s nazev = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id,"Nazev"))
            s obj = ##class(%DynamicObject).%New()
            d obj.%Set("value", id)
            d obj.%Set("label", $s(nazev'="":nazev, 1:"Skupina "_id))
            d arr.%Push(obj)
        }
        s ^||ML("OUT","1") = arr.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Linky pro výběr linek (číslo, název, platnost), i linky bez trasy - agenda u nich napíše důvod
/// (`DUVOD_PRAZDNE`). Vstup a výstup viz <method>SeznamLinek</method>.
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s err = ""   // $$$CatchErrRest čte err; bez init by catch (např. firma bez MHD dat → throw) padl na <UNDEFINED>err
    try {
        $$$FillParams
        s ^||ML("OUT","1") = ..SeznamLinek(IdFa, dataDB).%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Linky skupiny pro výběr instancí.
/// IN["search"] = volitelný filtr podle textu (bez ohledu na velikost písmen, na display = "Cislo - Nazev")
/// IN["limit"]  = max počet řádků (default 50)
/// IN["SKUPINA_LINEK_ID"] = ID skupiny linek (volitelné; default = první nezblokovaná)
/// Vrací pole { "value": LinkaID, "cislo", "nazev", "display": "Cislo - Nazev", "subtitle": "Platnost ..." }.
/// Do 23. 10. vývěska linky bez trasy schovávala, protože na nich padala.
ClassMethod SeznamLinek(IdFa As %String, dataDB As %String) As %DynamicArray
{
    s search = $zcvt($g(^||ML("IN","search")), "L")
    s limit  = +$g(^||ML("IN","limit"))
    if 'limit s limit = 50

    // Skupina linek = organizační kontext. V budoucnu předáváno z Edisona;
    // zatím default první nezblokovaná skupina v ENUMS.
    s SkupinaLinekID = +$g(^||ML("IN","SKUPINA_LINEK_ID"))
    if 'SkupinaLinekID s SkupinaLinekID = ..GetDefaultSkupinaLinekID(IdFa, dataDB)

    s arr = ##class(%DynamicArray).%New()
    s count = 0
    // Bez aktivní skupiny linek (typicky firma bez MHD dat, např. čistě AD firma) → prázdný
    // picker, NE chyba: přepnutí na agendu MHD u takové firmy nemá vyhazovat popup. Smyčka níže
    // se SkupinaLinekID=0 nenajde v indexu žádnou linku a vrátí prázdné pole.
    // Iterace přes index ^ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,SkupinaLinekID,LinkaID) - jen linky skupiny.
    s LinkaID = ""
    for {
        s LinkaID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,SkupinaLinekID,LinkaID))
        q:LinkaID=""

        s cislo  =$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloVerejne"))
        if cislo = "" s cislo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloLinkyDleLicence"))
        s nazev  = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"NazevLinky"))
        // LSO drží ISO tvar; ven jako u AD (14.12.2025), ne „2025-12-14" (Jan 23. 10.).
        s platOd = ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).FormatDatum($zdh($g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiOd")),3,,,,,,,""))
        s platDo = ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).FormatDatum($zdh($g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiDo")),3,,,,,,,""))

        s display = cislo
        if nazev '= "" {
            if display '= "" s display = display_" - "_nazev
            else  s display = nazev
        }
        if display = "" s display = "Linka "_LinkaID

        // Filtr - substring match case-insensitive na display
        if search '= "" {
            if '$f($zcvt(display, "L"), search) continue
        }

        s subtitle = ""
        if platOd '= "" {
            s subtitle = "Platnost od "_platOd
            if platDo '= "" s subtitle = subtitle_" do "_platDo
        }

        s obj = ##class(%DynamicObject).%New()
        // "string" type - .NET čte přes GetString(); bez toho číselné cislo/LinkaID → JSON number → chyba
        d obj.%Set("value",    LinkaID, "string")
        d obj.%Set("cislo",    cislo, "string")
        d obj.%Set("nazev",    nazev, "string")
        d obj.%Set("display",  display, "string")
        if subtitle '= "" d obj.%Set("subtitle", subtitle, "string")
        // Řadí se podle čísla linky jako v AD, ne podle interního ID (to dávalo 11, 1, 25, 2 -
        // Jan 27. 10.): nejdřív čísla vzestupně, pak ostatní označení abecedně, linky bez čísla na konci.
        s razeni = $s(cislo?1.N:$lb(0, +cislo), cislo'="":$lb(1, $zcvt(cislo,"U")), 1:$lb(2, 0))
        s radky($lg(razeni,1), $lg(razeni,2), LinkaID) = obj
    }
    s skupina = ""
    for {
        s skupina = $o(radky(skupina))  q:skupina=""
        s klic = ""
        for {
            s klic = $o(radky(skupina, klic))  q:klic=""
            s id = ""
            for {
                s id = $o(radky(skupina, klic, id))  q:id=""
                q:count>=limit
                d arr.%Push(radky(skupina, klic, id))
                s count = count + 1
            }
        }
    }
    q arr
}

/// Vrátí ID první nezblokované skupiny linek pro danou IdFa.
/// Vrátí 0 pokud žádná aktivní skupina není.
/// Použito jako default pro výběr linek - v budoucnu nahrazeno parametrem z Edisona.
ClassMethod GetDefaultSkupinaLinekID(IdFa As %String, dataDB As %String) As %Integer
{
    s id = ""
    for {
        s id = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id))
        q:id=""
        if '+$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id,"Blocked")) ret id
    }
    ret 0
}

/// Údaje hlavičky linky MHD, které posílají obě agendy MHD stejně: číslo podle licence (číslo),
/// veřejné označení (text), název, údaje dopravce po polích, data platnosti, typ JŘ a QR kód.
/// Co si agenda skládá po svém (popis dopravce na řádky × jedním řádkem, hotová věta platnosti
/// pro starší šablony), přidá sama za ně. Hodnoty jsou z dat, bez hotových vět a náhradních textů
/// (`cache-api.md` §Data, ne hotové věty). Do 30. 10. si hlavičku skládala každá agenda zvlášť (`B56` S12).
ClassMethod AddHlavickaLinky(ByRef sys As %String, dataDB As %String, IdFa As %String, LinkaID As %String, vals As %DynamicObject)
{
    s cisloLinky = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloLinkyDleLicence"))
    d vals.%Set("CISLO_LINKY",            $s(cisloLinky?1.N:+cisloLinky, 1:cisloLinky))
    // Veřejné označení je text - v KV_DP má část linek písmena (`KontraktAgendTest.TestTypyCislaLinky`).
    d vals.%Set("LOKALNI_OZNACENI_LINKY", $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloVerejne")), "string")
    d vals.%Set("POPIS_LINKY",            $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"NazevLinky")))
    d ..DopravceSkalary(.sys, dataDB, IdFa, LinkaID, vals)
    // LSO drží datum v ISO tvaru; ven jde jako u AD („1.11.2018" ne „2018-11-01").
    d vals.%Set("DATUM_PLATNOSTI_OD",     ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).FormatDatum($zdh($g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiOd")),3,,,,,,,"")))
    d vals.%Set("DATUM_PLATNOSTI_DO",     ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).FormatDatum($zdh($g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiDo")),3,,,,,,,"")))
    d vals.%Set("TYP_JR",                 ..TypJrLinky(IdFa, dataDB, LinkaID))
    d vals.%Set("QR_KOD",                 $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"QRCode")))
}

/// Razítko typu JŘ podle příznaků linky; při návrhu i výluce vyhrává výluka (stará vývěska
/// `LinkyPrintFormatMHD.cls:692-693`). Texty jako u AD (`Jr.DataAd.TypJrLinky`), vědomá výjimka
/// z pravidla „data, ne hotové texty" (Jan 30. 10.).
ClassMethod TypJrLinky(IdFa As %String, dataDB As %String, LinkaID As %String) As %String
{
    i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"Vyluka")) q "VÝLUKOVÝ"
    i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"Navrh")) q "NÁVRH"
    q ""
}

/// Popis dopravce linky MHD: vlastní firma (`DruhDopravceID` = 0, `$$gIdFa^MlJFapi`) nebo cizí dopravce
/// (`DruhDopravceID` = 1 + `DopravceCiziFirmaID`, `$$gDopFa^JRapi`). Firemní data žijí v systémovém NS
/// (`sys("U","SystemDB")`), ne v datovém. Defensivní - selhání legacy volání → prázdno.
///
/// ⚠️ Vrací **víc řádků** (název, ulice, město, telefon), ne "Dopravce: <název>" jako do 31. 8.
/// Zákazník uvození nechtěl a adresu ano (#13158, rozhodnutí Jan 31. 8.); tvar je shodný s AD
/// vývěskou, ať se dvě vývěsky vedle sebe neliší. Tím se to rozešlo se vzorem MHD2 `Export.cls`,
/// odkud tvar původně pocházel - je to vědomé, ne drift.
/// `jednimRadkem` = řádek hlavičky linkového JŘ MHD (viz `RadkyDopravce`).
ClassMethod PopisDopravce(ByRef sys As %String, dataDB As %String, IdFa As %String, LinkaID As %String, jednimRadkem As %Boolean = 0) As %String
{
    d ..UdajeDopravce(.sys, dataDB, IdFa, LinkaID, .udaje)
    ret ..RadkyDopravce($g(udaje("NAZEV")), $g(udaje("ULICE")), $g(udaje("MESTO")), $g(udaje("TELEFON")), jednimRadkem)
}

/// Údaje dopravce linky po položkách: `udaje("NAZEV")`, `("ULICE")`, `("MESTO")`, `("TELEFON")`,
/// `("EMAIL")`, `("ICO")`. Zdroj viz `PopisDopravce`; selhání legacy volání → prázdné pole.
ClassMethod UdajeDopravce(ByRef sys As %String, dataDB As %String, IdFa As %String, LinkaID As %String, ByRef udaje As %String)
{
    k udaje
    try {
        s sysnmsp = $g(sys("U","SystemDB"))
        s druh = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"DruhDopravceID"))
        i druh = 0 {
            // Vlastní firma (MlJFapiFa: 02 název, 03 upřesnění, 07 IČO, 13 ulice, 14 město, 15 PSČ, 23 tel)
            // ⚠️ E-mail se nebere: jediný na kartě (26) je odesílací adresa EDISONu vedle SMTP serveru
            // a hesla (KV_DP edison@…, CB_DP no-reply@m-line.cz), ne kontakt pro cestující (Jan 27. 10.).
            k tmpArr  s err = $$gIdFa^MlJFapi(IdFa, sysnmsp, "", .tmpArr)
            i 'err {
                s udaje("NAZEV") = $g(tmpArr("dta",2))
                i $g(tmpArr("dta",3)) '= "" s udaje("NAZEV") = udaje("NAZEV") _ $g(tmpArr("dta",3))
                s udaje("ULICE") = $g(tmpArr("dta",13))
                s udaje("MESTO") = $zstrip($g(tmpArr("dta",15))_" "_$g(tmpArr("dta",14)), "<>W")
                s udaje("TELEFON") = $g(tmpArr("dta",23))
                s udaje("ICO") = $g(tmpArr("dta",7))
            }
        } elseif druh = 1 {
            // Cizí dopravce (gDopFa: 1 sídlo, 2 ulice, 3 telefon, 5 IČO, 7 obchodní název, 13 e-mail)
            s ciziFa = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"DopravceCiziFirmaID"))
            i ciziFa '= "" {
                k tmpArr  s err = $$gDopFa^JRapi(IdFa, dataDB, ciziFa, "", .tmpArr)
                i 'err {
                    s udaje("NAZEV") = $g(tmpArr("dta",7))
                    s udaje("ULICE") = $g(tmpArr("dta",2))
                    s udaje("MESTO") = $g(tmpArr("dta",1))
                    s udaje("TELEFON") = $g(tmpArr("dta",3))
                    s udaje("EMAIL") = $g(tmpArr("dta",13))
                    s udaje("ICO") = $g(tmpArr("dta",5))
                }
            }
        }
    } catch ex { k udaje }
    q
}

/// Údaje dopravce po jednotlivých polích pod stejnými klíči jako agendy AD (`Jr.DataAd.DopravceSkalary`),
/// aby šablona šla mezi agendami přenášet (`B194c` D4). Adresa = ulice, PSČ a obec.
/// Linka bez dopravce zapíše prázdno, ať pole v datech existuje vždy.
ClassMethod DopravceSkalary(ByRef sys As %String, dataDB As %String, IdFa As %String, LinkaID As %String, vals As %DynamicObject)
{
    d ..UdajeDopravce(.sys, dataDB, IdFa, LinkaID, .udaje)
    s ulice = $zstrip($g(udaje("ULICE")), "<>W"), mesto = $zstrip($g(udaje("MESTO")), "<>W")
    d vals.%Set("NAZEV_DOPRAVCE",   $zstrip($g(udaje("NAZEV")), "<>W"))
    d vals.%Set("ADRESA_DOPRAVCE",  ulice_$s((ulice '= "") && (mesto '= ""):", ", 1:"")_mesto)
    d vals.%Set("TELEFON_DOPRAVCE", $zstrip($g(udaje("TELEFON")), "<>W"))
    d vals.%Set("EMAIL_DOPRAVCE",   $zstrip($g(udaje("EMAIL")), "<>W"))
    d vals.%Set("ICO_DOPRAVCE",     $zstrip($g(udaje("ICO")), "<>W"))
}

/// Složí dopravce do řádků: název, ulice, sídlo/město, telefon. Prázdné položky se vynechají,
/// takže u dopravce bez adresy zbyde samotný název jako dřív.
/// `jednimRadkem` = tvar řádku „Přepravu zajišťuje" linkového JŘ: název, ulice, PSČ obec, „tel. "
/// oddělené čárkou. Oddělovače a „tel." jako linkový AD (`LinkovyJrAd.NazevDopravce`), pořadí adresy
/// jako vývěska MHD - do 26. 10. „PSČ obec, ulice" převzaté z AD, kde jsou data členěná jinak (`B194a` T6).
ClassMethod RadkyDopravce(nazev As %String, ulice As %String, mesto As %String, tel As %String, jednimRadkem As %Boolean = 0) As %String
{
    s nazev = $zstrip($g(nazev), "<>W")
    i nazev = "" q ""
    s ulice = $zstrip($g(ulice), "<>W"), mesto = $zstrip($g(mesto), "<>W"), tel = $zstrip($g(tel), "<>W")
    s text = nazev
    i jednimRadkem {
        i ulice '= "" s text = text_", "_ulice
        i mesto '= "" s text = text_", "_mesto
        i tel '= ""   s text = text_", tel. "_tel
        q text
    }
    i ulice '= "" s text = text_$c(10)_ulice
    i mesto '= "" s text = text_$c(10)_mesto
    i tel '= ""   s text = text_$c(10)_"tel: "_tel
    q text
}

}
