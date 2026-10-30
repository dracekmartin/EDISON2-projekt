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

/// Ukázka bez vybrané linky nese typy provozu své linky jako tisk - jinak panel ukáže všechny
/// typy provozu firmy (KV_DP jich má desítky, linka pár).
Method TestUkazkaNeseTypyProvozuLinky()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertTrue(1, "firma KV_DP není na serveru - přeskočeno")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetMockData(.sys)
	do $$$AssertEquals($get(^||ML("ERR")), "", "GetMockData skončil chybou")
	set page = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT", 1), "{}"))
	if '$isobject(page.%Get("rows")) || (page.rows.%Get("SPOJE").%Size() = 0) {
		do $$$AssertTrue(1, "první linka KV_DP nedala ukázku - přeskočeno")
		quit
	}
	set typy = page.values.%Get("TYPY_PROVOZU_LINKY")
	do $$$AssertTrue($isobject(typy) && (typy.%Size() > 0), "ukázka nemá seznam typů provozu linky")
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
		set (oznacene, czst, nazev) = "", it = page.rows.%Get("ZASTAVKY").%GetIterator()
		while it.%GetNext(.k, .z) {
			set ma = ($zconvert(z.%Get("NAZEV_ZASTAVKY"), "L") [ $zconvert(hledana, "L")) && (hledana '= "")
			do $$$AssertEquals(z.%Get("AKTUALNI_ZASTAVKA"), $select(ma:"true", 1:"false"), """"_hledana_""": "_z.%Get("NAZEV_ZASTAVKY"))
			if ma set oznacene = oznacene + 1, czst = z.%Get("CZST"), nazev = z.%Get("NAZEV_ZASTAVKY")
		}
		do $$$AssertEquals(+oznacene, $case(hledana, "TRŽNICE":1, "tašovice":3, :0), """"_hledana_""": počet označených řádků")
		do $$$AssertEquals(page.values.%Get("AKTUALNI_ZASTAVKA_CZST"), $select(oznacene=1:czst, 1:""), """"_hledana_""": číslo zastávky pro QR")
		// Název aktuální zastávky jen při jediné shodě; řádek ho může mít doplněný o značky poznámek.
		set popis = page.values.%Get("POPIS_ZASTAVKA")
		if oznacene = 1 {
			do $$$AssertTrue((popis '= "") && ($extract(nazev, 1, $length(popis)) = popis), """"_hledana_""": název aktuální zastávky „"_popis_"“ × řádek „"_nazev_"“")
		}
		else {
			do $$$AssertEquals(popis, "", """"_hledana_""": bez jediné shody název aktuální zastávky prázdný")
		}
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
	set mhd = ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrPoctuStran(0, 3)
	do $$$AssertEquals(mhd.%Get("defaultValue")_"|"_mhd.options.%ToJSON(), "Bez omezení|[""Bez omezení"",""1 strana"",""2 strany"",""3 strany""]", "MHD")
	set ad = ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrPoctuStran()
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

/// Hraniční zastávka ukáže všechny zóny systému linky („1/2"), ne jen jednu jako starý tisk. Dev data
/// takovou zastávku nemají (sonda `LinkovyJrMhdProbe.ViceZon`, KV_DP i CB_DP 22. 10.), proto si test
/// založí číselník pod vlastní firmou s jedinečným jménem a po sobě ho smaže.
Method TestZonyHranicniZastavky()
{
	set idfa = "TDTEST"_$translate($system.Util.CreateGUID(), "-"), db = $namespace
	try {
		// Zóny (id → systém, číslo): 11 → 7 „1", 12 → 7 „2", 13 → 8 „P", 14 → 7 bez čísla (přeskočí se).
		// Zastávka 500 má 11, 13, 14, 12 a znovu 11 - duplicitu tisknout nemá.
		for zona = $listbuild(11, 7, "1"), $listbuild(12, 7, "2"), $listbuild(13, 8, "P"), $listbuild(14, 7, "") {
			set ^[db]ALVA.MHD.ENUMS(idfa, "Zony", "DTA", $list(zona, 1), "ZonaIDSID") = $list(zona, 2)
			set ^[db]ALVA.MHD.ENUMS(idfa, "Zony", "DTA", $list(zona, 1), "Cislo") = $list(zona, 3)
		}
		set n = 0
		for zonaId = 11, 13, 14, 12, 11 { set ^[db]ALVA.MHD.ZST(idfa, 1, "DTA", 500, "DTA", 1, "ZonyID", $increment(n), "Itm") = zonaId }

		do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).ZonyZastavky(idfa, db, 500, 7), "1/2", "systém linky 7: obě zóny, každá jednou")
		do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).ZonyZastavky(idfa, db, 500, 8), "P", "systém 8")
		do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).ZonyZastavky(idfa, db, 500, 9), "", "jiný systém nic")
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]ALVA.MHD.ENUMS(idfa), ^[db]ALVA.MHD.ZST(idfa)
}

/// Linka bez jediné stránky vrátí místo prázdna důvod a radu (`DUVOD_PRAZDNE`). KV_DP linka 20
/// (1105): 136 spojů, žádný nemá spočítané časy - starý tisk na ní spadne na `<SUBSCRIPT>`. Linka 1
/// se všemi typy provozu vypnutými řekne, že je zapnout. Linka 21 (1106) data má - starý tisk na
/// ní padá jen kvůli spoji, který začíná uprostřed trasy.
Method TestDuvodPrazdnehoVystupu()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertTrue(1, "firma KV_DP není na serveru - přeskočeno")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = 1105
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
	do $$$AssertTrue($get(^||ML("OUT","DUVOD_PRAZDNE")) [ "jízdní doby", "linka 20: "_$get(^||ML("OUT","DUVOD_PRAZDNE"), "(bez důvodu)"))
	do $$$AssertEquals(..PocetStranek(), 0, "linka 20 nemá stránky")

	kill ^||ML
	set ^||ML("IN","LINKA_ID") = 1073, tp = ""
	for  { set tp = $order(^["KV_DP"]ALVA.MHD.ENUMS("KV_DP","TypyProvozu","DTA",tp)) quit:tp=""  set ^||ML("IN", ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).ParametrTypuProvozu(tp)) = "false" }
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
	do $$$AssertTrue($get(^||ML("OUT","DUVOD_PRAZDNE")) [ "skupině Tabulka spojů", "linka 1 bez typů: "_$get(^||ML("OUT","DUVOD_PRAZDNE"), "(bez důvodu)"))

	for lin = 1073, 1106 {
		kill ^||ML
		set ^||ML("IN","LINKA_ID") = lin
		do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
		do $$$AssertTrue(..PocetStranek() > 0, lin_" má mít stránky")
		do $$$AssertTrue('$data(^||ML("OUT","DUVOD_PRAZDNE")), lin_": s daty bez důvodu")
	}
}

/// Poznámky z trasy jako ve starém tisku (legacy :418-436, :812-821, :1212-1219): značka za názvem
/// zastávky („Tržnice, ab") a vysvětlivka mezi textovými poznámkami (druh 4). Bere se jen textová
/// poznámka určená pro knižní tisk; „jen značka" se do legendy nedává. Dev data takovou poznámku nemají,
/// proto si test založí číselník a trasu pod vlastní firmou s jedinečným jménem a po sobě je smaže.
Method TestPoznamkyZTrasy()
{
	set idfa = "TDTEST"_$translate($system.Util.CreateGUID(), "-"), db = $namespace
	try {
		// Poznámky (id → značka, popis, knižní, jen značka, typ): 21 „a" s popisem, 22 „b" jen značka,
		// 23 „c" jen pro zastávkový tisk, 24 „N" negativní značka (typ 2), 25 bez značky.
		for p = $listbuild(21, "a", "u vchodu do nemocnice", 1, 0, ""), $listbuild(22, "b", "nepíše se", 1, 1, ""), $listbuild(23, "c", "zastávkový", 0, 0, ""), $listbuild(24, "N", "negativní", 1, 0, 2), $listbuild(25, "", "bez značky", 1, 0, "") {
			set g = $name(^[db]ALVA.MHD.ENUMS(idfa, "PoznamkaSpoj", "DTA", $list(p, 1)))
			set @g@("Text") = $list(p, 2), @g@("PopisPoznamky") = $list(p, 3), @g@("TiskJRMHDKnizni") = $list(p, 4)
			set @g@("TiskJRMHDKnizniPouzeZnacku") = $list(p, 5), @g@("TypPoznamkyID") = $list(p, 6)
		}
		set bod = $name(^[db]ALVA.MHD.LSO(idfa, 4, "DTA", 7, "DTA", 1, "ModelTrasaBodDetail"))
		set n = 0
		for pozn = 22, 21, 23, 24, 25, 21 { set @bod@(1, "Val", "PoznamkyTam", $increment(n), "Itm") = pozn }
		set @bod@(1, "Val", "PoznamkyZpet", 1, "Itm") = 23
		set @bod@(2, "Val", "PoznamkyTam", 1, "Itm") = 22

		kill popisy
		set tam = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).PoznamkyBodu(idfa, db, 7, 1, 0, .popisy)
		do $$$AssertEquals($listtostring(tam), "a,b", "tam: jen knižní textové, každá jednou")
		do $$$AssertEquals($get(popisy("a"))_"|"_$get(popisy("b")), "u vchodu do nemocnice|", "jen značka bez vysvětlivky")
		do $$$AssertEquals($listtostring(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).PoznamkyBodu(idfa, db, 7, 1, 1, .popisy)), "", "zpět: poznámka jen pro zastávkový tisk se nebere")
		do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).NazevSPoznamkami("Tržnice", tam), "Tržnice, ab", "značky za názvem")
		do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).NazevSPoznamkami("Tržnice", ""), "Tržnice", "bez poznámek beze změny")

		// Sloučený bod 2 přidá „b" k řádku, který už ji má - zůstane jednou.
		set slouceno = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).SloucenePoznamky(tam, ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).PoznamkyBodu(idfa, db, 7, 2, 0, .popisy))
		do $$$AssertEquals($listtostring(slouceno), "a,b", "sloučené body")

		// Legenda: „a" s vysvětlivkou jednou, i když ji má i spoj; „b" (jen značka) vůbec.
		set body(1) = $listbuild(1, "", "Tržnice", "", 0, "", "", "", slouceno)
		set poznamky = ##class(%DynamicArray).%New()
		do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).PridatPoznamkyTrasyMhd(.body, 1, poznamky, .popisy, .poznSpoju)
		do $$$AssertEquals(poznamky.%ToJSON(), "[{""DRUH"":4,""TYP"":""text"",""ZNAK"":""a"",""TEXT"":""u vchodu do nemocnice""}]", "legenda")
		set poznamky = ##class(%DynamicArray).%New(), poznSpoju("seen", 4, "a", "u vchodu do nemocnice") = ""
		do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).PridatPoznamkyTrasyMhd(.body, 1, poznamky, .popisy, .poznSpoju)
		do $$$AssertEquals(poznamky.%Size(), 0, "stejná poznámka u spoje se neopakuje")
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]ALVA.MHD.ENUMS(idfa), ^[db]ALVA.MHD.LSO(idfa)
}

