Include (%occInclude, GENERAL)

/// Přípojné řádky linkového JŘ: **neúplný přípoj řádek nedostane**.
///
/// Starý modul zahodí přípoj, kterému chybí minuta příjezdu nebo směr (`JRapiLinPrtWin01.mac:84-87`).
/// My jsme do 4. 9. obojí posílali dál, takže v tabulce vznikl řádek s časem „00:00" a prázdným
/// popiskem - přesně ten prázdný přípojný řádek, na který si zákazník stěžuje v tiketu `#11300`
/// (jen z jiné příčiny než tam popisuje).
///
/// Naměřeno 4. 9. na čtyřech firmách: **965 z 71 889** přípojů, které řádek dělají, je neúplných
/// (nejvíc CB_GWBUS, 4,2 %). Není to tedy obrana proti stavu, který nikdo nepořídí.
///
/// ⚠️ **Poznámek pod tabulkou se to netýká** - ty se skládají z téhož přípoje, ale vlastní cestou
/// podle `typZobrazeni`. Filtr proto sahá jen na `RADEK`, ne na přítomnost přípoje ve výstupu.
///
/// Firma, která na serveru není, se přeskočí; test nesmí padat kvůli tomu, kde běží.
Class ALVA.TemplateDesigner.Tests.LinkovyJrPripojeTest Extends ALVA.EDTesting.MLTestCase
{

/// Linka, na které měření našlo neúplné přípoje (spoje 5, 7, 9 a 13, tarifní čísla 1 a 9).
Parameter LINKANEUPLNA = "CB_CSADAD~4~0340101";

/// Linka se zdravými přípoji - tři různé směry u Velešína, doklad k `#11300`.
Parameter LINKAZDRAVA = "CB_CSADAD~1~0330511";

/// Projde přípoje jedné linky. Vrací 0 = firma tu není. `sRadkem` / `bezRadku` = počty,
/// `vadnySRadkem` = neúplný přípoj, který si řádek přesto vzal (to je ta chyba).
ClassMethod Zmer(ByRef sys As %String, popis As %String, ByRef sRadkem, ByRef bezRadku, ByRef vadnySRadkem) As %Boolean
{
	set db = $piece(popis,"~",1), sku = $piece(popis,"~",2), lin = $piece(popis,"~",3)
	set (sRadkem, bezRadku, vadnySRadkem) = 0
	quit:'##class(%SYS.Namespace).Exists(db) 0

	set page = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).BuildStrankaLinky(db, sku, lin, "T", 2)
	quit:page="" 0

	set spoje = page.%Get("rows").%Get("SPOJE")
	quit:'$isobject(spoje) 0
	for si = 0:1:spoje.%Size()-1 {
		set pripoje = spoje.%Get(si).%Get("PRIPOJE")
		continue:'$isobject(pripoje)
		for pi = 0:1:pripoje.%Size()-1 {
			set pr = pripoje.%Get(pi)
			// Neúplný = chybí čas (agenda ho pak složí na „00:00") nebo směr.
			set neuplny = (pr.CAS = "00:00") || (pr.TEXT = "")
			if pr.RADEK {
				set sRadkem = sRadkem + 1
				if neuplny set vadnySRadkem = vadnySRadkem + 1
			} else {
				set bezRadku = bezRadku + 1
			}
		}
	}
	quit 1
}

/// Hlavní brána: na lince s neúplnými přípoji nesmí ani jeden dostat řádek.
Method TestNeuplnyPripojNedostaneRadek()
{
	do ..SetupSys(.sys)
	if '..Zmer(.sys, ..#LINKANEUPLNA, .sRadkem, .bezRadku, .vadny) {
		do $$$AssertTrue(1, "firma linky s neúplnými přípoji tu není - přeskočeno")
		quit
	}
	// Doklad, že test měl co měřit - kdyby linka přípoje ztratila, prošel by naprázdno.
	do $$$AssertTrue((sRadkem + bezRadku) > 0, "linka nemá žádné přípoje - test neměl co měřit")
	do $$$AssertEquals(vadny, 0, "neúplný přípoj si vzal řádek ("_vadny_" z "_sRadkem_") - v tabulce vyjde ""00:00"" s prázdným popiskem")
	do $$$AssertTrue(bezRadku > 0, "na téhle lince mají být přípoje bez řádku - jinak měření z 4. 9. už neplatí")
}

/// Druhá polovina: filtr nesmí sebrat řádek zdravému přípoji.
Method TestZdravyPripojRadekDostane()
{
	do ..SetupSys(.sys)
	if '..Zmer(.sys, ..#LINKAZDRAVA, .sRadkem, .bezRadku, .vadny) {
		do $$$AssertTrue(1, "firma linky se zdravými přípoji tu není - přeskočeno")
		quit
	}
	do $$$AssertTrue(sRadkem > 0, "zdravý přípoj musí dostat řádek - jinak filtr bere i to, co brát nemá")
	do $$$AssertEquals(vadny, 0, "na zdravé lince nemá být neúplný přípoj s řádkem")
}

}
