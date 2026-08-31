Include (%occInclude, GENERAL)

/// Turnusy ke spojům (`L6c`) - **data**: co posílá agenda a jak to sedí se starým modulem.
///
/// Turnusy nejsou v `^JRL` - agenda si je vytahuje voláním do modulu turnusů
/// (`ALVA.ADMAIN.MLAPI.gSplTur`, týmž, jaké volá starý tisk). Dotaz stojí desítky až stovky ms,
/// proto ho **zapíná parametr sestavy** `TURNUSY_KE_SPOJUM` - stejnou pákou jako legacy
/// (`in("A",2,"arr",6)`). Vypnutý parametr tedy musí znamenat, že se nevolá **a nic nepřijde**.
///
/// ⚠️ **Oracle na hodnoty tady nesedne: starý engine s naším tvarem čísla linky nevrátí nic.**
/// Do dotazu předá `lin`, jak ho dostal, ale odpověď filtruje číslem z turnusového záznamu
/// (`108360`), takže s indexovým tvarem s nulami („0108360" - ten je v `^JRL` i v oracle) se
/// nespáruje nic a `TUmax` zůstane 0 (`gSplTur`, `MLAPI.cls:545`). Změřeno oběma tvary sondou
/// `LinkovyJrNastaveniProbe.TurnusyTvarLinky`: „0108360" → 0 spojů, `108360` → 3 spoje.
/// **Co posílá skutečný klient TiskJR, ověřené není** - jeho překladová vrstva má v komentáři
/// „4|820701" bez vodicí nuly, takže v produkci turnusy tisknout mohou.
///
/// Ověřuje se proto **proti témuž zdroji, jaký volá legacy** (`gSplTur`, stejné datum, stejné
/// sjednocení podle označení): `TestZapnutyParametrPosilaTurnusy`. `TestParitaSLegacy` zůstává
/// a přeskočí se, dokud starý modul nic nevytiskne - kdyby se spravil, porovnání se rozjede samo.
/// **Pozice řádků (turnusy nad platností) je odvozená z kontraktu starého enginu, ne z oracle**:
/// jeho klíče řádků jdou `1` (číslo spoje) → `1.001xx` (turnusy) → `1.1` (platnost) → `1.9` (NZ).
Class ALVA.TemplateDesigner.Tests.LinkovyJrTurnusyTest Extends ALVA.EDTesting.MLTestCase
{

/// Linka, na které turnusy reálně jsou: sonda 28. 8. našla 6 spojů ze 6 s turnusem,
/// nejvíc 2 na spoj (označení „110.01"…). Bez toho by testy prošly na prázdných datech.
Parameter TESTDB = "CB_CSADAD";

Parameter TESTSKU = 1;

Parameter TESTLIN = "0108360";

/// Turnusy **nejsou parametr sestavy** - je to volba prvku (rozhodnutí Jan 28. 8.).
///
/// Dvě volby téhož by si šlapaly: klient posílá `TURNUSY_KE_SPOJUM` podle prvku, takže parametr
/// vedle toho by uměl jen ukazovat něco jiného, než šablona tiskne.
Method TestNeniParametremSestavy()
{
	do ..SetupSys(.sys)
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetParameters(.sys)
	set json = $get(^||ML("OUT", 1))
	do $$$AssertTrue(json '[ "TURNUSY_KE_SPOJUM", "turnusy se vrátily mezi parametry sestavy - je to volba prvku")
}

/// Vypnutý parametr: ani jeden spoj nesmí nést `TURNUSY`.
Method TestVypnutyParametrNepositaNic()
{
	if '..NactiSpoje(0, .turnusy, .spoju, .sKlicem) quit
	do $$$AssertTrue(spoju > 0, "linka nevrátila žádné spoje - rozbitý fixture?")
	do $$$AssertEquals(sKlicem, 0, "s vypnutým parametrem má turnusy "_sKlicem_" spojů, má být 0")
}

/// Zapnutý parametr: turnusy dorazí a **sedí se zdrojem** (týmž voláním, jaké dělá agenda).
Method TestZapnutyParametrPosilaTurnusy()
{
	if '..NactiSpoje(1, .turnusy, .spoju, .sKlicem) quit
	do $$$AssertTrue(sKlicem > 0, "se zapnutým parametrem nemá turnus ani jeden spoj")

	// Zdroj = přímé volání do modulu turnusů. Porovnává se sada označení, ne jen počet.
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).NactiTurnusy(..#TESTDB, ..#TESTSKU, ..#TESTLIN, .zdroj)
	set rozdilu = 0, sp = ""
	for  {
		set sp = $order(zdroj(sp))
		quit:sp=""
		// Spoj, který se netiskne (jiná skupina dnů, nezařazený), ve výstupu být nemusí.
		continue:'$data(turnusy(sp))
		set tu = ""
		for  {
			set tu = $order(zdroj(sp, tu))
			quit:tu=""
			if '$data(turnusy(sp, tu)) set rozdilu = rozdilu + 1
		}
	}
	do $$$AssertEquals(rozdilu, 0, "u "_rozdilu_" turnusů se výstup rozešel se zdrojem")

	// Posílá se **číslo turnusu**, ne celé označení s variantou: jeden turnus vrací modul za každou
	// kombinaci zpracování × datum, takže bez sjednocení vyjde z jednoho turnusu i osm řádků.
	set sTeckou = "", sp = ""
	for  {
		set sp = $order(turnusy(sp))
		quit:sp=""
		set tu = ""
		for  {
			set tu = $order(turnusy(sp, tu))
			quit:tu=""
			if tu [ "." set sTeckou = sTeckou_$select(sTeckou'="":", ", 1:"")_sp_"/"_tu
		}
	}
	do $$$AssertEquals(sTeckou, "", "označení s variantou po tečce se nesjednotila: "_sTeckou)
}

/// **Parita se starým modulem**: označení turnusů spoj po spoji a jejich pozice nad platností.
Method TestParitaSLegacy()
{
	if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).OracleDostupny() {
		do $$$AssertTrue(1, "starý modul není na tomto serveru - parita přeskočena")
		quit
	}
	if '..NactiSpoje(1, .nase, .spoju, .sKlicem) quit
	set ok = ##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).NactiLegacyTurnusy(..#TESTDB, ..#TESTSKU, ..#TESTLIN, .leg, .radekTurnusu, .radekPlatnosti)
	if 'ok {
		do $$$AssertTrue(1, "starý modul linku nespočítal - parita přeskočena")
		quit
	}

	// ⚠️ **Starý modul turnusy netiskne** (zjištěno 28. 8., mechanismus níže) - na dnešních datech
	// tedy není proti čemu porovnávat a test se přeskočí. Zůstává tu schválně: kdyby se legacy
	// spravil (nebo se objevila firma, kde to projde), porovnání se rozjede samo.
	set legSpoju = 0, sp = ""
	for  {
		set sp = $order(leg(sp))
		quit:sp=""
		set legSpoju = legSpoju + 1
	}
	if legSpoju = 0 {
		do $$$AssertTrue(1, "starý modul nevytiskl turnus ani u jednoho spoje - porovnání přeskočeno (vada legacy, viz komentář třídy)")
		quit
	}

	set chybi = "", navic = "", sp = ""
	for  {
		set sp = $order(leg(sp))
		quit:sp=""
		continue:'$data(nase(sp))
		set tu = ""
		for  {
			set tu = $order(leg(sp, tu))
			quit:tu=""
			if '$data(nase(sp, tu)) set chybi = chybi_$select(chybi'="":", ", 1:"")_sp_"/"_tu
		}
		set tu = ""
		for  {
			set tu = $order(nase(sp, tu))
			quit:tu=""
			if '$data(leg(sp, tu)) set navic = navic_$select(navic'="":", ", 1:"")_sp_"/"_tu
		}
	}
	do $$$AssertEquals(chybi, "", "turnusy, které starý modul tiskne a my ne: "_chybi)
	do $$$AssertEquals(navic, "", "turnusy, které tiskneme my a starý modul ne: "_navic)

	// Pořadí řádků: turnusy nad platností. Tohle je to, co u nás dělá řádkový plán prvku.
	do $$$AssertTrue(radekTurnusu > 0, "ve výstupu starého modulu není žádný řádek turnusů")
	do $$$AssertTrue(radekPlatnosti > 0, "ve výstupu starého modulu není žádný řádek platnosti")
	do $$$AssertTrue(radekTurnusu < radekPlatnosti, "starý modul má turnusy na řádku "_radekTurnusu_" a platnost na "_radekPlatnosti_" - naše pořadí je obrácené")
}