/// `B193a`: dopravce z téhož zdroje jako vývěska MHD, jen jedním řádkem s oddělovači a „tel. " jako
/// linkový AD. Pořadí adresy jako vývěska MHD - ulice, PSČ obec (`B194a` T6, do 26. 10. obráceně).
/// Poznámku nad linku linkový MHD nenabízí - modul MHD ji nedává zadat.
Method TestDopravceJakoVyveska()
{
	set data = "ALVA.TemplateDesigner.Agendy.Jr.DataMhd"
	do $$$AssertEquals($classmethod(data, "RadkyDopravce", " DP, a.s. ", "Hlavní 1", "123 45 Město", "111 222", 1), "DP, a.s., Hlavní 1, 123 45 Město, tel. 111 222", "jedním řádkem")
	do $$$AssertEquals($classmethod(data, "RadkyDopravce", "DP", "", "", "", 1), "DP", "bez adresy jen název")
	do $$$AssertEquals($classmethod(data, "RadkyDopravce", "DP", "Hlavní 1", "Město", "111"), "DP"_$c(10)_"Hlavní 1"_$c(10)_"Město"_$c(10)_"tel: 111", "vývěska po řádcích beze změny")

	do ..SetupSys(.sys)
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetSchema(.sys)
	do $$$AssertTrue($get(^||ML("OUT",1)) '[ "POZNAMKA_NAD_LINKU", "nabídka dat bez poznámky nad linku")
	do $$$AssertTrue($get(^||ML("OUT",1)) [ """POPIS_DOPRAVCE""", "nabídka dat s dopravcem")

	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertSkipped("firma KV_DP není na serveru")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = 1073, ^||ML("IN","SMER_TISKU") = "Tam"
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
	set hodnoty = ##class(%DynamicObject).%FromJSON(^||ML("OUT",1)).values
	do $$$AssertTrue('hodnoty.%IsDefined("POZNAMKA_NAD_LINKU"), "poznámka nad linku se neposílá")
	do $$$AssertTrue('hodnoty.%IsDefined("POPIS_PLATNOSTI"), "hotová věta platnosti se neposílá (agenda nevyšla, Jan 23. 10.)")
	set dopravce = hodnoty.%Get("POPIS_DOPRAVCE")
	do $$$AssertEquals(dopravce, ##class(ALVA.TemplateDesigner.Agendy.Jr.DataMhd).PopisDopravce(.sys, "KV_DP", "KV_DP", 1073, 1), "dopravce ze zdroje vývěsky")
	set nazev = $piece(##class(ALVA.TemplateDesigner.Agendy.Jr.DataMhd).PopisDopravce(.sys, "KV_DP", "KV_DP", 1073), $c(10), 1)
	if nazev = "" {
		do $$$AssertSkipped("firemní údaje KV_DP nejsou v systémovém namespace testu")
		quit
	}
	do $$$AssertEquals($extract(dopravce, 1, $length(nazev)), nazev, "linka 1 začíná názvem dopravce z vývěsky: "_dopravce)
	do $$$AssertTrue(dopravce '[ $c(10), "jedním řádkem")
}

/// `B193c`: linkový JŘ MHD se chová jako vývěska MHD - razítko typu JŘ (při návrhu i výluce vyhrává
/// výluka), výchozí skupina linek (zablokovaná se přeskočí), skupiny a položky výběru linek
/// a popis Směru pro tisk bez obousměrného tisku.
Method TestJakoVyveskaMhd()
{
	set vyv = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd", data = "ALVA.TemplateDesigner.Agendy.Jr.DataMhd"
	set idfa = "TDTEST"_$translate($system.Util.CreateGUID(), "-"), db = $namespace
	try {
		set g = $name(^[db]ALVA.MHD.LSO(idfa, 1, "DTA", 7, "DTA", 1))
		set @g@("Navrh") = 1, @g@("Vyluka") = 1
		do $$$AssertEquals($classmethod(data, "TypJrLinky", idfa, db, 7), "VÝLUKOVÝ", "návrh i výluka")
		set @g@("Vyluka") = 0
		do $$$AssertEquals($classmethod(data, "TypJrLinky", idfa, db, 7), "NÁVRH", "jen návrh")
		set @g@("Navrh") = 0
		do $$$AssertEquals($classmethod(data, "TypJrLinky", idfa, db, 7), "", "bez příznaků")
		set ^[db]ALVA.MHD.ENUMS(idfa, "SkupinyLinek", "DTA", 1, "Blocked") = 1
		set ^[db]ALVA.MHD.ENUMS(idfa, "SkupinyLinek", "DTA", 2, "Blocked") = 0
		do $$$AssertEquals($classmethod(data, "GetDefaultSkupinaLinekID", idfa, db), 2, "zablokovaná skupina se přeskočí")
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]ALVA.MHD.LSO(idfa), ^[db]ALVA.MHD.ENUMS(idfa)

	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertSkipped("firma KV_DP není na serveru")
		quit
	}
	do ..SysFirmy(.sys, "KV_DP")
	for agenda = vyv, "ALVA.TemplateDesigner.Agendy.LinkovyJrMhd" {
		kill ^||ML
		set ^||ML("IN","limit") = 10000
		do $classmethod(agenda, "GetAvailableInstances", .sys)
		set linky(agenda) = $get(^||ML("OUT",1))
		kill ^||ML
		do $classmethod(agenda, "GetAvailableSkupiny", .sys)
		set skupiny(agenda) = $get(^||ML("OUT",1))
	}
	do $$$AssertEquals(skupiny("ALVA.TemplateDesigner.Agendy.LinkovyJrMhd"), skupiny(vyv), "skupiny linek jako vývěska")
	kill polozky
	set it = ##class(%DynamicArray).%FromJSON(linky("ALVA.TemplateDesigner.Agendy.LinkovyJrMhd")).%GetIterator()
	while it.%GetNext(.k, .o) { set polozky(o.value) = o.%ToJSON() }
	set it = ##class(%DynamicArray).%FromJSON(linky(vyv)).%GetIterator(), pocet = 0
	while it.%GetNext(.k, .o) {
		set pocet = pocet + 1
		do $$$AssertEquals($get(polozky(o.value), "(chybí)"), o.%ToJSON(), "linka "_o.value_" ve výběru jako vývěska")
		kill polozky(o.value)
	}
	do $$$AssertTrue(pocet > 0, "vývěska nabídla aspoň jednu linku")
	// Řazení podle čísla linky (Jan 27. 10.): čísla vzestupně, pak ostatní označení, ne podle interního ID.
	set it = ##class(%DynamicArray).%FromJSON(linky(vyv)).%GetIterator(), predchozi = "", spatne = ""
	while it.%GetNext(.k, .o) {
		set c = o.cislo
		if c?1.N { set klic = "0~"_$translate($justify(+c, 12), " ", "0") }
		elseif c '= "" { set klic = "1~"_$zconvert(c, "U") }
		else { set klic = "2~" }
		if (predchozi '= "") && (predchozi ]] klic) { set spatne = spatne_" "_c }
		set predchozi = klic
	}
	do $$$AssertEquals(spatne, "", "linky ve výběru nejsou seřazené podle čísla, mimo pořadí:"_spatne)
	set prvni = ##class(%DynamicArray).%FromJSON(linky(vyv)).%Get(0)
	do $$$AssertTrue(prvni.subtitle?1"Platnost od "2N1"."2N1"."4N.E, "platnost ve výběru linek jako u AD (dd.mm.rrrr): "_prvni.subtitle)
	// Od `B193g` nabízejí obě agendy i linky bez trasy (napíšou u nich důvod) - výběr je totožný.
	do $$$AssertEquals($order(polozky("")), "", "linkový nabízí linku, kterou vývěska ne: "_$order(polozky("")))

	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetParameters(.sys)
	set it = ##class(%DynamicArray).%FromJSON(^||ML("OUT",1)).%GetIterator(), popis = "(chybí)"
	while it.%GetNext(.k, .p) { set:p.name="SMER_TISKU" popis = p.description }
	do $$$AssertTrue((popis '= "(chybí)") && (popis '[ "obousměrný"), "Směr pro tisk bez obousměrného tisku: "_popis)
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
