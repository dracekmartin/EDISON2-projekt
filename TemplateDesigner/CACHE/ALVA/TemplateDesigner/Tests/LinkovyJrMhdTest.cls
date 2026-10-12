Include %occInclude

/// `B191b`/`B191c`: linkový JŘ MHD dává tutéž tabulku jako starý tisk a obě linkové agendy tisknou
/// zvolený směr. Porovnává se se starým modulem (`LinkovyJrMhdLegacyOracle`), ne s naším
/// odtiskem. Firma nebo starý modul, který na serveru není, se přeskočí.
Class ALVA.TemplateDesigner.Tests.LinkovyJrMhdTest Extends ALVA.EDTesting.MLTestCase
{

/// Linky `firma:linka`, výběr v analýze linkového JŘ §18.3; KV_DP 1438 a 1520 přidané 12. 10. -
/// název zastávky k datu platnosti a „jede jinudy" za poslední zastávkou spoje.
Parameter LINKY = "CB_DP:11350,CB_DP:11341,CB_DP:11354,CB_DP:11018,KV_DP:1073,KV_DP:1438,KV_DP:1520";

/// Celá tabulka proti starému tisku: značky spojů a legenda, časy po zastávkách, seznam zastávek
/// (název k datu platnosti, pásmo, značky), rozdělení spojů do bloků po typech provozu a pořadí.
Method TestTabulkaJakoStaryModul()
{
	set porovnano = 0, nz = 0, texty = 0
	for i=1:1:$length(..#LINKY, ",") {
		set idfa = $piece($piece(..#LINKY, ",", i), ":", 1), lin = $piece($piece(..#LINKY, ",", i), ":", 2)
		do ..SysFirmy(.sys, idfa)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdLegacyOracle).Porovnej(.sys, idfa, idfa, lin, .res)
		set porovnano = porovnano + 1, nz = nz + res("sNz"), texty = texty + res("sTexty")
		do $$$AssertTrue(res("pocet") > 0, idfa_" "_lin_": starý tisk nevrátil žádný spoj")
		do $$$AssertTrue(res("spoj"), idfa_" "_lin_": spoje se liší ("_res("spoj","pocet")_") "_res("spoj","rozdil"))
		do $$$AssertTrue(res("pozn"), idfa_" "_lin_": legenda se liší ("_res("pozn","pocet")_") "_res("pozn","rozdil"))
		do $$$AssertTrue(res("cas"), idfa_" "_lin_": časy se liší ("_res("cas","pocet")_") "_res("cas","rozdil"))
		do $$$AssertTrue(res("zast"), idfa_" "_lin_": zastávky se liší ("_res("zast","pocet")_") "_res("zast","rozdil"))
		do $$$AssertTrue(res("blok"), idfa_" "_lin_": spoje jsou jinak rozdělené do typů provozu "_res("blok","rozdil"))
		do $$$AssertTrue(res("poradi"), idfa_" "_lin_": pořadí spojů se liší "_res("poradi","rozdil"))
	}
	if 'porovnano {
		do $$$AssertTrue(1, "starý tisk MHD nebo firmy na serveru nejsou - přeskočeno")
		quit
	}
	// Shoda nad prázdnými značkami by nic nedokazovala.
	do $$$AssertTrue(nz > 0, "žádná porovnaná linka nemá spoj s negativní značkou")
	do $$$AssertTrue(texty > 0, "žádná porovnaná linka nemá spoj s textovou poznámkou")
}

Method TestMhdTiskneZvolenySmer()
{
	if '##class(%SYS.Namespace).Exists("CB_DP") {
		do $$$AssertTrue(1, "firma CB_DP není na serveru - přeskočeno")
		quit
	}
	do ..SysFirmy(.sys, "CB_DP")
	for volba = "Tam", "Zpět", "" {
		kill ^||ML
		set ^||ML("IN","LINKA_ID") = 11350, ^||ML("IN","SMER_TISKU") = volba
		do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
		do $$$AssertEquals(..SmeryStranek(), $case(volba, "Tam":"T", "Zpět":"Z", :"T,Z"), "MHD s volbou """_volba_"""")
	}
}

/// U AD platí volba i s obousměrným tiskem - jeden směr nemá co zrcadlit.
Method TestAdTiskneZvolenySmer()
{
	do ..SetupSys(.sys)
	for volba = "Tam", "Zpět", "Oba" {
		kill parametry
		set parametry("SMER_TISKU") = volba, parametry("OBOUSMERNY_TISK") = "true"
		if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, "CB_CSADAD", 1, "0330511", .parametry) {
			do $$$AssertTrue(1, "linka CB_CSADAD 0330511 není na serveru - přeskočeno")
			quit
		}
		// Obousměrná stránka nese prázdný SMER (`LinkovyJrAd.cls:777`), proto u „Oba" jedna prázdná.
		do $$$AssertEquals(..SmeryStranek(), $case(volba, "Tam":"T", "Zpět":"Z", :""), "AD s volbou "_volba)
		do $$$AssertEquals(..PocetStranek(), 1, "AD s volbou "_volba_" má mít jednu stránku")
	}
}

/// Směry stránek v ^||ML("OUT") v pořadí, čárkou.
ClassMethod SmeryStranek() As %String
{
	set out = "", idx = ""
	for  {
		set idx = $order(^||ML("OUT", idx))
		quit:idx=""
		continue:'$isvalidnum(idx)
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", idx))
		set out = out_$select(out="":"", 1:",")_page.values.%Get("SMER")
	}
	quit out
}

ClassMethod PocetStranek() As %Integer
{
	set pocet = 0, idx = ""
	for  {
		set idx = $order(^||ML("OUT", idx))
		quit:idx=""
		if $isvalidnum(idx) set pocet = pocet + 1
	}
	quit pocet
}

/// sys pro agendu MHD dané firmy - data jsou v namespace se jménem firmy.
Method SysFirmy(ByRef sys, idfa As %String)
{
	do ..SetupSys(.sys, idfa)
	set sys("U","DATA") = idfa
}

}
