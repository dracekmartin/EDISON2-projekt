Include (%occInclude, GENERAL)

/// Typová sada linek **linkového** JŘ z AD dat: každá situace, na které záleží, má svou linku.
/// Jedna linka pokrývá jen svůj tvar dat - linka bez návazností například nedokáže doložit, že se
/// přípojné řádky vůbec vyrábějí, takže by změny v nich prošly jako inertní.
///
/// Sada je sestavená z měření (`Profily` nad dostupnými firmami), ne z odhadu. `TestSadaPokryvaTypy`
/// je pojistka proti hnilobě: kdyby zákazník data upravil a linka svůj typ přestala nést, test to
/// řekne - jinak by ostatní testy dál běžely, ale nic by nekontrolovaly.
///
/// Firma, která na serveru není, se přeskočí; sada nesmí padat kvůli tomu, kde běží.
///
/// Sourozenec `ZastavkoveJrAdSadaTest` (tentýž vzor pro vývěsky). Zdroj dat je společný (`^JRL`),
/// ale typy se překrývají jen částečně - linkový JŘ má navíc přípojné řádky, km sloupce, sloupec
/// MHD a skupiny dnů jako svislé bloky.
Class ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest Extends ALVA.EDTesting.MLTestCase
{

/// Firmy, ve kterých se hledají kandidáti (`Profily`). Pořadí = pořadí prohledávání.
Parameter FIRMY = "CB_CSADAD,CB_GWBUS,KV_DP,VS_CSAD,ZL_KOVED,SKNI_MHDNITRA";

/// Sada: `firma~skupina~linka~typy`. Typy jsou značky, které musí být na té lince reálně vidět
/// (kontroluje `TestSadaPokryvaTypy`).
///
/// Naplněno měřením `Profily` 2026-08-19 (48 linek pěti firem); při doplnění linky vždy uveď,
/// co nového pokrývá, ať je vidět, proč v sadě je.
///
/// ⚠️ **Přípojné řádky nese jen `CB_CSADAD`.** Návaznost má v datech kdekdo (21 ze 48 linek), ale
/// jako tištěný řádek se projeví jen tam, kde je u ní nastaveno „Tisk na JŘ jako = Přípoj. řádek"
/// (6 linek, všechny v CB_CSADAD). Bez těch dvou linek by testy přípojných řádků běžely naprázdno.
///
/// ⚠️ **Lichá Tč, stálé značky a poznámky linky nese jen CB_GWBUS.** 0320118 z dat zmizela (zjištěno
/// 30. 10.); 0320157 je nese taky, km sloupce a zóny ve skupině 1 už nemá žádná linka (drží je jiné firmy).
ClassMethod Sada(ByRef sada)
{
	kill sada
	set sada($increment(sada)) = "CB_CSADAD~1~0330510~pripojneRadky,navaznosti,zony,mhdSloupec,skupinyDnu,projizdi,nz,hodneSpoju"
	set sada($increment(sada)) = "CB_CSADAD~1~0330511~pripojneRadky,navaznosti,zdvojeneRadky,bezSkupin,viceKmSloupcu"
	set sada($increment(sada)) = "KV_DP~1~0410107~hodneSpoju,zdvojeneRadky,zony,mhdSloupec,viceKmSloupcu,navaznosti"
	set sada($increment(sada)) = "VS_CSAD~1~0945001~hodneSpoju,zdvojeneRadky,jedeJinudy,prazdno,skupinyDnu"
	set sada($increment(sada)) = "ZL_KOVED~1~0823955~zony,mhdSloupec,zdvojeneRadky,projizdi"
	set sada($increment(sada)) = "CB_GWBUS~1~0320157~mhdSloupec,lichaTc,staleZnacky,poznamkyLinky"
}

/// Jádro sady pro **drahé** kontroly (každý průchod = GetData nad celou linkou i legacy oracle):
/// linky s nejrozdílnějším tvarem - nejbohatší (přípoje, MHD, skupiny dnů), bez skupin dnů,
/// největší podle počtu spojů a nejširší jednostránková.
///
/// **Poslední dvě jsou tu kvůli konkrétním značkám**, ne kvůli tvaru: zásilky za zvláštních
/// podmínek (`POS`) a spoj v systému integrované dopravy (`IDS`) se na první čtyřce nevyskytují,
/// takže věta jejich legendy byla jen opsaná ze zdroje legacy a **porovnáním nepotvrzená**.
/// ⚠️ Tři značky (`SAO`, `CBE`, `zasu`) nemá v datech **nikdo ze šesti firem**, takže jejich věty
/// ověřit nejde a zůstávají opsané - viz `AtributyLinky` v sondě nastavení.
ClassMethod Jadro(ByRef jadro)
{
	kill jadro
	set jadro($increment(jadro)) = "CB_CSADAD~1~0330510"
	set jadro($increment(jadro)) = "CB_CSADAD~1~0330511"
	set jadro($increment(jadro)) = "KV_DP~1~0410107"
	set jadro($increment(jadro)) = "VS_CSAD~1~0945001"
	set jadro($increment(jadro)) = "VS_CSAD~13~0883637"     // POS
	set jadro($increment(jadro)) = "CB_CSADAD~4~0380780"    // IDS
}

/// Změří, co ve výstupu jedné linky (už načtené v `^||ML("OUT")`) reálně je. Klíče `p(...)`:
/// `stranek`, `zastavek`, `prijezdovych`, `spoju`, `maxSpojuNaStranku`, `skupinDnu`, `pripojnych`,
/// `projizdi`, `jedeJinudy`, `prazdnoCas`, `kmSloupcu`, `sZonou`, `sMhd`, `lichychTc`,
/// `sNz`, `druh_<n>` (počet poznámek daného druhu).
///
/// Sdílené sondou i testem, aby se měření a kontrola nerozešly.
ClassMethod ZmerProfil(ByRef p)
{
	kill p
	set p("stranek") = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.%Get("ZASTAVKY")
		set spoje = page.rows.%Get("SPOJE")
		continue:'$isobject(zast)
		set p("stranek") = p("stranek") + 1

		set iz = zast.%GetIterator()
		while iz.%GetNext(.i, .radek) {
			set p("zastavek") = $get(p("zastavek")) + 1
			if radek.%Get("TYP") = "prijezd" set p("prijezdovych") = $get(p("prijezdovych")) + 1
			set zona = radek.%Get("ZONY")
			if (zona '= "") && (zona '= "-") set p("sZonou") = $get(p("sZonou")) + 1
			if radek.%Get("MHD") '= "" set p("sMhd") = $get(p("sMhd")) + 1
			set tc = radek.%Get("TC")
			if $isvalidnum(tc), (tc # 2) = 1 set p("lichychTc") = $get(p("lichychTc")) + 1
			set km = radek.%Get("KM")
			if $isobject(km), km.%Size() > $get(p("kmSloupcu")) set p("kmSloupcu") = km.%Size()
		}

		if $isobject(spoje) {
			if spoje.%Size() > $get(p("maxSpojuNaStranku")) set p("maxSpojuNaStranku") = spoje.%Size()
			kill skupiny
			set is = spoje.%GetIterator()
			while is.%GetNext(.j, .spoj) {
				set p("spoju") = $get(p("spoju")) + 1
				set skupiny(spoj.%Get("SKUPINA")) = ""
				if spoj.%Get("NZ") '= "" set p("sNz") = $get(p("sNz")) + 1
				set casy = spoj.%Get("CASY")
				if $isobject(casy) {
					for ci = 0:1:casy.%Size()-1 {
						set c = casy.%Get(ci)
						if c = "|" { set p("projizdi") = $get(p("projizdi")) + 1 }
						elseif c = "<" { set p("jedeJinudy") = $get(p("jedeJinudy")) + 1 }
						elseif c = "" { set p("prazdnoCas") = $get(p("prazdnoCas")) + 1 }
					}
				}
				set pripoje = spoj.%Get("PRIPOJE")
				if $isobject(pripoje) {
					for pi = 0:1:pripoje.%Size()-1 {
						set p("navaznosti") = $get(p("navaznosti")) + 1
						// „Tisk na JŘ jako = Přípoj. řádek" u návaznosti; bez toho jde návaznost jen
						// do legendy. V datech je to vzácné, proto se počítá zvlášť - jinak by sada
						// tvrdila, že přípojné řádky pokrývá, a přitom by je nikdy nevyrobila.
						continue:pripoje.%Get(pi).%Get("RADEK")'=1
						set p("pripojnych") = $get(p("pripojnych")) + 1
					}
				}
			}
			set pocetSk = 0, sk = ""
			for  { set sk = $order(skupiny(sk))  quit:sk=""  set pocetSk = pocetSk + 1 }
			if pocetSk > $get(p("skupinDnu")) set p("skupinDnu") = pocetSk
		}

		set pozn = page.rows.%Get("POZNAMKY")
		if $isobject(pozn) {
			set ip = pozn.%GetIterator()
			while ip.%GetNext(.k, .r) {
				set druh = r.%Get("DRUH")
				continue:druh=""
				set p("druh_"_druh) = $get(p("druh_"_druh)) + 1
			}
		}
	}
}

/// Je typ ve změřeném profilu vidět? Jedno místo, kde se značka ze sady překládá na měření - jinak
/// by se sada a kontrola rozešly.
ClassMethod MaTyp(typ As %String, ByRef p) As %Boolean
{
	// ⚠️ Číselné srovnání vždy přes `+$get(...)`: `=` je v ObjectScriptu řetězcové, takže nedefinovaná
	// hodnota („") se nerovná 0 a kontrola typu by mlčky vycházela naopak.
	if typ = "pripojneRadky"  quit $get(p("pripojnych")) > 0
	if typ = "navaznosti"     quit $get(p("navaznosti")) > 0
	if typ = "zdvojeneRadky"  quit $get(p("prijezdovych")) > 0
	if typ = "zony"           quit $get(p("sZonou")) > 0
	if typ = "mhdSloupec"     quit $get(p("sMhd")) > 0
	if typ = "viceKmSloupcu"  quit $get(p("kmSloupcu")) > 1
	if typ = "skupinyDnu"     quit $get(p("skupinDnu")) > 1
	if typ = "bezSkupin"      quit +$get(p("skupinDnu")) = 1
	if typ = "projizdi"       quit $get(p("projizdi")) > 0
	if typ = "jedeJinudy"     quit $get(p("jedeJinudy")) > 0
	if typ = "prazdno"        quit $get(p("prazdnoCas")) > 0
	if typ = "lichaTc"        quit $get(p("lichychTc")) > 0
	if typ = "nz"             quit $get(p("sNz")) > 0
	if typ = "hodneSpoju"     quit $get(p("maxSpojuNaStranku")) '< 30
	if typ = "staleZnacky"    quit $get(p("druh_7")) > 0
	if typ = "poznamkyLinky"  quit ($get(p("druh_8")) + $get(p("druh_9")) + $get(p("druh_10"))) > 0
	quit 0
}

/// Načte jednu linku (GetData) s volitelnými parametry sestavy; 0 = firma nebo linka na serveru není.
/// `parametry` je pole `parametry(<klíč IN>) = <hodnota>`.
ClassMethod NactiLinku(ByRef sys, db As %String, sku As %String, lin As %String, ByRef parametry) As %Boolean
{
	if '##class(%SYS.Namespace).Exists(db) quit 0
	if '$data(^[db]JRL(sku, lin, "I")) quit 0
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	set k = ""
	for  {
		set k = $order(parametry(k))
		quit:k=""
		set ^||ML("IN", k) = parametry(k)
	}
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)
	quit 1
}

