Include (%occInclude, GENERAL)

/// **Brána proti starému modulu** pro linkový JŘ: pro každou linku jádra sady se spustí legacy
/// `mkPLinWin` i naše `LinkovyJrAd.GetData` a porovnají se pole po poli (pořadí spojů, názvy
/// zastávek, km, zóny, sloupec MHD, časy včetně značek, legenda).
///
/// **Proč ne zlatý odtisk:** digest je hash **našeho** výstupu - hlídá regresi proti včerejšku, ale
/// odchylku od starého modulu nezachytí ani v principu (pravidlo R6). Odtisk má vedle toho smysl
/// a žije v `LinkovyJrGoldTest`; tenhle test je ta druhá polovina.
///
/// **Co je pád a co ne** (mechanika v `LinkovyJrLegacyOracle.ZnameOdchylky`):
/// - `rozdil` = nikdo o něm nerozhodl → **pád**
/// - `odchylka` = chceme se lišit, s důvodem → projde
/// - `dluh` = nechceme se lišit, ale zatím se lišíme; má přiřazenou vlnu → projde, ale je vidět
///
/// Rozdělení je záměr, ne měkkost: síť má chytat, že jsme se rozešli **omylem**, a nesmí bránit
/// tomu, abychom se rozešli **záměrně** (požadavky z helpdesku, analýza §14).
///
/// ⚠️ Vázané na per-firma namespace (CB_CSADAD / KV_DP / VS_CSAD) a na přítomnost starého modulu.
/// Co na serveru není, se přeskočí - test nesmí padat kvůli tomu, kde běží.
/// Diagnostika mimo runner: `cache_query_cvar` na `LinkovyJrLegacyOracle.Souhrn`.
///
/// **Když spadne, začni tady.** 3. 9. dvakrát spadl a pak bez zásahu do kódu prošel; hlásil rozdíl
/// v legendě poznámek (starý modul značku vůbec neměl, my ano) a název zastávky s dvojitou čárkou.
/// Deset běhů to nezreprodukovalo, takže **tyhle příčiny už hledat nemusíš** - jsou vyloučené:
///
/// - **Dva běhy si nešlapou v pracovním globálu.** Starý modul si index alokuje `$i(^CacheTempJB)`,
///   což je atomický inkrement nad persistentním globálem, takže každý proces dostane svůj.
/// - **Chyba starého modulu neshodí test, ale přeskočí linku** (`SpustLegacy` vrací 0). Pád tedy
///   znamená, že starý modul odpověděl a hodnota se opravdu lišila.
/// - **Ani jedna strana nezávisí na dni, kdy test běží.** Obě berou datum z platnosti linky
///   (`PATCH,"Plati"`), ne z dneška, takže výsledek se nemůže lišit podle svátku nebo prázdnin.
///
/// Zůstává **změna dat na devu mezi běhy** - kolegové z jízdních řádů tyhle linky sdílejí. Doložit
/// to zpětně nejde, proto při dalším pádu **nejdřív ulož `Souhrn` a ručně porovnej se starým
/// modulem** (padalo to na hodnotách, které mají být hlídané) - ne přerazit na odchylku.
Class ALVA.TemplateDesigner.Tests.LinkovyJrAdLegacyTest Extends ALVA.EDTesting.MLTestCase
{

/// Kategorie, které se porovnávají. Pojmenované česky, ať je z hlášky poznat, o co jde.
ClassMethod Kategorie(ByRef kat)
{
	kill kat
	set kat("spoje") = "pořadí spojů"
	set kat("zast") = "názvy zastávek"
	set kat("km") = "kilometry"
	set kat("zona") = "zóny"
	set kat("mhd") = "sloupec MHD"
	set kat("cas") = "časy v buňkách"
	set kat("pozn") = "legenda poznámek"
}

/// Hlavní brána: na jádru sady nesmí zůstat **žádný nedeklarovaný rozdíl**.
Method TestParitaSeStarymModulem()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro)
	do ..Kategorie(.kat)

	set overeno = 0, nalezy = "", souhrn = "", porovnano = 0, odchylek = 0, dluhu = 0
	set i = ""
	for  {
		set i = $order(jadro(i))
		quit:i=""
		set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).Porovnej(.sys, db, sku, lin, .res)
		set overeno = overeno + 1
		set k = ""
		for  {
			set k = $order(kat(k))
			quit:k=""
			continue:'$data(res(k))
			set porovnano = porovnano + +$get(res(k, "leg"))
			set odchylek = odchylek + +$get(res(k, "odchylka"))
			set dluhu = dluhu + +$get(res(k, "dluh"))
			continue:'+$get(res(k, "rozdil"))
			set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ db_" "_lin_" "_kat(k)_" ("_res(k,"rozdil")_"): "_$get(res(k, "prvni"))
		}
	}

	if 'overeno {
		do $$$AssertTrue(1, "starý modul ani jedna firma jádra sady na tomto serveru není - přeskočeno")
		quit
	}
	set souhrn = "linek "_overeno_", porovnáno hodnot "_porovnano_", vědomých odchylek "_odchylek_", dluhů "_dluhu
	do $$$AssertEquals(nalezy, "", "nedeklarovaný rozdíl proti starému modulu ["_souhrn_"]: "_nalezy)
	// Doklad, že brána reálně něco měřila - prázdné porovnání by prošlo taky.
	do $$$AssertEquals(porovnano > 1000, 1, "porovnáno podezřele málo hodnot ["_souhrn_"]")
}

