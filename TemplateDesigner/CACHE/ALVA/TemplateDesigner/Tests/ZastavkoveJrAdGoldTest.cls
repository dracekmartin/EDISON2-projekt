Include %occInclude

/// Základní regresní pojistka: 1:1 kontrola výstupu GetData agendy ZastavkoveJrAd proti
/// zmrazenému "golden" digestu na pár reprezentativních gold linkách.
///
/// ⚠️ ZÁMĚRNĚ PEVNĚ VŮČI CB_CSADAD DATŮM (per-firma namespace). Není to úplná/robustní verze
/// (data mohou driftovat = falešný pád → přegeneruj digest; jiný server bez CB_CSADAD → skip).
/// Robustní verze (zmrazit vstupní ^JRL/^JRZ/^JRSET do izolovaného namespace) = viz roadmap.
/// Smysl: zachytit, že jsme kódem nerozbili nic zásadního v datovém výstupu.
///
/// Přegenerování baseline po ZÁMĚRNÉ změně výstupu: spusť test, z hlášky „aktuální: <digest>"
/// vezmi nový digest a přepiš konstantu Expected*.
///
/// ⚠️ **Kdo přidá pole do `values` nebo do řádků, musí přebaselinovat hned ve svém commitu.**
/// Digest je MD5 celého JSON, takže **nové pole změní md5, ale ne počty** - a pád pak spadne na
/// někoho jiného za dva dny (tak to bylo u #12997 s polem `BARVA_LINKY`). Když počty sedí a liší se
/// jen md5, hledej **přidané/změněné pole**, ne rozbitou logiku; `AdProdlouzeniProbe.StrankaValues`
/// vypíše `values` jedné stránky pro porovnání.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdGoldTest Extends ALVA.EDTesting.MLTestCase
{

/// Namespace, kde žijí gold data (AD je per-firma-namespace).
Parameter GOLDDB = "CB_CSADAD";

Method TestGold0365001()
{
	// Re-baseline 2026-07-31 (#13045): denní značky týdenní platnosti se přestaly tisknout tam,
	// kde je říká nadpis sloupce → o 14 řádků legendy méně (pozn 110 → 96), odjezdy beze změny.
	// Re-baseline 2026-07-31 (#13008): do `values` přibylo pole DOPRAVCI_SOUHRNNY_JR → mění se JEN
	// md5, počty stránek, zastávek, odjezdů i poznámek zůstaly shodné (podpis přidaného pole).
	// Kontrola při dalším posunu: **nejdřív porovnej složky digestu** - když sedí počty a liší se
	// jen md5, je to přidané/přejmenované pole, ne změna chování (past, na kterou jsem 31. 7. najel).
	// Re-baseline 2026-08-03 (#13088): přibylo pole SLOUPEK ve `values` i v řádcích ZASTAVKY → opět jen md5.
	// Re-baseline 2026-08-03 (#13088 podruhé): + SLOUPEK_SPOJU, SLOUPEK_POPIS, SLOUPEK_VARIANT → dtto.
	// Re-baseline 2026-08-03 (#13088 potřetí): + ZDROJ_STANOVISTE. Výchozí zdroj stanoviště zůstal
	// označník, takže se kromě nového pole nic nezměnilo - a to je zároveň důkaz, že nový přepínač
	// je zpětně neutrální.
	// Re-baseline 2026-08-03 (#13088 počtvrté): + CIS čísla zastávek, pak přejmenovaná na příponu
	// `_CIS` dle konvence agendy (CISLO_ZASTAVKA_CIS …) → dtto, jen md5.
	// Re-baseline 2026-08-03 (#13048 bod 1): přestupy dostaly řádek legendy (kategorie „prestup") →
	// ⚠️ **mění se i POČTY**, ne jen md5: pozn 96 → 157 (+61 na 39 stránek, tedy max 2 řádky na
	// stránku pro „A" a „L"). Je to záměrná změna výstupu, ne přidané pole. Na vývěsce se ty řádky
	// objeví jen když prvek zastávek přestupy tiskne - Caché posílá nadmnožinu a prořezává ji
	// render-time filtr (`NoteVisibilityFilter.ShouldHidePrestupLegend`).
	// Re-baseline 2026-08-03 (#12997): + pole BARVA_LINKY_2 (druhá barva linky) → zpět jen md5.
	// Re-baseline 2026-08-03 (#13093 bod 1): + pole VYLUKOVE_TEXTY. **Počty poznámek se nezměnily**,
	// což je zároveň důkaz, že nový parametr „Poznámky zadané u linky" je defaultně vypnutý a
	// existujícím šablonám se legenda nerozrostla.
	// Re-baseline 2026-08-10 (#13045b vlastní sloupce): + pole NZ_KONFIG a NEVYPISOVAT_ZNACKY_SLOUPCU
	// (podklad pro prázdninové vzory v editoru sloupců) → jen md5. Počty beze změny = režim
	// „automaticky" se vlastními sloupci nehnul; paritu presetů hlídá ZastavkoveJrAdDefiniceParitaTest.
	// Re-baseline 2026-08-10 (#13045b etapa 2): + pole PRESETY_SLOUPCU → jen md5, počty beze změny.
	// Re-baseline 2026-08-12 (#12843): varianty tras se rozlišují jen podle úseku OD vývěskové zastávky
	// dál (1:1 legacy, dřív se srovnával celý směr). ⚠️ Mění se **obsah**, ne počty: sloupců variant
	// 116 → 60, protože 56 z nich mělo v tištěném úseku identická čísla a lišilo se jen před vývěskou.
	// Počty stránek/zastávek/odjezdů/poznámek zůstaly shodné = neztratil se žádný spoj ani řádek.
	// Re-baseline 2026-08-12 (#12843 podruhé): jediný sloupec časů se vybírá podle POKRYTÍ seznamu
	// (kolik zastávek trasa od vývěsky obslouží), ne podle četnosti spojů - nejvíc spojů měla často
	// zkrácená trasa a sloupec končil v polovině. Mění se hodnoty CAS_JIZDY, počty zůstávají.
	// Re-baseline 2026-08-13 (`B67` kroky 1+2): dvě záměrné změny obsahu naráz. (1) CAS_JIZDY má tři
	// stavy - zastávka, kterou základní trasa projíždí bez stání, má „<" místo prázdna (legacy ho tam má).
	// (2) Variantní sloupec nese průběh JEDNÉ vybrané trasy (delší jízda vyhrává), ne maximum per
	// zastávka, které mohlo dát kombinaci, kterou nejede žádný spoj. ⚠️ **Počty jsou shodné**
	// (pages/zast/odj/pozn beze změny) = neztratil se žádný spoj ani řádek, změnily se jen hodnoty
	// v časových sloupcích. Že sloupec odpovídá reálnému spoji, hlídá `TestVariantniSloupecJeProbehJedneTrasy`.
	// Re-baseline 2026-08-14 (`B82`): presety vlastních sloupců posílají u „Pracovních dnů" značku
	// `X` z vyhlášky místo kalendářního `12345` - pracovní dny nezahrnují svátky, kdežto vypsané
	// Po-Pá ano. ⚠️ **Mění se JEN md5**, počty stránek/zastávek/odjezdů/poznámek jsou shodné;
	// ověřeno izolovaně: s presety vrácenými na `12345` projdou odtisky beze změny, takže rozdíl
	// nepochází odjinud než z pole PRESETY_SLOUPCU.
	// Re-baseline 2026-08-14 (`B82` podruhé): vzory posílají „Neděle a svátky" jako samotné `+`
	// a slitý víkend jako `6+` - „+" znamená „neděle a svátky" i u sloupce, takže vyjmenovávat
	// k němu ještě `7` bylo matoucí. ⚠️ Zase JEN md5, počty shodné; ověřeno izolovaně vrácením
	// vzorů na `7+`/`67+`, kdy odtisky prošly beze změny.
	// Re-baseline 2026-08-17 (`B90`): zrušený parametr „Písmena variant u odjezdů" - písmena jsou
	// vždy. ⚠️ **Odtisky do teď pinovaly VYPNUTOU větev**: server čte nenastavený parametr jako
	// `false` a gold posílá jen `LINKA_ID`, takže se měřilo chování, které uživatel s aktuálním
	// klientem nikdy neviděl. Teď měří to, co se reálně tiskne. Zase JEN md5, počty shodné do
	// jednoho (stránky, zastávky, odjezdy i poznámky) - přibyla jen písmena u minut.
	// Re-baseline 2026-08-31 (#13158): POPIS_DOPRAVCE už není "Zajišťuje: A, B" v jednom odstavci,
	// ale dopravce s adresou a telefonem po řádcích; POPIS_SMER se přejmenovalo na KONECNA_ZASTAVKA.
	// Obojí mění **jen md5** - počty stránek, zastávek, odjezdů i poznámek zůstaly shodné, což je
	// přesně ten podpis „změněný obsah pole / přejmenované pole", ne změna chování.
	do ..AssertLine(1, "0365001", "pages=39 zast=800 odj=844 pozn=235 md5=LWWibQw+M3THHpxsNQ7AYQ==")
}

Method TestGold0365005()
{
	// Re-baseline 2026-07-31 (#13045): dtto - o 15 řádků legendy méně (pozn 183 → 168),
	// počty stránek, zastávek i odjezdů beze změny.
	// Re-baseline 2026-07-31 (#13008): + pole DOPRAVCI_SOUHRNNY_JR ve `values` → jen md5.
	// Re-baseline 2026-08-03 (#13088): + pole SLOUPEK, pak + SLOUPEK_SPOJU/POPIS/VARIANT
	// a nakonec + ZDROJ_STANOVISTE → dtto, jen md5.
	// Re-baseline 2026-08-03 (#13088 počtvrté): + CIS čísla zastávek (pak přejmenovaná) → dtto, jen md5.
	// Re-baseline 2026-08-03 (#13048 bod 1): + řádky legendy pro přestupy → ⚠️ mění i počty
	// (pozn 168 → 198, +30 na 38 stránek). Viz vysvětlení u gold 0365001.
	// Re-baseline 2026-08-03 (#12997): + pole BARVA_LINKY_2 → jen md5.
	// Re-baseline 2026-08-03 (#13093 bod 1): + VYLUKOVE_TEXTY, počty beze změny (parametr je vypnutý).
	// Re-baseline 2026-08-10 (#13045b): + NZ_KONFIG a NEVYPISOVAT_ZNACKY_SLOUPCU → jen md5, počty beze změny.
	// Re-baseline 2026-08-10 (#13045b etapa 2): + pole PRESETY_SLOUPCU → jen md5, počty beze změny.
	// Re-baseline 2026-08-12 (#12843): varianty jen z úseku od vývěskové zastávky - viz gold 0365001,
	// tady taky jen obsah TRASA_MIN, počty beze změny.
	// Re-baseline 2026-08-12 (#12843 podruhé): volba trasy pro jediný sloupec dle pokrytí - viz 0365001.
	// Re-baseline 2026-08-13 (`B67` kroky 1+2): tři stavy CAS_JIZDY + variantní sloupec z jedné trasy -
	// viz gold 0365001. Počty stránek, zastávek, odjezdů i poznámek beze změny.
	// Re-baseline 2026-08-14 (`B82`): presety posílají „Pracovní dny" jako `X` z vyhlášky místo
	// kalendářního `12345` → jen md5, počty shodné (viz vysvětlení u gold 0365001).
	// Re-baseline 2026-08-14 (`B82` podruhé): vzory posílají „Neděle a svátky" jako samotné `+`
	// a slitý víkend jako `6+` - „+" znamená „neděle a svátky" i u sloupce, takže vyjmenovávat
	// k němu ještě `7` bylo matoucí. ⚠️ Zase JEN md5, počty shodné; ověřeno izolovaně vrácením
	// vzorů na `7+`/`67+`, kdy odtisky prošly beze změny.
	// Re-baseline 2026-08-17 (#7959): seznam zastávek zúžený na zastávky **dosažitelné z této
	// vývěsky** - zastávka, kam odtud nic nejede, mizí. ⚠️ **Mění POČTY, ne jen md5**: zastávek
	// 762 → 709. Odjezdů se to nedotklo (498 → 498), což je ten podstatný důkaz: neztratil se žádný
	// spoj, ubyly jen řádky, ke kterým nešlo dojet.
	// Re-baseline 2026-08-31 (#13158): dopravce po řádcích + přejmenování na KONECNA_ZASTAVKA,
	// viz gold 0365001. Jen md5, počty beze změny.
	do ..AssertLine(1, "0365005", "pages=38 zast=709 odj=498 pozn=236 md5=SJZ8uv7e4Rr7SZy7ZvEk8w==")
}

/// Dva variantní sloupce se **nesmí** v tištěném úseku lišit jen před vývěskovou zastávkou - to dá
/// dva sloupce s identickými čísly a uživatel v nich hledá rozdíl, který tam není (TRANSDEV #12843).
/// Před opravou to na 0365001 bylo 56 duplicitních sloupců ze 116; digest sám tuhle vadu nechytí,
/// protože je to hash - tenhle test hlídá ten invariant přímo.
Method TestZadneDuplicitniVariantniSloupce()
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na tomto serveru - přeskočeno")
		quit
	}
	do ..SetupSys(.sys)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = 1
	set ^||ML("IN","LINKA_ID") = "0365001"
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

	set duplicit = 0, sloupcu = 0, kde = "", key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.ZASTAVKY
		if '$isobject(zast) continue
		// Sloupce jako svislé řezy TRASA_MIN, ale jen od vývěskové zastávky dál (projeté se tisknou
		// jen na přání a tam se varianty legitimně liší).
		kill sloupec
		set poVyvesce = 0, it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			if $zcvt(radek.%Get("AKTUALNI_ZASTAVKA"),"L") = "true" set poVyvesce = 1
			continue:'poVyvesce
			set casy = radek.%Get("TRASA_MIN")
			continue:'$isobject(casy)
			for ci = 0:1:casy.%Size()-1 { set sloupec(ci) = $g(sloupec(ci))_"~"_casy.%Get(ci) }
		}
		kill videno
		set ci = ""
		for  {
			set ci = $order(sloupec(ci))
			quit:ci=""
			set sloupcu = sloupcu + 1
			if $data(videno(sloupec(ci))) {
				set duplicit = duplicit + 1
				if kde = "" set kde = "stránka "_key_" ("_page.values.%Get("POPIS_ZASTAVKA")_", směr "_page.values.SMER_")"
			}
			set videno(sloupec(ci)) = ""
		}
	}
	do $$$AssertTrue(sloupcu > 0, "gold 0365001 nedal žádný variantní sloupec - rozbitý fixture?")
	do $$$AssertEquals(duplicit, 0, "duplicitní variantní sloupce ("_duplicit_" z "_sloupcu_"), první: "_kde)
}