/// Zavolá `GetData` a vybere turnusy: `turnusy(spoj, označení) = ""`, počet spojů,
/// počet spojů s klíčem `TURNUSY`. Vrací 0, když linka na serveru není (skip).
Method NactiSpoje(sTurnusy As %Boolean, ByRef turnusy, ByRef spoju, ByRef sKlicem) As %Boolean
{
	set db = ..#TESTDB, sku = ..#TESTSKU, lin = ..#TESTLIN
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "firma "_db_" není na tomto serveru - test přeskočen")
		quit 0
	}
	if '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertTrue(0, "testovací linka "_lin_" chybí v "_db_" - rozbitý fixture?")
		quit 0
	}

	do ..SetupSys(.sys)
	kill parametry
	set parametry("TURNUSY_KE_SPOJUM") = $select(sTurnusy:"true", 1:"false")
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)

	kill turnusy
	set spoju = 0, sKlicem = 0, idx = ""
	for  {
		set idx = $order(^||ML("OUT", idx))
		quit:idx=""
		continue:idx="ERR"
		continue:'$isvalidnum(idx)
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", idx))
		set it = page.rows.SPOJE.%GetIterator()
		while it.%GetNext(.i, .spoj) {
			set spoju = spoju + 1
			set pole = spoj.%Get("TURNUSY")
			continue:'$isobject(pole)
			set sKlicem = sKlicem + 1
			set sp = +spoj.%Get("CISLO_SPOJE")
			set it2 = pole.%GetIterator()
			while it2.%GetNext(.j, .tu) {
				set turnusy(sp, tu) = ""
			}
		}
	}
	quit 1
}

}
