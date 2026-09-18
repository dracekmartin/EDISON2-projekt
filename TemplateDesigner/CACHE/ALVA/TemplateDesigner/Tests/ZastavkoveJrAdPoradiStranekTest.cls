Include %occInclude

/// `B161` - volba **Pořadí stránek** u zastávkového JŘ z AD dat.
///
/// Výchozí pořadí projde linku jednou a u každé zastávky vydá obě její vývěsky za sebou (parita se
/// starým tiskem, `JRapiLinPrtMhd1.mac:397-398`). Druhá volba prohodí smyčky: nejdřív celý směr Tam
/// od první zastávky k poslední, pak celý směr Zpět od poslední k první.
///
/// Testuje se **pořadí, ne obsah** - obě volby musí vydat tytéž stránky, jen jinak seřazené.
/// Kdyby se ve druhé volbě některá ztratila nebo přibyla, znamenalo by to, že podmínky v obou
/// větvích nejsou shodné; právě proto je tělo smyčky vytažené do jedné metody.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdPoradiStranekTest Extends ALVA.EDTesting.MLTestCase
{

Parameter Firma = "CB_CSADAD";

Parameter Skupina = 1;

Parameter Linka = "0330530";

Parameter PoSmerech = "Po směrech (tam vzestupně, zpět sestupně)";

/// Posloupnost stránek načtené linky jako `poradi(n) = <směr>~<název zastávky>`.
ClassMethod PosloupnostStranek(ByRef poradi)
{
	kill poradi
	set key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		continue:'$isvalidnum(key)
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set poradi($increment(poradi)) = page.values.SMER _ "~" _ page.values."POPIS_ZASTAVKA"
	}
}

/// Vytáhne z posloupnosti jen stránky jednoho směru, jako seznam názvů oddělený `|`.
/// ⚠️ Ne čárkou - názvy zastávek ji samy obsahují („České Budějovice,,aut.nádr."), takže by se
/// seznam při obracení rozpadl uvnitř názvu a test by hlásil rozdíl, který ve výstupu není.
ClassMethod NazvySmeru(ByRef poradi, smer As %String) As %String
{
	set nazvy = "", i = ""
	for  {
		set i = $order(poradi(i))
		quit:i=""
		continue:$piece(poradi(i), "~", 1)'=smer
		set nazvy = nazvy _ $select(nazvy="":"", 1:"|") _ $piece(poradi(i), "~", 2)
	}
	quit nazvy
}

ClassMethod Obrat(seznam As %String) As %String
{
	set obraceny = ""
	for i = $length(seznam, "|") : -1 : 1 {
		set obraceny = obraceny _ $select(obraceny="":"", 1:"|") _ $piece(seznam, "|", i)
	}
	quit obraceny
}

/// Načte testovací linku s daným pořadím stránek; 0 = firma nebo linka na serveru není.
ClassMethod Nacti(ByRef sys, poradiStranek As %String = "") As %Boolean
{
	kill parametry
	set:poradiStranek'="" parametry("PORADI_STRANEK") = poradiStranek
	quit ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).NactiLinku(
		.sys, ..#Firma, ..#Skupina, ..#Linka, .parametry)
}

/// Výchozí pořadí: obě vývěsky téže zastávky jdou za sebou.
Method TestVychoziPoradiDrziZastavkyPohromade()
{
	do ..SetupSys(.sys)
	if '..Nacti(.sys) quit
	do ..PosloupnostStranek(.poradi)
	do $$$AssertNotEquals($get(poradi), "", "linka nevydala žádnou stránku")

	// Sousední dvojice T+Z téže zastávky - aspoň jedna musí být, jinak se pořadí rozešlo.
	set dvojic = 0
	for i = 1 : 1 : ($get(poradi) - 1) {
		continue:$piece(poradi(i),"~",1)'="T"
		continue:$piece(poradi(i+1),"~",1)'="Z"
		continue:$piece(poradi(i),"~",2)'=$piece(poradi(i+1),"~",2)
		set dvojic = dvojic + 1
	}
	do $$$AssertTrue(dvojic > 0, "ve výchozím pořadí nejde za sebou ani jedna dvojice vývěsek téže zastávky")
}

/// Volba „Po směrech": nejdřív celý směr Tam, pak celý směr Zpět.
Method TestPoSmerechNejprveCelyTam()
{
	do ..SetupSys(.sys)
	if '..Nacti(.sys, ..#PoSmerech) quit
	do ..PosloupnostStranek(.poradi)
	do $$$AssertNotEquals($get(poradi), "", "linka nevydala žádnou stránku")

	set prvniZpet = 0
	for i = 1 : 1 : $get(poradi) {
		if $piece(poradi(i),"~",1) = "Z" set prvniZpet = i  quit
	}
	do $$$AssertTrue(prvniZpet > 1, "chybí stránky směru Tam před prvním směrem Zpět")
	set tamPoZpetu = 0
	for i = prvniZpet : 1 : $get(poradi) {
		if $piece(poradi(i),"~",1) = "T" set tamPoZpetu = i  quit
	}
	do $$$AssertEquals(tamPoZpetu, 0, "za směrem Zpět se objevila stránka směru Tam (pořadí se prokládá)")
}

/// Zpáteční směr jde odzadu, směr Tam zůstává jako dřív - a žádná stránka se neztratí.
Method TestPoSmerechObraciJenZpatecniSmer()
{
	do ..SetupSys(.sys)
	if '..Nacti(.sys) quit
	do ..PosloupnostStranek(.vychozi)
	set tamVychozi = ..NazvySmeru(.vychozi, "T"), zpetVychozi = ..NazvySmeru(.vychozi, "Z")

	if '..Nacti(.sys, ..#PoSmerech) quit
	do ..PosloupnostStranek(.poSmerech)
	set tamNove = ..NazvySmeru(.poSmerech, "T"), zpetNove = ..NazvySmeru(.poSmerech, "Z")

	do $$$AssertEquals(tamNove, tamVychozi, "směr Tam má zůstat ve stejném pořadí")
	do $$$AssertEquals(zpetNove, ..Obrat(zpetVychozi), "směr Zpět má jít odzadu")
	do $$$AssertEquals($get(poSmerech), $get(vychozi), "obě volby musí vydat stejný počet stránek")
}

}