/// Linky, na kterých je vidět **věta atributu spoje**, a proč je tam každá z nich (`L14`).
/// Formát: `firma~skupina~linka~atribut~co na ní je vidět`.
ClassMethod LinkySVetouAtributu(ByRef sada)
{
	kill sada
	// Jediná linka v měřených datech, kde je atribut „zásilky" vůbec nastavený.
	set sada(1) = "VS_CSAD~13~0883637~POS~výčet spojů (generickou větu tenhle atribut nemá nikdy)"
	// Atribut na všech spojích → tvar „všechny spoje", a taky bez generické věty.
	set sada(2) = "VS_CSAD~9~0240801~IDS~tvar ""všechny spoje"""
	// Rozhodující případ pro rozsah počítání: ve směru Z ho mají všechny spoje, na celé lince ne.
	set sada(3) = "VS_CSAD~24~0941210~BEV~všechny spoje směru, ale ne celé linky"
	// Upřesnění za větou (`B194` T1): u linky nebo firmy, a vlastní text spoje.
	set sada(4) = "CB_GWBUS~216~0103942~RBU~upřesnění linky nebo firmy za větou"
	set sada(5) = "CB_GWBUS~216~0125850~RBP~upřesnění z textu spoje za větou"
}

/// Zastávka na objednání u času (`B194` T12): kolik buněk starý linkový tisk kreslí s telefonem,
/// tolik buněk náš Linkový JŘ AD posílá se značkou „r". Do 26. 10. je vynechával. CB_GWBUS 216/0340941.
Method TestZnackaNaObjednaniUCasu()
{
	do ..SetupSys(.sys)
	set db = "CB_GWBUS", sku = 216, lin = "0340941"
	kill parametry
	if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry) {
		do $$$AssertTrue(1, "linka "_lin_" není na tomto serveru - přeskočeno")
		quit
	}
	set nasich = 0, key = ""
	for {
		set key = $order(^||ML("OUT", key)) quit:key=""
		continue:'$isvalidnum(key)
		set spoje = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key)).rows.SPOJE
		continue:'$isobject(spoje)
		set is = spoje.%GetIterator()
		while is.%GetNext(.j, .s) {
			set zn = s.%Get("ZNACKY")
			continue:'$isobject(zn)
			set iz = zn.%GetIterator()
			while iz.%GetNext(.k, .z) { set:z["r" nasich = nasich + 1 }
		}
	}
	quit:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).NactiObeStrany(.sys, db, sku, lin, .leg, .nase)
	set starych = 0, sm = ""
	for {
		set sm = $order(leg("cas", sm)) quit:sm=""
		set r = ""
		for {
			set r = $order(leg("cas", sm, r)) quit:r=""
			set sp = ""
			for { set sp = $order(leg("cas", sm, r, sp), 1, hodnota) quit:sp=""  set:hodnota="RBU" starych = starych + 1 }
		}
	}
	do $$$AssertTrue(starych > 0, "starý tisk nemá žádnou buňku s telefonem - fixture vyhnila?")
	do $$$AssertEquals(nasich, starych, "buňky se značkou „r"" (my "_nasich_") × buňky s telefonem (starý tisk "_starych_")")
}

