Include %occInclude

/// %UnitTest pro ALVA.TemplateDesigner.API.Sablony (CRUD šablon — primární úložiště TD).
/// Vzor "round-trip nad vlastními daty": testy běží v izolované IdFa větvi "EDTESTING"
/// globálu ^ALVA.TemplateDesigner.Templates — nezávislé na živých firemních datech,
/// cleanup před každým testem.
///
/// Chybové cesty: Sablony metody vrací $$$OK i po chybě ($$$CatchErrRest plní `ret`,
/// který zvedá až MLRest HTTP vrstva — in-process není zpráva dostupná). Chyba se proto
/// assertuje přes OBSERVABLE stav: chybějící OUT klíče + nezměněná data v globálu.
Class ALVA.TemplateDesigner.Tests.SablonyTest Extends ALVA.EDTesting.MLTestCase
{

/// Cleanup ^||ML (base) + celé testovací IdFa větve úložiště šablon.
Method OnBeforeOneTest(testname As %String) As %Status
{
	kill ^||ML
	kill ^ALVA.TemplateDesigner.Templates("EDTESTING")
	quit $$$OK
}

/// Malý blob (1 kus) se ukládá a vrací JEDNOKLÍČOVĚ — kompatibilita se staršími klienty
/// bez podpory kusů (#13013: starý klient četl jen OUT("data") → chunked šablona „porušená").
Method TestMalyBlobJednoklicovyProStarehoKlienta()
{
	do ..SetupSys(.sys, "EDTESTING")
	set json = "{""version"":""1.0.0"",""name"":""Kompat"",""sections"":[]}"
	set ^||ML("IN","templateId") = "TEST-COMPAT-1"
	set ^||ML("IN","name")       = "Kompat"
	set ^||ML("IN","agendaCode") = "GENERAL"
	set ^||ML("IN","dataChunkCount") = 1
	set ^||ML("IN","data1")      = json
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-COMPAT-1"
	do ##class(ALVA.TemplateDesigner.API.Sablony).LoadTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","data")), json, "malý blob: Load vrací jednoklíčové data (čte i starý klient)")
	do $$$AssertEquals($data(^||ML("OUT","dataChunkCount")), 0, "malý blob: bez chunk klíčů")
}

/// Vícekusý blob zůstává po kusech (jeden řetězec by překročil limit délky řádku na wire).
Method TestVelkyBlobPoKusech()
{
	do ..SetupSys(.sys, "EDTESTING")
	set ^||ML("IN","templateId") = "TEST-CHUNK-2"
	set ^||ML("IN","name")       = "Velká"
	set ^||ML("IN","agendaCode") = "GENERAL"
	set ^||ML("IN","dataChunkCount") = 2
	set ^||ML("IN","data1")      = "{""version"":""1.0.0"",""name"":""Vel"
	set ^||ML("IN","data2")      = "ká"",""sections"":[]}"
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-CHUNK-2"
	do ##class(ALVA.TemplateDesigner.API.Sablony).LoadTemplate(.sys)
	do $$$AssertEquals(+$get(^||ML("OUT","dataChunkCount")), 2, "velký blob: 2 kusy")
	do $$$AssertEquals($get(^||ML("OUT","data1"))_$get(^||ML("OUT","data2")), "{""version"":""1.0.0"",""name"":""Velká"",""sections"":[]}", "velký blob: kusy round-trip")
	do $$$AssertEquals($data(^||ML("OUT","data"))#2, 0, "velký blob: bez jednoklíčového data")
}

Method TestSaveLoadRoundTrip()
{
	do ..SetupSys(.sys, "EDTESTING")
	set json = "{""version"":""1.0.0"",""name"":""Testovací šablona"",""sections"":[]}"
	set ^||ML("IN","templateId") = "TEST-RT-1"
	set ^||ML("IN","name")       = "Testovací šablona"
	set ^||ML("IN","agendaCode") = "GENERAL"
	set ^||ML("IN","data")       = json
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","templateId")), "TEST-RT-1", "Save vrací echo templateId")

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-RT-1"
	do ##class(ALVA.TemplateDesigner.API.Sablony).LoadTemplate(.sys)
	// Load vrací blob po kusech (data1..N + dataChunkCount; Save kanonizuje i legacy
	// jednoklíčový vstup na 1 kus) — klientská strana kusy slepuje, test dtto
	set chunkCount = +$get(^||ML("OUT","dataChunkCount"))
	set loaded = ""
	if chunkCount {
		for n = 1:1:chunkCount { set loaded = loaded_$get(^||ML("OUT","data"_n)) }
	}
	else {
		set loaded = $get(^||ML("OUT","data"))
	}
	do $$$AssertEquals(loaded, json, "Load vrací byte-identický JSON blob")

	set meta = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT","metadata")))
	do $$$AssertEquals(meta.name, "Testovací šablona", "Metadata: name")
	do $$$AssertEquals(meta.agendaCode, "GENERAL", "Metadata: agendaCode")
	do $$$AssertNotEquals(meta.createdAt, "", "Metadata: createdAt vyplněno")
}

Method TestDeleteSmazeAListJePrazdny()
{
	do ..SetupSys(.sys, "EDTESTING")
	do ..UlozTestovaciSablonu("TEST-DEL-1", "Ke smazání", "GENERAL")

	kill ^||ML
	set ^||ML("IN","agendaCode") = ""
	do ##class(ALVA.TemplateDesigner.API.Sablony).ListTemplates(.sys)
	set arr = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT","1")))
	do $$$AssertEquals(arr.%Size(), 1, "List po Save obsahuje 1 šablonu")

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-DEL-1"
	do ##class(ALVA.TemplateDesigner.API.Sablony).DeleteTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","deleted")), "1", "Delete existující → deleted=1")

	kill ^||ML
	set ^||ML("IN","agendaCode") = ""
	do ##class(ALVA.TemplateDesigner.API.Sablony).ListTemplates(.sys)
	set arr = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT","1")))
	do $$$AssertEquals(arr.%Size(), 0, "List po Delete je prázdný")

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-DEL-1"
	do ##class(ALVA.TemplateDesigner.API.Sablony).LoadTemplate(.sys)
	do $$$AssertEquals($data(^||ML("OUT","data")), 0, "Load smazané šablony nevrací data")

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-DEL-1"
	do ##class(ALVA.TemplateDesigner.API.Sablony).DeleteTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","deleted")), "0", "Delete neexistující → deleted=0 (ne chyba)")
}

Method TestUpdateZachovaCreatedAtAPreindexujeAgendu()
{
	do ..SetupSys(.sys, "EDTESTING")
	do ..UlozTestovaciSablonu("TEST-UPD-1", "Původní", "GENERAL")
	set createdAt = $get(^ALVA.TemplateDesigner.Templates("EDTESTING","DTA","TEST-UPD-1","Metadata","CreatedAt"))
	do $$$AssertNotEquals(createdAt, "", "CreatedAt po prvním Save vyplněno")

	// Update: stejné ID, nové jméno + JINÁ agenda → CreatedAt zůstává, index se přeindexuje
	kill ^||ML
	do ..UlozTestovaciSablonu("TEST-UPD-1", "Přejmenovaná", "ZASTAVKOVY_JR_AD")

	do $$$AssertEquals($get(^ALVA.TemplateDesigner.Templates("EDTESTING","DTA","TEST-UPD-1","Metadata","CreatedAt")), createdAt, "Update zachová CreatedAt")
	do $$$AssertEquals($get(^ALVA.TemplateDesigner.Templates("EDTESTING","DTA","TEST-UPD-1","Metadata","Name")), "Přejmenovaná", "Update přepíše Name")

	kill ^||ML
	set ^||ML("IN","agendaCode") = "GENERAL"
	do ##class(ALVA.TemplateDesigner.API.Sablony).ListTemplates(.sys)
	set arr = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT","1")))
	do $$$AssertEquals(arr.%Size(), 0, "ByAgenda index: stará agenda po přeindexování prázdná")

	kill ^||ML
	set ^||ML("IN","agendaCode") = "ZASTAVKOVY_JR_AD"
	do ##class(ALVA.TemplateDesigner.API.Sablony).ListTemplates(.sys)
	set arr = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT","1")))
	do $$$AssertEquals(arr.%Size(), 1, "ByAgenda index: nová agenda obsahuje šablonu")
}

Method TestRenameZmeniJenName()
{
	do ..SetupSys(.sys, "EDTESTING")
	do ..UlozTestovaciSablonu("TEST-REN-1", "Před", "GENERAL")

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-REN-1"
	set ^||ML("IN","newName")    = "Po přejmenování"
	do ##class(ALVA.TemplateDesigner.API.Sablony).RenameTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","renamed")), "1", "Rename → renamed=1")

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-REN-1"
	do ##class(ALVA.TemplateDesigner.API.Sablony).LoadTemplate(.sys)
	set meta = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT","metadata")))
	do $$$AssertEquals(meta.name, "Po přejmenování", "Load vrací nové jméno")
	do $$$AssertEquals(meta.agendaCode, "GENERAL", "Agenda nedotčena")

	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-REN-NEEXISTUJE"
	set ^||ML("IN","newName")    = "x"
	do ##class(ALVA.TemplateDesigner.API.Sablony).RenameTemplate(.sys)
	do $$$AssertEquals($data(^||ML("OUT","renamed")), 0, "Rename neexistující nevrací renamed=1")
}

Method TestSaveValidacePovinnychPoli()
{
	do ..SetupSys(.sys, "EDTESTING")

	// Bez templateId → Save neuloží (OUT echo chybí)
	set ^||ML("IN","name") = "x", ^||ML("IN","agendaCode") = "GENERAL", ^||ML("IN","data") = "{}"
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)
	do $$$AssertEquals($data(^||ML("OUT","templateId")), 0, "Save bez templateId nevrací echo (chyba)")

	// Bez data → Save neuloží a nic nezapíše do globálu
	kill ^||ML
	set ^||ML("IN","templateId") = "TEST-VAL-1", ^||ML("IN","name") = "x", ^||ML("IN","agendaCode") = "GENERAL"
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)
	do $$$AssertEquals($data(^||ML("OUT","templateId")), 0, "Save bez data nevrací echo (chyba)")
	do $$$AssertEquals($data(^ALVA.TemplateDesigner.Templates("EDTESTING","DTA","TEST-VAL-1")), 0, "Nevalidní Save nic nezapsal")
}

/// Helper: uloží šablonu s minimálním validním JSON přes SaveTemplate API.
Method UlozTestovaciSablonu(templateId As %String, name As %String, agendaCode As %String)
{
	kill ^||ML
	do ..SetupSys(.sys, "EDTESTING")
	set ^||ML("IN","templateId") = templateId
	set ^||ML("IN","name")       = name
	set ^||ML("IN","agendaCode") = agendaCode
	set ^||ML("IN","data")       = "{""version"":""1.0.0"",""name"":"""_name_""",""sections"":[]}"
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)
}

}