/// Třetí gold linka, přibrána 13. 8. (`B67`): ze všech prohledaných má **nejvíc variantních sloupců**
/// (12 na stránku) a na 18 místech „<" v Času, takže hlídá jiný tvar výstupu než 0365001 a 0365005.
/// Ze stejné firmy záměrně - digest na živých datech jiného zákazníka by padal, kdykoli si upraví
/// jízdní řád; ostatní firmy proto drží invariantní testy, ne odtisk.
Method TestGold0330530()
{
	// Re-baseline 2026-08-14 (`B82`): presety posílají „Pracovní dny" jako `X` z vyhlášky místo
	// kalendářního `12345` → jen md5, počty shodné (viz vysvětlení u gold 0365001).
	// Re-baseline 2026-08-14 (`B82` podruhé): vzory posílají „Neděle a svátky" jako samotné `+`
	// a slitý víkend jako `6+` - „+" znamená „neděle a svátky" i u sloupce, takže vyjmenovávat
	// k němu ještě `7` bylo matoucí. ⚠️ Zase JEN md5, počty shodné; ověřeno izolovaně vrácením
	// vzorů na `7+`/`67+`, kdy odtisky prošly beze změny.
	// Re-baseline 2026-08-17 (#7959): dtto zúžení seznamu na dosažitelné zastávky - zastávek
	// 1058 → 1022, poznámek 639 → 638 (s posledním výskytem zmizel jeden řádek legendy atributů).
	// Odjezdy i stránky beze změny. Je to ta linka, na které se to doložilo (Střítež,žel.st.točna
	// měla „<" ve sloupci časů i ve všech osmi variantách).
	// Re-baseline 2026-08-31 (#13158): dopravce po řádcích + přejmenování na KONECNA_ZASTAVKA,
	// viz gold 0365001. Jen md5, počty beze změny.
	// Re-baseline 2026-08-31 (#13158 podruhé): adresa se doplňuje z firmy i tehdy, když provozovna
	// má název a telefon, ale ne ulici („Oblast Písek"). Tahle linka je jediná ze sady, kde se to
	// projevilo - je to zároveň důkaz, že záloha opravdu zabírá. Opět jen md5, počty shodné.
	do ..AssertLine(1, "0330530", "pages=45 zast=1022 odj=1236 pozn=638 md5=q3AA9/bc+hRJVHaqnghsJA==")
}