/// Sada musí pokrývat typy, které tvrdí. Pojistka proti tichému vyhnití fixture: kdyby linka svůj
/// typ přestala nést (zákazník upravil jízdní řád), ostatní testy by běžely dál a nic nekontrolovaly.
Method TestSadaPokryvaTypy()
{
	do ..SetupSys(.sys)
	do ..Sada(.sada)
	kill parametry
	set chybi = "", overeno = 0, prehled = "", i = ""
	for  {
		set i = $order(sada(i))
		quit:i=""
		set db = $piece(sada(i),"~",1), sku = $piece(sada(i),"~",2), lin = $piece(sada(i),"~",3), typy = $piece(sada(i),"~",4)
		continue:'..NactiLinku(.sys, db, sku, lin, .parametry)
		set overeno = overeno + 1
		do ..ZmerProfil(.p)
		set prehled = prehled _ db _ " " _ lin _ " (stran " _ $get(p("stranek")) _ ", spojů " _ $get(p("spoju")) _ "); "
		for ti = 1:1:$length(typy, ",") {
			set typ = $piece(typy, ",", ti)
			continue:typ=""
			continue:..MaTyp(typ, .p)
			set chybi = chybi _ $select(chybi="":"", 1:", ") _ db _ " " _ lin _ ": " _ typ
		}
	}
	if 'overeno {
		do $$$AssertSkipped("žádná firma z testovací sady není na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(chybi, "", "sada už tyhle typy nepokrývá: "_chybi)
	do $$$AssertNotEquals(prehled, "", "pokryto: "_prehled)
}

/// Sada musí dohromady pokrýt **všechny typy**, které `MaTyp` zná. Kdyby některý nepokrývala,
/// tvářily by se testy jako plné pokrytí, ačkoli ten stav nikdo neověřuje. Běží jen na serveru
/// s celou sadou - na neúplném by hlásila chybějící typ kvůli tomu, kde běží.
Method TestSadaPokryvaVsechnyTypy()
{
	set vsechny = "pripojneRadky,navaznosti,zdvojeneRadky,zony,mhdSloupec,viceKmSloupcu,skupinyDnu,bezSkupin,projizdi,jedeJinudy,prazdno,lichaTc,nz,hodneSpoju,staleZnacky,poznamkyLinky"
	do ..Sada(.sada)
	kill pokryto
	set i = ""
	for  {
		set i = $order(sada(i))
		quit:i=""
		set typy = $piece(sada(i),"~",4)
		for ti = 1:1:$length(typy, ",") {
			set typ = $piece(typy, ",", ti)
			continue:typ=""
			set pokryto(typ) = ""
		}
	}
	set chybi = ""
	for ti = 1:1:$length(vsechny, ",") {
		set typ = $piece(vsechny, ",", ti)
		continue:$data(pokryto(typ))
		set chybi = chybi _ $select(chybi="":"", 1:", ") _ typ
	}
	do $$$AssertEquals(chybi, "", "sada nepokrývá typy: "_chybi)
}

/// Diagnostická sonda (mimo runner, přes `cache_query_cvar`): projde linky ve firmách z parametru
/// `FIRMY` a vypíše změřený profil každé. Slouží k **sestavení sady z měření** - ne k testování.
///
/// IN: `FIRMY` (nepovinné, seznam oddělený čárkou), `SKU` (nepovinné, jen tahle skupina linek),
/// `MAX` (nepovinné, kolik linek ze skupiny; výchozí 15)
/// OUT: `firma~skupina~linka~stran~zastávek~příjezdových~spojů~skupinDnů~přípojných~projíždí~jedeJinudy~prázdno~kmSloupců~sZónou~sMhd~lichýchTč~sNz~druhy` + `~` seznam typů
ClassMethod Profily(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set firmy = $get(^||ML("IN","FIRMY"))
		if firmy = "" set firmy = ..#FIRMY
		set maxLinek = +$get(^||ML("IN","MAX"))
		if 'maxLinek set maxLinek = 15
		set jenSkupina = $get(^||ML("IN","SKU"))
		kill parametry
		for fi = 1:1:$length(firmy, ",") {
			set db = $zstrip($piece(firmy, ",", fi), "<>W")
			continue:db=""
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				continue:(jenSkupina'="")&&(sku'=jenSkupina)
				set pocet = 0, lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					quit:pocet'<maxLinek
					continue:'$data(^[db]JRL(sku, lin, "I"))
					continue:'..NactiLinku(.sys, db, sku, lin, .parametry)
					set pocet = pocet + 1
					do ..ZmerProfil(.p)
					// Typy, které linka reálně nese - to je ta hodnota, kvůli které se sonda spouští.
					set typy = "", vsechny = "pripojneRadky,navaznosti,zdvojeneRadky,zony,mhdSloupec,viceKmSloupcu,skupinyDnu,bezSkupin,projizdi,jedeJinudy,prazdno,lichaTc,nz,hodneSpoju,staleZnacky,poznamkyLinky"
					for ti = 1:1:$length(vsechny, ",") {
						set typ = $piece(vsechny, ",", ti)
						continue:'..MaTyp(typ, .p)
						set typy = typy _ $select(typy="":"", 1:",") _ typ
					}
					set radek = db_"~"_sku_"~"_lin
						_"~str="_$get(p("stranek"))_"~zast="_$get(p("zastavek"))_"~prij="_$get(p("prijezdovych"))
						_"~spoju="_$get(p("spoju"))_"~maxSp="_$get(p("maxSpojuNaStranku"))_"~skup="_$get(p("skupinDnu"))
						_"~prip="_$get(p("pripojnych"))_"~navaz="_$get(p("navaznosti"))_"~proj="_$get(p("projizdi"))_"~jinudy="_$get(p("jedeJinudy"))
						_"~prazdno="_$get(p("prazdnoCas"))_"~km="_$get(p("kmSloupcu"))_"~zona="_$get(p("sZonou"))
						_"~mhd="_$get(p("sMhd"))_"~lichaTc="_$get(p("lichychTc"))_"~nz="_$get(p("sNz"))
						_"~"_typy
					// OUT se plní až po měření - `NactiLinku` maže `^||ML`, takže dřív zapsané řádky
					// by zmizely. Držíme je v lokálním poli a vypíšeme na konci.
					set vysledky($increment(vysledky)) = radek
				}
			}
		}
		kill ^||ML
		set idx = ""
		for  {
			set idx = $order(vysledky(idx))
			quit:idx=""
			set ^||ML("OUT", $increment(^||ML("OUT"))) = vysledky(idx)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
