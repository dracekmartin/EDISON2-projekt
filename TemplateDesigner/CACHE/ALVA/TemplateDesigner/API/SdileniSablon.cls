Include GENERAL

/// Sdílení šablon mezi firmami přes sdílení dat ISYCloudu (`B189`, rozhodnutí R9 v `architektonicka-rozhodnuti-agendy.md`).
/// Spojení s ISYCloudem drží obecný `Sdileni.Klient`; tady je jen to, co je o šablonách.
Class ALVA.TemplateDesigner.API.SdileniSablon Extends %RegisteredObject
{

/// Nejvýš souborů v jedné zásilce (strop API ISYCloudu); víc šablon jde ve více zásilkách.
Parameter MaxSablonVZasilce = 20;

/// Sdílení šablon se v návrháři nabízí. Do vydání jen na vývojovém serveru (`B189`), u zákazníků skryté.
ClassMethod JeZapnute() As %Boolean
{
    q ##class(ALVA.GENERALAPI.Server).IsDev()
}

/// Firmy, kterým jde šablony poslat: OUT["1"] = [{idfa, nazev, zakaznik}], bez vlastní firmy.
ClassMethod GetPrijemci(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s vsechny = ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "GET", "/prijemci")
        s prijemci = ##class(%DynamicArray).%New()
        s iter = vsechny.%GetIterator()
        while iter.%GetNext(.i, .firma) {
            d:firma.idfa'=IdFa prijemci.%Push(firma)
        }
        s ^||ML("OUT","1") = prijemci.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Odešle vlastní šablony firmy vybraným firmám; každá šablona je v zásilce jeden JSON (typ dat `SABLONA`).
/// IN["templateIds"] = Id šablon čárkou, IN["prijemci"] = IdFa čárkou, IN["zprava"] = zpráva příjemcům
/// OUT["sablon"] = počet odeslaných šablon, OUT["zasilek"] = počet zásilek
ClassMethod SendSablony(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s prijemci = ..SeznamZTextu($g(^||ML("IN","prijemci")))
        i prijemci.%Size() = 0 $$$ThrowStatus($$$ERROR($$$GeneralError, "Vyberte firmu, které se šablony pošlou."))
        s ids = ..SeznamZTextu($g(^||ML("IN","templateIds")))
        i ids.%Size() = 0 $$$ThrowStatus($$$ERROR($$$GeneralError, "Vyberte šablonu, která se pošle."))
        s n = 0
        f i = 0:1:ids.%Size()-1 s sablony($i(n)) = ..ObsahKOdeslani(IdFa, ids.%Get(i))
        s zasilek = 0
        f prvni = 1:..#MaxSablonVZasilce:n {
            s posledni = prvni + ..#MaxSablonVZasilce - 1
            s:posledni>n posledni = n
            d ..SendZasilka(IdFa, .sablony, prvni, posledni, prijemci, $g(^||ML("IN","zprava")), loginName)
            s zasilek = zasilek + 1
        }
        s ^||ML("OUT","sablon") = n
        s ^||ML("OUT","zasilek") = zasilek
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Jedna zásilka se šablonami sablony(prvni..posledni). Chyba uprostřed rozpracovanou zásilku zruší,
/// příjemci ji neuvidí.
ClassMethod SendZasilka(IdFa As %String, ByRef sablony, prvni As %Integer, posledni As %Integer, prijemci As %DynamicArray, zprava As %String, odeslal As %String)
{
    s nazvy = ""
    f i = prvni:1:posledni s nazvy = nazvy_$s(nazvy="":"", 1:", ")_sablony(i).name
    s nazev = ..NazevZasilky(.sablony, prvni, posledni)
    s telo = {"typ": "SABLONA", "nazev": (nazev), "atributy": {"sablony": (nazvy)}, "prijemci": (prijemci), "odeslal": (odeslal)}
    s:zprava'="" telo.zprava = zprava
    s zasilka = ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "POST", "/zasilky", telo)
    try {
        f i = prvni:1:posledni {
            d ##class(ALVA.TemplateDesigner.Sdileni.Klient).UploadSoubor(IdFa, zasilka.id, sablony(i).id_".json", sablony(i).%ToJSON())
        }
        d ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "POST", "/zasilky/"_zasilka.id_"/odeslat")
    } catch ex {
        try { d ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "DELETE", "/zasilky/"_zasilka.id) } catch { }
        throw ex
    }
}

/// Název zásilky pro příjemce: jméno jediné šablony, jinak počet (3 šablony, 5 šablon).
ClassMethod NazevZasilky(ByRef sablony, prvni As %Integer, posledni As %Integer) As %String
{
    s pocet = posledni - prvni + 1
    q:pocet=1 sablony(prvni).name
    q pocet_" "_$s(pocet<5:"šablony", 1:"šablon")
}

/// JSON vlastní šablony firmy připravený k odeslání: bez hodnot parametrů platných jen ve firmě
/// (`BaseAgenda.ParametryJenVeFirme`), u příjemce pak platí výchozí - jako u šablon M-line (`B191f`).
/// Šablonu M-line poslat nejde, mají ji všechny firmy.
ClassMethod ObsahKOdeslani(IdFa As %String, templateId As %String) As %DynamicObject
{
    i '$d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId)) {
        $$$ThrowStatus($$$ERROR($$$GeneralError, "Poslat jde jen vlastní šablonu firmy, šablonu M-line mají všechny firmy."))
    }
    s json = ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaJson(IdFa, templateId)
    i '$isobject(json) $$$ThrowStatus($$$ERROR($$$GeneralError, "Šablonu „"_templateId_"“ nejde přečíst, je poškozená."))
    d ..RemoveHodnotyFirmy(json)
    q json
}

/// Odebere ze šablony uložené hodnoty parametrů, jejichž název začíná předponou z `ParametryJenVeFirme` agendy.
ClassMethod RemoveHodnotyFirmy(json As %DynamicObject)
{
    q:'$isobject(json.printParameters)
    s agendaClass = ##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(json.agendaCode)
    q:agendaClass=""
    s predpony = $parameter(agendaClass, "ParametryJenVeFirme")
    q:predpony=""
    s iter = json.printParameters.%GetIterator()
    while iter.%GetNext(.nazev, .hodnota) {
        f i = 1:1:$l(predpony, ",") {
            s predpona = $p(predpony, ",", i)
            i (predpona '= "") && ($e(nazev, 1, $l(predpona)) = predpona) s odebrat(nazev) = ""
        }
    }
    s nazev = ""
    f {
        s nazev = $o(odebrat(nazev))
        q:nazev=""
        d json.printParameters.%Remove(nazev)
    }
}

ClassMethod SeznamZTextu(text As %String) As %DynamicArray
{
    s seznam = ##class(%DynamicArray).%New()
    f i = 1:1:$l(text, ",") {
        s polozka = $zstrip($p(text, ",", i), "<>W")
        d:polozka'="" seznam.%Push(polozka)
    }
    q seznam
}

/// Stav sdílení firmy: OUT["1"] = {aktivni, nazev, duvod}. Nevyhazuje chybu - firma bez povoleného sdílení
/// dostane důvod pro uživatele (akce zůstane vidět a řekne, kdo sdílení povoluje).
ClassMethod GetStav(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s ^||ML("OUT","1") = ##class(ALVA.TemplateDesigner.Sdileni.Klient).Stav(IdFa).%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

}
