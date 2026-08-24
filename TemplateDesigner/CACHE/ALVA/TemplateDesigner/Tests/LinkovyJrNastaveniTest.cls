Include (%occInclude, GENERAL)

/// **Matice nastavení** linkového JŘ (vlna `L2`): ke každému přepínači z EDISONu se ověří, že se
/// podle něj tisk reálně chová - a to v **každém stavu, který se v datech vyskytuje**.
///
/// **Proč zvlášť od `LinkovyJrAdLegacyTest`.** Porovnání celé linky se starým modulem hlídá, že se
/// nelišíme, ale **neřekne, čím to je**: nastavení, které si nikdo na testovaných linkách nezapnul,
/// projde jako zelené a nikdo se nedozví, že ho ignorujeme. Tenhle test bere nastavení jako **vstup**:
/// najde linku, kde ten stav je, a tvrdí, co má být na výstupu.
///
/// ── Jak se přidá další nastavení ──────────────────────────────────────────────
///
/// **Jeden řádek do `Matice`**, nic víc; když k tomu chybí měření, přidá se jedna větev do `Zmer`.
/// Formát řádku: `klíč | hodnota | měření | očekáváno | stav`
///
/// - **klíč** - jméno pod `^JRL(sku,lin,"PATCH",...)`
/// - **hodnota** - hodnota nastavení; prázdná = nenastaveno
/// - **měření** - co se na výstupu měří (viz `Zmer`)
/// - **očekáváno** - `0` nebo `1`; **`?`** = jen změřit a vypsat, netvrdit
/// - **stav** - `plati` = rozdíl je pád · `dluh:<krok> <důvod>` = jen se hlásí
///
/// ⚠️ **`?` a `dluh` nejsou úhybný manévr, jsou to dvě různé pravdy.** `?` znamená „z dat to nejde
/// tvrdit" - nastavení říká „neodstraňuj písmena", ale linka žádná mít nemusí, takže z jejich
/// absence nic nevyplývá. `dluh` znamená „my to ještě neumíme" a má u sebe krok, ve kterém zmizí.
/// Kdyby se obojí slilo do „netestuje se", nikdo by za měsíc nepoznal, co je nedodělek.
///
/// ⚠️ **Past, na kterou matice sama umí najet:** u nastavení, které něco **potlačuje**, je nulové
/// měření splněné i tehdy, když tu vlastnost vůbec neumíme („žádné barvy" vyjde samo, když barvy
/// neposíláme). Takový řádek musí být `?`, dokud nejde doplnit předpoklad nad **zdrojovými** daty
/// („linka má u spojů barvu"). Jinak by matice tvrdila něco, co jsme neudělali.
///
/// Stavy, které v datech nejsou, se **hlásí jako neověřené** - nikdy se netváří jako pokryté.
/// Změřený výskyt nastavení v datech: `linkovy-knizni-jr-implementace-analyza.md` §13.6.
Class ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniTest Extends ALVA.EDTesting.MLTestCase
{

/// Matice: co se u kterého nastavení a stavu čeká na výstupu.
///
/// Zdroj významů je legacy `gPLSetWin` (`JRapiLinPrtWin.mac:1840-1897`); u každé skupiny je
/// v komentáři, co ta hodnota znamená, ať se to nemusí dohledávat ve zdroji starého modulu.
ClassMethod Matice(ByRef m)
{
	kill m

	// ── zóny × km: čtyřstavový přepínač (`:1861-1868`) ──────────────────
	// 0 i nenastaveno = jen km · 1 = jen zóna · 2 = obojí · 3 = ani jedno
	// Opraveno 21. 8.; dřív se čtlo jen „je neprázdné → tiskni zónu".
	set m($increment(m)) = "linIDS||zona|0|plati"
	set m($increment(m)) = "linIDS||km|1|plati"
	set m($increment(m)) = "linIDS|0|zona|0|plati"
	set m($increment(m)) = "linIDS|0|km|1|plati"
	set m($increment(m)) = "linIDS|1|zona|1|plati"
	set m($increment(m)) = "linIDS|1|km|0|plati"
	set m($increment(m)) = "linIDS|2|zona|1|plati"
	set m($increment(m)) = "linIDS|2|km|1|plati"
	set m($increment(m)) = "linIDS|3|zona|0|plati"
	set m($increment(m)) = "linIDS|3|km|0|plati"

	// ── schovat sloupec tarifních čísel ─────────────────────────────────
	// U linky 1 = schovat. Zapnuto na 377 linkách. Uživatel to smí přebít parametrem `SLOUPEC_TC`
	// („Automaticky (dle linky)" / „Tisknout" / „Netisknout"); tady se ověřuje výchozí volba,
	// tedy že se bez zásahu uživatele respektuje linka.
	// Past z jiných řádků tu nehrozí: tarifní čísla posíláme vždy, takže nula je skutečný doklad
	// potlačení, ne důsledek toho, že nemáme co potlačit.
	set m($increment(m)) = "netisknoutSloupecTC|1|tc|0|plati"
	set m($increment(m)) = "netisknoutSloupecTC|0|tc|1|plati"

	// ── vyhodit řádky, kde žádný spoj nestaví ───────────────────────────
	// U linky 1 = vyhodit. Zapnuto na 362 linkách. Přebíjí parametr `ZASTAVKY_BEZ_ZASTAVENI`.
	// Past tu nehrozí: na lince s tímhle zapnutým bylo před opravou 21. 8. změřeno 1, takže nula
	// je skutečný doklad filtru, ne důsledek toho, že tam nebylo co vyhodit.
	set m($increment(m)) = "tisknoutJenTcKdeSpojeZastavuji|1|radkyBezZastaveni|0|plati"
	set m($increment(m)) = "tisknoutJenTcKdeSpojeZastavuji|0|radkyBezZastaveni|?|plati"

	// ── barvy spojů ─────────────────────────────────────────────────────
	// U linky 1 = tiskni černě (barvy potlačit). Zapnuto na 24 linkách; barvu má v datech 3 926 spojů.
	//
	// ⚠️ Řádek „potlačeno" **zůstává na `?`**, a to schválně: „žádné barvy" by vyšlo i na lince,
	// jejíž spoje barvu vůbec nemají, takže by to netvrdilo nic o potlačení. Tvrdit se dá **opačný
	// směr** - na lince, kde barvy povolené jsou a spoje je mají, musí v datech být. Tím je
	// pokrytá i ta část, na kterou byl dřív dluh (barvy jsme neposílali vůbec).
	set m($increment(m)) = "barevnyTiskSpoju|1|barvySpoju|?|plati"
	set m($increment(m)) = "barevnyTiskSpoju|0|barvySpoju|?|plati"
	// Předpoklad nad zdrojovými daty: linka, jejíž spoje barvu skutečně mají (3 926 spojů v datech
	// ji má). Bez toho by hledač trefil jakoukoli linku a nula by nedokazovala nic.
	set m($increment(m)) = "@barvaVDatech||barvySpoju|1|plati"

	// ── odstranit písmena z označení zóny ───────────────────────────────
	// U linky se ukládá hodnota+1 a přebíjí globální (`:1847`): 1 → neodstraňovat, 2 → odstraňovat,
	// 0 nebo nenastaveno → platí globální nastavení.
	// ⚠️ Táž past jako u barev, proto **předpoklad nad zdrojovými daty**: hledá se linka, která
	// v zóně písmeno skutečně má a zóny se u ní tisknou. Bez toho by „nejsou písmena" vyšlo i tam,
	// kde žádná nebyla, a nedokazovalo by nic.
	set m($increment(m)) = "@pismenoVZone-odstranit||zonaPismena|0|plati"
	set m($increment(m)) = "@pismenoVZone-nechat||zonaPismena|1|plati"
}

/// **Zvýraznění zastávky, ze které se uživatel dívá** - tři tvrzení, protože každé samo o sobě jde
/// splnit i špatnou implementací: bez volby se nezvýrazní nic, s kusem názvu se označí právě ty
/// zastávky, které ho v názvu mají, a přidané tarifní číslo to zúží na jediný řádek.
///
/// Bez toho prvního by prošlo i „označ všechno", bez toho třetího „označ celou zastávku vždycky".
Method TestZvyrazneniZastavky()
{
	do ..SetupSys(.sys)
	set db = "CB_CSADAD", sku = 1, lin = "0330511"
	if '##class(%SYS.Namespace).Exists(db) || '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertTrue(1, "linka pro zvýraznění není na tomto serveru - přeskočeno")
		quit
	}

	// ── bez volby se nezvýrazní nic ─────────────────────────────────────
	kill parametry
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
	do $$$AssertEquals(..PocetZvyraznenych(.nazev), 0, "bez volby se nesmí zvýraznit žádná zastávka")

	// ── kus názvu označí právě ty své ───────────────────────────────────
	kill parametry
	set parametry("ZVYRAZNIT_ZASTAVKU") = "velešín"
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
	set oznacenych = ..PocetZvyraznenych(.jmena)
	do $$$AssertTrue(oznacenych > 0, "kus názvu musí zvýraznit aspoň jednu zastávku")
	// všechna označená jména musí ten kus opravdu obsahovat - jinak označujeme naslepo
	set spatne = "", i = ""
	for  {
		set i = $order(jmena(i))
		quit:i=""
		// podmínka za `continue:` končí první mezerou, proto běžný `if`
		if '($zconvert(jmena(i), "L") [ "velešín") {
			set spatne = spatne _ $select(spatne="":"", 1:", ") _ jmena(i)
		}
	}
	do $$$AssertEquals(spatne, "", "zvýrazněné bez hledaného textu v názvu: "_spatne)

	// ── tarifní číslo to zúží na ten jeden řádek ────────────────────────
	// ⚠️ Ne na jediný řádek v celé sestavě: tarifní číslo je v obou směrech totéž, takže se
	// označí jeden řádek **v každém směru**. Tvrzení je proto „všechny označené mají to číslo".
	set prvniTc = ..PrvniZvyrazneneTc()
	if prvniTc '= "" {
		kill parametry
		set parametry("ZVYRAZNIT_ZASTAVKU") = "velešín"
		set parametry("ZVYRAZNIT_TC") = prvniTc
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		set uzsi = ..PocetZvyraznenych(.jmena2, .tcecka)
		do $$$AssertTrue(uzsi > 0, "zúžené zvýraznění nesmí zmizet úplně")
		do $$$AssertTrue(uzsi < oznacenych, "tarifní číslo musí počet zvýrazněných řádků snížit")
		set jina = "", i = ""
		for  {
			set i = $order(tcecka(i))
			quit:i=""
			if +tcecka(i) '= +prvniTc {
				set jina = jina _ $select(jina="":"", 1:", ") _ tcecka(i)
			}
		}
		do $$$AssertEquals(jina, "", "zvýrazněné s jiným tarifním číslem než zvoleným: "_jina)
	}
}