/// Věta o pokračování spoje jde z číselníku firmy v jazyce šablony (`B194` T10a) - do 26. 10. byla
/// napevno česky i ve slovenské šabloně. Český číselník dává totéž znění jako dřívější text napevno.
Method TestPokracovaniSpojeZCiselniku()
{
	set cls = "ALVA.TemplateDesigner.Agendy.LinkovyJrAd"
	if ##class(%SYS.Namespace).Exists("CB_CSADAD") {
		do $$$AssertEquals($classmethod(cls, "TextPokracovaniSpoje", "CB_CSADAD", "CZ", 5, 320, 3, "Brno"), "spoj 5 pokračuje na lince číslo 320 spojem číslo 3 do zastávky Brno", "CZ z číselníku")
	}
	if ##class(%SYS.Namespace).Exists("SKNI_MHDNITRA") {
		do $$$AssertEquals($classmethod(cls, "TextPokracovaniSpoje", "SKNI_MHDNITRA", "*SK", 5, 320, 3, "Nitra"), "spoj 5 pokračuje na linke číslo 320 spojom číslo 3 do zastávky Nitra", "SK z číselníku")
	}
	if $get(^[$namespace]JRSET("NCOME","CZ",34)) = "" {
		do $$$AssertEquals($classmethod(cls, "TextPokracovaniSpoje", $namespace, "CZ", 5, 320, 3, "Brno"), "spoj 5 pokračuje na lince číslo 320 spojem číslo 3 do zastávky Brno", "bez číselníku české znění")
	}
}

/// Liší-li se upřesnění spojů „na objednání", dostane každé vlastní větu s výčtem spojů; spoj bez
/// upřesnění vlastní větu bez dovětku. Data takovou linku dnes nemají (sonda `NaObjednaniVyskyt`,
/// 26. 10.), takže se to proti starému modulu porovnat nedá - test hlídá pravidlo opsané
/// z `gAtrNZLi^JRapiNZUtl3` (`:239-256`).
Method TestVetyRuznychUpresneni()
{
	set poznamky = ##class(%DynamicArray).%New()
	kill seen
	set seen("RBU", 1) = "tel. 111", seen("RBU", 3) = "tel. 111", seen("RBU", 5) = ""
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).PridatVetyUpresneni(poznamky, "RBU", "CZ", "spoj jede jen na objednání", .seen)
	do $$$AssertEquals(poznamky.%Size(), 2, "dvě různá upřesnění = dvě věty")
	do $$$AssertEquals(poznamky.%Get(0).%Get("TEXT"), "spoje 1, 3 jedou jen na objednání tel. 111")
	do $$$AssertEquals(poznamky.%Get(1).%Get("TEXT"), "spoj 5 jede jen na objednání")

	set poznamky = ##class(%DynamicArray).%New()
	kill seen
	set seen("RBP", 2) = "jen v sezóně", seen("RBP", 4) = "jen v sezóně"
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).PridatVetyUpresneni(poznamky, "RBP", "CZ", "spoj jede jen podmínečně", .seen)
	do $$$AssertEquals(poznamky.%Size(), 1, "jedno upřesnění = jedna obecná věta")
	do $$$AssertEquals(poznamky.%Get(0).%Get("TEXT"), "spoj jede jen podmínečně jen v sezóně")
}

