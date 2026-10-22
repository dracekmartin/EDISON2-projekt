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

/// Parametry nabízí každý nezablokovaný typ provozu firmy jako zaškrtávátko, které je zapnuté
/// a jeho změna načte data znovu; zablokovaný typ starý dialog nenabízel, my také ne.
Method TestParametryTypuProvozu()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertTrue(1, "firma KV_DP není na serveru - přeskočeno")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetParameters(.sys)
	do $$$AssertEquals($get(^||ML("ERR")), "", "GetParameters skončil chybou")
	set it = ##class(%DynamicArray).%FromJSON(^||ML("OUT",1)).%GetIterator()
	while it.%GetNext(.k, .p) {
		set nabidnute(p.%Get("name")) = p
		set label = p.%Get("label")
		do $$$AssertTrue('$data(labely(label)), "dva parametry se stejným popiskem """_label_"""")
		set labely(label) = ""
	}
	set tp = "", pocet = 0
	for  {
		set tp = $order(^["KV_DP"]ALVA.MHD.ENUMS("KV_DP","TypyProvozu","DTA",tp))
		quit:tp=""
		set nazev = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).ParametrTypuProvozu(tp)
		if +$get(^["KV_DP"]ALVA.MHD.ENUMS("KV_DP","TypyProvozu","DTA",tp,"Blocked")) {
			do $$$AssertTrue('$data(nabidnute(nazev)), "zablokovaný typ provozu "_tp_" se nabízí")
			continue
		}
		set pocet = pocet + 1
		if '$data(nabidnute(nazev)) {
			do $$$AssertTrue(0, "typ provozu "_tp_" chybí v parametrech")
			continue
		}
		set p = nabidnute(nazev)
		do $$$AssertEquals(p.%Get("type")_"|"_p.%Get("defaultValue")_"|"_p.%Get("serverSide")_"|"_p.%Get("visibleWhenListedIn"), "bool|true|1|TYPY_PROVOZU_LINKY", nazev)
	}
	do $$$AssertTrue(pocet > 0, "KV_DP nemá žádný typ provozu - test nic nedokazuje")
}