/// Sada linek pro invarianty žije v `ZastavkoveJrAdSadaTest.Sada` (tam je i popis, co která linka
/// pokrývá, a test proti vyhnití fixture). Tady se jen zbaví typových značek - dva seznamy linek by se
/// rozešly.
ClassMethod Pripady(ByRef pripady)
{
	kill pripady
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).Sada(.sada)
	set i = ""
	for  {
		set i = $order(sada(i))
		quit:i=""
		set pripady($increment(pripady)) = $piece(sada(i), "~", 1, 3)
	}
}

/// Spustí agendu nad jednou linkou sady; 0 = firma nebo linka na tomto serveru není.
ClassMethod NactiLinku(ByRef sys, db As %String, sku As %String, lin As %String) As %Boolean
{
	kill parametry
	quit ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
}

/// Sloupec „Čas jízdy" smí nést jen tři stavy: číslo, „<" (základní trasa zastávkou projíždí bez
/// stání) nebo prázdno (mimo základní trasu). Test hlídá dvě věci naráz: že se do sloupce nedostane
/// nic jiného (od `B67` je to textová hodnota, takže by tudy mohl protéct třeba `|` z W-věty)
/// a že „<" i prázdno na sadě **reálně vznikají** - jinak by byla ta změna inertní a nikdo by si
/// toho nevšiml. Gold linka sama by to nedoložila, na ní ani jeden z těch stavů nevzniká.
Method TestZakladniCasNeseJenPovoleneStavy()
{
	do ..SetupSys(.sys)
	do ..Pripady(.pripady)
	set cisel = 0, mensitek = 0, prazdnych = 0, jinych = 0, prvniJine = "", overeno = 0, kdeMensitko = ""
	set i = ""
	for  {
		set i = $order(pripady(i))
		quit:i=""
		set db = $piece(pripady(i),"~",1), sku = $piece(pripady(i),"~",2), lin = $piece(pripady(i),"~",3)
		continue:'..NactiLinku(.sys, db, sku, lin)
		set overeno = overeno + 1
		set key = ""
		for  {
			set key = $order(^||ML("OUT", key))
			quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			set zast = page.rows.ZASTAVKY
			continue:'$isobject(zast)
			set it = zast.%GetIterator()
			while it.%GetNext(.j, .radek) {
				set c = radek.%Get("CAS_JIZDY")
				if c = "<" {
					set mensitek = mensitek + 1
					if kdeMensitko = "" set kdeMensitko = db_" "_lin_", vývěska "_page.values.%Get("POPIS_ZASTAVKA")_", zastávka "_radek.%Get("NAZEV_ZASTAVKY")
				}
				elseif c = "" { set prazdnych = prazdnych + 1 }
				elseif $isvalidnum(c) { if c > 0 set cisel = cisel + 1 }
				else {
					set jinych = jinych + 1
					if prvniJine = "" set prvniJine = db_" "_lin_" "_radek.%Get("NAZEV_ZASTAVKY")_" = "_c
				}
			}
		}
	}
	if 'overeno {
		do $$$AssertTrue(1, "žádná firma z testovací sady není na tomto serveru - přeskočeno")
		quit
	}
	set stavy = "linek "_overeno_" z "_pripady_", čísel "_cisel_", projíždí "_mensitek_", prázdných "_prazdnych_", jiných "_jinych
	do $$$AssertEquals(jinych, 0, "čas jízdy nese nepovolený stav ("_prvniJine_"; "_stavy_")")
	do $$$AssertEquals(cisel > 0, 1, "žádný čas jízdy - rozbitý fixture? ("_stavy_")")
	// „<" a prázdno umí vyrobit jen část sady, takže na neúplném serveru se ty dva stavy nekontrolují -
	// jinak by test padal kvůli tomu, kde běží, ne kvůli kódu.
	if overeno < pripady {
		do $$$AssertTrue(1, "část sady na serveru chybí, stavy projíždění a prázdno nekontrolovány ("_stavy_")")
	} else {
		do $$$AssertEquals(mensitek > 0, 1, "na celé sadě nevznikl stav projíždění, změna je inertní ("_stavy_")")
		do $$$AssertEquals(prazdnych > 0, 1, "na celé sadě nevzniklo prázdno (zastávka mimo základní trasu) ("_stavy_")")
	}
	// Doklad pro ruční kontrolu na vývěsce - ať je v logu vidět, kde se to dá prokliknout.
	do $$$AssertNotEquals(kdeMensitko, "", "první projíždění: "_kdeMensitko)
}

