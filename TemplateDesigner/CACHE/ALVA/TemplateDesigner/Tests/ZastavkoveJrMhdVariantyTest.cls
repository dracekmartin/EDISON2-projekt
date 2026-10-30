Include %occInclude

/// Varianty tras na vývěsce MHD (od 30. 10., `Jr.VariantyTras` jako vývěska AD). Starý tisk MHD varianty
/// neměl, takže není s čím porovnat (R6) - testy hlídají vlastnosti výstupu na lince 1 v CB_DP
/// (`LINKA_ID` 1001), která se na konci větví do sedmi variant (Nové Vráto, Rudolfov, Hlincová Hora).
/// Pravidla samotná (zástupce, řazení, ořez, slití) hlídá `VariantyTrasTest`.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrMhdVariantyTest Extends ALVA.EDTesting.MLTestCase
{

Parameter GOLDDB = "CB_DP";

Parameter GOLDIDFA = "CB_DP";

/// Data-lock: první vývěska směru tam (Haklovy Dvory) má sedm variant a písmena z výchozí sady.
Method TestVyvesaSeSedmiVariantami()
{
	quit:'..NactiStranky("1001", .stranky)
	set page = stranky(1)
	do $$$AssertEquals(page.values.%Get("POPIS_ZASTAVKA"), "Haklovy Dvory", "první stránka je Haklovy Dvory")
	do $$$AssertEquals(page.values.%Get("VARIANTY_TRAS").%ToJSON(), "["""",""A"",""D"",""L"",""M"",""N"",""P""]", "sedm variant, první bez písmene")
}

/// Každý řádek má hodnotu za každý sloupec, aktuální zastávka nulu, za ní kladné a před ní záporné doby;
/// písmena u minut jsou právě písmena sloupců a kategorii nese jen písmeno varianty.
Method TestTvarVsechStranek()
{
	quit:'..NactiStranky("1001", .stranky)
	set chyby = ""
	for s=1:1:stranky {
		set page = stranky(s), nazev = page.values.%Get("SMER")_" "_page.values.%Get("POPIS_ZASTAVKA")
		set varianty = page.values.%Get("VARIANTY_TRAS"), pocet = varianty.%Size()
		kill pismena, sloupce
		for i=0:1:pocet-1 set:varianty.%Get(i)'="" pismena(varianty.%Get(i)) = 0
		set za = 0, iz = page.rows.%Get("ZASTAVKY").%GetIterator()
		while iz.%GetNext(.k, .z) {
			set casy = z.%Get("TRASA_MIN"), aktualni = (z.%Get("AKTUALNI_ZASTAVKA") = "true")
			if (casy.%Size() '= pocet) || (z.%Get("NASTUPISTE_VARIANT").%Size() '= pocet) set chyby = chyby_nazev_": řádek bez hodnoty za každý sloupec; " continue
			for i=0:1:pocet-1 {
				set t = casy.%Get(i), sloupce(i) = $get(sloupce(i))_"~"_t
				continue:'$isvalidnum(t)
				if aktualni && (t '= 0) set chyby = chyby_nazev_": aktuální zastávka "_t_"; "
				if 'aktualni && za && (t < 0) set chyby = chyby_nazev_": záporná doba za vývěskou; "
				if 'aktualni && 'za && (t > 0) set chyby = chyby_nazev_": kladná doba před vývěskou; "
			}
			set:aktualni za = 1
		}
		// Dva sloupce se stejnými hodnotami by byly jedna varianta.
		kill videne
		for i=0:1:pocet-1 {
			if $data(videne(sloupce(i))) set chyby = chyby_nazev_": dva stejné sloupce; "
			set videne(sloupce(i)) = ""
		}
		set io = page.rows.%Get("ODJEZDY").%GetIterator()
		while io.%GetNext(.k, .o) {
			set ia = o.%Get("ANOTACE").%GetIterator()
			while ia.%GetNext(.ka, .a) {
				continue:a.%Get("KATEGORIE")=""
				if a.%Get("KATEGORIE") '= "varianta-trasy" set chyby = chyby_nazev_": kategorie "_a.%Get("KATEGORIE")_"; " continue
				if '$data(pismena(a.%Get("ZNAK"))) set chyby = chyby_nazev_": písmeno "_a.%Get("ZNAK")_" nemá sloupec; " continue
				set pismena(a.%Get("ZNAK")) = 1
			}
		}
		set p = ""
		for {
			set p = $order(pismena(p))
			quit:p=""
			if 'pismena(p) set chyby = chyby_nazev_": sloupec "_p_" nemá žádnou minutu; "
		}
	}
	do $$$AssertEquals(chyby, "", "varianty na všech "_stranky_" stránkách linky 1")
}

/// Stránky GetData linky do `stranky(1..n)`. Vrátí 0 a test přeskočí, když firma na serveru není.
Method NactiStranky(lin As %String, ByRef stranky) As %Boolean
{
	kill stranky
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("namespace "_db_" není na tomto serveru")
		quit 0
	}
	do ##class(ALVA.TemplateDesigner.Tests.MhdLegacyOracle).SetupOur(.sys, ..#GOLDIDFA, db)
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
	set stranky = 0, p = ""
	for {
		set p = $order(^||ML("OUT",p), 1, json)
		quit:p=""
		continue:'$isvalidnum(p)
		set stranky($increment(stranky)) = ##class(%DynamicObject).%FromJSON(json)
	}
	kill ^||ML
	do $$$AssertTrue(stranky > 0, "linka "_lin_" má stránky")
	quit stranky > 0
}

}
