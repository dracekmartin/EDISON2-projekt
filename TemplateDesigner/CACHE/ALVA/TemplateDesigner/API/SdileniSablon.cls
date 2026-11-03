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

// ── Příjem: zásilky od jiných firem ──────────────────────────────────────────
// Zásilka se převezme nebo odmítne celá (Jan 3. 11.). Převzatá šablona je vlastní šablonou firmy s novým Id
// a pamatuje si původ (firmu a Id šablony u ní); šablona téhož původu se pak nabídne k přepsání (R9).
// Údaje o šablonách zásilky (název, agenda, verze formátu, Id u odesílatele) si server drží, ať se soubory
// nestahují při každém zobrazení seznamu: ^ALVA.TemplateDesigner.Templates(IdFa, "PRIJATE", zásilka, soubor) = $lb(…)

/// Nevyřízené zásilky: OUT["1"] = {pocet, odesilatel, nazev, sablon} - počet zásilek a nejnovější z nich pro pruh
/// v hlavním okně a počet u uzlu Přijaté šablony. Firma bez klíče sdílení nikdy nepoužila a nikdo jí nic poslat
/// nemohl, server bez sdílení ho nenabízí: ISYCloud se pak nevolá.
ClassMethod GetSouhrnPrijatych(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s souhrn = {"pocet": 0, "odesilatel": "", "nazev": "", "sablon": 0}
        i ..JeZapnute() && (##class(ALVA.TemplateDesigner.Sdileni.Klient).Klic(IdFa) '= "") {
            s odpoved = ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "GET", ..CestaPrijatych(1))
            s souhrn.pocet = +odpoved.celkem
            s prvni = $s($isobject(odpoved.polozky):odpoved.polozky.%Get(0), 1:"")
            i $isobject(prvni) {
                s souhrn.odesilatel = prvni.odesilatel.nazev
                s souhrn.nazev = prvni.nazev
                s souhrn.sablon = $s($isobject(prvni.soubory):prvni.soubory.%Size(), 1:0)
            }
        }
        s ^||ML("OUT","1") = souhrn.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Dotaz na nevyřízené zásilky šablon (u příjemce nové nebo stažené), nejnovější první.
ClassMethod CestaPrijatych(limit As %Integer) As %String
{
    q "/prijate?typ=SABLONA&stav=nova,stazena&limit="_limit
}

/// Řádky uzlu Přijaté šablony v okně šablon (`EDList.Sablony`), jedna zásilka = jeden řádek s Id zásilky.
/// Údaje o zásilkách, které mezitím vyřídil někdo jinde (portál ISYCloudu), se zahodí.
ClassMethod AddPrijateRows(list As ALVA.EDControls.EDList.List, IdFa As %String)
{
    s odpoved = ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "GET", ..CestaPrijatych(200))
    s zasilky = $s($isobject(odpoved.polozky):odpoved.polozky, 1:##class(%DynamicArray).%New())
    s iter = zasilky.%GetIterator()
    while iter.%GetNext(.i, .zasilka) {
        d ..AddPrijataRow(list, IdFa, zasilka)
        s nevyrizene(zasilka.id) = ""
    }
    s zasilkaId = ""
    f {
        s zasilkaId = $o(^ALVA.TemplateDesigner.Templates(IdFa,"PRIJATE",zasilkaId))
        q:zasilkaId=""
        k:'$d(nevyrizene(zasilkaId)) ^ALVA.TemplateDesigner.Templates(IdFa,"PRIJATE",zasilkaId)
    }
}