/// Tři typy provozu jako na webu DPKV (`Linka1.pdf`, stažený 22. 10.): KV_DP linka 1 dá tytéž
/// spoje v obou směrech jako tamní jízdní řády. Ostatní typy jsou vypnuté. Navíc se porovná se
/// starým tiskem se stejně zaškrtnutými typy - ten výběr typů provozu dělal před tiskem.
Method TestVyberTypuProvozuJakoWeb()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertTrue(1, "firma KV_DP není na serveru - přeskočeno")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	// Čísla spojů opsaná z webového PDF; klíč = nadpis bloku, jak ho tiskneme.
	set web("Pracovní den mimo 1.7. - 31.8. a 27.12. - 31.12.","T") = "1,5,7,9,13,15,19,21,25,29,35,37,43,49,55,57,63,69,75,77,83,85,91,93,97,101,103,107,109,113,117,121,127,133,135,139,145,147,151"
	set web("Pracovní den mimo 1.7. - 31.8. a 27.12. - 31.12.","Z") = "2,6,8,12,14,18,22,24,26,30,34,36,42,48,52,54,60,66,70,72,78,82,86,90,92,96,98,102,106,108,114,120,122,128,134,140,142,146,150"
	set web("Pracovní den 1.7. - 31.8. a 27.12. - 31.12.","T") = "1,5,7,9,13,15,19,21,25,29,31,39,45,51,59,65,71,79,83,87,91,93,97,101,103,107,109,115,117,123,131,135,141,147,151"
	set web("Pracovní den 1.7. - 31.8. a 27.12. - 31.12.","Z") = "2,6,8,12,14,18,22,26,30,34,40,46,52,58,64,70,76,80,82,86,90,92,96,98,102,106,112,118,126,130,136,142,146,150"
	set web("Sobota, neděle a státní svátky","T") = "3,11,17,23,27,33,41,47,53,61,67,73,81,89,95,99,105,111,119,125,129,137,143,149,153"
	set web("Sobota, neděle a státní svátky","Z") = "4,10,16,20,28,32,38,44,50,56,62,68,74,84,88,94,100,104,110,116,124,132,138,144,148,152"
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).NactiSkupinyProvozu("KV_DP", "KV_DP", .poradi, .nadpis)
	set vypnute = "", tp = ""
	for  {
		set tp = $order(nadpis(tp))
		quit:tp=""
		set:'$data(web(nadpis(tp))) vypnute = vypnute_$select(vypnute="":"", 1:",")_tp
	}

	kill ^||ML
	set ^||ML("IN","LINKA_ID") = 1073
	for i=1:1:$length(vypnute, ",") set ^||ML("IN", ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).ParametrTypuProvozu($piece(vypnute, ",", i))) = "false"
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
	do $$$AssertEquals($get(^||ML("ERR")), "", "GetData skončil chybou")
	set idx = ""
	for  {
		set idx = $order(^||ML("OUT", idx))
		quit:idx=""
		continue:'$isvalidnum(idx)
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", idx)), smer = page.values.%Get("SMER")
		// Panel ukazuje typy linky podle tohoto seznamu - vypnuté v něm zůstat musí, jinak by
		// zmizely z panelu a nešly zapnout zpátky. Linka 1 má sedm typů provozu, tisknou se tři.
		set typy = page.values.%Get("TYPY_PROVOZU_LINKY"), seznam = ""
		if $isobject(typy) { for j=0:1:typy.%Size()-1 set seznam = seznam_","_typy.%Get(j) }
		do $$$AssertEquals($select($isobject(typy):typy.%Size(), 1:0), 7, smer_": typy provozu linky "_seznam)
		kill blok, spoje
		set it = page.rows.%Get("SKUPINY_DNU").%GetIterator()
		while it.%GetNext(.k, .s) { set blok(s.%Get("ID")) = s.%Get("NADPIS") }
		set it = page.rows.%Get("SPOJE").%GetIterator()
		while it.%GetNext(.k, .s) { set spoje(blok(s.%Get("SKUPINA")), +s.%Get("CISLO_SPOJE")) = "" }
		set n = ""
		for  {
			set n = $order(spoje(n))
			quit:n=""
			if '$data(web(n, smer)) {
				do $$$AssertTrue(0, smer_": tiskne se blok """_n_""", který na webu není")
				continue
			}
			set nase = "", c = ""
			for  { set c = $order(spoje(n, c)) quit:c=""  set nase = nase_$select(nase="":"", 1:",")_c }
			do $$$AssertEquals(..Serazene(nase), ..Serazene(web(n, smer)), smer_" """_n_"""")
			set vytisteno(n, smer) = ""
		}
	}
	set n = ""
	for  {
		set n = $order(web(n))
		quit:n=""
		for smer = "T", "Z" do $$$AssertTrue($data(vytisteno(n, smer)), smer_": blok """_n_""" chybí")
	}

	if ##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdLegacyOracle).Porovnej(.sys, "KV_DP", "KV_DP", 1073, .res, vypnute) {
		for kat = "spoj", "pozn", "cas", "zast", "blok", "poradi" {
			do $$$AssertTrue(res(kat), "proti starému tisku se liší "_kat_" ("_res(kat,"pocet")_") "_res(kat,"rozdil"))
		}
	} else {
		do $$$AssertTrue(1, "starý tisk MHD na serveru není - porovnání přeskočeno")
	}
}

/// Vypnutý typ provozu proti starému tisku se stejným výběrem na celé sadě linek. Vypíná se typ
/// prvního spoje s ručně zadanou platností, protože takový spoj dostává volnou negativní značku
/// z rozsahu - jeho vynechání posune číslování dalších (starý tisk přiděluje jen tištěným).
Method TestVyberTypuProvozuJakoStaryModul()
{
	set porovnano = 0
	for i=1:1:$length(..#LINKY, ",") {
		set idfa = $piece($piece(..#LINKY, ",", i), ":", 1), lin = $piece($piece(..#LINKY, ",", i), ":", 2)
		continue:'##class(%SYS.Namespace).Exists(idfa)
		set (vypnout, spojId) = ""
		for  {
			set spojId = $order(^[idfa]ALVA.MHD.LSO(idfa,2,"IDX",1,1,1,lin,spojId))
			quit:spojId=""
			set tp = $get(^[idfa]ALVA.MHD.LSO(idfa,2,"DTA",spojId,"DTA",1,"TypProvozuID"))
			if vypnout = "" set vypnout = tp
			if $get(^[idfa]ALVA.MHD.LSO(idfa,2,"DTA",spojId,"DTA",1,"RucniZadani")) set vypnout = tp quit
		}
		continue:vypnout=""
		do ..SysFirmy(.sys, idfa)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdLegacyOracle).Porovnej(.sys, idfa, idfa, lin, .res, vypnout)
		set porovnano = porovnano + 1
		for kat = "spoj", "pozn", "cas", "zast", "blok", "poradi" {
			do $$$AssertTrue(res(kat), idfa_" "_lin_" bez typu provozu "_vypnout_": liší se "_kat_" ("_res(kat,"pocet")_") "_res(kat,"rozdil"))
		}
	}
	do $$$AssertTrue(1, "porovnáno linek: "_porovnano)
}

/// Zvýraznit zastávku jako u linkového AD (`B191d`): kus názvu označí řádky zastávky, číslo zastávky
/// pro adresu v QR jde jen při jediné shodě. KV_DP linka 1 má Tržnici jednou a Tašovice třikrát
/// (Tašovice a dvakrát Tašovice, rozcestí).
Method TestZvyraznitZastavku()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertTrue(1, "firma KV_DP není na serveru - přeskočeno")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetParameters(.sys)
	do $$$AssertTrue(^||ML("OUT",1) [ """ZVYRAZNIT_ZASTAVKU""", "parametr Zvýraznit zastávku chybí")
	do $$$AssertTrue('(^||ML("OUT",1) [ """ZVYRAZNIT_TC"""), "MHD nemá sloupec Tč, Zvýraznit tarifní číslo nemá nabízet")

	for hledana = "TRŽNICE", "tašovice", "" {
		kill ^||ML
		set ^||ML("IN","LINKA_ID") = 1073, ^||ML("IN","SMER_TISKU") = "Tam", ^||ML("IN","ZVYRAZNIT_ZASTAVKU") = hledana
		do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT",1))
		set (oznacene, czst) = "", it = page.rows.%Get("ZASTAVKY").%GetIterator()
		while it.%GetNext(.k, .z) {
			set ma = ($zconvert(z.%Get("NAZEV_ZASTAVKY"), "L") [ $zconvert(hledana, "L")) && (hledana '= "")
			do $$$AssertEquals(z.%Get("AKTUALNI_ZASTAVKA"), $select(ma:"true", 1:"false"), """"_hledana_""": "_z.%Get("NAZEV_ZASTAVKY"))
			if ma set oznacene = oznacene + 1, czst = z.%Get("CZST")
		}
		do $$$AssertEquals(+oznacene, $case(hledana, "TRŽNICE":1, "tašovice":3, :0), """"_hledana_""": počet označených řádků")
		do $$$AssertEquals(page.values.%Get("AKTUALNI_ZASTAVKA_CZST"), $select(oznacene=1:czst, 1:""), """"_hledana_""": číslo zastávky pro QR")
	}
}

/// Zastávky, kde žádný spoj nestaví (`B191d`): s Netisknout zmizí přesně řádky, kde ve stejném
/// výstupu s Tisknout nemá čas žádný tištěný spoj, a časy ostatních zůstanou na svých řádcích.
/// Rozhodují jen vybrané typy provozu - KV_DP linka 1 se třemi typy z webu má na konci trasy za
/// Lázněmi III zastávky, kam jezdí jen ostatní typy.
Method TestZastavkyBezZastaveni()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertTrue(1, "firma KV_DP není na serveru - přeskočeno")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetParameters(.sys)
	set it = ##class(%DynamicArray).%FromJSON(^||ML("OUT",1)).%GetIterator()
	while it.%GetNext(.k, .p) { set param(p.%Get("name")) = p }
	do $$$AssertTrue($data(param("ZASTAVKY_BEZ_ZASTAVENI")), "parametr chybí")
	quit:'$data(param("ZASTAVKY_BEZ_ZASTAVENI"))
	do $$$AssertEquals(param("ZASTAVKY_BEZ_ZASTAVENI").%Get("defaultValue")_"|"_param("ZASTAVKY_BEZ_ZASTAVENI").options.%ToJSON(), "Tisknout|[""Tisknout"",""Netisknout""]", "MHD nemá volbu dle linky")

	// Jen typy provozu jako na webu, ať je co vynechat.
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).NactiSkupinyProvozu("KV_DP", "KV_DP", .poradi, .nadpis)
	set tp = ""
	for  {
		set tp = $order(nadpis(tp))
		quit:tp=""
		set:'$listfind($listbuild("Pracovní den mimo 1.7. - 31.8. a 27.12. - 31.12.", "Pracovní den 1.7. - 31.8. a 27.12. - 31.12.", "Sobota, neděle a státní svátky"), nadpis(tp)) vypnout(tp) = ""
	}
	for volba = "Tisknout", "Netisknout" {
		kill ^||ML
		set ^||ML("IN","LINKA_ID") = 1073, ^||ML("IN","SMER_TISKU") = "Tam", ^||ML("IN","ZASTAVKY_BEZ_ZASTAVENI") = volba
		set tp = ""
		for  { set tp = $order(vypnout(tp)) quit:tp=""  set ^||ML("IN", ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).ParametrTypuProvozu(tp)) = "false" }
		do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT",1)), zastavky = page.rows.%Get("ZASTAVKY"), spoje = page.rows.%Get("SPOJE")
		for r=0:1:zastavky.%Size()-1 {
			set casy = "", stavi = 0
			for s=0:1:spoje.%Size()-1 {
				set v = spoje.%Get(s).%Get("CASY").%Get(r), casy = casy_","_v
				set:v?1.2N1":"2N stavi = 1
			}
			set radky(volba, $increment(radky(volba))) = $listbuild(zastavky.%Get(r).%Get("NAZEV_ZASTAVKY"), stavi, casy)
		}
	}
	// Očekávané řádky = řádky s Tisknout, kde staví aspoň jeden spoj - i se svými časy.
	set (ocekavane, vynechano) = 0
	for r=1:1:radky("Tisknout") {
		if '$listget(radky("Tisknout", r), 2) { set vynechano = vynechano + 1  continue }
		set ocekavane = ocekavane + 1
		do $$$AssertEquals($listget($get(radky("Netisknout", ocekavane)), 1, "(chybí)")_" "_$listget($get(radky("Netisknout", ocekavane)), 3), $listget(radky("Tisknout", r), 1)_" "_$listget(radky("Tisknout", r), 3), "řádek "_ocekavane)
	}
	do $$$AssertEquals(+$get(radky("Netisknout")), ocekavane, "počet řádků s Netisknout")
	do $$$AssertTrue(vynechano > 0, "linka nemá zastávku bez zastavení - test nic nedokazuje")
}

