Include %occInclude

/// C1 gate: 1:1 parita MHD zastávkového JŘ proti SKUTEČNÉMU legacy producentovi
/// (ALVA.MHD.LinkyPrintFormatMHD.MakeData) — poziční porovnání odjezdů (zastávka×skupina-dnů×HH:MM),
/// zastávek (názvy) a poznámek (typ×znak×text) přes celou linku. Jádro v MhdLegacyOracle.CompareLine.
///
/// Na rozdíl od ZastavkoveJrMhdGoldTest (regrese vlastního výstupu) tohle ověřuje shodu s LEGACY.
/// Náš engine odpovídá legacy s TiskProjetychZast=1 → parita se testuje v tomto režimu.
///
/// Skip (ne fail) jen když legacy třída nebo namespace CB_DP na serveru nejsou (jiný dev/CI).
/// Pre-push runner jede pod CB_DP + legacy MHD třídy jsou nasazené → reálně běží.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrMhdLegacyParityTest Extends ALVA.EDTesting.MLTestCase
{

Parameter GOLDDB = "CB_DP";

Parameter GOLDIDFA = "CB_DP";

Method TestParita1001()
{
	do ..AssertParita("1001")
}

Method TestParita1003()
{
	do ..AssertParita("1003")
}

Method TestParita1006()
{
	do ..AssertParita("1006")
}

/// KV_DP 1073 — složitá linka se SMO kolizí (4 typy provozu ve sloupci 4). Plná 1:1 parita zde
/// NENÍ cíl: legacy odjezdy ve sloučeném sloupci ztrátově přepisuje a nadpis sloupce lže — náš
/// výstup je vědomá nadmnožina (bezztrátové sloučení + auto-značky „jede jen…" + složený nadpis).
/// Pinuje se proto: zastávky 1:1, poznámky = nadmnožina legacy (covered), odjezdy >= legacy.
Method TestParita1073KvDpSmoKolize()
{
	set ok = ##class(ALVA.TemplateDesigner.Tests.MhdLegacyOracle).CompareLine("KV_DP", "KV_DP", "1073", 1, .res)
	if 'ok {
		do $$$AssertTrue(1, "legacy oracle / namespace KV_DP nedostupný — parita 1073 přeskočena")
		quit
	}
	do $$$AssertEquals(res("zast","match"), 1, "zastávky 1:1 1073 (leg="_res("zast","legN")_" our="_res("zast","ourN")_") "_res("zast","diff"))
	do $$$AssertEquals(res("pozn","covered"), 1, "poznámky pokrývají legacy 1073 (leg="_res("pozn","legN")_" our="_res("pozn","ourN")_") "_res("pozn","diff"))
	do $$$AssertTrue(res("dep","ourN") >= res("dep","legN"), "odjezdy nadmnožina legacy 1073 (leg="_res("dep","legN")_" our="_res("dep","ourN")_")")
}

Method AssertParita(lin As %String)
{
	set ok = ##class(ALVA.TemplateDesigner.Tests.MhdLegacyOracle).CompareLine(..#GOLDIDFA, ..#GOLDDB, lin, 1, .res)
	if 'ok {
		do $$$AssertTrue(1, "legacy oracle nedostupný (ALVA.MHD.LinkyPrintFormatMHD / namespace "_..#GOLDDB_") — parita "_lin_" přeskočena")
		quit
	}
	do $$$AssertEquals(res("dep","match"),  1, "odjezdy 1:1 "_lin_" (leg="_res("dep","legN")_" our="_res("dep","ourN")_") "_res("dep","diff"))
	do $$$AssertEquals(res("zast","match"), 1, "zastávky 1:1 "_lin_" (leg="_res("zast","legN")_" our="_res("zast","ourN")_") "_res("zast","diff"))
	do $$$AssertEquals(res("pozn","match"), 1, "poznámky 1:1 "_lin_" (leg="_res("pozn","legN")_" our="_res("pozn","ourN")_") "_res("pozn","diff"))
}

}
