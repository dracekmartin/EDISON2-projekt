Include (%occInclude, GENERAL)

/// Oracle linkového JŘ: spustí **starý modul** (`mkPLinWin^JRapiLinPrtWin` přes `ALVA.JRMAIN.MLAPI`)
/// a náš `LinkovyJrAd.GetData` nad toutéž linkou a porovná je pole po poli.
///
/// **Proč přímo `mkPLinWin` a ne `ALVA.TISKJR.Data.LoadData`:** LoadData je jen překladová vrstva
/// pro VB klienta (měřicí řetězce, formát pro tisk). Skutečná byznys logika je v `mkPLinWin`,
/// takže porovnávat se má s ním - jinak měříme odchylku formátu, ne dat. Stejný důvod, proč
/// `MhdLegacyOracle` obchází `ALVA.TISKMHD.Data.LoadData`.
///
/// **Layout se vědomě neporovnává.** Starý modul rozřezává tabulku na stránkové bloky sám
/// (`Strih`) a k tomu potřebuje šířku tiskové plochy v „měřicích znacích", které mu naměřil klient.
/// My mu proto dáme **záměrně obrovskou plochu**, takže vyrobí jeden blok na směr - a porovnání
/// pak měří **obsah dat**, což je přesně ta hranice, kterou v novém návrháři držíme (data = CO,
/// šablona = JAK). Rozřezání je naše vlastní vrstva a přijde s vlnou `L5`.
///
/// **Vědomé odchylky** (`ZnameOdchylky`) jsou první třídy: rozdíl, který odpovídá deklarované
/// odchylce, se počítá zvlášť a **není pád**. Bez toho by síť zabetonovala starý modul a každé
/// vylepšení z helpdesku by muselo začít bojem s vlastními testy.
Class ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle
{

/// Jméno globálu, do kterého píše legacy engine. Process-private, uklízí se před každým během.
Parameter GLB = "^||TdLinOracle";

/// Trim + srovnání bílých znaků na jednu mezeru. Starý modul skládá texty spojováním kousků
/// a občas mu vyjde mezera navíc („zastávce X  spoj 21"); to není datová odchylka a nesmí se
/// jako odchylka tvářit.
ClassMethod Norm(s As %String) As %String
{
	set t = $zstrip($translate(s, $char(9,10,13), "   "), "<>W")
	for  {
		quit:t'["  "
		set t = $replace(t, "  ", " ")
	}
	quit t
}

/// Je starý modul na tomto serveru vůbec k dispozici?
ClassMethod OracleDostupny() As %Boolean
{
	quit ##class(%Dictionary.CompiledClass).%ExistsId("ALVA.JRMAIN.MLAPI")
}

/// Spustí legacy `mkPLinWin` nad jednou linkou. 0 = starý modul nebo firma na serveru není.
/// Výstup zůstane v globálu `GLB`; hlavička linky v `out("dta",...)`.
ClassMethod SpustLegacy(db As %String, sku As %String, lin As %String, deleni As %Integer, ByRef out, turnusy As %Boolean = 0) As %Boolean
{
	kill out
	quit:'..OracleDostupny() 0
	quit:'##class(%SYS.Namespace).Exists(db) 0

	kill @..#GLB
	kill in
	set in("A",2,"arr",3) = deleni       // dělení sloupců na skupiny dnů (shodné s naším parametrem)
	set in("A",2,"arr",6) = +turnusy     // tisk turnusů ke spojům (náš TURNUSY_KE_SPOJUM)
	set in("A",2,"arr",4) = 2            // poznámky souhrnně na konec - jeden blok legendy, ne per směr
	set in("A",2,"arr",13) = 0           // bez dorovnání prázdnými sloupci (to je layout, řeší L5)

	// Šířky sloupců v „měřicích znacích": bereme délku měřicího řetězce z číselníku starého modulu.
	// Klient tam posílá naměřenou šířku v bodech; nám jde jen o to, aby se Strih měl čeho chytit -
	// plocha je stejně nastavená tak, aby se vešlo všechno.
	kill ms
	do ##class(ALVA.JRMAIN.MLAPI).gMSArrPLinWin(.ms)
	set typ = ""
	for  {
		set typ = $order(ms(typ))
		quit:typ=""
		set ptyp = ""
		for  {
			set ptyp = $order(ms(typ, ptyp))
			quit:ptyp=""
			set in("A",2,"MS",typ,ptyp) = $length($get(ms(typ, ptyp, "MS")))
		}
	}
	// Záměrně obrovská tisková plocha → jeden blok na směr, žádné rozřezání.
	set in("A",2,"dta",1) = 1000000
	set in("A",2,"dta",2) = 1000000

	set err = ##class(ALVA.JRMAIN.MLAPI).mkPLinWin(db, db, sku, lin, ..#GLB, .in, .out)
	quit:err'="" 0
	quit 1
}

/// Čas na společný tvar `H:MM` - starý modul tiskne hodinu bez vodicí nuly, my s ní.
/// Rozdíl ve formátu není datová odchylka a nesmí se jako odchylka tvářit.
ClassMethod NormCas(s As %String) As %String
{
	quit:s'[":" s
	quit +$piece(s, ":", 1) _ ":" _ $piece(s, ":", 2)
}

/// Číselník značek starého modulu v **jeho** pořadí - id značky = pozice v tomto seznamu.
/// Opsáno 1:1 z `GSId^JRapiLinPrtWin` (`C:\DEV\EDISON\JRMAIN\CACHE\JRapiLinPrtWin.mac:1550-1622`).
///
/// ⚠️ Dřívější podklad uváděl „74 = projíždí, 75 = jede jinudy" - to platí pro **překladovou vrstvu
/// starého klienta**, ne pro engine, proti kterému se tu porovnává. Proti `mkPLinWin` je projíždí 1
/// a jede jinudy 2.
Parameter ZNACKY = "|;<;MHD;u;m;^;p;1;2;3;4;5;6;7;+;X;AMS;&;R;Ü;þ;BEV;IDS;NKA;OBC;PCZ;PJI;POS;RBU;SAO;N;V;x;A;L;C;G;a;b;c;šb;šc;še;od;př;zbez;WC;y;z;e;f;g;O;RZ;BWC;CBE;RBP;TP;ZP;M;lod;PR;šbu;šeu;wifi;zasu";

/// Značka podle id z číselníku starého modulu; prázdno u neznámého id.
/// Vrací **týž tvar, jaký nese náš wire** (legacy klíč, ne pozice ve fontu) - převod na piktogram
/// je záležitost vykreslení a přijde s vlnou symbolového fontu.
ClassMethod ZnakZId(id As %String) As %String
{
	// Podmínka postconditionalu končí první mezerou, proto je zapsaná bez mezer.
	quit:'$isvalidnum(id) ""
	quit:(id<1)||(id>$length(..#ZNACKY,";")) ""
	quit $piece(..#ZNACKY, ";", id)
}

/// Hodnota jedné buňky legacy tabulky, převedená na tvar, ve kterém ji nese náš wire.
/// Typy položky: 1 text, 2 symbol, 3 NZ, 4 čas, 5 zmenšený text.
ClassMethod Bunka(idobj As %String, radek As %String, sloupec As %String) As %String
{
	set glb = ..#GLB
	set typ = $get(@glb@(idobj, "dta", radek, sloupec, 1, "arr", 1))
	quit:typ="" ""
	if typ = 4 quit ..NormCas($get(@glb@(idobj, "dta", radek, sloupec, 1, "dta", 4)))
	if typ = 2 quit ..ZnakZId($get(@glb@(idobj, "dta", radek, sloupec, 1, "dta", 2)))
	if typ = 3 quit $get(@glb@(idobj, "dta", radek, sloupec, 1, "dta", 3))
	if typ = 5 quit ..Norm($get(@glb@(idobj, "dta", radek, sloupec, 1, "dta", 5)))
	quit ..Norm($get(@glb@(idobj, "dta", radek, sloupec, 1, "dta", 1)))
}

/// Načte legacy výstup do **normalizovaného** tvaru, ve kterém se porovnává s naším.
/// Klíče `n(...)`: `("spoje", sm)`, `("cas", sm, radek, spoj)`, `("zast", sm, radek)`,
/// `("km", sm, radek)`, `("zona", sm, radek)`, `("mhd", sm, radek)`, `("pozn", klic)`.
/// Řádek je **pořadové číslo zastávkového řádku** v rámci směru - obě strany ho počítají stejně,
/// takže případný posun je sám o sobě nález.
ClassMethod NactiLegacy(db As %String, sku As %String, lin As %String, deleni As %Integer, ByRef n) As %Boolean
{
	kill n
	quit:'..SpustLegacy(db, sku, lin, deleni, .legOut) 0

	// Hlavička: text dopravce drží legacy v out("dta",10,1) - viz Dump.
	set n("dopravce") = ..Norm($get(legOut("dta", 10, 1)))

	set glb = ..#GLB, idobj = ""
	for  {
		set idobj = $order(@glb@(idobj))
		quit:idobj=""
		set typObj = $get(@glb@(idobj, "arr", 1))

		// ── blok poznámek (typ 3) ───────────────────────────────────────────
		if typObj = 3 {
			set pi = ""
			for  {
				set pi = $order(@glb@(idobj, "dta", pi))
				quit:pi=""
				set druh = $get(@glb@(idobj, "dta", pi, "arr", 1))
				set uvod = $get(@glb@(idobj, "dta", pi, "arr", 2))
				// uvození: 0 bez id, 1 text, 2 znak, 3 NZ - hodnota je ve stejně číslovaném dta
				set znak = ""
				if uvod = 1 set znak = ..Norm($get(@glb@(idobj, "dta", pi, "dta", 1)))
				if uvod = 2 set znak = ..ZnakZId($get(@glb@(idobj, "dta", pi, "dta", 2)))
				if uvod = 3 set znak = $get(@glb@(idobj, "dta", pi, "dta", 3))
				set text = "", ti = ""
				for  {
					set ti = $order(@glb@(idobj, "dta", pi, "txt", ti))
					quit:ti=""
					set text = text _ $select(text="":"", 1:" ") _ $get(@glb@(idobj, "dta", pi, "txt", ti))
				}
				set klic = druh_"~"_znak_"~"_..Norm(text)
				set n("pozn", klic) = $get(n("pozn", klic)) + 1
			}
			continue
		}
		continue:typObj'=2

		// ── datový blok (typ 2) ─────────────────────────────────────────────
		set sm = $get(@glb@(idobj, "arr", 2))
		kill colTyp, colSpoj, kmSloupce
		set sl = "", poradiSpoju = "", pocetKm = 0
		for  {
			set sl = $order(@glb@(idobj, "column", sl))
			quit:sl=""
			set t = $get(@glb@(idobj, "column", sl, "arr", 1))
			set colTyp(sl) = t
			if t = 7 {
				set colSpoj(sl) = $get(@glb@(idobj, "column", sl, "arr", 3))
				set poradiSpoju = poradiSpoju _ $select(poradiSpoju="":"", 1:"~") _ colSpoj(sl)
			}
			if t = 2 set pocetKm = pocetKm + 1, kmSloupce(pocetKm) = sl
		}
		set n("spoje", sm) = $get(n("spoje", sm)) _ $select($get(n("spoje", sm))="":"", 1:"~") _ poradiSpoju

		set radek = "", idx = 0
		for  {
			set radek = $order(@glb@(idobj, "row", radek))
			quit:radek=""
			continue:$get(@glb@(idobj, "row", radek, "arr", 1))'=2
			set idx = idx + 1
			set sl = "", km = ""
			for  {
				set sl = $order(colTyp(sl))
				quit:sl=""
				set t = colTyp(sl)
				if t = 1 { set n("zona", sm, idx) = ..Bunka(idobj, radek, sl) }
				elseif t = 2 { set km = km _ $select(km="":"", 1:"|") _ ..Bunka(idobj, radek, sl) }
				elseif t = 4 { set n("zast", sm, idx) = ..Bunka(idobj, radek, sl) }
				elseif t = 6 { set n("mhd", sm, idx) = ..Bunka(idobj, radek, sl) }
				elseif t = 7 { set n("cas", sm, idx, colSpoj(sl)) = ..Bunka(idobj, radek, sl) }
			}
			set n("km", sm, idx) = km
		}
	}
	quit 1
}

/// Turnusy spojů **podle starého modulu**: `t(číslo spoje, označení turnusu) = ""`.
///
/// ByRef `prvniRadekTurnusu` / `prvniRadekPlatnosti` = pořadí řádku ve výstupu (od 1) - z nich se
/// pozná, že legacy má turnusy **nad** řádkem platnosti, aniž by se muselo věřit čtení zdroje.
///
/// ⚠️ Vnitřní klíč řádku (`1.001xx` u turnusů, `1.1` u platnosti) se ve výstupu **neobjeví** -
/// řádky jsou přečíslované na posloupnost a klíč zůstal v mezikroku. Turnus se proto poznává po
/// **typu položky**: `5` = „zmenšovaný text na šířku sloupce" (`JRapiLinPrtWin.mac:269`), zatímco
/// platnost jsou symboly (typ `2`). Vedle turnusů má typ 5 jen „zdrojová linka a spoj", a ta vzniká
/// pouze u souhrnného JŘ napříč linkami - tady tedy nehrozí.
ClassMethod NactiLegacyTurnusy(db As %String, sku As %String, lin As %String, ByRef t, ByRef prvniRadekTurnusu, ByRef prvniRadekPlatnosti) As %Boolean
{
	kill t
	set prvniRadekTurnusu = 0, prvniRadekPlatnosti = 0
	quit:'..SpustLegacy(db, sku, lin, 2, .legOut, 1) 0

	set glb = ..#GLB, idobj = ""
	for  {
		set idobj = $order(@glb@(idobj))
		quit:idobj=""
		continue:$get(@glb@(idobj, "arr", 1))'=2

		// sloupce spojů: typ 7 (za zastávkami) i 9 (zrcadlené před zastávkami)
		kill colSpoj
		set sl = ""
		for  {
			set sl = $order(@glb@(idobj, "column", sl))
			quit:sl=""
			set typSl = $get(@glb@(idobj, "column", sl, "arr", 1))
			continue:(typSl'=7)&&(typSl'=9)
			set colSpoj(sl) = $get(@glb@(idobj, "column", sl, "arr", 3))
		}

		set radek = "", poradi = 0
		for  {
			set radek = $order(@glb@(idobj, "row", radek))
			quit:radek=""
			set poradi = poradi + 1
			// jen řádky hlavy (typ řádku 1); zastávkové řádky mají 2
			continue:$get(@glb@(idobj, "row", radek, "arr", 1))'=1
			set sl = ""
			for  {
				set sl = $order(colSpoj(sl))
				quit:sl=""
				set typPol = $get(@glb@(idobj, "dta", radek, sl, 1, "arr", 1))
				if typPol = 5 {
					set hodnota = ..Norm($get(@glb@(idobj, "dta", radek, sl, 1, "dta", 5)))
					continue:hodnota=""
					set t(+colSpoj(sl), hodnota) = ""
					if 'prvniRadekTurnusu set prvniRadekTurnusu = poradi
				}
				if (typPol = 2) && ('prvniRadekPlatnosti) set prvniRadekPlatnosti = poradi
			}
		}
	}
	quit 1
}

/// Načte náš výstup (`LinkovyJrAd.GetData` už proběhl) do téhož normalizovaného tvaru.
ClassMethod NactiNase(ByRef n)
{
	kill n
	set key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set sm = page.values.%Get("SMER")
		// Pruh s dopravcem je hlavičkové pole, tedy per linku, ne per směr.
		set n("dopravce") = ..Norm(page.values.%Get("POPIS_DOPRAVCE"))
		set zast = page.rows.%Get("ZASTAVKY")
		set spoje = page.rows.%Get("SPOJE")
		continue:'$isobject(zast)

		set poradiSpoju = ""
		if $isobject(spoje) {
			set is = spoje.%GetIterator()
			while is.%GetNext(.j, .spoj) {
				set poradiSpoju = poradiSpoju _ $select(poradiSpoju="":"", 1:"~") _ spoj.%Get("CISLO_SPOJE")
			}
		}
		set n("spoje", sm) = poradiSpoju

		set iz = zast.%GetIterator(), idx = 0
		while iz.%GetNext(.i, .radek) {
			set idx = idx + 1
			set n("zast", sm, idx) = ..Norm(radek.%Get("NAZEV_ZASTAVKY"))
			set zona = radek.%Get("ZONY")
			// Starý modul dává u IDS linky bez zóny pomlčku a u ne-IDS linky prázdno - náš wire taky.
			set n("zona", sm, idx) = ..Norm(zona)
			set n("mhd", sm, idx) = ..Norm(radek.%Get("MHD"))
			set km = "", kmArr = radek.%Get("KM")
			if $isobject(kmArr) {
				for ki = 0:1:kmArr.%Size()-1 {
					set km = km _ $select(km="":"", 1:"|") _ kmArr.%Get(ki)
				}
			}
			set n("km", sm, idx) = km
		}

		if $isobject(spoje) {
			set is2 = spoje.%GetIterator()
			while is2.%GetNext(.j2, .spoj2) {
				set cislo = spoj2.%Get("CISLO_SPOJE")
				set casy = spoj2.%Get("CASY")
				continue:'$isobject(casy)
				for ci = 0:1:casy.%Size()-1 {
					set n("cas", sm, ci + 1, cislo) = ..NormCas(casy.%Get(ci))
				}
			}
		}

		set pozn = page.rows.%Get("POZNAMKY")
		if $isobject(pozn) {
			set ip = pozn.%GetIterator()
			while ip.%GetNext(.k, .r) {
				// Poznámky linky nese náš wire jako jeden text s „#" mezi odstavci, starý modul
				// jako samostatné řádky. Srovnáním na mezeru se porovnává obsah, ne způsob dělení.
				set klic = r.%Get("DRUH")_"~"_r.%Get("ZNAK")_"~"_..Norm($translate(r.%Get("TEXT"), "#", " "))
				set n("pozn", klic) = $get(n("pozn", klic)) + 1
			}
		}
	}
}

/// **Vědomé odchylky od starého modulu.** `odch(kategorie, klíč) = důvod`; klíč `"*"` platí pro
/// celou kategorii. Rozdíl, který sem sedí, se počítá jako `odchylka` a **není pád**.
///
/// Tohle je celý smysl téhle vrstvy: síť má chytat, že jsme se od starého modulu rozešli **omylem**,
/// a nesmí bránit tomu, abychom se od něj rozešli **záměrně**. Bez registru by každé vylepšení
/// z helpdesku (analýza §14) začínalo bojem s vlastními testy a skončilo by vypnutím testu.
///
/// **Dva druhy záznamu, a je to podstatný rozdíl:**
///
/// - `odchylka` = **chceme** se lišit. Trvalé, s důvodem. Sem patří vylepšení z helpdesku
///   (analýza §14) a rozhodnutí o tvaru dat.
/// - `dluh` = **nechceme** se lišit, ale zatím se lišíme. Povinně s **vlnou**, ve které zmizí.
///   Dluh není omluva, je to termín.
///
/// Obojí **nepadá**, ale v reportu se jmenuje jinak - jinak by za rok nikdo nepoznal, co byl záměr
/// a co nedodělek. Rozdíl, který sem nesedí, je pád.
///
/// **Pravidlo pro nový záznam:** uveď, čí je to rozhodnutí a proč - „liší se to" není důvod.
/// Odchylka, kterou nikdo neodsouhlasil, je chyba, ne odchylka.
ClassMethod ZnameOdchylky(ByRef odch)
{
	kill odch

	// ── vědomé odchylky ────────────────────────────────────────────────
	// (dnes žádná mimo pravidel podle tvaru hodnot - viz `PravidloOdchylky`)

	// ── známé dluhy ────────────────────────────────────────────────────
	// (dluh na zařazení spoje do skupin dnů splacen 24. 8. - spoj bez týdenní platnosti jde do
	// všech skupin, jako v legacy; náš dřívější odhad z datumů omezení ho zařadil do dvou)

	// (dluh na pořadí spojů splacen 21. 8. - řazení se skládá jako v legacy `mkPoradiSp`,
	// včetně ručního pořadí u linky, takže se hlídá normálně)

	// (dluh na název zastávky v textu návaznosti splacen 24. 8. - věta bere týž zdroj jména jako
	// sloupec, jen vždy ve zkráceném tvaru, přesně jako starý modul)
	// (dluh na tvar názvu zastávky splacen 24. 8. - název se skládá jako v legacy, včetně toho,
	// že upravený název pro MHD platí jen na městských linkách, a že text poznámky má zkrácený
	// tvar tam, kde sloupec má plný)

	// (dluh na poznámky druhu 5 splacen 21. 8. - věty atributů se skládají ze šablon opsaných
	// z legacy, takže se hlídají normálně)
}

/// Odchylky dané **tvarem hodnot**, ne konkrétní linkou - platí všude, kde ta dvojice hodnot padne.
/// Vrací `druh~důvod`, nebo prázdno.
ClassMethod PravidloOdchylky(kat As %String, legHodnota As %String, naseHodnota As %String) As %String
{
	// Starý modul v buňce **přepíše čas** značkou návaznosti (vyčká „m", navazuje „^", pokračuje „u").
	// Náš wire nechá v buňce čas a návaznost nese zvlášť v PRIPOJE - jinak by ji šablona nemohla
	// odlišit řezem ani velikostí, což je přesně to, co chce KU HK (#8360). Šablona si tedy může
	// vybrat, jestli místo času vykreslí značku; data o čas nepřijdou.
	if kat = "cas" {
		if ((legHodnota="m")||(legHodnota="^")||(legHodnota="u")) && (naseHodnota[":") {
			quit "odchylka~návaznost nese wire zvlášť (PRIPOJE), aby ji šlo v šabloně odlišit - #8360; čas v buňce zůstává"
		}
	}
	// Starý modul má na některých řádcích v buňce **zastávky** samotnou značku („x"), ne jméno -
	// systematicky, ne na jedné lince (32 buněk na dvou linkách sady). Buňka přitom není jiný
	// sloupec, sloupec zastávek je v bloku jediný. Dokud není jasné, jestli je to řádek jiného
	// druhu, nebo chyba čtení v téhle síti, **netvrdíme paritu**: je to dluh s krokem, ne
	// odsouhlasená odchylka. Pravidlo je úzké schválně (legacy = jediná známá značka × naše =
	// text s písmenem), aby nezakrylo skutečný rozdíl ve jméně.
	if kat = "zast" {
		if (naseHodnota '= "") && ($zcvt(naseHodnota,"U") '= $zcvt(naseHodnota,"L")) {
			if (legHodnota '= "") && ((";"_..#ZNACKY_";") [ (";"_legHodnota_";")) {
				quit "dluh~L2b starý modul má v buňce zastávky značku místo jména - nerozklíčováno"
			}
		}
	}
	quit ""
}

/// Sedí rozdíl na deklarovanou odchylku nebo dluh? Vrací `druh~důvod`, nebo prázdno.
ClassMethod DuvodOdchylky(ByRef odch, kat As %String, klic As %String, legHodnota As %String, naseHodnota As %String) As %String
{
	set zPravidla = ..PravidloOdchylky(kat, legHodnota, naseHodnota)
	quit:zPravidla'="" zPravidla
	set k = ""
	if $data(odch(kat, klic, "druh")) { set k = klic }
	elseif $data(odch(kat, "*", "druh")) { set k = "*" }
	quit:k="" ""
	quit $get(odch(kat, k, "druh")) _ "~" _ $get(odch(kat, k, "vlna")) _ " " _ $get(odch(kat, k, "duvod"))
}

/// Porovná jednu linku proti starému modulu. Vrací 0 = přeskočeno (starý modul nebo firma tu není).
///
/// `res(kat, "leg"/"our")` = kolik hodnot má která strana, `res(kat, "shoda")`, `res(kat, "rozdil")`,
/// `res(kat, "odchylka")` = kolik rozdílů kryje deklarovaná odchylka, `res(kat, "prvni")` = první
/// nekrytý rozdíl v čitelném tvaru. `res("odchylkaKlic", kat, klic)` = kolikrát se která odchylka
/// uplatnila (podklad pro kontrolu, že v registru neleží mrtvý záznam).
ClassMethod Porovnej(ByRef sys, db As %String, sku As %String, lin As %String, ByRef res) As %Boolean
{
	kill res
	quit:'..OracleDostupny() 0
	quit:'##class(%SYS.Namespace).Exists(db) 0
	quit:'$data(^[db]JRL(sku, lin, "I")) 0

	// Náš výstup se čte jako první - legacy běh přepisuje ^||ML.
	kill parametry
	quit:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry) 0
	do ..NactiNase(.nase)
	// Dělení 2 = výchozí hodnota našeho parametru „Dělení spojů dle platnosti"; obě strany musí
	// dostat totéž, jinak by se porovnávaly jiné sestavy.
	quit:'..NactiLegacy(db, sku, lin, 2, .leg) 0

	do ..ZnameOdchylky(.odch)
	set kontext = db_"~"_lin

	// ── kategorie s jednou hodnotou na směr ─────────────────────────────
	set sm = ""
	for  {
		set sm = $order(leg("spoje", sm))
		quit:sm=""
		do ..PorovnejHodnotu("spoje", kontext_" směr "_sm, $get(leg("spoje", sm)), $get(nase("spoje", sm)), kontext, .odch, .res)
	}

	// ── hlavička: pruh s dopravcem ──────────────────────────────────────
	// Porovnává se od 24. 8. Do té doby se hlavička nekontrolovala vůbec, takže „naše vlastní
	// logika skládání dopravců" nebyla proti starému modulu ověřená ani náhodou.
	do ..PorovnejHodnotu("dopravce", kontext, $get(leg("dopravce")), $get(nase("dopravce")), kontext, .odch, .res)

	// ── poziční kategorie (hodnota na zastávkový řádek) ─────────────────
	// ⚠️ Iteruje se **sjednocení** klíčů obou stran, ne jen klíče legacy. Kdyby se šlo jen podle
	// legacy, byla by hodnota, kterou pošleme **navíc**, neviditelná - a to je zrádnější než
	// rozdíl v hodnotě: sloupec, který u starého modulu vůbec není (zóna u linky, která ji tisknout
	// nemá), by prošel jako zelený. Našlo se to při opravě čtyřstavového přepínače zóny × km.
	for kat = "zast", "km", "zona", "mhd" {
		kill smery, indexy
		do ..SjednoceniSmeru(kat, .leg, .nase, .smery)
		set sm = ""
		for  {
			set sm = $order(smery(sm))
			quit:sm=""
			kill indexy
			do ..SjednoceniIndexu(kat, sm, .leg, .nase, .indexy)
			set idx = ""
			for  {
				set idx = $order(indexy(idx))
				quit:idx=""
				do ..PorovnejHodnotu(kat, kontext_" směr "_sm_" řádek "_idx, $get(leg(kat, sm, idx)), $get(nase(kat, sm, idx)), kontext, .odch, .res)
			}
		}
	}

	// ── časy (řádek × spoj), taky přes sjednocení ───────────────────────
	kill smery
	do ..SjednoceniSmeru("cas", .leg, .nase, .smery)
	set sm = ""
	for  {
		set sm = $order(smery(sm))
		quit:sm=""
		kill indexy
		do ..SjednoceniIndexu("cas", sm, .leg, .nase, .indexy)
		set idx = ""
		for  {
			set idx = $order(indexy(idx))
			quit:idx=""
			kill spoje
			set spoj = ""
			for  { set spoj = $order(leg("cas", sm, idx, spoj))  quit:spoj=""  set spoje(spoj) = "" }
			set spoj = ""
			for  { set spoj = $order(nase("cas", sm, idx, spoj))  quit:spoj=""  set spoje(spoj) = "" }
			set spoj = ""
			for  {
				set spoj = $order(spoje(spoj))
				quit:spoj=""
				do ..PorovnejHodnotu("cas", kontext_" směr "_sm_" řádek "_idx_" spoj "_spoj, $get(leg("cas", sm, idx, spoj)), $get(nase("cas", sm, idx, spoj)), kontext, .odch, .res)
			}
		}
	}

	// ── poznámky (multimnožina druh~znak~text) ──────────────────────────
	kill vsechny
	set klic = ""
	for  { set klic = $order(leg("pozn", klic))  quit:klic=""  set vsechny(klic) = "" }
	set klic = ""
	for  { set klic = $order(nase("pozn", klic))  quit:klic=""  set vsechny(klic) = "" }
	set klic = ""
	for  {
		set klic = $order(vsechny(klic))
		quit:klic=""
		// Porovnává se **přítomnost**, ne počet: starý modul tiskne legendu jednou souhrnně
		// (`arr,4` = 2), náš wire ji nese na každé stránce. Násobek počtu je rozdíl struktury,
		// ne dat - kdyby se porovnávaly počty, hlásila by se odchylka u každé obousměrné linky.
		// Klíč registru je **druh poznámky**, ne linka - „atributy spojů mají jiné texty" je dobře
		// ohraničený dluh, kdežto klíč per linka by ho schoval a „*" by oslepil celou legendu.
		do ..PorovnejHodnotu("pozn", kontext_" poznámka „"_klic_"""", ($get(leg("pozn", klic))>0), ($get(nase("pozn", klic))>0), "druh"_$piece(klic, "~", 1), .odch, .res)
	}
	quit 1
}

/// Směry, které v dané kategorii má **kterákoli** strana.
ClassMethod SjednoceniSmeru(kat As %String, ByRef leg, ByRef nase, ByRef smery)
{
	kill smery
	set sm = ""
	for  { set sm = $order(leg(kat, sm))  quit:sm=""  set smery(sm) = "" }
	set sm = ""
	for  { set sm = $order(nase(kat, sm))  quit:sm=""  set smery(sm) = "" }
}

/// Indexy řádků, které v dané kategorii a směru má **kterákoli** strana.
ClassMethod SjednoceniIndexu(kat As %String, sm As %String, ByRef leg, ByRef nase, ByRef indexy)
{
	kill indexy
	set idx = ""
	for  { set idx = $order(leg(kat, sm, idx))  quit:idx=""  set indexy(idx) = "" }
	set idx = ""
	for  { set idx = $order(nase(kat, sm, idx))  quit:idx=""  set indexy(idx) = "" }
}

/// Jedno porovnání dvou hodnot - jediné místo, kde se rozhoduje „shoda / vědomá odchylka / rozdíl".
ClassMethod PorovnejHodnotu(kat As %String, popis As %String, legHodnota As %String, naseHodnota As %String, kontext As %String, ByRef odch, ByRef res)
{
	set res(kat, "leg") = $get(res(kat, "leg")) + 1
	if legHodnota = naseHodnota {
		set res(kat, "shoda") = $get(res(kat, "shoda")) + 1
		quit
	}
	set duvod = ..DuvodOdchylky(.odch, kat, kontext, legHodnota, naseHodnota)
	if duvod '= "" {
		set druh = $piece(duvod, "~", 1)
		set res(kat, druh) = $get(res(kat, druh)) + 1
		set res("duvod", kat, druh) = $piece(duvod, "~", 2, 99)
		quit
	}
	set res(kat, "rozdil") = $get(res(kat, "rozdil")) + 1
	set popisRozdilu = popis_": starý ["_legHodnota_"] × náš ["_naseHodnota_"]"
	if $get(res(kat, "prvni")) = "" set res(kat, "prvni") = popisRozdilu
	// Prvních pár rozdílů se drží celé - jeden příklad na diagnózu nestačí, a všechny by zahltily.
	if $get(res(kat, "vzorku")) < 8 {
		set res(kat, "vzorku") = $get(res(kat, "vzorku")) + 1
		set res(kat, "vzorek", res(kat, "vzorku")) = popisRozdilu
	}
}

/// Diagnostika (mimo runner, přes `cache_query_cvar`): porovná linky sady proti starému modulu
/// a vypíše souhrn per linka a kategorie. Slouží k **měření**, test si výsledek vyhodnocuje sám.
/// IN: `DB`/`SKU`/`LIN` pro jednu linku, jinak se bere jádro sady.
ClassMethod Souhrn(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
		kill pripady
		if lin '= "" {
			set pripady(1) = db_"~"_sku_"~"_lin
		} else {
			do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.pripady)
		}
		kill ^||ML, radky
		set i = ""
		for  {
			set i = $order(pripady(i))
			quit:i=""
			set d = $piece(pripady(i),"~",1), s = $piece(pripady(i),"~",2), l = $piece(pripady(i),"~",3)
			if '..Porovnej(.sys, d, s, l, .res) {
				set radky($increment(radky)) = d_" "_l_" SKIP (starý modul nebo firma tu není)"
				continue
			}
			for kat = "spoje", "dopravce", "zast", "km", "zona", "mhd", "cas", "pozn" {
				continue:'$data(res(kat))
				set radky($increment(radky)) = d_" "_l_" "_kat
					_" porovnano="_+$get(res(kat,"leg"))
					_" shoda="_+$get(res(kat,"shoda"))
					_" rozdil="_+$get(res(kat,"rozdil"))
					_" odchylka="_+$get(res(kat,"odchylka"))
					_" dluh="_+$get(res(kat,"dluh"))
				set vi = ""
				for  {
					set vi = $order(res(kat, "vzorek", vi))
					quit:vi=""
					set radky($increment(radky)) = "    " _ res(kat, "vzorek", vi)
				}
			}
		}
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

/// Diagnostika (mimo runner, přes `cache_query_cvar`): vypíše strukturu legacy výstupu jedné linky,
/// aby šlo psát normalizaci proti skutečnosti, ne proti dokumentaci.
/// IN: `DB`, `SKU`, `LIN`, `MAX` (kolik uzlů), `FILTR` (nepovinný podřetězec uzlu)
/// Vypíše prvních N rozdílných názvů zastávek vedle sebe. IN: `DB`, `SKU`, `LIN`, `MAX`.
ClassMethod RozdilyZastavek(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
		set maxRadku = +$get(^||ML("IN","MAX"))
		if 'maxRadku set maxRadku = 12
		kill ^||ML, radky
		if '..NactiLegacy(db, sku, lin, 2, .leg) {
			set ^||ML("OUT", $increment(^||ML("OUT"))) = "SKIP"
			ret $$$OK
		}
		kill parametry
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		do ..NactiNase(.nase)
		set pocet = 0
		for sm = "T", "Z" {
			set idx = ""
			for  {
				set idx = $order(leg("zast", sm, idx))
				quit:idx=""
				quit:pocet'<maxRadku
				set l = $get(leg("zast", sm, idx)), n = $get(nase("zast", sm, idx))
				continue:l=n
				set pocet = pocet + 1
				set radky($increment(radky)) = sm_"/"_idx_" stary: "_l
				set radky($increment(radky)) = sm_"/"_idx_" nas:   "_n
			}
		}
		kill ^||ML
		set i = ""
		for  {
			set i = $order(radky(i))
			quit:i=""
			set ^||ML("OUT", $increment(^||ML("OUT"))) = radky(i)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kolik **stránkových bloků** vyrobí starý modul při různě široké tiskové ploše - podklad
/// k vlně `L5`. Náš oracle jinde dává plochu záměrně obrovskou, aby neřezal; tady se zkoumá
/// právě to řezání. IN: `DB`, `SKU`, `LIN`.
ClassMethod BlokyDleSirky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
		kill ^||ML, radky
		for sirka = 120, 200, 400, 1000000 {
			kill legOut
			kill @..#GLB
			kill in
			set in("A",2,"arr",3) = 2
			set in("A",2,"arr",4) = 2
			set in("A",2,"arr",13) = 0
			kill ms
			do ##class(ALVA.JRMAIN.MLAPI).gMSArrPLinWin(.ms)
			set typ = ""
			for  {
				set typ = $order(ms(typ))
				quit:typ=""
				set ptyp = ""
				for  {
					set ptyp = $order(ms(typ, ptyp))
					quit:ptyp=""
					set in("A",2,"MS",typ,ptyp) = $length($get(ms(typ, ptyp, "MS")))
				}
			}
			set in("A",2,"dta",1) = sirka
			set in("A",2,"dta",2) = 60
			set chyba = ##class(ALVA.JRMAIN.MLAPI).mkPLinWin(db, db, sku, lin, ..#GLB, .in, .legOut)
			if chyba '= "" {
				set radky($increment(radky)) = "šířka "_sirka_": chyba "_chyba
				continue
			}
			set glb = ..#GLB, idobj = "", bloku = 0, popis = ""
			for  {
				set idobj = $order(@glb@(idobj))
				quit:idobj=""
				continue:$get(@glb@(idobj,"arr",1))'=2
				set bloku = bloku + 1
				set sloupcu = 0, sl = ""
				for  {
					set sl = $order(@glb@(idobj,"column",sl))
					quit:sl=""
					continue:$get(@glb@(idobj,"column",sl,"arr",1))'=7
					set sloupcu = sloupcu + 1
				}
				if bloku < 7 set popis = popis_$select(popis="":"", 1:", ")_"["_$get(@glb@(idobj,"arr",2))_"/"_sloupcu_" spojů]"
			}
			set radky($increment(radky)) = "šířka "_sirka_" → bloků="_bloku_"  "_popis
		}
		kill @..#GLB
		kill ^||ML
		set i = ""
		for  {
			set i = $order(radky(i))
			quit:i=""
			set ^||ML("OUT", $increment(^||ML("OUT"))) = radky(i)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Najde v jádru sady místa, kde **starý modul** vypisuje v buňce značku návaznosti
/// (vyčká `m`, pokračuje `u`, navazuje `^`) místo času - podklad k tomu, aby si šlo v původním
/// tisku ověřit, jestli se kreslí piktogramem, nebo písmenem.
ClassMethod KdeJsouZnackyNavaznosti(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		// ⚠️ Parametry se musí přečíst **před** `kill ^||ML`, jinak už tam nejsou.
		// Bez nich projde jádro sady a z každé linky ukáže první výskyt každé značky;
		// se zadanou linkou vypíše na ní **všechny** - podklad k porovnání s tištěným vzorem.
		set jenLinka = $get(^||ML("IN","LIN"))
		set vstupDb = $get(^||ML("IN","DB")), vstupSku = $get(^||ML("IN","SKU"))
		kill ^||ML, radky
		if jenLinka '= "" {
			set jadro(1) = vstupDb_"~"_vstupSku_"~"_jenLinka
		}
		else { do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro) }
		set i = ""
		for  {
			set i = $order(jadro(i))
			quit:i=""
			set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
			continue:'..NactiLegacy(db, sku, lin, 2, .leg)
			kill videno
			set sm = ""
			for  {
				set sm = $order(leg("cas", sm))
				quit:sm=""
				set radek = ""
				for  {
					set radek = $order(leg("cas", sm, radek))
					quit:radek=""
					set sp = ""
					for  {
						set sp = $order(leg("cas", sm, radek, sp))
						quit:sp=""
						set hod = $get(leg("cas", sm, radek, sp))
						continue:(hod'="m")&&(hod'="u")&&(hod'="^")
						// u konkrétní linky chceme všechny, jinak stačí první výskyt každé značky
						continue:(jenLinka="")&&$data(videno(hod))
						set videno(hod) = ""
						set radky($increment(radky)) = db_" linka "_lin_" · značka ["_hod_"] · směr "_sm_" · tarifní číslo "_radek_" · spoj "_sp
					}
				}
			}
		}
		if '$data(radky) set radky(1) = "V jádru sady se žádná značka návaznosti nevyskytuje."
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

/// Vypíše pořadí spojů obou směrů vedle sebe - starý modul vs my. IN: `DB`, `SKU`, `LIN`.
ClassMethod PoradiSpoju(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
		kill ^||ML, radky
		if '..NactiLegacy(db, sku, lin, 2, .leg) {
			set ^||ML("OUT", $increment(^||ML("OUT"))) = "SKIP: starý modul nebo firma "_db_" není na serveru"
			ret $$$OK
		}
		kill parametry
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		do ..NactiNase(.nase)
		for sm = "T", "Z" {
			set radky($increment(radky)) = sm_" stary: "_$extract($get(leg("spoje", sm)), 1, 400)
			set radky($increment(radky)) = sm_" nas:   "_$extract($get(nase("spoje", sm)), 1, 400)
		}
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

ClassMethod Dump(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
		set maxUzlu = +$get(^||ML("IN","MAX"))
		if 'maxUzlu set maxUzlu = 120
		set filtr = $get(^||ML("IN","FILTR"))
		kill ^||ML

		if '..SpustLegacy(db, sku, lin, 2, .legOut) {
			set ^||ML("OUT", $increment(^||ML("OUT"))) = "SKIP: starý modul nebo firma "_db_" není na serveru"
			ret $$$OK
		}
		// `$query` nad lokálním polem není v ClassMethodu dovolené (private variables) - proto $order.
		set ^||ML("OUT", $increment(^||ML("OUT"))) = "== hlavicka out(dta,*) =="
		set i1 = ""
		for  {
			set i1 = $order(legOut("dta", i1))
			quit:i1=""
			set ^||ML("OUT", $increment(^||ML("OUT"))) = "dta("_i1_") = "_$get(legOut("dta", i1))
			set i2 = ""
			for  {
				set i2 = $order(legOut("dta", i1, i2))
				quit:i2=""
				set ^||ML("OUT", $increment(^||ML("OUT"))) = "dta("_i1_","_i2_") = "_$get(legOut("dta", i1, i2))
			}
		}
		set ^||ML("OUT", $increment(^||ML("OUT"))) = "== glbout =="
		set ref = ..#GLB, pocet = 0
		for  {
			set ref = $query(@ref)
			quit:ref=""
			quit:pocet'<maxUzlu
			continue:(filtr'="")&&(ref'[filtr)
			set pocet = pocet + 1
			set ^||ML("OUT", $increment(^||ML("OUT"))) = $piece(ref, "TdLinOracle", 2)_" = "_$get(@ref)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
