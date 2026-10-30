Include (%occInclude, GENERAL)

/// `B194f` G8: regresní pojistka linkového JŘ MHD - digest celého výstupu `LinkovyJrMhd.GetData`
/// (stránky **včetně hlavičky** - `values`) na linkách `LinkovyJrMhdTest.LINKY`, s výchozími parametry.
/// Porovnání se starým modulem (`LinkovyJrMhdLegacyOracle`) kryje jen tabulku a hlavičku bere linkový MHD
/// z vývěsky MHD (dopravce, typ JŘ, seznam linek, skupiny). Přesun těchto metod v `B56` by se jinak
/// neprojevil v žádném testu.
///
/// ⚠️ **Digest neplatí jako doklad parity** (pravidlo R6): hlídá náš výstup proti včerejšku, ne proti
/// starému modulu. Přegenerování po záměrné změně: z hlášky „aktuální: &lt;digest&gt;" vezmi novou
/// hodnotu, přepiš konstantu a do komentáře napiš proč. Nové pole mění jen md5, ne počty
/// (viz `LinkovyJrGoldTest`).
Class ALVA.TemplateDesigner.Tests.LinkovyJrMhdGoldTest Extends ALVA.EDTesting.MLTestCase
{

/// Výchozí odtisk 2026-10-29 (`G8`), sejmutý na dev serveru před sloučením kódu agend (`B56`);
/// dvě volání po sobě daly totéž. Formát „firma:linka=digest", oddělené „|".
/// Re-baseline 2026-10-30 (`B56` S7): číslo linky podle licence jde jako číslo, ne text, jako ve všech
/// agendách JŘ (`KontraktAgendTest.TestTypyCislaLinky`). Jen md5, počty u všech sedmi linek beze změny.
/// Re-baseline 2026-10-30 (`B56` S12): hlavičku linky skládá `Jr.DataMhd.AddHlavickaLinky`, změnilo se jen
/// pořadí položek. Jen md5, počty beze změny; snímek výstupu agend porovnaný podle obsahu jiný rozdíl nemá.
Parameter DIGESTY = "CB_DP:11350=pages=2 zast=41 spoju=122 casu=2500 pozn=6 md5=7dd4sUq2eWaOPibRo7nLjg==|CB_DP:11341=pages=2 zast=58 spoju=318 casu=8095 pozn=10 md5=KH7xsqecjn6gTVJw576ZRw==|CB_DP:11354=pages=2 zast=17 spoju=411 casu=3494 pozn=4 md5=G3QCn4XpwN6bIbOHt037Vw==|CB_DP:11018=pages=2 zast=47 spoju=228 casu=4945 pozn=13 md5=bUIXcnDiVS6yFEDj7Q3otQ==|KV_DP:1073=pages=2 zast=84 spoju=408 casu=10066 pozn=4 md5=Imx17o7mQlhM7dfbH/GVhQ==|KV_DP:1438=pages=2 zast=44 spoju=42 casu=260 pozn=4 md5=84+xZAOS+kphjBJiG4M/4g==|KV_DP:1520=pages=2 zast=42 spoju=351 casu=4334 pozn=3 md5=JH1iutIimN7VRw487vPo4A==";

Method TestGold()
{
	set overeno = 0
	for i=1:1:$length(##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdTest).#LINKY, ",") {
		set polozka = $piece(##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdTest).#LINKY, ",", i)
		set idfa = $piece(polozka, ":", 1), lin = $piece(polozka, ":", 2)
		continue:'##class(%SYS.Namespace).Exists(idfa)
		set ocekavano = ..Ocekavany(idfa, lin)
		do $$$AssertNotEquals(ocekavano, "", idfa_" "_lin_": chybí výchozí digest")
		set digest = ..Digest(idfa, lin)
		do $$$AssertEquals(digest, ocekavano, "gold MHD "_idfa_" "_lin_" (aktuální: "_digest_")")
		set overeno = overeno + 1
	}
	if 'overeno do $$$AssertSkipped("firmy MHD z LinkovyJrMhdTest.LINKY nejsou na tomto serveru")
}

/// Výchozí digest linky z parametru DIGESTY („firma:linka=digest|…").
ClassMethod Ocekavany(idfa As %String, lin As %String) As %String
{
	for i=1:1:$length(..#DIGESTY, "|") {
		set polozka = $piece(..#DIGESTY, "|", i)
		quit:$piece(polozka, "=", 1)=(idfa_":"_lin)
		set polozka = ""
	}
	quit $piece($get(polozka), "=", 2, *)
}

/// Digest výstupu linky s výchozími parametry. `sys` jako v `LinkovyJrMhdTest.SysFirmy`.
ClassMethod Digest(idfa As %String, lin As %String) As %String
{
	kill sys
	do ##class(ALVA.EDTesting.MLTestCase).SetupSys(.sys, idfa)
	set sys("U","DATA") = idfa
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
	if $get(^||ML("ERR")) '= "" quit "chyba: "_^||ML("ERR")
	quit ##class(ALVA.TemplateDesigner.Tests.GoldUtil).DigestLinkovy()
}

/// Vypíše aktuální digesty linek - podklad pro (pře)baseline, ne test. Voláno přes `cache_query_cvar`.
ClassMethod Digesty(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill radky
		for i=1:1:$length(##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdTest).#LINKY, ",") {
			set polozka = $piece(##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdTest).#LINKY, ",", i)
			set idfa = $piece(polozka, ":", 1), lin = $piece(polozka, ":", 2)
			continue:'##class(%SYS.Namespace).Exists(idfa)
			set radky($increment(radky)) = idfa_":"_lin_"="_..Digest(idfa, lin)
		}
		kill ^||ML
		for i=1:1:$get(radky) set ^||ML("OUT", i) = radky(i)
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
