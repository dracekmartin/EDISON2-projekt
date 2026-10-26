Include %occInclude

/// `B194a` C11: chyby v datech linky posílají agendy JŘ jako srozumitelný text (`BaseAgenda.ChybaDat`,
/// obecná chyba 5001), ze kterého návrhář udělá informaci s tím, co opravit. Do 26. 10. přicházelo
/// „LINKA_ID is required" a „Linka 1/0999999 neexistuje v ^JRL".
Class ALVA.TemplateDesigner.Tests.ChybaDatTest Extends ALVA.EDTesting.MLTestCase
{

/// Text chyby dat z ^||ML("ERR"); "" = agenda chybu neposlala, nebo neměla tvar chyby dat.
ClassMethod TextChyby() As %String
{
	set err = $get(^||ML("ERR")), znacka = "ERROR #5001: "
	quit:err'[znacka ""
	quit $piece($piece(err, znacka, 2), $char(13))
}

/// Linka AD, která ve skupině není: zastávkový i linkový JŘ řeknou číslo, skupinu a co dělat.
Method TestLinkaAdMimoJr()
{
	set db = $namespace, sku = "TDTEST"_$translate($system.Util.CreateGUID(), "-")
	do ..SetupSys(.sys)
	set sys("U","DATA") = db
	for agenda = "ZastavkoveJrAd", "LinkovyJrAd" {
		kill ^||ML
		set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = "0999999"
		do $classmethod("ALVA.TemplateDesigner.Agendy."_agenda, "GetData", .sys)
		set text = ..TextChyby()
		do $$$AssertTrue((text [ "Linka 999999 ve skupině "_sku) && (text [ "vyberte linky znovu"), agenda_": "_text)
	}
}

/// Linka MHD, která v modulu MHD není.
Method TestLinkaMhdMimoModul()
{
	do ..SetupSys(.sys, "TDTEST"_$translate($system.Util.CreateGUID(), "-"))
	set sys("U","DATA") = $namespace
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = "999999"
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
	do $$$AssertTrue(..TextChyby() [ "v modulu MHD není", "linkový MHD: "_..TextChyby())
}

/// Bez vybrané linky - všechny čtyři agendy stejnou větou.
Method TestBezLinky()
{
	do ..SetupSys(.sys)
	set sys("U","DATA") = $namespace
	for agenda = "ZastavkoveJrAd", "LinkovyJrAd", "ZastavkoveJrMhd", "LinkovyJrMhd" {
		kill ^||ML
		do $classmethod("ALVA.TemplateDesigner.Agendy."_agenda, "GetData", .sys)
		do $$$AssertEquals(..TextChyby(), "Není vybraná linka. Vyberte linky k tisku.", agenda)
	}
}

}