/// Řádek zásilky: název zásilky, agenda a varianta šablon (víc agend čárkou), odesílatel ve sloupci Zdroj, datum
/// odeslání. Skryté: příznak přijaté zásilky, kdo poslal, zpráva a názvy šablon - okno z nich skládá nápovědu.
ClassMethod AddPrijataRow(list As ALVA.EDControls.EDList.List, IdFa As %String, zasilka As %DynamicObject)
{
    s nazvy = ""
    s iter = zasilka.soubory.%GetIterator()
    while iter.%GetNext(.i, .soubor) {
        s meta = ..MetaSouboru(IdFa, zasilka.id, soubor)
        s nazvy = nazvy_$s(nazvy="":"", 1:", ")_$lg(meta, 1)
        d ##class(ALVA.TemplateDesigner.EDList.Sablony).ResolveAgendaVariant($lg(meta, 2), .agenda, .varianta)
        s agendy($s(varianta'="":agenda_" · "_varianta, 1:agenda)) = $lb(agenda, varianta)
    }
    s prvni = $o(agendy("")), agenda = "", varianta = ""
    i (prvni '= "") && ($o(agendy(prvni)) = "") {
        s agenda = $lg(agendy(prvni), 1), varianta = $lg(agendy(prvni), 2)
    } else {
        s text = ""
        f {
            s text = $o(agendy(text))
            q:text=""
            s agenda = agenda_$s(agenda="":"", 1:", ")_text
        }
    }

    s row = list.AddRow(zasilka.id)
    s row.name       = zasilka.nazev
    s row.agenda     = agenda
    s row.varianta   = varianta
    s row.zdroj      = zasilka.odesilatel.nazev
    s row.ikona      = "EDIcons.Company"
    s row.modifiedAt = ..CasMistni(zasilka.odeslano)
    s row.prijata    = "1"
    s row.odeslal    = zasilka.odeslal
    s row.zprava     = zasilka.zprava
    s row.sablony    = nazvy
}

/// Název, agenda, verze formátu a Id šablony u odesílatele jako $lb. Soubor se stáhne jen poprvé; první stažení
/// přepne zásilku u příjemce na staženou, nevyřízená ale zůstane.
ClassMethod MetaSouboru(IdFa As %String, zasilkaId As %String, soubor As %DynamicObject) As %List
{
    s meta = $g(^ALVA.TemplateDesigner.Templates(IdFa,"PRIJATE",zasilkaId,soubor.id))
    q:meta'="" meta
    s json = ..ParseSablona(##class(ALVA.TemplateDesigner.Sdileni.Klient).DownloadSoubor(IdFa, zasilkaId, soubor.id), soubor.nazev)
    s meta = $lb(json.name, ..AgendaSablony(json), json.version, json.id)
    s ^ALVA.TemplateDesigner.Templates(IdFa,"PRIJATE",zasilkaId,soubor.id) = meta
    q meta
}

/// Obsah souboru zásilky jako šablona; soubor, který šablonou návrháře není, je chyba pro uživatele.
ClassMethod ParseSablona(obsah As %String, nazevSouboru As %String) As %DynamicObject
{
    s json = ""
    try { s json = ##class(%DynamicAbstractObject).%FromJSON(obsah) } catch { s json = "" }
    i '$isobject(json) || ($classname(json) '= "%Library.DynamicObject") || 'json.%IsDefined("sections") {
        $$$ThrowStatus($$$ERROR($$$GeneralError, "Soubor „"_nazevSouboru_"“ v zásilce není šablona návrháře."))
    }
    q json
}

/// Kód agendy šablony. Bez agendy je Obecná; starý kód "MHD" návrhář při otevření převádí stejně
/// (`TemplateSerializer.LegacyAgendaCodes`).
ClassMethod AgendaSablony(json As %DynamicObject) As %String
{
    s kod = json.agendaCode
    q:kod="" "GENERAL"
    q:kod="MHD" "ZASTAVKOVY_JR_MHD"
    q kod
}

/// Agendu zná návrhář této firmy (Obecná nemá třídu, je vždy).
ClassMethod JeZnamaAgenda(agendaCode As %String) As %Boolean
{
    q (agendaCode = "GENERAL") || (##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(agendaCode) '= "")
}

/// Šablony zásilky před převzetím: OUT["1"] = {sablony: [{souborId, name, agendaCode, agenda, version, agendaZnama,
/// existujici: {templateId, name, upravena}}]}, `existujici` jen u šablony, kterou firma od téže firmy už převzala.
/// Návrhář podle toho odmítne šablonu formátu, který nezná, a u převzaté se zeptá, jestli ji přepsat (R9).
/// IN["zasilkaId"]
ClassMethod GetObsahZasilky(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s zasilka = ..LoadZasilka(IdFa, $g(^||ML("IN","zasilkaId")))
        s odesilatel = zasilka.odesilatel.idfa
        s sablony = ##class(%DynamicArray).%New()
        s iter = zasilka.soubory.%GetIterator()
        while iter.%GetNext(.i, .soubor) {
            s meta = ..MetaSouboru(IdFa, zasilka.id, soubor)
            s agendaCode = $lg(meta, 2)
            d ##class(ALVA.TemplateDesigner.EDList.Sablony).ResolveAgendaVariant(agendaCode, .agenda, .varianta)
            s sablona = {"souborId": (soubor.id), "name": ($lg(meta, 1)), "agendaCode": (agendaCode), "version": ($lg(meta, 3))}
            s sablona.agenda = agenda_$s(varianta'="":" · "_varianta, 1:"")
            d sablona.%Set("agendaZnama", ..JeZnamaAgenda(agendaCode), "boolean")
            s existujici = ..PrevzataSablona(IdFa, odesilatel, $lg(meta, 4))
            i existujici '= "" {
                s sablona.existujici = {"templateId": (existujici), "name": ($g(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",existujici,"Metadata","Name")))}
                d sablona.existujici.%Set("upravena", '$d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",existujici,"Metadata","PuvodBezUprav")), "boolean")
            }
            d sablony.%Push(sablona)
        }
        s ^||ML("OUT","1") = {"sablony": (sablony)}.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Převezme celou zásilku: každá šablona se uloží jako vlastní šablona firmy a ISYCloud se dozví, že je načtená.
/// Šablona, kterou převzít nejde (agenda, kterou firma nemá), zastaví celou zásilku dřív, než se cokoli uloží.
/// IN["zasilkaId"], IN["prepsat"] = Id souborů čárkou, jejichž dřív převzatou šablonu nová verze přepíše; ostatní
/// se uloží jako nové a dřív převzatá šablona téhož původu se stane obyčejnou vlastní šablonou.
/// OUT["1"] = [{templateId, name, agendaCode}]
ClassMethod AcceptZasilka(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s zasilka = ..LoadZasilka(IdFa, $g(^||ML("IN","zasilkaId")))
        s prepsat = ","_$g(^||ML("IN","prepsat"))_","
        s odesilatel = zasilka.odesilatel.idfa
        s n = 0
        s iter = zasilka.soubory.%GetIterator()
        while iter.%GetNext(.i, .soubor) {
            s json = ..ParseSablona(##class(ALVA.TemplateDesigner.Sdileni.Klient).DownloadSoubor(IdFa, zasilka.id, soubor.id), soubor.nazev)
            d json.%Set("agendaCode", ..AgendaSablony(json))
            i '..JeZnamaAgenda(json.agendaCode) {
                $$$ThrowStatus($$$ERROR($$$GeneralError, "Šablonu „"_json.name_"“ nejde převzít: agendu "_json.agendaCode_" návrhář této firmy nezná."))
            }
            s sablony($i(n)) = json, prepsatSoubor(n) = (prepsat [ (","_soubor.id_","))
        }
        i n = 0 $$$ThrowStatus($$$ERROR($$$GeneralError, "Zásilka neobsahuje žádnou šablonu."))

        s prevzate = ##class(%DynamicArray).%New()
        f i = 1:1:n {
            s json = sablony(i)
            s existujici = ..PrevzataSablona(IdFa, odesilatel, json.id)
            d:(existujici'="")&&'prepsatSoubor(i) ..RemovePuvod(IdFa, existujici)
            s templateId = ..WritePrevzata(IdFa, json, odesilatel, $s(prepsatSoubor(i):existujici, 1:""), userID)
            d prevzate.%Push({"templateId": (templateId), "name": (json.name), "agendaCode": (json.agendaCode)})
        }
        d ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "POST", "/zasilky/"_zasilka.id_"/nacteno")
        k ^ALVA.TemplateDesigner.Templates(IdFa,"PRIJATE",zasilka.id)
        s ^||ML("OUT","1") = prevzate.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Odmítne celou zásilku; odesílatel to uvidí v portálu ISYCloudu. IN["zasilkaId"]
ClassMethod RejectZasilka(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s zasilkaId = ..CheckZasilkaId($g(^||ML("IN","zasilkaId")))
        d ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "POST", "/zasilky/"_zasilkaId_"/odmitnout")
        k ^ALVA.TemplateDesigner.Templates(IdFa,"PRIJATE",zasilkaId)
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Zásilka šablon, kterou firma ještě nevyřídila; jinak chyba pro uživatele.
ClassMethod LoadZasilka(IdFa As %String, zasilkaId As %String) As %DynamicObject
{
    s zasilka = ##class(ALVA.TemplateDesigner.Sdileni.Klient).Request(IdFa, "GET", "/zasilky/"_..CheckZasilkaId(zasilkaId))
    d ..CheckKPrevzeti(zasilka)
    q zasilka
}

/// Převzít jde jen zásilku šablon, kterou firma ještě nenačetla ani neodmítla.
ClassMethod CheckKPrevzeti(zasilka As %DynamicObject)
{
    i zasilka.typ '= "SABLONA" $$$ThrowStatus($$$ERROR($$$GeneralError, "Zásilka neobsahuje šablony návrháře."))
    s stav = $s($isobject(zasilka.mujStav):zasilka.mujStav.stav, 1:"")
    i (stav '= "nova") && (stav '= "stazena") $$$ThrowStatus($$$ERROR($$$GeneralError, "Zásilka už je vyřízená."))
}

/// Id zásilky jde do cesty URL: jen písmena, číslice a pomlčky.
ClassMethod CheckZasilkaId(zasilkaId As %String) As %String
{
    i (zasilkaId = "") || (zasilkaId '? 1.(1AN,1"-")) $$$ThrowStatus($$$ERROR($$$GeneralError, "Zásilka není vybraná."))
    q zasilkaId
}

/// Vlastní šablona firmy převzatá od firmy `odesilatel` ze šablony `puvodniId`; "" když taková není.
ClassMethod PrevzataSablona(IdFa As %String, odesilatel As %String, puvodniId As %String) As %String
{
    q:(odesilatel="")||(puvodniId="") ""
    s id = "", nalezena = ""
    f {
        s id = $o(^ALVA.TemplateDesigner.Templates(IdFa,"IDX","ByPuvod",odesilatel,puvodniId,id))
        q:id=""
        s:$d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",id)) nalezena = id
    }
    q nalezena
}

/// Uloží převzatou šablonu jako vlastní šablonu firmy a vrátí její Id: nové, nebo `prepsatId` u šablony, kterou nová
/// verze přepíše. Šablona si pamatuje původ a značku „bez úprav", kterou smaže každé další uložení nebo přejmenování.
ClassMethod WritePrevzata(IdFa As %String, json As %DynamicObject, odesilatel As %String, prepsatId As %String, userID As %String) As %String
{
    s puvodniId = json.id
    s templateId = $s(prepsatId'="":prepsatId, 1:$zcvt($system.Util.CreateGUID(), "L"))
    d json.%Set("id", templateId)
    s name = json.name
    s:name="" name = "Šablona bez názvu"
    d ##class(ALVA.TemplateDesigner.API.Sablony).WriteMetadata(IdFa, templateId, name, ..AgendaSablony(json), json.version, userID)
    d ##class(ALVA.TemplateDesigner.API.Sablony).WriteData(IdFa, templateId, ..ConvertJsonAscii(json.%ToJSON()))
    i (odesilatel '= "") && (puvodniId '= "") {
        s ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodFirma") = odesilatel
        s ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodSablona") = puvodniId
        s ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodBezUprav") = 1
        s ^ALVA.TemplateDesigner.Templates(IdFa,"IDX","ByPuvod",odesilatel,puvodniId,templateId) = ""
    }
    q templateId
}

/// Šablona přestane být převzatou: další zásilka téhož původu ji nenabídne k přepsání.
ClassMethod RemovePuvod(IdFa As %String, templateId As %String)
{
    s firma = $g(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodFirma"))
    s sablona = $g(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodSablona"))
    k:(firma'="")&&(sablona'="") ^ALVA.TemplateDesigner.Templates(IdFa,"IDX","ByPuvod",firma,sablona,templateId)
    k ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodFirma")
    k ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodSablona")
    k ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","PuvodBezUprav")
}

/// JSON se znaky mimo ASCII zapsanými jako \uXXXX, jak ho ukládá návrhář: kus obsahu pak nerozdělí znak a jeho
/// délka na wire odpovídá počtu znaků. Mimo textové hodnoty takové znaky JSON nemá, přepis je bezeztrátový.
ClassMethod ConvertJsonAscii(text As %String) As %String
{
    s vysledek = "", zacatek = 1
    f i = 1:1:$l(text) {
        s kod = $a(text, i)
        continue:kod<128
        s vysledek = vysledek_$e(text, zacatek, i - 1)_"\u"_$tr($j($zhex(kod), 4), " ", "0")
        s zacatek = i + 1
    }
    q vysledek_$e(text, zacatek, $l(text))
}

/// Čas z ISYCloudu (ISO 8601 v UTC) jako místní čas ve tvaru metadat šablon; nečitelný se vrátí, jak přišel.
ClassMethod CasMistni(iso As %String) As %String
{
    q:iso="" ""
    s h = ""
    try { s h = $zdth($tr($e(iso, 1, 19), "T", " "), 3) } catch { s h = "" }
    q:h="" iso
    q $zdt($system.Util.UTCtoLocalWithZTIMEZONE(h), 3)
}

}
