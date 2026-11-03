Include %occInclude

/// %UnitTest pro posílání šablon jiným firmám (`API.SdileniSablon`, `B189`) - část bez ISYCloudu: co se pošle
/// a co ne, jak se přijatá šablona uloží a pozná při další zásilce. Spojení s ISYCloudem tu není, celý tok se
/// ověřuje proti testovacímu ISYCloudu.
/// Firma = větev "EDTESTING", M-line = větev "EDTESTINGML", jako `SablonyProTiskTest`.
Class ALVA.TemplateDesigner.Tests.SdileniSablonTest Extends ALVA.EDTesting.MLTestCase
{

Parameter Firma = "EDTESTING";

Parameter Mline = "EDTESTINGML";

Method OnBeforeOneTest(testname As %String) As %Status
{
	kill ^||ML
	kill ^ALVA.TemplateDesigner.Templates(..#Firma)
	kill ^ALVA.TemplateDesigner.Templates(..#Mline)
	quit $$$OK
}

Method OnAfterOneTest(testname As %String) As %Status
{
	kill ^ALVA.TemplateDesigner.Templates(..#Firma)
	kill ^ALVA.TemplateDesigner.Templates(..#Mline)
	quit $$$OK
}

/// Výběr typů provozu linkového JŘ MHD má v názvu číslo typu firmy - příjemci by vypnul jiný typ.
/// Ostatní uložené volby šablona nese dál.
Method TestHodnotyFirmySeNeposilaji()
{
	do ..Uloz(..#Firma, "FA-MHD", "Linky MHD", "LINKOVY_JR_MHD", "{""TYP_PROVOZU_3"":""false"",""TYP_PROVOZU_12"":""true"",""SMER_TISKU"":""Oba směry""}")
	set json = ##class(ALVA.TemplateDesigner.API.SdileniSablon).ObsahKOdeslani(..#Firma, "FA-MHD")
	do $$$AssertEquals(json.printParameters.%IsDefined("TYP_PROVOZU_3"), 0, "typ provozu firmy se nepošle")
	do $$$AssertEquals(json.printParameters.%IsDefined("TYP_PROVOZU_12"), 0, "ani jiný typ provozu")
	do $$$AssertEquals(json.printParameters.%Get("SMER_TISKU"), "Oba směry", "ostatní volba zůstala")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaJson(..#Firma, "FA-MHD").printParameters.%IsDefined("TYP_PROVOZU_3"), 1, "uložená šablona se nemění")
}

/// Agenda bez hodnot platných jen ve firmě pošle šablonu, jak je.
Method TestAgendaBezHodnotFirmy()
{
	do ..Uloz(..#Firma, "FA-AD", "Linky AD", "LINKOVY_JR_AD", "{""TYP_PROVOZU_3"":""false"",""SMER_TISKU"":""Tam""}")
	set json = ##class(ALVA.TemplateDesigner.API.SdileniSablon).ObsahKOdeslani(..#Firma, "FA-AD")
	do $$$AssertEquals(json.printParameters.%Get("TYP_PROVOZU_3"), "false", "linkový JŘ AD předponu nemá")
	do $$$AssertEquals(json.name, "Linky AD", "jméno šablony")
}

/// Šablonu M-line mají všechny firmy, poslat ji nejde.
Method TestSablonaMlineSeNeposila()
{
	do ..Uloz(..#Mline, "ML-1", "Šablona M-line", "LINKOVY_JR_AD")
	set chyba = ""
	try { do ##class(ALVA.TemplateDesigner.API.SdileniSablon).ObsahKOdeslani(..#Firma, "ML-1") }
	catch ex { set chyba = ex.DisplayString() }
	do $$$AssertTrue(chyba [ "jen vlastní šablonu firmy", "šablona M-line: "_chyba)
}

Method TestNazevZasilky()
{
	for i = 1:1:5 set sablony(i) = {"name": ("Šablona "_i)}
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).NazevZasilky(.sablony, 2, 2), "Šablona 2", "jedna šablona: její jméno")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).NazevZasilky(.sablony, 1, 3), "3 šablony", "tři")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).NazevZasilky(.sablony, 1, 5), "5 šablon", "pět")
}

/// Bez příjemce nebo bez šablony se nic neposílá a ISYCloud se nevolá.
Method TestOdeslaniBezVyberu()
{
	do $$$AssertTrue(..Odeslat("FA-1", "") [ "Vyberte firmu", "bez příjemce")
	do $$$AssertTrue(..Odeslat(" , ", "CB_JINA") [ "Vyberte šablonu", "bez šablony")
}

/// Okno šablon se podle stromu rozhodne, jestli nabídne posílání šablon.
Method TestStromPosilaSdileni()
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	do ##class(ALVA.TemplateDesigner.API.StromSablon).GetStrom(.sys)
	set strom = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT","1")))
	do $$$AssertEquals(strom.%GetTypeOf("sdileni"), "boolean", "GetStrom posílá sdileni jako ano/ne")
	kill ^||ML
}

// ── Příjem ──

/// Převzatá šablona je vlastní šablonou firmy s novým Id (i uvnitř obsahu) a pamatuje si, od koho a z čeho je.
Method TestPrevzetiNoveSablony()
{
	set json = ..Prijata("PUV-1", "Od kolegů", "LINKOVY_JR_AD")
	set id = ##class(ALVA.TemplateDesigner.API.SdileniSablon).WritePrevzata(..#Firma, json, "CB_ODESILATEL", "", "test")
	do $$$AssertTrue((id '= "") && (id '= "PUV-1"), "nové Id: "_id)
	set ulozena = ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaJson(..#Firma, id)
	do $$$AssertEquals(ulozena.id, id, "Id v obsahu = Id šablony")
	do $$$AssertEquals(ulozena.name, "Od kolegů", "název")
	do $$$AssertEquals($get(^ALVA.TemplateDesigner.Templates(..#Firma,"DTA",id,"Metadata","Name")), "Od kolegů", "metadata")
	do $$$AssertTrue($data(^ALVA.TemplateDesigner.Templates(..#Firma,"IDX","ByAgenda","LINKOVY_JR_AD",id)), "index agendy")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).PrevzataSablona(..#Firma, "CB_ODESILATEL", "PUV-1"), id, "původ")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).PrevzataSablona(..#Firma, "CB_JINA", "PUV-1"), "", "jiná firma téže šablony není týž původ")
}

/// Nová verze téhož původu přepíše převzatou šablonu: Id zůstane, obsah je nový, šablona je zase bez úprav.
Method TestPrepsaniPrevzate()
{
	set id = ##class(ALVA.TemplateDesigner.API.SdileniSablon).WritePrevzata(..#Firma, ..Prijata("PUV-1", "Verze 1", "LINKOVY_JR_AD"), "CB_ODESILATEL", "", "test")
	do ##class(ALVA.TemplateDesigner.API.Sablony).WriteMetadata(..#Firma, id, "Upravená", "LINKOVY_JR_AD", "1.0.0", "test")
	do $$$AssertTrue(..JeUpravena(id), "uložení firmy šablonu označí jako upravenou")
	set id2 = ##class(ALVA.TemplateDesigner.API.SdileniSablon).WritePrevzata(..#Firma, ..Prijata("PUV-1", "Verze 2", "LINKOVY_JR_AD"), "CB_ODESILATEL", id, "test")
	do $$$AssertEquals(id2, id, "přepsání drží Id")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaJson(..#Firma, id).name, "Verze 2", "obsah je nový")
	do $$$AssertNotTrue(..JeUpravena(id), "po přepsání bez úprav")
}

/// Přejmenování v okně šablon je úprava - převzatá šablona se pak nepřepíše bez dotazu.
Method TestPrejmenovaniJeUprava()
{
	set id = ##class(ALVA.TemplateDesigner.API.SdileniSablon).WritePrevzata(..#Firma, ..Prijata("PUV-1", "Od kolegů", "LINKOVY_JR_AD"), "CB_ODESILATEL", "", "test")
	do $$$AssertNotTrue(..JeUpravena(id), "čerstvě převzatá")
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateId") = id
	set ^||ML("IN","newName") = "Naše"
	do ##class(ALVA.TemplateDesigner.API.Sablony).RenameTemplate(.sys)
	kill ^||ML
	do $$$AssertTrue(..JeUpravena(id), "přejmenovaná")
}

/// Uložení jako nové přenese původ na novou kopii; odstraněná šablona původ nemá.
Method TestPuvodPoUlozeniJakoNoveAOdstraneni()
{
	set stara = ##class(ALVA.TemplateDesigner.API.SdileniSablon).WritePrevzata(..#Firma, ..Prijata("PUV-1", "Stará", "LINKOVY_JR_AD"), "CB_ODESILATEL", "", "test")
	do ##class(ALVA.TemplateDesigner.API.SdileniSablon).RemovePuvod(..#Firma, stara)
	set nova = ##class(ALVA.TemplateDesigner.API.SdileniSablon).WritePrevzata(..#Firma, ..Prijata("PUV-1", "Nová", "LINKOVY_JR_AD"), "CB_ODESILATEL", "", "test")
	do $$$AssertTrue(stara '= nova, "kopie má vlastní Id")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).PrevzataSablona(..#Firma, "CB_ODESILATEL", "PUV-1"), nova, "původ má nová kopie")
	do $$$AssertEquals($get(^ALVA.TemplateDesigner.Templates(..#Firma,"DTA",stara,"Metadata","PuvodFirma")), "", "stará je obyčejná vlastní šablona")

	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateId") = nova
	do ##class(ALVA.TemplateDesigner.API.Sablony).DeleteTemplate(.sys)
	kill ^||ML
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).PrevzataSablona(..#Firma, "CB_ODESILATEL", "PUV-1"), "", "odstraněná šablona původ nemá")
	do $$$AssertNotTrue($data(^ALVA.TemplateDesigner.Templates(..#Firma,"IDX","ByPuvod","CB_ODESILATEL","PUV-1",nova)), "index původu je pryč")
}

/// Velká šablona s češtinou se uloží po kusech v ASCII jako z návrháře a přečte se beze ztráty.
Method TestVelkaSablonaPoKusech()
{
	set text = "", kus = "Žluťoučký kůň úpěl ďábelské ódy. "
	for i = 1:1:2000 set text = text_kus
	set json = ..Prijata("PUV-VELKA", "Velká", "LINKOVY_JR_AD")
	set json.description = text
	set id = ##class(ALVA.TemplateDesigner.API.SdileniSablon).WritePrevzata(..#Firma, json, "CB_ODESILATEL", "", "test")
	set pocet = +$get(^ALVA.TemplateDesigner.Templates(..#Firma,"DTA",id,"Data"))
	do $$$AssertTrue(pocet > 1, "po kusech: "_pocet)
	set ascii = 1, nejdelsi = 0
	for n = 1:1:pocet {
		set kusDat = $get(^ALVA.TemplateDesigner.Templates(..#Firma,"DTA",id,"Data",n))
		set:$length(kusDat)>nejdelsi nejdelsi = $length(kusDat)
		for z = 1:1:$length(kusDat) if $ascii(kusDat, z) > 127 set ascii = 0 quit
	}
	do $$$AssertTrue(nejdelsi <= $parameter("ALVA.TemplateDesigner.API.Sablony", "MaxZnakuKusu"), "kus pod limitem: "_nejdelsi)
	do $$$AssertTrue(ascii, "obsah je v ASCII")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaJson(..#Firma, id).description, text, "čeština beze ztráty")
}

Method TestJsonAscii()
{
	// Zpětné lomítko přes $c(92): text „\u“ se čtyřmi číslicemi přímo ve zdrojáku se při nasazení změní na znak.
	set ascii = ##class(ALVA.TemplateDesigner.API.SdileniSablon).ConvertJsonAscii("{""a"":""Ř1""}")
	do $$$AssertEquals(ascii, "{""a"":"""_$char(92)_"u01581""}", "znak mimo ASCII: "_ascii)
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).ConvertJsonAscii("abc"), "abc", "ASCII beze změny")
	set obnoveno = ##class(%DynamicObject).%FromJSON(##class(ALVA.TemplateDesigner.API.SdileniSablon).ConvertJsonAscii({"a": "Příliš žluťoučký"}.%ToJSON()))
	do $$$AssertEquals(obnoveno.a, "Příliš žluťoučký", "JSON se přečte zpátky")
}

/// Agendu bez třídy na serveru převzít nejde; Obecná třídu nemá a jde vždy.
Method TestZnamaAgenda()
{
	do $$$AssertTrue(##class(ALVA.TemplateDesigner.API.SdileniSablon).JeZnamaAgenda("LINKOVY_JR_AD"), "linkový JŘ AD")
	do $$$AssertTrue(##class(ALVA.TemplateDesigner.API.SdileniSablon).JeZnamaAgenda("GENERAL"), "Obecná")
	do $$$AssertNotTrue(##class(ALVA.TemplateDesigner.API.SdileniSablon).JeZnamaAgenda("NEEXISTUJE_X"), "neznámá")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).AgendaSablony({"agendaCode": ""}), "GENERAL", "bez agendy = Obecná")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).AgendaSablony({"agendaCode": "MHD"}), "ZASTAVKOVY_JR_MHD", "starý kód MHD")
}

/// Soubor zásilky, který není šablonou, je chyba pro uživatele s názvem souboru.
Method TestSouborNeniSablona()
{
	for obsah = "není JSON", "[1,2]", "{""name"":""bez sekcí""}" {
		set chyba = ""
		try { do ##class(ALVA.TemplateDesigner.API.SdileniSablon).ParseSablona(obsah, "x.json") }
		catch ex { set chyba = ex.DisplayString() }
		do $$$AssertTrue(chyba [ "„x.json“ v zásilce není šablona", obsah_": "_chyba)
	}
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).ParseSablona("{""name"":""a"",""sections"":[]}", "x.json").name, "a", "šablona projde")
}

/// Převzít jde jen nevyřízenou zásilku šablon; Id zásilky jde do adresy, jiné znaky neprojdou.
Method TestKontrolaZasilky()
{
	do $$$AssertEquals(..Chyba({"typ": "SABLONA", "mujStav": {"stav": "nova"}}), "", "nová")
	do $$$AssertEquals(..Chyba({"typ": "SABLONA", "mujStav": {"stav": "stazena"}}), "", "stažená")
	do $$$AssertTrue(..Chyba({"typ": "SABLONA", "mujStav": {"stav": "nactena"}}) [ "už je vyřízená", "načtená")
	do $$$AssertTrue(..Chyba({"typ": "ISYJR", "mujStav": {"stav": "nova"}}) [ "neobsahuje šablony", "jiný typ dat")
	set chyba = ""
	try { do ##class(ALVA.TemplateDesigner.API.SdileniSablon).CheckZasilkaId("../ja") } catch ex { set chyba = ex.DisplayString() }
	do $$$AssertTrue(chyba [ "není vybraná", "cesta v Id: "_chyba)
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).CheckZasilkaId("3b3e0054-ad0e-4184-add7-ec2cf736df4d"), "3b3e0054-ad0e-4184-add7-ec2cf736df4d", "Id z ISYCloudu")
}

/// Řádek zásilky v okně šablon z uložených údajů o šablonách (bez stahování): název, agenda, odesílatel, příznak.
Method TestRadekZasilky()
{
	set ^ALVA.TemplateDesigner.Templates(..#Firma,"PRIJATE","Z1","S1") = $listbuild("Linky A4", "LINKOVY_JR_AD", "1.18.0", "PUV-1")
	set ^ALVA.TemplateDesigner.Templates(..#Firma,"PRIJATE","Z1","S2") = $listbuild("Linky A5", "LINKOVY_JR_AD", "1.18.0", "PUV-2")
	set zasilka = {"id": "Z1", "nazev": "2 šablony", "odesilatel": {"idfa": "CB_ODESILATEL", "nazev": "Odesílatel s.r.o."}, "odeslal": "novak", "zprava": "Posíláme", "odeslano": "2026-11-03T17:47:31.487Z", "soubory": [{"id": "S1", "nazev": "S1.json"}, {"id": "S2", "nazev": "S2.json"}]}
	set list = ##class(ALVA.EDControls.EDList.List).%New()
	do ##class(ALVA.TemplateDesigner.API.SdileniSablon).AddPrijataRow(list, ..#Firma, zasilka)
	set row = list.rows.%Get(0)
	do $$$AssertEquals(row.id, "Z1", "Id řádku = zásilka")
	do $$$AssertEquals(row.name, "2 šablony", "název zásilky")
	do $$$AssertEquals(row.agenda_" · "_row.varianta, "Linkový JŘ · AD", "jedna agenda: agenda a varianta")
	do $$$AssertEquals(row.zdroj, "Odesílatel s.r.o.", "odesílatel")
	do $$$AssertEquals(row.prijata, "1", "příznak přijaté")
	do $$$AssertEquals(row.sablony, "Linky A4, Linky A5", "názvy šablon")
	do $$$AssertEquals(row.zprava, "Posíláme", "zpráva")
	do $$$AssertTrue(row.modifiedAt ? 4N1"-"2N1"-"2N1" "2N1":"2N1":"2N, "místní čas: "_row.modifiedAt)

	set ^ALVA.TemplateDesigner.Templates(..#Firma,"PRIJATE","Z1","S2") = $listbuild("Obecná", "GENERAL", "1.18.0", "PUV-2")
	set list = ##class(ALVA.EDControls.EDList.List).%New()
	do ##class(ALVA.TemplateDesigner.API.SdileniSablon).AddPrijataRow(list, ..#Firma, zasilka)
	set row = list.rows.%Get(0)
	do $$$AssertEquals(row.agenda, "Linkový JŘ · AD, Obecná (bez dat)", "víc agend čárkou")
	do $$$AssertEquals(row.varianta, "", "bez varianty")
}

/// Firma, která sdílení nikdy nepoužila (nemá klíč), dostane nulu bez volání ISYCloudu.
Method TestSouhrnBezKlice()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Sdileni.Klient).Klic(..#Firma), "", "testovací firma klíč nemá")
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	do ##class(ALVA.TemplateDesigner.API.SdileniSablon).GetSouhrnPrijatych(.sys)
	do $$$AssertEquals($get(^||ML("ERR")), "", "bez chyby")
	do $$$AssertEquals(##class(%DynamicObject).%FromJSON($get(^||ML("OUT","1"))).pocet, 0, "nula")
	kill ^||ML
}

Method TestCasMistni()
{
	set cas = ##class(ALVA.TemplateDesigner.API.SdileniSablon).CasMistni("2026-11-03T17:47:31.487Z")
	do $$$AssertTrue(cas ? 1"2026-11-03 "2N1":47:31", "tvar metadat: "_cas)
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).CasMistni("nesmysl"), "nesmysl", "nečitelný beze změny")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).CasMistni(""), "", "prázdný")
}

/// JSON zásilky jde do požadavku jako text: na UTF-8 ho převede %Net.HttpRequest. Převedený předem dorazil
/// do ISYCloudu převedený dvakrát (název a zpráva zásilky s rozbitou češtinou, 3. 11.).
Method TestTeloJsonJednouVUtf8()
{
	set req = ##class(%Net.HttpRequest).%New()
	set telo = {"nazev": "Linkový JŘ", "zprava": "žluťoučký kůň"}
	do ##class(ALVA.TemplateDesigner.Sdileni.Klient).WriteTelo(req, telo)
	do req.EntityBody.Rewind()
	do $$$AssertEquals(req.EntityBody.Read(32000), telo.%ToJSON(), "tělo je text JSON, ne předem převedené bajty")
	do $$$AssertTrue(req.ContentType [ "charset=utf-8", "převod na UTF-8 řídí ContentType")
}

/// Šablona, jak ji pošle jiná firma.
Method Prijata(id As %String, name As %String, agendaCode As %String) As %DynamicObject
{
	quit {"version": "1.18.0", "id": (id), "name": (name), "agendaCode": (agendaCode), "printParameters": {}, "sections": []}
}

Method JeUpravena(templateId As %String) As %Boolean
{
	quit '$data(^ALVA.TemplateDesigner.Templates(..#Firma,"DTA",templateId,"Metadata","PuvodBezUprav"))
}

/// Chyba kontroly zásilky před převzetím; "" když projde.
Method Chyba(zasilka As %DynamicObject) As %String
{
	set chyba = ""
	try { do ##class(ALVA.TemplateDesigner.API.SdileniSablon).CheckKPrevzeti(zasilka) } catch ex { set chyba = ex.DisplayString() }
	quit chyba
}

/// Chyba odeslání; "" když prošlo.
Method Odeslat(templateIds As %String, prijemci As %String) As %String
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateIds") = templateIds
	set ^||ML("IN","prijemci") = prijemci
	do ##class(ALVA.TemplateDesigner.API.SdileniSablon).SendSablony(.sys)
	set chyba = $get(^||ML("ERR"))
	kill ^||ML
	quit chyba
}

Method Uloz(vetev As %String, templateId As %String, name As %String, agendaCode As %String, printParameters As %String = "{}")
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateId") = templateId
	set ^||ML("IN","name")       = name
	set ^||ML("IN","agendaCode") = agendaCode
	set ^||ML("IN","targetIdFa") = vetev
	set ^||ML("IN","data")       = "{""version"":""1.0.0"",""id"":"""_templateId_""",""name"":"""_name_""",""agendaCode"":"""_agendaCode_""",""printParameters"":"_printParameters_",""sections"":[]}"
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","templateId")), templateId, "uložení testovací šablony "_templateId)
	kill ^||ML
}

}
