Include %occInclude

/// Linky, na kterých testy stojí, musí na serveru existovat. Test linku, kterou v datech nenajde,
/// přeskočí stejně jako firmu, která na serveru není (sada nesmí padat kvůli tomu, kde běží), takže
/// smazaná linka tiše vypne všechno, co na ní stálo. Tak zmizely CB_GWBUS 0320118 a 0320113 a všech
/// 268 testů dál hlásilo úspěch (zjištěno 30. 10.), ačkoli linkový JŘ AD tím přišel o jedinou linku
/// s lichými tarifními čísly, stálými značkami a poznámkami linky.
///
/// Test projde zdrojáky všech tříd balíčku a vezme z nich linky ve tvarech z `Tvary`. Linka ve firmě,
/// která na serveru není, se nekontroluje. Náhradu za zmizelou linku vyber měřením
/// (`LinkovyJrSadaTest.Profily`, `AdSloupekProbe.ProfilyLinek`), ne odhadem.
Class ALVA.TemplateDesigner.Tests.TestovaciLinkyTest Extends ALVA.EDTesting.MLTestCase
{

/// Každá linka z testů, jejíž firma na serveru je, je v `^JRL`.
Method TestLinkyTestuJsouNaServeru()
{
	do ..Linky(.linky, .tvary)
	set chybi = "", klic = ""
	for {
		set klic = $order(linky(klic))
		quit:klic=""
		set db = $piece(klic, "~", 1), sku = $piece(klic, "~", 2), lin = $piece(klic, "~", 3)
		continue:'##class(%SYS.Namespace).Exists(db)
		continue:$data(^[db]JRL(sku, lin, "I"))
		set chybi = chybi _ $select(chybi="":"", 1:"; ") _ db _ " " _ sku _ "/" _ lin _ " (" _ linky(klic) _ ")"
	}
	do $$$AssertEquals(chybi, "", "linky z testů v datech nejsou, testy na nich se tiše přeskakují: "_chybi)
}

/// Hledání musí najít linky sad a každý tvar zápisu aspoň jednou. Jinak by změna zápisu v testech
/// hlídání tiše vypnula a předchozí test by procházel nad prázdným seznamem.
Method TestHledaniNajdeLinkySad()
{
	do ..Linky(.linky, .tvary)
	do ..Tvary(.vyrazy)
	for t = 1:1:vyrazy {
		do $$$AssertTrue($get(tvary(t)) > 0, "tvar zápisu linky č. "_t_" se ve zdrojácích nenašel: "_vyrazy(t))
	}
	set nenalezeno = ""
	for trida = "ZastavkoveJrAdSadaTest", "LinkovyJrSadaTest" {
		for metoda = "Sada", "Jadro" {
			do $classmethod("ALVA.TemplateDesigner.Tests."_trida, metoda, .sada)
			for i = 1:1:$get(sada) {
				set klic = ..Klic($piece(sada(i), "~", 1), $piece(sada(i), "~", 2), $piece(sada(i), "~", 3))
				continue:$data(linky(klic))
				set nenalezeno = nenalezeno _ $select(nenalezeno="":"", 1:", ") _ trida _ "." _ metoda _ " " _ klic
			}
		}
	}
	do $$$AssertEquals(nenalezeno, "", "hledání ve zdrojácích nenašlo linky sad: "_nenalezeno)
}

/// Tvary, kterými se linka v testech zapisuje (regulární výrazy pro `$locate`). Firma = velká písmena
/// s podtržítkem, skupina a linka jsou první dvě čísla za ní.
ClassMethod Tvary(ByRef vyrazy)
{
	kill vyrazy
	// Položka sady a parametr třídy: "CB_GWBUS~1~0320118~typy"
	set vyrazy($increment(vyrazy)) = "[A-Z]+_[A-Z_]+~[0-9]+~[0-9]{3,7}"
	// Jedna linka v proměnných: set db = "CB_GWBUS", sku = 216, lin = "0340941"
	set vyrazy($increment(vyrazy)) = "db = ""[A-Z]+_[A-Z_]+"", sku = [0-9]+, lin = ""?[0-9]{3,7}"
	// Porovnání zón se starým modulem: AssertZony("KV_DP", 1, 410101, 1)
	set vyrazy($increment(vyrazy)) = "AssertZony\(""[A-Z]+_[A-Z_]+"", [0-9]+, [0-9]{3,7}"
}

/// Linky ze zdrojáků tříd balíčku: `linky("FIRMA~skupina~linka")` = třídy, kde se vyskytují,
/// `tvary(n)` = kolikrát se našel n-tý tvar zápisu.
ClassMethod Linky(ByRef linky, ByRef tvary)
{
	kill linky, tvary
	do ..Tvary(.vyrazy)
	set rs = ##class(%SQL.Statement).%ExecDirect(, "SELECT ID FROM %Dictionary.ClassDefinition WHERE ID %STARTSWITH ? ORDER BY ID", "ALVA.TemplateDesigner.Tests.")
	if rs.%SQLCODE < 0 {
		$$$ThrowStatus($$$ERROR($$$GeneralError, "výpis tříd balíčku selhal: "_rs.%Message))
	}
	while rs.%Next() {
		set trida = rs.%Get("ID")
		set def = ##class(%Dictionary.ClassDefinition).%OpenId(trida)
		continue:'$isobject(def)
		set kratce = $piece(trida, ".", *)
		for i = 1:1:def.Parameters.Count() {
			do ..LinkyZTextu(def.Parameters.GetAt(i).Default, kratce, .vyrazy, .linky, .tvary)
		}
		for i = 1:1:def.Methods.Count() {
			set impl = def.Methods.GetAt(i).Implementation
			do impl.Rewind()
			while 'impl.AtEnd {
				set radek = impl.ReadLine()
				// Odkaz na linku v komentáři (původ nálezu, oracle) není podklad testu.
				continue:$extract($zstrip(radek,"<W"),1,2)="//"
				do ..LinkyZTextu(radek, kratce, .vyrazy, .linky, .tvary)
			}
		}
	}
}

ClassMethod LinkyZTextu(text As %String, trida As %String, ByRef vyrazy, ByRef linky, ByRef tvary)
{
	for t = 1:1:vyrazy {
		set od = 1
		for {
			set od = $locate(text, vyrazy(t), od, konec, nalez)
			quit:'od
			set od = konec
			set tvary(t) = $get(tvary(t)) + 1
			set slova = $translate(nalez, "~""(),=", "      ")
			set db = "", cisla = 0
			for s = 1:1:$length(slova, " ") {
				set slovo = $piece(slova, " ", s)
				continue:slovo=""
				if (db = "") && (slovo [ "_") { set db = slovo  continue }
				if (db '= "") && (slovo ? 1.N) set cisla = cisla + 1, cisla(cisla) = slovo
			}
			continue:(db="")||(cisla<2)
			set klic = ..Klic(db, cisla(1), cisla(2))
			set:(","_$get(linky(klic))_",")'[(","_trida_",") linky(klic) = $get(linky(klic)) _ $select($get(linky(klic))="":"", 1:",") _ trida
		}
	}
}

/// Klíč linky; číslo linky na sedm míst s nulami zepředu, jak ho drží `^JRL`.
ClassMethod Klic(db As %String, sku As %String, lin As %String) As %String
{
	quit db _ "~" _ +sku _ "~" _ $translate($justify(+lin, 7), " ", "0")
}

}