/// Kolik řádků zastávek je označených jako zvýrazněné; `jmena` naplní jejich názvy.
ClassMethod PocetZvyraznenych(ByRef jmena, ByRef tcecka) As %Integer
{
	kill jmena, tcecka
	set pocet = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.%Get("ZASTAVKY")
		continue:'$isobject(zast)
		set it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			continue:radek.%Get("AKTUALNI_ZASTAVKA")'="true"
			set pocet = pocet + 1
			set jmena(pocet) = radek.%Get("NAZEV_ZASTAVKY")
			set tcecka(pocet) = radek.%Get("PORADI")
		}
	}
	quit pocet
}

/// Tarifní číslo prvního zvýrazněného řádku z posledního běhu - podklad pro zúžení.
ClassMethod PrvniZvyrazneneTc() As %String
{
	set key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.%Get("ZASTAVKY")
		continue:'$isobject(zast)
		set it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			continue:radek.%Get("AKTUALNI_ZASTAVKA")'="true"
			ret radek.%Get("PORADI")
		}
	}
	quit ""
}

/// **Ruční pořadí spojů u linky** - dvě tvrzení naráz, protože jedno bez druhého nic neříká:
/// platné pořadí se použije **a** rozbité se zahodí. Kdyby platilo jen první, stačilo by pořadí
/// slepě opsat; kdyby jen druhé, stačilo by ho ignorovat (což jsme do 21. 8. dělali).
///
/// Není to řádek matice schválně: matice měří výstup `GetData`, kdežto tohle je tvrzení o pořadí
/// sloupců, které by se z JSON muselo zpětně rekonstruovat přes skupiny dnů. Přímé srovnání
/// se seznamem z EDISONu je čitelnější i přesnější.
Method TestRucniPoradiSpoju()
{
	set sonda = "ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe"
	set agenda = "ALVA.TemplateDesigner.Agendy.LinkovyJrAd"

	// ── platné pořadí se použije ────────────────────────────────────────
	if $classmethod(sonda, "NajdiLinkuSRucnimPoradim", 1, .db, .sku, .lin) {
		kill serazene
		do $classmethod(agenda, "SeradSpoje", db, sku, lin, "T", .serazene, .pocet)
		set nase = "", i = ""
		for  {
			set i = $order(serazene(i))
			quit:i=""
			set nase = nase _ $select(nase="":"", 1:",") _ serazene(i)
		}
		set rucne = "", poradi = ""
		for  {
			set poradi = $order(^[db]JRL(sku, lin, "PATCH", "SORT", "T", poradi))
			quit:poradi=""
			set rucne = rucne _ $select(rucne="":"", 1:",") _ +$get(^[db]JRL(sku, lin, "PATCH", "SORT", "T", poradi))
		}
		do $$$AssertEquals(nase, rucne, "ruční pořadí u linky "_db_" "_lin_" se musí použít")
	}
	else {
		do $$$AssertTrue(1, "na tomto serveru není linka s platným ručním pořadím - přeskočeno")
	}

	// ── rozbité pořadí se zahodí a spočítá ──────────────────────────────
	// Kontrola je tvrdší, než se zdá: rozbité pořadí typicky vznikne přidáním spoje, takže
	// nesmí chybět **žádný** spoj - kdybychom neplatné pořadí použili, tenhle počet by nesedl.
	if $classmethod(sonda, "NajdiLinkuSRucnimPoradim", 0, .db2, .sku2, .lin2) {
		kill serazene2
		do $classmethod(agenda, "SeradSpoje", db2, sku2, lin2, "T", .serazene2, .pocet2)
		set vDatech = 0, sp = ""
		for  {
			set sp = $order(^[db2]JRL(sku2, lin2, "T", sp))
			quit:sp=""
			continue:'$isvalidnum(sp)
			set vDatech = vDatech + 1
		}
		do $$$AssertEquals(pocet2, vDatech, "rozbité ruční pořadí ("_db2_" "_lin2_") se musí zahodit a spočítat - všechny spoje")
	}
	else {
		do $$$AssertTrue(1, "na tomto serveru není linka s rozbitým ručním pořadím - přeskočeno")
	}
}