/// Variantní sloupec musí být průběh **jedné skutečné trasy**. Dřív se skládal jako maximum per
/// zastávka přes spoje varianty, takže mohl nést kombinaci časů, kterou nejede žádný spoj - a v datech
/// to není poznat, protože každé číslo samo o sobě existuje. Test ke každému sloupci hledá trasu,
/// jejíž průběh mu odpovídá ve všech zastávkách; když žádná není, sloupec je míchaný.
/// Jede přes **jádro** sady (pět linek ze čtyř firem), ne jen gold - jedna linka pokrývá jen svůj tvar
/// dat. Celá sada tu záměrně není: hledání odpovídající trasy je nejdražší kontrola z celé sady
/// (sloupce × trasy × zastávky) a na deseti linkách zdvojnásobila dobu běhu, aniž by přidala typ.
Method TestVariantniSloupecJeProbehJedneTrasy()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).Jadro(.pripady)
	set sloupcu = 0, michanych = 0, kde = "", overeno = 0
	set i = ""
	for  {
		set i = $order(pripady(i))
		quit:i=""
		set db = $piece(pripady(i),"~",1), sku = $piece(pripady(i),"~",2), lin = $piece(pripady(i),"~",3)
		continue:'..NactiLinku(.sys, db, sku, lin)
		set overeno = overeno + 1
		set key = ""
		for  {
			set key = $order(^||ML("OUT", key))
			quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			set zast = page.rows.ZASTAVKY
			continue:'$isobject(zast)
			set smer = page.values.SMER
			// Čísla sloupců od vývěskové zastávky dál (projeté se tisknou jen na přání a berou se z téže
			// trasy, takže je nemusíme kontrolovat zvlášť). „<" a prázdno nejsou čas.
			kill hodnoty
			set aktPoradi = "", poVyvesce = 0, it = zast.%GetIterator()
			while it.%GetNext(.j, .radek) {
				if $zcvt(radek.%Get("AKTUALNI_ZASTAVKA"),"L") = "true" set poVyvesce = 1, aktPoradi = radek.%Get("ID")
				continue:'poVyvesce
				set casy = radek.%Get("TRASA_MIN")
				continue:'$isobject(casy)
				for ci = 0:1:casy.%Size()-1 {
					set v = casy.%Get(ci)
					continue:'$isvalidnum(v)
					set hodnoty(ci, radek.%Get("ID")) = v
				}
			}
			continue:aktPoradi=""
			// Trasy, které v tomhle směru někdo jede - jen mezi nimi má smysl hledat.
			kill trasy
			set sp = ""
			for  {
				set sp = $order(^[db]JRL(sku,lin,smer,sp))
				quit:sp=""
				continue:'$isvalidnum(sp)
				set ct = $piece($piece($get(^[db]JRL(sku,lin,smer,sp)),"#",1),":",3)
				if ct '= "" set trasy(ct) = ""
			}
			set ci = ""
			for  {
				set ci = $order(hodnoty(ci))
				quit:ci=""
				set sloupcu = sloupcu + 1
				set sedi = 0, ct = ""
				for  {
					set ct = $order(trasy(ct))
					quit:ct=""
					quit:sedi
					kill wMap
					do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).WMapa(db, sku, lin, ct, .wMap)
					set ok = 1, pp = ""
					for  {
						set pp = $order(hodnoty(ci, pp))
						quit:pp=""
						if ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota($get(wMap(pp))) = "" { set ok = 0  quit }
						if ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasJizdyZMapy(.wMap, aktPoradi, pp) '= hodnoty(ci, pp) { set ok = 0  quit }
					}
					set sedi = ok
				}
				if 'sedi {
					set michanych = michanych + 1
					if kde = "" set kde = db_" "_lin_", stránka "_key_" ("_page.values.%Get("POPIS_ZASTAVKA")_", směr "_smer_"), sloupec "_ci
				}
			}
		}
	}
	if 'overeno {
		do $$$AssertTrue(1, "žádná firma z testovací sady není na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(sloupcu > 0, 1, "sada nedala žádný variantní sloupec - rozbitý fixture? (linek "_overeno_")")
	do $$$AssertEquals(michanych, 0, "variantních sloupců bez odpovídající trasy: "_michanych_" z "_sloupcu_" (linek "_overeno_"), první: "_kde)
}

/// Ověří digest linky proti očekávané hodnotě. Rozlišuje:
///   namespace chybí        → skip (jiný server, legitimní)
///   namespace je, linka ne  → FAIL (gold data zmizela / rozbitý fixture)
///   jinak                   → 1:1 digest assert
Method AssertLine(sku As %String, lin As %String, expected As %String)
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na tomto serveru - gold "_lin_" přeskočen")
		quit
	}
	if '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertTrue(0, "gold linka "_lin_" chybí v "_db_" (namespace je, data ne) - rozbitý fixture?")
		quit
	}

	do ..SetupSys(.sys)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

	set digest = ##class(ALVA.TemplateDesigner.Tests.GoldUtil).Digest()
	do $$$AssertEquals(digest, expected, "gold "_lin_" digest (aktuální: "_digest_")")
}

}
