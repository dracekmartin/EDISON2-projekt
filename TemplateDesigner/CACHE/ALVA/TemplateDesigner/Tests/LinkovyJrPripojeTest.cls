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
		do $$$AssertSkipped("firma linky s neúplnými přípoji tu není - přeskočeno")
		quit
	}
	// Doklad, že test měl co měřit - kdyby linka přípoje ztratila, prošel by naprázdno.
	do $$$AssertTrue((sRadkem + bezRadku) > 0, "linka nemá žádné přípoje - test neměl co měřit")
	do $$$AssertEquals(vadny, 0, "neúplný přípoj si vzal řádek ("_vadny_" z "_sRadkem_") - v tabulce vyjde ""00:00"" s prázdným popiskem")
	do $$$AssertTrue(bezRadku > 0, "na téhle lince mají být přípoje bez řádku - jinak měření z 4. 9. už neplatí")
}

/// Linka, kde je u přípojů vyplněná vlastní „linka pro tisk" - u vlaku stojí v surovém poli číslo
/// vlaku („Os 8009"), ale tisknout se má označení linky („S1").
Parameter LINKASPROTISK = "CB_GWBUS~2~0320144";

/// Vlastní „linka pro tisk" musí přebít surové číslo linky.
///
/// Naměřeno 4. 9.: 3 515 z 71 889 přípojů to pole má a u 3 512 se číslo liší (CB_GWBUS 29 %).
/// ⚠️ **Linky jádra zlatých odtisků to pole nemají**, takže tuhle opravu nekryjí - bez tohohle
/// testu by se přepis dal zrušit a nic by se nerozsvítilo.
Method TestLinkaProTiskPrebijeCisloLinky()
{
	do ..SetupSys(.sys)
	set popis = ..#LINKASPROTISK
	set db = $piece(popis,"~",1), sku = $piece(popis,"~",2), lin = $piece(popis,"~",3)
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("firma linky s vlastní linkou pro tisk tu není - přeskočeno")
		quit
	}
	set page = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).BuildStrankaLinky(db, sku, lin, "T", 2)
	if page = "" {
		do $$$AssertSkipped("stránka se nesestavila - přeskočeno")
		quit
	}

	set (overeno, surovych) = 0
	set spoje = page.%Get("rows").%Get("SPOJE")
	for si = 0:1:spoje.%Size()-1 {
		set pripoje = spoje.%Get(si).%Get("PRIPOJE")
		continue:'$isobject(pripoje)
		for pi = 0:1:pripoje.%Size()-1 {
			set pr = pripoje.%Get(pi)
			continue:'pr.RADEK
			set overeno = overeno + 1
			// Surové číslo vlaku začíná zkratkou druhu („Os ", „R ", „Sp ") - to se tisknout nemá.
			if $piece(pr.LINKA," ",1) = "Os" set surovych = surovych + 1
		}
	}
	if 'overeno {
		do $$$AssertSkipped("linka nemá přípoje s řádkem - přeskočeno")
		quit
	}
	do $$$AssertEquals(surovych, 0, "u "_surovych_" z "_overeno_" přípojů se tiskne číslo vlaku místo označení linky - vlastní ""linka pro tisk"" se neuplatnila")
}

/// Druh dopravy musí být na drátě - prvek podle něj seskupuje přípojné řádky, takže bez něj
/// by se autobus sloučil s vlakem téhož směru a popisek by neřekl, čím to jede.
Method TestPripojNeseDruhDopravy()
{
	do ..SetupSys(.sys)
	set popis = ..#LINKAZDRAVA
	set db = $piece(popis,"~",1), sku = $piece(popis,"~",2), lin = $piece(popis,"~",3)
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("firma tu není - přeskočeno")
		quit
	}
	set page = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).BuildStrankaLinky(db, sku, lin, "T", 2)
	do $$$AssertNotEquals(page, "", "stránka se musí sestavit")

	set (overeno, bezAvl) = 0
	set spoje = page.%Get("rows").%Get("SPOJE")
	for si = 0:1:spoje.%Size()-1 {
		set pripoje = spoje.%Get(si).%Get("PRIPOJE")
		continue:'$isobject(pripoje)
		for pi = 0:1:pripoje.%Size()-1 {
			set pr = pripoje.%Get(pi)
			continue:'pr.RADEK
			set overeno = overeno + 1
			// Prázdné legacy čte jako autobus, takže i nezadaný druh musí dorazit jako "A".
			if (pr.AVL '= "A") && (pr.AVL '= "V") && (pr.AVL '= "L") set bezAvl = bezAvl + 1
		}
	}
	do $$$AssertTrue(overeno > 0, "linka nemá přípoje s řádkem - test neměl co měřit")
	do $$$AssertEquals(bezAvl, 0, "přípoj bez druhu dopravy ("_bezAvl_" z "_overeno_") - prvek by sloučil autobus s vlakem")
}

/// Druhá polovina: filtr nesmí sebrat řádek zdravému přípoji.
Method TestZdravyPripojRadekDostane()
{
	do ..SetupSys(.sys)
	if '..Zmer(.sys, ..#LINKAZDRAVA, .sRadkem, .bezRadku, .vadny) {
		do $$$AssertSkipped("firma linky se zdravými přípoji tu není - přeskočeno")
		quit
	}
	do $$$AssertTrue(sRadkem > 0, "zdravý přípoj musí dostat řádek - jinak filtr bere i to, co brát nemá")
	do $$$AssertEquals(vadny, 0, "na zdravé lince nemá být neúplný přípoj s řádkem")
}

}