/// Hlavní brána matice: **žádný rozdíl u nastavení, které už umíme**.
Method TestMaticeNastaveni()
{
	do ..SetupSys(.sys)
	do ..Matice(.m)

	set nalezy = "", dluhy = 0, neovereno = "", overeno = 0, jenZmereno = 0, i = ""
	for  {
		set i = $order(m(i))
		quit:i=""
		set klic = $piece(m(i), "|", 1)
		set hodnota = $piece(m(i), "|", 2)
		set mereni = $piece(m(i), "|", 3)
		set cekano = $piece(m(i), "|", 4)
		set stav = $piece(m(i), "|", 5)
		set popis = $select($extract(klic)="@":"předpoklad "_$extract(klic,2,*), 1:klic_"=„"_hodnota_"""")_" → "_mereni

		// Km sloupce musí být u linky nakonfigurované, jinak by tvrzení „km se netisknou" vyšlo
		// triviálně i tam, kde žádné nejsou.
		if '..NajdiPripad(klic, hodnota, mereni, .db, .sku, .lin) {
			set neovereno = neovereno _ $select(neovereno="":"", 1:", ") _ popis
			continue
		}
		kill parametry
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		set zmereno = ..Zmer(mereni)

		if cekano = "?" {
			set jenZmereno = jenZmereno + 1
			continue
		}
		if zmereno = cekano {
			set overeno = overeno + 1
			continue
		}
		if $piece(stav, ":", 1) = "dluh" {
			set dluhy = dluhy + 1
			continue
		}
		set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ db_" "_lin_" "_popis_": čekáno "_cekano_", změřeno "_zmereno
	}

	if 'overeno && (nalezy = "") {
		do $$$AssertTrue(1, "žádná linka k ověření na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(nalezy, "", "nastavení se nedodržuje: "_nalezy)
	// Souhrn je součástí výsledku, ne ozdoba: „zelená" musí říct, kolik toho reálně kryje.
	do $$$AssertNotEquals(overeno, "", "ověřeno "_overeno_", jen změřeno "_jenZmereno_", známých dluhů "_dluhy)
	if neovereno '= "" {
		do $$$AssertTrue(1, "v datech se nevyskytuje, zůstává NEOVĚŘENÉ: "_neovereno)
	}
}

/// Známý dluh, který už se chová správně, je **mrtvý záznam** - ať se z matice přepíše na `plati`
/// a začne se hlídat. Bez toho by tam dluhy zůstaly navěky a matice by tvrdila méně, než umíme.
Method TestZadnyDluhNeniJizSplneny()
{
	do ..SetupSys(.sys)
	do ..Matice(.m)
	set splnene = "", i = ""
	for  {
		set i = $order(m(i))
		quit:i=""
		continue:$piece($piece(m(i), "|", 5), ":", 1)'="dluh"
		set klic = $piece(m(i), "|", 1), hodnota = $piece(m(i), "|", 2)
		set mereni = $piece(m(i), "|", 3), cekano = $piece(m(i), "|", 4)
		continue:'..NajdiPripad(klic, hodnota, mereni, .db, .sku, .lin)
		kill parametry
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		continue:..Zmer(mereni)'=cekano
		set splnene = splnene _ $select(splnene="":"", 1:", ") _ klic_"=„"_hodnota_""" → "_mereni
	}
	do $$$AssertEquals(splnene, "", "tyhle dluhy už jsou splněné, přepiš je v matici na „plati"": "_splnene)
}

/// Najde linku pro řádek matice. Klíč začínající `@` je **pevně zadaná linka** ve tvaru
/// `@firma~skupina~linka` - pro případ, kdy tvrzení stojí na konkrétní vlastnosti zdrojových dat
/// (spoje mají barvu), kterou hledač podle nastavení u linky nepozná. Jinak se hledá podle
/// nastavení. Vrací 1 = má na čem měřit.
ClassMethod NajdiPripad(klic As %String, hodnota As %String, mereni As %String, ByRef db, ByRef sku, ByRef lin) As %Boolean
{
	// Klíč `@<předpoklad>` = linku hledá **předpoklad nad zdrojovými daty**, ne nastavení u linky.
	// Pinovat konkrétní linku by bylo křehké: čísla linek se v jednotlivých skupinách opakují
	// a zákazník je může upravit, takže by řádek jednou tiše přestal měřit to, co má.
	if klic = "@barvaVDatech" {
		quit ##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinkuSBarvouSpoje(.db, .sku, .lin)
	}
	if klic = "@pismenoVZone-odstranit" {
		quit ##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinkuSPismenemVZone(1, .db, .sku, .lin)
	}
	if klic = "@pismenoVZone-nechat" {
		quit ##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinkuSPismenemVZone(0, .db, .sku, .lin)
	}
	quit ##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinku(klic, hodnota, .db, .sku, .lin, mereni = "km")
}

/// Změří jednu vlastnost výstupu v `^||ML("OUT")`. Vrací 0/1.
/// Nová položka matice, která potřebuje nové měření, přidá **jednu větev sem** - ne vlastní test.
ClassMethod Zmer(mereni As %String) As %Integer
{
	set vysledek = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.%Get("ZASTAVKY")
		set spoje = page.rows.%Get("SPOJE")
		continue:'$isobject(zast)

		if (mereni = "zona") || (mereni = "km") || (mereni = "tc") || (mereni = "zonaPismena") {
			set it = zast.%GetIterator()
			while it.%GetNext(.i, .radek) {
				// ⚠️ U zóny se pomlčka POČÍTÁ: otázka není „má zastávka zónu", ale „tiskne se
				// sloupec zón", a legacy u zapnutého sloupce bez hodnoty tiskne právě „-".
				if mereni = "zona", radek.%Get("ZONY") '= "" set vysledek = 1
				if mereni = "tc", radek.%Get("TC") '= "" set vysledek = 1
				if mereni = "km" {
					set km = radek.%Get("KM")
					if $isobject(km), km.%Size() > 0 set vysledek = 1
				}
				if mereni = "zonaPismena" {
					// Písmeno se pozná tím, že se velká a malá varianta textu liší.
					set z = radek.%Get("ZONY")
					if $zcvt(z, "U") '= $zcvt(z, "L") set vysledek = 1
				}
			}
		}
		elseif mereni = "barvySpoju" {
			continue:'$isobject(spoje)
			set it = spoje.%GetIterator()
			while it.%GetNext(.i, .spoj) {
				if spoj.%Get("FORE_COLOR") '= "" set vysledek = 1
			}
		}
		elseif mereni = "radkyBezZastaveni" {
			// Řádek, na kterém nemá čas ani jeden spoj - přesně to, co legacy umí vyhodit.
			continue:'$isobject(spoje)
			for zi = 0:1:zast.%Size()-1 {
				set maCas = 0
				for si = 0:1:spoje.%Size()-1 {
					set casy = spoje.%Get(si).%Get("CASY")
					continue:'$isobject(casy)
					continue:zi'<casy.%Size()
					if casy.%Get(zi) [ ":" set maCas = 1
				}
				if 'maCas set vysledek = 1
			}
		}
	}
	quit vysledek
}

/// Diagnostika mimo runner (`cache_query_cvar`): celá matice s tím, co reálně vyšlo.
/// Runner hlásí jen pass/fail, takže je potřeba mít kde se podívat, čím to je.
ClassMethod Diag(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		do ..Matice(.m)
		kill ^||ML, radky
		set i = ""
		for  {
			set i = $order(m(i))
			quit:i=""
			set klic = $piece(m(i), "|", 1), hodnota = $piece(m(i), "|", 2)
			set mereni = $piece(m(i), "|", 3), cekano = $piece(m(i), "|", 4), stav = $piece(m(i), "|", 5)
			if '..NajdiPripad(klic, hodnota, mereni, .db, .sku, .lin) {
				set radky($increment(radky)) = klic_"=„"_hodnota_""" "_mereni_" -> V DATECH NENÍ"
				continue
			}
			kill parametry
			do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
			set zmereno = ..Zmer(mereni)
			set verdikt = $select(cekano="?":"jen změřeno", zmereno=cekano:"OK", $piece(stav,":",1)="dluh":"DLUH", 1:"ROZDÍL")
			set radky($increment(radky)) = klic_"=„"_hodnota_""" "_mereni_" -> "_zmereno_" (čekáno "_cekano_") "_verdikt_"   "_db_" "_sku_"/"_lin
		}
		// Poslední čtení dat nechalo v OUT celý výstup linky - bez vyklizení by se souhrn přilepil
		// za něj a diagnostika by vrátila megabajt JSONu.
		kill ^||ML
		set idx = ""
		for  {
			set idx = $order(radky(idx))
			quit:idx=""
			set ^||ML("OUT", $increment(^||ML("OUT"))) = radky(idx)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