/// **Věty u atributů spojů se musí shodovat se starým modulem, včetně volby tvaru.**
///
/// Věta má dva tvary - „všechny spoje s…" a výčet „spoje 1, 7 …" - a o tom, který padne, rozhoduje
/// poměr spojů s atributem ku všem spojům **na téže sestavě**. Tohle je jediné místo, kde se ten
/// výběr dá porovnat proti starému modulu na datech, kde ty atributy skutečně jsou; do 31. 8. se
/// tvrdilo, že shoda platí, ale doloženo to nebylo (`L14`).
Method TestVetyAtributuProtiLegacy()
{
	do ..SetupSys(.sys)
	do ..LinkySVetouAtributu(.sada)
	set overeno = 0, nalezy = "", i = ""
	for  {
		set i = $order(sada(i))
		quit:i=""
		set db = $piece(sada(i),"~",1), sku = $piece(sada(i),"~",2), lin = $piece(sada(i),"~",3)
		set klic = $piece(sada(i),"~",4), popis = $piece(sada(i),"~",5)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).NactiObeStrany(.sys, db, sku, lin, .leg, .nase)
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).VetyAtributu(.leg, .vetyLeg)
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).VetyAtributu(.nase, .vetyNase)
		// Fixture smí vyhnít: kdyby zákazník atribut z linky sundal, test by od té chvíle
		// porovnával prázdno s prázdnem a tvářil se zeleně.
		if '$data(vetyLeg(klic)) {
			set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ db_" "_lin_" "_klic_": starý modul větu nemá, fixture vyhnila ("_popis_")"
			continue
		}
		set overeno = overeno + 1
		continue:$get(vetyNase(klic))=$get(vetyLeg(klic))
		set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ db_" "_lin_" "_klic_" ("_popis_"): starý ["_$get(vetyLeg(klic))_"] × náš ["_$get(vetyNase(klic))_"]"
	}
	if 'overeno {
		do $$$AssertTrue(1, "starý modul ani firma VS_CSAD na tomto serveru není - přeskočeno")
		quit
	}
	do $$$AssertEquals(nalezy, "", "věta atributu se liší od starého modulu: "_nalezy)
}

/// Deklarovaná odchylka ani dluh nesmí být **mrtvý záznam**. Kdyby se rozdíl přestal objevovat
/// (opravili jsme ho, nebo zmizel z dat), zůstane v registru výjimka, která už nic nekryje - a příště
/// pod ní projde skutečná chyba. Běží jen na serveru s celým jádrem, jinak by hlásil mrtvý záznam
/// kvůli chybějící firmě.
Method TestRegistrNemaMrtveZaznamy()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro)
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).ZnameOdchylky(.odch)

	set overeno = 0, pocetLinek = 0, i = ""
	kill uplatneno
	for  {
		set i = $order(jadro(i))
		quit:i=""
		set pocetLinek = pocetLinek + 1
		set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).Porovnej(.sys, db, sku, lin, .res)
		set overeno = overeno + 1
		set k = ""
		for  {
			set k = $order(res(k))
			quit:k=""
			continue:k="duvod"
			if +$get(res(k, "odchylka")) set uplatneno(k) = ""
			if +$get(res(k, "dluh")) set uplatneno(k) = ""
		}
	}
	if overeno < pocetLinek {
		do $$$AssertTrue(1, "část jádra sady na serveru chybí - mrtvé záznamy registru se nekontrolují")
		quit
	}

	// Registr je vedený po kategoriích; mrtvá je ta, u které se na celém jádru neuplatnil ani jeden rozdíl.
	set mrtve = "", k = ""
	for  {
		set k = $order(odch(k))
		quit:k=""
		continue:$data(uplatneno(k))
		set mrtve = mrtve _ $select(mrtve="":"", 1:", ") _ k
	}
	do $$$AssertEquals(mrtve, "", "registr má záznam, který už žádný rozdíl nekryje (smaž ho): "_mrtve)
}

}
