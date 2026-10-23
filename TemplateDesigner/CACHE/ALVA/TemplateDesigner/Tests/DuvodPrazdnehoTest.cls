Include %occInclude

/// `B193g`: linka bez jediné stránky pošle ve všech agendách JŘ důvod a radu (`DUVOD_PRAZDNE`),
/// ze kterého návrhář udělá hlášku „nemá data, nevytiskne se". Do 23. 10. to uměl jen linkový JŘ MHD;
/// linkový AD i vývěska AD vrátily prázdno a vývěska MHD u linky bez trasy spadla na chybu, která
/// by při tisku víc linek zastavila i ty s daty. Data si test zakládá sám pod jedinečným jménem
/// a po sobě je smaže; linkový JŘ MHD kryje `LinkovyJrMhdTest.TestDuvodPrazdnehoVystupu`.
Class ALVA.TemplateDesigner.Tests.DuvodPrazdnehoTest Extends ALVA.EDTesting.MLTestCase
{

/// Sběr důvodů: stejný jen jednou, pošlou se jen bez jediné stránky, bez zapsaného důvodu výchozí.
Method TestSberDuvodu()
{
	set base = "ALVA.TemplateDesigner.Agendy.BaseAgenda"
	kill ^||ML
	do $classmethod(base, "ZacitDuvodyPrazdne")
	do $$$AssertEquals($classmethod(base, "PrazdnaStranka", "A."), "", "vrací prázdnou stránku")
	do $classmethod(base, "PrazdnaStranka", "A.")
	do $classmethod(base, "PrazdnaStranka", "B.")
	do $classmethod(base, "VydatDuvodPrazdne", "výchozí")
	do $$$AssertEquals($get(^||ML("OUT","DUVOD_PRAZDNE")), "A. B.", "důvody v pořadí, každý jednou")

	kill ^||ML
	do $classmethod(base, "ZacitDuvodyPrazdne")
	do $classmethod(base, "PrazdnaStranka", "A.")
	set ^||ML("OUT",1) = "{}"
	do $classmethod(base, "VydatDuvodPrazdne", "výchozí")
	do $$$AssertTrue('$data(^||ML("OUT","DUVOD_PRAZDNE")), "se stránkou žádný důvod")

	kill ^||ML
	do $classmethod(base, "ZacitDuvodyPrazdne")
	do $classmethod(base, "VydatDuvodPrazdne", "výchozí")
	do $$$AssertEquals($get(^||ML("OUT","DUVOD_PRAZDNE")), "výchozí", "bez zapsaného důvodu výchozí")
}

/// Linka AD bez spojů: linkový, rozdílový i vývěska pošlou důvod místo prázdna. Rozdílový do 23. 10.
/// četl důvod linkového JŘ jako stránku a spadl.
Method TestLinkaAdBezSpoju()
{
	set db = $namespace, sku = "TDTEST"_$translate($system.Util.CreateGUID(), "-"), lin = "0999999"
	try {
		set ^[db]JRL(sku, lin, "I") = "", ^[db]JRL(sku, lin, "I", 1) = "Zkušební linka"
		do ..SetupSys(.sys)
		set sys("U","DATA") = db
		for agenda = "LinkovyJrAd", "LinkovyJrAdRozdilovy", "ZastavkoveJrAd" {
			kill ^||ML
			set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
			do $classmethod("ALVA.TemplateDesigner.Agendy."_agenda, "GetData", .sys)
			do $$$AssertEquals($get(^||ML("ERR")), "", agenda_": bez chyby "_$get(^||ML("ERR")))
			do $$$AssertTrue($get(^||ML("OUT","DUVOD_PRAZDNE")) [ "žádné spoje", agenda_": "_$get(^||ML("OUT","DUVOD_PRAZDNE"), "(bez důvodu)"))
		}
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]JRL(sku)
}

/// Rozdílový: původní verze bez spojů porovnání nezastaví - nová verze se vytiskne a hlášení
/// porovnání řekne proč. Nová verze je linka z jádra sady, původní prázdná kopie pod jedinečnou skupinou.
Method TestRozdilovyPuvodniBezSpoju()
{
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro)
	set db = $piece(jadro(1),"~",1), sku = $piece(jadro(1),"~",2), lin = $piece(jadro(1),"~",3)
	if '##class(%SYS.Namespace).Exists(db) || '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertSkipped("linka "_lin_" z jádra sady není na tomto serveru")
		quit
	}
	set skuPuvodni = "TDTEST"_$translate($system.Util.CreateGUID(), "-")
	try {
		set ^[db]JRL(skuPuvodni, lin, "I") = ""
		do ..SetupSys(.sys)
		set sys("U","DATA") = db
		kill ^||ML
		set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin, ^||ML("IN","POROVNAT_SKUPINU") = skuPuvodni
		do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy).GetData(.sys)
		do $$$AssertEquals($get(^||ML("ERR")), "", "bez chyby "_$get(^||ML("ERR")))
		do $$$AssertTrue($data(^||ML("OUT",1)), "nová verze se vytiskne")
		set stranka = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT",1), "{}"))
		set hlaseni = $select($isobject(stranka.values):stranka.values.%Get("POROVNANI_HLASENI"), 1:"")
		do $$$AssertTrue((hlaseni [ "nemá data") && (hlaseni [ "žádné spoje"), "hlášení: "_hlaseni)
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]JRL(skuPuvodni)
}

/// Linka MHD bez trasy: vývěska pošle důvod (do 23. 10. chyba, která zastavila tisk ostatních linek).
Method TestVyveskaMhdBezTrasy()
{
	set db = $namespace, idfa = "TDTEST"_$translate($system.Util.CreateGUID(), "-")
	try {
		set g = $name(^[db]ALVA.MHD.LSO(idfa, 1, "DTA", 7, "DTA", 1))
		set @g@("PlatiOd") = "2026-01-01", @g@("PlatiDo") = "2026-12-31", @g@("NazevLinky") = "Zkušební"
		do ..SetupSys(.sys, idfa)
		set sys("U","DATA") = db
		kill ^||ML
		set ^||ML("IN","LINKA_ID") = 7
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
		do $$$AssertEquals($get(^||ML("ERR")), "", "bez chyby")
		do $$$AssertTrue($get(^||ML("OUT","DUVOD_PRAZDNE")) [ "trasu", "důvod: "_$get(^||ML("OUT","DUVOD_PRAZDNE"), "(bez důvodu)"))
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]ALVA.MHD.LSO(idfa)
}

}