/// Počet stran sdílí definici s AD (`B191d`): MHD má místo „Automaticky (dle linky)" volbu Bez omezení
/// (linka MHD nastavení nemá) a až 3 strany; AD zůstává, jak bylo. Návrhář čte cíl z čísla na začátku.
Method TestPocetStran()
{
	set mhd = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).ParametrPoctuStran(0, 3)
	do $$$AssertEquals(mhd.%Get("defaultValue")_"|"_mhd.options.%ToJSON(), "Bez omezení|[""Bez omezení"",""1 strana"",""2 strany"",""3 strany""]", "MHD")
	set ad = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).ParametrPoctuStran()
	do $$$AssertEquals(ad.%Get("defaultValue")_"|"_ad.options.%ToJSON(), "Automaticky (dle linky)|[""Automaticky (dle linky)"",""1 strana"",""2 strany""]", "AD")
	do $$$AssertEquals(mhd.%Get("name")_"|"_mhd.%Get("group")_"|"_mhd.%Get("serverSide"), "POCET_STRAN_SESTAVY|Obecné|", "řeší návrhář, ne server")
}

/// Vynechání zastávek bez zastavení přečísluje i sloučené sousední body trasy (stejné číslo
/// zastávky = jeden řádek, `bodyMapa` na něj ukazuje z obou bodů). KV_DP linka 1 je má taky
/// (`TestZastavkyBezZastaveni` to chytí přes časy), tady je případ izolovaný na smyšlených datech:
/// řádek 2 obsluhuje spoj jen přes sloučený bod 103.
Method TestJenZastavujiciSeSloucenymBodem()
{
	kill body, bodyMapa, spojTabulka
	set body(1) = $listbuild(101, "", "První"), body(2) = $listbuild(102, "", "Sloučená"), body(3) = $listbuild(104, "", "Třetí")
	set bodyMapa(101) = 1, bodyMapa(102) = 2, bodyMapa(103) = 2, bodyMapa(104) = 3
	// $lb(?, ?, příjezd, odjezd, projíždí): na prvním bodu jen projíždí, staví na 103 a 104.
	set spojTabulka("A", 101) = $listbuild("", "", "", "", 1)
	set spojTabulka("A", 103) = $listbuild("", "", 310, 310, 0)
	set spojTabulka("A", 104) = $listbuild("", "", 320, 320, 0)
	set pocetBodu = 3
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).JenZastavujici(.body, .pocetBodu, .bodyMapa, .spojTabulka)

	do $$$AssertEquals(pocetBodu, 2, "vynechá jen první zastávku")
	do $$$AssertEquals($listget(body(1), 3)_","_$listget(body(2), 3), "Sloučená,Třetí", "pořadí řádků")
	do $$$AssertEquals($get(bodyMapa(102))_","_$get(bodyMapa(103))_","_$get(bodyMapa(104)), "1,1,2", "sloučený bod ukazuje na přečíslovaný řádek")
	do $$$AssertTrue('$data(bodyMapa(101)), "vynechaný bod v mapě nezůstane")
}

/// Čísla čárkou seřazená vzestupně, aby nezáleželo na pořadí.
ClassMethod Serazene(cisla As %String) As %String
{
	kill s
	for i=1:1:$length(cisla, ",") set:$piece(cisla, ",", i)'="" s(+$piece(cisla, ",", i)) = ""
	set out = "", c = ""
	for  { set c = $order(s(c)) quit:c=""  set out = out_$select(out="":"", 1:",")_c }
	quit out
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
