Include (%occInclude, GENERAL)

/// Read-only sondy nad AD daty. Původně k #13048 bodu 3 / #13088 (kde v datech leží „sloupek", který
/// KOVED odlišuje od nástupiště = našeho označníku); fulltext přes legacy rutiny (`^rMAC`) a rozpad
/// `#`-segmentů J-věty. Od `B67` navíc `NajdiVariantniPripady` + `PripadyLinky`, kterými se vybírají
/// linky do testovací sady (`ZastavkoveJrAdGoldTest.Pripady`) - **ty dvě si nech**, hledání kandidátů
/// bude potřeba u každé další změny variantních sloupců. Nic nemění.
Class ALVA.TemplateDesigner.Tests.AdSloupekProbe
{

/// Linky, na kterých je vidět rozdíl mezi rovnoměrným a pevným rastrem minut (`B160`). Hledá se
/// vývěska, kde **má značku jen menšina odjezdů, ale některý jich má víc** - tam rovnoměrný rastr
/// roztáhne rozestup všem kvůli hrstce spojů, kdežto pevný ne.
///
/// IN: `LIMIT` (kolik linek projít, výchozí 25), `MAXOUT` (kolik vypsat, výchozí 8).
/// OUT: `linka~<n>` = `sku/lin  odjezdu=<n>, se znackou=<n> (<%>), nejvic znacek na odjezdu=<n>`.
/// ⚠️ Volá `GetData` na každou linku, takže je to pomalé.
ClassMethod KdeJeVidetRastr(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s limit = +$g(^||ML("IN","LIMIT")), maxOut = +$g(^||ML("IN","MAXOUT"))
		s:limit<1 limit = 25
		s:maxOut<1 maxOut = 8

		k kandidat
		s projito = 0, hotovo = 0, s1 = ""
		f {
			s s1 = $o(^[dataDB]JRL(s1))
			q:(s1="")||hotovo
			continue:'$isvalidnum(s1)
			s l1 = ""
			f {
				s l1 = $o(^[dataDB]JRL(s1, l1))
				q:(l1="")||hotovo
				continue:'$d(^[dataDB]JRL(s1, l1, "I"))
				continue:'+$g(^[dataDB]JRL(s1, l1, "T"))
				s projito = projito + 1
				i projito > limit s hotovo = 1 q
				d ..ZmerZnackyLinky(s1, l1, .sys, .celkem, .sZnackou, .maxZnacek)
				continue:celkem<10
				// Zajímavé je jen „pár označkovaných mezi mnoha holými" - jedna značka na odjezd
				// rastr skoro nezvedne a linka, kde má značku skoro každý, vypadá v obou režimech stejně.
				continue:maxZnacek<2
				s podil = $j(sZnackou * 100 / celkem, 0, 0)
				continue:podil>60
				s kandidat(maxZnacek, 100-podil, s1_"/"_l1) =
					"odjezdu="_celkem_", se znackou="_sZnackou_" ("_podil_"%), nejvic znacek na odjezdu="_maxZnacek
			}
		}

		s ^||ML("OUT","souhrn") = "projito linek="_projito
		s vypsano = 0, mz = ""
		f {
			s mz = $o(kandidat(mz), -1)
			q:(mz="")||(vypsano'<maxOut)
			s p = ""
			f {
				s p = $o(kandidat(mz, p))
				q:(p="")||(vypsano'<maxOut)
				s klic = ""
				f {
					s klic = $o(kandidat(mz, p, klic))
					q:(klic="")||(vypsano'<maxOut)
					s vypsano = vypsano + 1
					s ^||ML("OUT","linka~"_$tr($j(vypsano,2)," ","0")) = klic_"  "_kandidat(mz, p, klic)
				}
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Konkrétní vývěsky jedné linky, na kterých je rozdíl rastru minut (`B160`) opravdu vidět.
/// Rozestup se pozná jen tam, kde v jednom **řádku hodiny** stojí několik minut vedle sebe -
/// na vývěsce s jedním odjezdem za hodinu není co rozestupovat.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID`, `MAXOUT` (kolik vypsat, výchozí 8).
/// OUT: `zastavka~<n>` = `<směr> <název>  nejvic minut v radku=<n>, nejvic znacek na odjezdu=<n>`.
ClassMethod RastrPoZastavkach(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID")), lin = $g(^||ML("IN","LINKA_ID"))
		s maxOut = +$g(^||ML("IN","MAXOUT"))
		s:maxOut<1 maxOut = 8

		k ^||ML("IN")
		s ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")

		k nalez
		s stranek = 0, key = ""
		f {
			s key = $o(odpoved(key))
			q:key=""
			continue:'$isvalidnum(key)
			s stranek = stranek + 1
			s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
			s odjezdy = page.rows.%Get("ODJEZDY")
			continue:'$isobject(odjezdy)

			k vRadku
			s maxVRadku = 0, maxZnacek = 0
			f oi=0:1:(odjezdy.%Size()-1) {
				s o = odjezdy.%Get(oi)
				s radek = o.%Get("SKUPINA_DNU_ID")_"~"_o.%Get("HODINA")
				s vRadku(radek) = $g(vRadku(radek)) + 1
				s:vRadku(radek)>maxVRadku maxVRadku = vRadku(radek)
				s anot = o.%Get("ANOTACE")
				s pocet = $s($isobject(anot):anot.%Size(), 1:0)
				s:pocet>maxZnacek maxZnacek = pocet
			}
			continue:maxVRadku<3
			continue:maxZnacek<2
			s nalez(maxZnacek, maxVRadku, stranek) =
				page.values.SMER_" "_page.values."POPIS_ZASTAVKA"_
				"  nejvic minut v radku="_maxVRadku_", nejvic znacek na odjezdu="_maxZnacek
		}

		s ^||ML("OUT","souhrn") = "linka "_sku_"/"_lin_", stranek="_stranek
		s vypsano = 0, mz = ""
		f {
			s mz = $o(nalez(mz), -1)
			q:(mz="")||(vypsano'<maxOut)
			s mr = ""
			f {
				s mr = $o(nalez(mz, mr), -1)
				q:(mr="")||(vypsano'<maxOut)
				s st = ""
				f {
					s st = $o(nalez(mz, mr, st))
					q:(st="")||(vypsano'<maxOut)
					s vypsano = vypsano + 1
					s ^||ML("OUT","zastavka~"_$tr($j(vypsano,2)," ","0")) = nalez(mz, mr, st)
				}
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kolik odjezdů linka má, kolik z nich nese značku a nejvíc značek na jednom odjezdu.
/// Helper - volá se jen z metody, která catch má.
ClassMethod ZmerZnackyLinky(sku As %String, lin As %String, ByRef sys As %String, ByRef celkem, ByRef sZnackou, ByRef maxZnacek)
{
	s celkem = 0, sZnackou = 0, maxZnacek = 0
	try {
		k ^||ML("IN")
		s ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")

		s key = ""
		f {
			s key = $o(odpoved(key))
			q:key=""
			continue:'$isvalidnum(key)
			s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
			s odjezdy = page.rows.%Get("ODJEZDY")
			continue:'$isobject(odjezdy)
			f oi=0:1:(odjezdy.%Size()-1) {
				s anot = odjezdy.%Get(oi).%Get("ANOTACE")
				s pocet = $s($isobject(anot):anot.%Size(), 1:0)
				s celkem = celkem + 1
				i pocet {
					s sZnackou = sZnackou + 1
					s:pocet>maxZnacek maxZnacek = pocet
				}
			}
		}
	}
	catch (ex) { s celkem = 0 }
}

/// Nejkratší obousměrné linky firmy - kolik zastávek mají a kolik stránek z nich vyjde
/// (zastávka × směr). Pro ruční zkoušení věcí, kde je potřeba projít celý export.
/// IN: `MAXOUT` (kolik vypsat, výchozí 10). Read-only, čte jen globály.
ClassMethod NejkratsiLinky(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s maxOut = +$g(^||ML("IN","MAXOUT"))
		s:maxOut<1 maxOut = 10
		// Linka o dvou zastávkách je na ruční zkoušení až moc krátká - `MIN_ZAST` zvedne spodní hranici.
		s minZast = +$g(^||ML("IN","MIN_ZAST"))
		s:minZast<2 minZast = 2

		s projito = 0, s1 = ""
		f {
			s s1 = $o(^[dataDB]JRL(s1))
			q:s1=""
			continue:'$isvalidnum(s1)
			s l1 = ""
			f {
				s l1 = $o(^[dataDB]JRL(s1, l1))
				q:l1=""
				continue:'$d(^[dataDB]JRL(s1, l1, "I"))
				continue:'+$g(^[dataDB]JRL(s1, l1, "T"))
				continue:+$g(^[dataDB]JRL(s1, l1, "PATCH", "linJEDNOSMERNA"))
				s pocetZast = 0, pp = ""
				f {
					s pp = $o(^[dataDB]JRL(s1, l1, "J", pp))
					q:pp=""
					continue:'$isvalidnum(pp)
					continue:$p($g(^[dataDB]JRL(s1, l1, "J", pp)),":",1)=""
					s pocetZast = pocetZast + 1
				}
				continue:pocetZast<minZast
				s projito = projito + 1
				s nalez(pocetZast, s1_"/"_l1) = ""
			}
		}

		s ^||ML("OUT","souhrn") = "obousmernych linek="_projito
		s vypsano = 0, pocet = ""
		f {
			s pocet = $o(nalez(pocet))
			// Postmínka nesmí obsahovat mezery - první z nich ji ukončí a zbytek je pak nový příkaz.
			q:(pocet="")||(vypsano'<maxOut)
			s klic = ""
			f {
				s klic = $o(nalez(pocet, klic))
				q:(klic="")||(vypsano'<maxOut)
				s vypsano = vypsano + 1
				s ^||ML("OUT","linka~"_$tr($j(vypsano,2)," ","0")) =
					klic_"  zastavek="_pocet_", stranek nejvyse "_(pocet * 2)
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Read-only hledání linek pro testovací sadu (`B67`): kde vznikají variantní sloupce, kde v základním
/// sloupci časů vzniká „<" (vybraná trasa zastávkou projíždí bez stání) a kde má **tentýž sled zastávek
/// dva různé časové průběhy** - právě tam rozhoduje pravidlo „vyhrává delší jízda". Jedna gold linka
/// pokrývá jen svůj tvar dat, proto se kandidáti hledají napříč firmami.
///
/// Signatury se skládají přes celý směr (ne per vývěskovou zastávku), takže je to **hrubý filtr** na
/// kandidáty - potvrzuje se pak spuštěním agendy nad konkrétní linkou (`PripadyLinky`).
/// IN: DB, SKU (default 1), MAXLIN (default 300), MAXOUT (default 30). Read-only.
ClassMethod NajdiVariantniPripady(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinne"))
		set sku = $get(^||ML("IN","SKU")) if sku = "" set sku = 1
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 300
		set maxOut = +$get(^||ML("IN","MAXOUT")) if maxOut < 1 set maxOut = 30
		kill ^||ML("OUT")

		set linek = 0, vypsanych = 0, lin = ""
		for {
			set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
			quit:linek'<maxLin
			quit:vypsanych'<maxOut
			continue:'$data(^[db]JRL(sku,lin,"W"))
			set linek = linek + 1
			kill ^||TiskAd                       // cache agendy je per linka, ať v tomhle běhu neroste
			set variant = 0, projizdeni = 0, stejnyVzor = 0, tras = 0
			for smer = "T", "Z" {
				kill served, sig
				do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ObsluhovanePoradi(db, sku, lin, smer, .served)
				continue:'$data(served)
				kill ct
				set sp = ""
				for {
					set sp = $order(^[db]JRL(sku,lin,smer,sp)) quit:sp=""
					continue:'$isvalidnum(sp)
					set c = $piece($piece($get(^[db]JRL(sku,lin,smer,sp)),"#",1),":",3)
					if c '= "" set ct(c) = ""
				}
				set pocetVzoru = 0, c = ""
				for {
					set c = $order(ct(c)) quit:c=""
					set tras = tras + 1
					kill wMap
					do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).WMapa(db, sku, lin, c, .wMap)
					set vzor = "", casy = "", pp = ""
					for {
						set pp = $order(served(pp)) quit:pp=""
						set v = $get(wMap(pp))
						set stav = $select(v="":"-", (v="<")||(v="|"):"<", 1:"S")
						if stav = "<" set projizdeni = 1
						set vzor = vzor_stav
						set casy = casy_"~"_##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota(v)
					}
					if '$data(sig(vzor)) { set pocetVzoru = pocetVzoru + 1 }
					elseif sig(vzor) '= casy { set stejnyVzor = 1 }
					set sig(vzor) = casy
				}
				if pocetVzoru > 1 set variant = 1
			}
			continue:('variant)&&('projizdeni)&&('stejnyVzor)
			set vypsanych = vypsanych + 1
			set ^||ML("OUT", "K"_vypsanych) = db_" lin="_lin_" tras="_tras_" varianty="_variant_" projizdeni="_projizdeni_" stejnyVzorJineCasy="_stejnyVzor_" prepinacAuto="_$get(^[db]JRL(sku,lin,"PATCH","mhd","par",1))
		}
		set ^||ML("OUT", "Z_SOUHRN") = "prohledano linek: "_linek_", vypsano: "_vypsanych
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// **Typový profil linek** - pro každou linku firmy pustí agendu a změří, které situace v jejím
/// výstupu vůbec vznikají. Slouží k sestavení testovací sady tak, aby každý typ situace držela nějaká
/// linka (jedna linka pokrývá jen svůj tvar dat).
/// IN: DB, SKU (default 1), MAXLIN (default 40). Read-only.
ClassMethod ProfilyLinek(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinne"))
		set sku = $get(^||ML("IN","SKU")) if sku = "" set sku = 1
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 40

		kill profil
		set linek = 0, lin = ""
		for {
			set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
			quit:linek'<maxLin
			continue:'$data(^[db]JRL(sku,lin,"I"))
			continue:'$data(^[db]JRL(sku,lin,"W"))
			set linek = linek + 1
			kill ^||TiskAd, ^||ML("OUT")
			set ^||ML("IN","SKUPINA_LINEK_ID") = sku
			set ^||ML("IN","LINKA_ID") = lin
			do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
			kill p
			set p("stranek") = 0, key = ""
			for {
				set key = $order(^||ML("OUT", key)) quit:key=""
				continue:key="ERR"
				set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
				set zast = page.rows.ZASTAVKY
				continue:'$isobject(zast)
				set p("stranek") = p("stranek") + 1
				set sloupcu = 0
				if $isobject(page.values.%Get("VARIANTY_TRAS")) set sloupcu = page.values.%Get("VARIANTY_TRAS").%Size()
				if sloupcu > $get(p("maxSloupcu")) set p("maxSloupcu") = sloupcu
				if sloupcu > 1 set p("sVariantami") = $get(p("sVariantami")) + 1
				// zastávky: stavy Času, víc stanovišť u jedné zastávky, atributy
				set it = zast.%GetIterator()
				while it.%GetNext(.i, .radek) {
					set c = radek.%Get("CAS_JIZDY")
					if c = "<" { set p("mensitko") = $get(p("mensitko")) + 1 }
					elseif c = "" { set p("prazdnoCas") = $get(p("prazdnoCas")) + 1 }
					set nv = radek.%Get("NASTUPISTE_VARIANT")
					if $isobject(nv) {
						kill stan
						for ni = 0:1:nv.%Size()-1 { set v = nv.%Get(ni)  if v '= "" set stan(v) = "" }
						set pocetStan = 0, sv = ""
						for { set sv = $order(stan(sv)) quit:sv=""  set pocetStan = pocetStan + 1 }
						if pocetStan > 1 set p("viceStanovist") = $get(p("viceStanovist")) + 1
					}
					if radek.%Get("ZONY") '= "" , $isobject(radek.%Get("ZONY")), radek.%Get("ZONY").%Size() > 0 set p("zony") = $get(p("zony")) + 1
					set atr = radek.%Get("ATRIBUTY")
					if $isobject(atr) {
						for ai = 0:1:atr.%Size()-1 {
							set znak = atr.%Get(ai).%Get("ZNAK")
							if (znak = "A") || (znak = "L") { set p("prestupy") = $get(p("prestupy")) + 1 }
							elseif znak = "x" { set p("naZnameni") = $get(p("naZnameni")) + 1 }
						}
					}
				}
				// anotace u odjezdů podle kategorie
				set odj = page.rows.ODJEZDY
				if $isobject(odj) {
					set io = odj.%GetIterator()
					while io.%GetNext(.i2, .o) {
						set an = o.%Get("ANOTACE")
						continue:'$isobject(an)
						for ai = 0:1:an.%Size()-1 {
							set kat = an.%Get(ai).%Get("KATEGORIE")
							continue:kat=""
							set p("kat_"_kat) = $get(p("kat_"_kat)) + 1
						}
					}
				}
				// legenda podle kategorie
				set pozn = page.rows.POZNAMKY
				if $isobject(pozn) {
					set ip = pozn.%GetIterator()
					while ip.%GetNext(.i3, .r) {
						set kat = r.%Get("KATEGORIE")
						continue:kat=""
						set p("legenda_"_kat) = $get(p("legenda_"_kat)) + 1
					}
				}
			}
			set radek = "lin=" _ lin
			set k = ""
			for { set k = $order(p(k)) quit:k=""  set radek = radek _ " " _ k _ "=" _ p(k) }
			set profil(lin) = radek
		}
		kill ^||ML("OUT")
		set lin = ""
		for {
			set lin = $order(profil(lin)) quit:lin=""
			set ^||ML("OUT", "P_" _ lin) = profil(lin)
		}
		set ^||ML("OUT","Z_SOUHRN") = "firma " _ db _ ", linek " _ linek
	}
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Co na jedné lince reálně vznikne, když se pustí agenda (`B67`): kolik stránek má variantní sloupce,
/// kde v základním sloupci časů je „<" a kde prázdno. Slouží k výběru linek do testovací sady a k tomu,
/// aby šlo říct **konkrétní zastávku**, na které se to dá vizuálně zkontrolovat.
/// IN: SKU (default 1), LIN. Read-only (jen čte, co vrátí GetData).
ClassMethod PripadyLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set sku = $get(^||ML("IN","SKU")) if sku = "" set sku = 1
		set lin = $get(^||ML("IN","LIN"))
		if lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LIN je povinne"))

		kill ^||ML("OUT")
		set ^||ML("IN","SKUPINA_LINEK_ID") = sku
		set ^||ML("IN","LINKA_ID") = lin
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

		set stranek = 0, sVariantami = 0, maxSloupcu = 0, mensitek = 0, prazdnych = 0, ukazek = 0
		kill vzorky
		set key = ""
		for {
			set key = $order(^||ML("OUT", key)) quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			set zast = page.rows.ZASTAVKY
			continue:'$isobject(zast)
			set stranek = stranek + 1
			set sloupcu = 0
			if $isobject(page.values.%Get("VARIANTY_TRAS")) set sloupcu = page.values.%Get("VARIANTY_TRAS").%Size()
			if sloupcu > maxSloupcu set maxSloupcu = sloupcu
			if sloupcu > 1 set sVariantami = sVariantami + 1
			set kdeMensitko = "", it = zast.%GetIterator()
			while it.%GetNext(.i, .radek) {
				set c = radek.%Get("CAS_JIZDY")
				if c = "<" {
					set mensitek = mensitek + 1
					if kdeMensitko = "" set kdeMensitko = radek.%Get("NAZEV_ZASTAVKY")
				}
				elseif c = "" { set prazdnych = prazdnych + 1 }
			}
			if (kdeMensitko '= "") && (ukazek < 5) {
				set ukazek = ukazek + 1
				set vzorky(ukazek) = "vyveska " _ page.values.%Get("POPIS_ZASTAVKA") _ " (smer " _ page.values.SMER _ ", sloupcu variant " _ sloupcu _ ") ma < u zastavky " _ kdeMensitko
			}
		}
		kill ^||ML("OUT")
		set ^||ML("OUT","A_LINKA") = "sku=" _ sku _ " lin=" _ lin
		set ^||ML("OUT","B_SOUHRN") = "stranek=" _ stranek _ " sVariantami=" _ sVariantami _ " maxSloupcuVariant=" _ maxSloupcu _ " mensitekVCase=" _ mensitek _ " prazdnychVCase=" _ prazdnych
		set u = ""
		for {
			set u = $order(vzorky(u)) quit:u=""
			set ^||ML("OUT","C_VZOREK_" _ u) = vzorky(u)
		}
	}
	// Bez úklidu OUT by chyba v sondě poslala klientovi celý výstup GetData (megabajty stránek).
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Sloupce variant JEDNÉ vývěsky, řádek po řádku (`B142`): která varianta kde staví. Slouží
/// k rozhodnutí, co je podmnožina čeho - z obrázku vývěsky se to spolehlivě přečíst nedá,
/// protože sloupce vedle sebe vypadají podobně a chybí u nich, kde která končí.
/// IN: SKU (default 1), LIN, ZAST (podřetězec názvu vývěskové zastávky), SMER (default T).
/// Read-only (jen čte, co vrátí GetData).
ClassMethod SloupceVyvesky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set sku = $get(^||ML("IN","SKU")) if sku = "" set sku = 1
		set lin = $get(^||ML("IN","LIN"))
		set hledana = $get(^||ML("IN","ZAST"))
		set smerH = $get(^||ML("IN","SMER")) if smerH = "" set smerH = "T"
		if lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LIN je povinne"))

		kill ^||ML("OUT")
		set ^||ML("IN","SKUPINA_LINEK_ID") = sku
		set ^||ML("IN","LINKA_ID") = lin
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

		set nalezeno = 0, key = ""
		for {
			set key = $order(^||ML("OUT", key)) quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			continue:page.values.SMER'=smerH
			set nazev = page.values.%Get("POPIS_ZASTAVKA")
			continue:'$find($zcvt(nazev,"L"), $zcvt(hledana,"L"))
			set zast = page.rows.ZASTAVKY
			continue:'$isobject(zast)
			set nalezeno = 1

			set pismena = "", vt = page.values.%Get("VARIANTY_TRAS")
			if $isobject(vt) {
				for vi = 0:1:vt.%Size()-1 { set pismena = pismena _ $select(vi:" | ",1:"") _ $select(vt.%Get(vi)="":"(bez)",1:vt.%Get(vi)) }
			}
			kill ^||ML("OUT")
			set ^||ML("OUT","A_VYVESKA") = nazev _ " (smer " _ smerH _ ")"
			set ^||ML("OUT","B_SLOUPCE") = pismena
			set poradiR = 0, it = zast.%GetIterator()
			while it.%GetNext(.i, .radek) {
				set poradiR = poradiR + 1
				set tm = radek.%Get("TRASA_MIN"), radekTxt = ""
				if $isobject(tm) {
					for vi = 0:1:tm.%Size()-1 { set radekTxt = radekTxt _ $select(vi:" | ",1:"") _ $select(tm.%Get(vi)="":".",1:tm.%Get(vi)) }
				}
				set ^||ML("OUT","C_" _ $translate($justify(poradiR,3)," ","0")) = radekTxt _ "   " _ radek.%Get("NAZEV_ZASTAVKY")
			}
			quit
		}
		if 'nalezeno {
			kill ^||ML("OUT")
			set ^||ML("OUT","A_VYVESKA") = "zastavka '" _ hledana _ "' ve smeru " _ smerH _ " nenalezena"
		}
	}
	// Bez úklidu OUT by chyba v sondě poslala klientovi celý výstup GetData (megabajty stránek).
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Plní zákazníci **znaky variantních sloupců** u linky (`B67` krok 3)? Starý modul je bere ze
/// stringu `^JRL(sku,lin,"PATCH","mhd","par",2)` - i-tý znak patří i-tému sloupci
/// (`JRapiLinPrtMhd1.mac:26` a `:297`). Žádný default v kódu není; prázdné nastavení = sloupce
/// zůstanou bez značky. Podle toho, kolik linek ho reálně má, se rozhoduje, jestli TD stačí
/// parita (číst nastavení), nebo potřebuje i vlastní záložní řadu.
/// IN: DB, SKU (default 1), MAXLIN (default 500). Read-only.
ClassMethod ZnakySloupcu(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB")) if db = "" set db = dataDB
		set sku = $get(^||ML("IN","SKU")) if sku = "" set sku = 1
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin = 0 set maxLin = 500

		kill ^||ML("OUT")
		set linek = 0, sZnaky = 0, ukazek = 0, lin = ""
		kill vzorky, delky
		for {
			set lin = $order(^[db]JRL(sku, lin)) quit:lin=""
			quit:linek'<maxLin
			continue:'$data(^[db]JRL(sku, lin, "I"))
			set linek = linek + 1
			set znaky = $get(^[db]JRL(sku, lin, "PATCH", "mhd", "par", 2))
			continue:znaky=""
			set sZnaky = sZnaky + 1
			set delky($length(znaky)) = $get(delky($length(znaky))) + 1
			if ukazek < 8 {
				set ukazek = ukazek + 1
				set vzorky(ukazek) = "lin=" _ lin _ " znaky=""" _ znaky _ """"
			}
		}

		set ^||ML("OUT","A_FIRMA") = db _ " sku=" _ sku
		set ^||ML("OUT","B_SOUHRN") = "linek=" _ linek _ " sVyplnenymiZnaky=" _ sZnaky
		set d = "", prehled = ""
		for {
			set d = $order(delky(d)) quit:d=""
			set prehled = prehled _ $select(prehled="":"", 1:", ") _ d _ " znaku: " _ delky(d) _ "x"
		}
		set ^||ML("OUT","C_DELKY") = prehled
		set u = ""
		for {
			set u = $order(vzorky(u)) quit:u=""
			set ^||ML("OUT","D_VZOREK_" _ u) = vzorky(u)
		}
	}
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Kde na lince se zastávka obsluhuje z **víc stanovišť** - tedy kde se v režimu „Řádek pro každé
/// stanoviště" rozpadne na víc řádků. Slouží k tomu, aby šlo říct konkrétní vývěsku a zastávku
/// k proklikání; bez toho se hledá ručně listováním stránek.
/// IN: SKU (default 1), LIN. Read-only.
ClassMethod ViceStanovistLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set sku = $get(^||ML("IN","SKU")) if sku = "" set sku = 1
		set lin = $get(^||ML("IN","LIN"))
		if lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LIN je povinne"))

		kill ^||ML("OUT")
		set ^||ML("IN","SKUPINA_LINEK_ID") = sku
		set ^||ML("IN","LINKA_ID") = lin
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

		set stranek = 0, nalezu = 0, ukazek = 0, key = ""
		kill vzorky
		for {
			set key = $order(^||ML("OUT", key)) quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			set zast = page.rows.ZASTAVKY
			continue:'$isobject(zast)
			set stranek = stranek + 1
			// Zastávky PŘED vývěskou jsou projeté - tisknou se šedě a jen na vyžádání, takže rozpad
			// na řádky tam nikdo neuvidí. Pro proklikání jsou k ničemu; hledáme ty za vývěskou.
			set zaVyveskou = 0
			set it = zast.%GetIterator()
			while it.%GetNext(.i, .radek) {
				if radek.%Get("AKTUALNI_ZASTAVKA") = "true" set zaVyveskou = 1  continue
				continue:'zaVyveskou
				set nv = radek.%Get("NASTUPISTE_VARIANT")
				continue:'$isobject(nv)
				kill ruzne
				set pocet = 0
				for vi = 0:1:nv.%Size()-1 {
					set hodnota = nv.%Get(vi)
					continue:hodnota=""
					continue:$data(ruzne(hodnota))
					set ruzne(hodnota) = "", pocet = pocet + 1
				}
				continue:pocet<2
				set nalezu = nalezu + 1
				continue:ukazek'<8
				set ukazek = ukazek + 1
				// Délka TRASA_MIN je tu podstatná: klient rozvíjí řádky jen po délku pole časů
				// (`Math.Min(varianty, TrasaMin)`), takže kratší pole rozpad tiše vypne.
				set tm = radek.%Get("TRASA_MIN")
				set delkaCasu = $select($isobject(tm):tm.%Size(), 1:-1)
				set vzorky(ukazek) = "vyveska " _ page.values.%Get("POPIS_ZASTAVKA") _ " (smer " _ page.values.SMER _ ") -> zastavka " _ radek.%Get("NAZEV_ZASTAVKY") _ " ma " _ pocet _ " stanovist, TRASA_MIN delka " _ delkaCasu
			}
		}
		kill ^||ML("OUT")
		set ^||ML("OUT","A_LINKA") = "sku=" _ sku _ " lin=" _ lin
		set ^||ML("OUT","B_SOUHRN") = "stranek=" _ stranek _ " zastavekSViceStanovisti=" _ nalezu
		set u = ""
		for {
			set u = $order(vzorky(u)) quit:u=""
			set ^||ML("OUT","C_VZOREK_" _ u) = vzorky(u)
		}
	}
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Linky se spoji s platností **„+"** (jede v neděli a ve státem uznané svátky) - `B81`. Takový spoj
/// dnes nespadne do vlastního sloupce s dny „7", takže sloupec „Neděle" vyjde prázdný. Sonda dá
/// konkrétní linku a čísla spojů, na kterých se to dá doložit i po opravě.
/// IN: DB, MAXLIN (default 3000). Read-only.
ClassMethod NajdiPlusSpoje(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 3000

		kill ^||ML("OUT")
		set linek = 0, plusSpoju = 0, ukazek = 0, sku = ""
		kill vzorky
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek'<maxLin
				continue:'$data(^[db]JRL(sku,lin,"I"))
				set linek = linek + 1
				set naLince = "", pocetNaLince = 0
				for smerIdx = "T","Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
						continue:'$isvalidnum(sp)
						set plk = $piece($piece($get(^[db]JRL(sku,lin,smerIdx,sp)),"#",1),":",7)
						continue:'(plk["+")
						set plusSpoju = plusSpoju + 1
						set pocetNaLince = pocetNaLince + 1
						if $length(naLince,",") < 6 set naLince = naLince_$select(naLince="":"", 1:",")_smerIdx_sp_"("_plk_")"
					}
				}
				continue:'pocetNaLince
				continue:ukazek'<8
				set ukazek = ukazek + 1
				set vzorky(ukazek) = "sku="_sku_" lin="_lin_" · spojů s ""+""="_pocetNaLince_" · "_naLince
			}
		}
		set ^||ML("OUT","A_FIRMA") = db
		set ^||ML("OUT","B_SOUHRN") = "linek="_linek_" spojů s platností ""+""="_plusSpoju
		set u = ""
		for {
			set u = $order(vzorky(u)) quit:u=""
			set ^||ML("OUT","C_VZOREK_"_u) = vzorky(u)
		}
	}
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Přehled **týdenních platností** v datech firmy - které tvary `Plk` reálně existují, jak často
/// a na které lince se dá každý ukázat. Slouží k výběru linek pro vizuální kontrolu vlastních
/// sloupců (`B81`/`B82`): pro sloupec „pracovní dny" je potřeba spoj jedoucí jen v jeden den,
/// pro nedělní sloupec spoj s „+" apod.
/// IN: DB, MAXLIN (default 3000), MAXOUT (default 20). Read-only.
ClassMethod PrehledPlatnosti(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 3000
		set maxOut = +$get(^||ML("IN","MAXOUT")) if maxOut < 1 set maxOut = 20

		kill ^||ML("OUT"), pocet, kde
		set linek = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek'<maxLin
				continue:'$data(^[db]JRL(sku,lin,"I"))
				set linek = linek + 1
				for smerIdx = "T","Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
						continue:'$isvalidnum(sp)
						set plk = $piece($piece($get(^[db]JRL(sku,lin,smerIdx,sp)),"#",1),":",7)
						continue:plk=""
						set pocet(plk) = $get(pocet(plk)) + 1
						if '$data(kde(plk)) set kde(plk) = "lin="_lin_" spoj="_smerIdx_sp
					}
				}
			}
		}
		// seřadit dle četnosti sestupně
		kill dle
		set plk = ""
		for { set plk = $order(pocet(plk)) quit:plk=""  set dle(-pocet(plk),plk) = "" }
		set ^||ML("OUT","A_FIRMA") = db_" linek="_linek
		set n = 0, c = ""
		for {
			set c = $order(dle(c)) quit:c=""
			set plk = ""
			for {
				set plk = $order(dle(c,plk)) quit:plk=""
				quit:n'<maxOut
				set n = n + 1
				set ^||ML("OUT","B_"_$translate($justify(n,3)," ","0")) = """"_plk_""" = "_pocet(plk)_"x · "_kde(plk)
			}
			quit:n'<maxOut
		}
	}
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Je zastávka v uzlu 32 **cíl** pokračování, nebo předávací bod? (#13093 bod 3, otázka na kolegy.)
/// Pro pár spojů s uzlem 32 dohledá trasu pokračujícího spoje a vypíše její první a poslední
/// obsluhovanou zastávku, aby šlo porovnat se zastávkou ze záznamu.
/// IN: DB, MAXLIN, LIN (volitelně jen jedna linka). Read-only.
ClassMethod PokracovaniOverit(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set jenLin = $get(^||ML("IN","LIN"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 400
		set uzel = +$get(^||ML("IN","UZEL")) if uzel < 1 set uzel = 34   // kolega 4. 8.: pokračování je v 34
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, ukazek = 0
		set ^||ML("OUT","0_UZEL") = "zkoumany uzel = " _ uzel
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek>=maxLin
				quit:ukazek>=5
				if (jenLin '= "") && (lin '= jenLin) continue
				set linek = linek + 1
				for sm = "T", "Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,sm,sp)) quit:sp=""
						quit:ukazek>=5
						continue:'$isvalidnum(sp)
						set zapis = $get(^[db]JRL(sku,lin,sm,sp,uzel))
						continue:zapis=""
						// statistika koncových polí + počtu záznamů (má f5/f6 vůbec varianty?)
						set recAll = $piece(zapis, "@", 2)
						set kombi = "f5=" _ $piece(recAll, ":", 5) _ " f6=" _ $piece(recAll, ":", 6) _ " poli=" _ $length(recAll, ":") _ " zaznamu=" _ $length(zapis, "@")
						set kombinace(kombi) = $get(kombinace(kombi)) + 1
						// uzel 34: `u@<nas spoj>:<linka>:<spoj>:<zastavka>:<f5>:<f6>`
						set rec = $piece(zapis, "@", 2)
						set pLin = $piece(rec, ":", 2), pSp = $piece(rec, ":", 3), pZast = $piece(rec, ":", 4)
						set ukazek = ukazek + 1
						set ^||ML("OUT","A_NAS", ukazek) = "sku=" _ sku _ " lin=" _ lin _ " smer=" _ sm _ " spoj=" _ sp _ "  RAW: " _ $extract(zapis, 1, 90)
						set ^||ML("OUT","B_ZAPIS", ukazek) = "pokracuje linka=" _ pLin _ " spoj=" _ pSp _ " zastavka=""" _ pZast _ """ f5=" _ $piece(rec, ":", 5) _ " f6=" _ $piece(rec, ":", 6) _ " poli=" _ $length(rec, ":")
						if pLin = "" set ^||ML("OUT","C_TRASA", ukazek) = "prazdna linka - neparsovatelne" continue
						// klíč linky v globálu je se vedoucími nulami na 7 znaků
						set pLinKey = $extract("0000000", 1, 7 - $length(pLin)) _ pLin
						set nalezeno = 0
						for sm2 = "T", "Z" {
							continue:nalezeno
							set hlava = $piece($get(^[db]JRL(sku,pLinKey,sm2,pSp)), "#", 1)
							continue:hlava=""
							set nalezeno = 1
							set cisloTrasy = $piece(hlava, ":", 3)
							set wTrasa = $get(^[db]JRL(sku,pLinKey,"W",cisloTrasy))
							set prvni = "", posledni = ""
							for i = 1:1:$length(wTrasa, "#") {
								set seg = $piece(wTrasa, "#", i)
								set pp = $piece(seg, ":", 1), cas = $piece(seg, ":", 2)
								continue:pp=""
								continue:(cas="<")||(cas="")||(cas="|")
								set:prvni="" prvni = pp
								set posledni = pp
							}
							set ^||ML("OUT","C_TRASA", ukazek) = "smer=" _ sm2 _ " trasa=" _ cisloTrasy _ " prvni poradi=" _ prvni _ " posledni poradi=" _ posledni
							set ^||ML("OUT","D_PRVNI", ukazek) = $extract($get(^[db]JRL(sku,pLinKey,"J",prvni)), 1, 60)
							set ^||ML("OUT","E_POSLEDNI", ukazek) = $extract($get(^[db]JRL(sku,pLinKey,"J",posledni)), 1, 60)
						}
						if 'nalezeno set ^||ML("OUT","C_TRASA", ukazek) = "pokracujici spoj nenalezen (jina skupina linek nebo vlak)"
					}
				}
			}
		}
		set k = "", i = 0
		for { set k = $order(kombinace(k)) quit:k=""  set i = i + 1, ^||ML("OUT","F_KOMBINACE", i) = k _ "  x" _ kombinace(k) }
		set ^||ML("OUT","SOUHRN") = db _ " prosleho linek=" _ linek _ " ukazek=" _ ukazek
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Jaké vztahy mezi spoji data nesou (#13093 bod 3). Legacy zná rodinu vyčká / navazuje / pokračuje
/// a označit se smí jen „pokračuje", jinak dostane značku i spoj, který jen čeká na návaznost.
/// IN: DB, MAXLIN. OUT: pro každý numerický subnode spoje počet + ukázky payloadu (prefix `m@<id>#`).
ClassMethod ScanVztahySpoju(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 400
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0
		kill pocty, ukazky
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for sm = "T", "Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,sm,sp)) quit:sp=""
						continue:'$isvalidnum(sp)
						set uzel = ""
						for {
							set uzel = $order(^[db]JRL(sku,lin,sm,sp,uzel)) quit:uzel=""
							continue:'$isvalidnum(uzel)
							set val = $get(^[db]JRL(sku,lin,sm,sp,uzel))
							set prefix = $piece(val, "#", 1)
							set klic = uzel _ " / " _ prefix
							set pocty(klic) = $get(pocty(klic)) + 1
							if '$data(ukazky(klic)) set ukazky(klic) = "lin=" _ lin _ " sp=" _ sp _ ": " _ $extract(val, 1, 110)
						}
					}
				}
			}
		}
		set klic = "", n = 0
		for {
			set klic = $order(pocty(klic)) quit:klic=""
			set n = n + 1
			set ^||ML("OUT","VZTAH",n) = "[uzel/prefix " _ klic _ "] x" _ pocty(klic) _ " -- " _ $get(ukazky(klic))
		}
		set ^||ML("OUT","SOUHRN") = db _ " linek=" _ linek _ " ruznych vztahu=" _ n
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Fulltext přes zdrojáky legacy rutin. IN: PREFIX (např. "JRapi"), PATTERN (case-insensitive),
/// MAX (default 60). OUT: "<rutina>:<řádek> = <text>".
ClassMethod GrepRoutines(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set prefix = $get(^||ML("IN","PREFIX"))
		set pattern = $zconvert($get(^||ML("IN","PATTERN")), "L")
		set max = +$get(^||ML("IN","MAX")) if max < 1 set max = 60
		kill ^||ML("OUT")
		if pattern = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "PATTERN je povinný"))

		set nalezeno = 0, prohledano = 0
		set rtn = $select(prefix="":"", 1:$extract(prefix, 1, $length(prefix) - 1) _ $char($ascii($extract(prefix, $length(prefix))) - 1))
		for {
			set rtn = $order(^rMAC(rtn))
			quit:rtn=""
			quit:(prefix'="")&&($extract(rtn, 1, $length(prefix))'=prefix)
			quit:nalezeno>=max
			set prohledano = prohledano + 1
			set i = ""
			for {
				set i = $order(^rMAC(rtn, 0, i))
				quit:i=""
				quit:nalezeno>=max
				set txt = $get(^rMAC(rtn, 0, i))
				continue:txt=""
				continue:'$find($zconvert(txt, "L"), pattern)
				set nalezeno = nalezeno + 1
				set ^||ML("OUT", "HIT", $translate($justify(nalezeno, 3), " ", "0")) = rtn _ ":" _ i _ " = " _ txt
			}
		}
		set ^||ML("OUT", "SOUHRN") = "rutin=" _ prohledano _ " nalezu=" _ nalezeno _ " (max " _ max _ ")"
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Poznámky linky, jak je vydá agenda (#13093 bod 1) - kategorie a výlukové texty.
/// IN: DB, SKU, LIN. Read-only.
ClassMethod PoznamkyLinkyDump(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set arr = ##class(%DynamicArray).%New()
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PoznamkyLinky(db, sku, lin, arr)
		set n = 0, it = arr.%GetIterator()
		while it.%GetNext(.k, .p) {
			set n = n + 1
			set ^||ML("OUT","POZNAMKA", n) = "[" _ p.%Get("KATEGORIE") _ "] " _ $extract(p.%Get("TEXT"), 1, 70)
		}
		set ^||ML("OUT","POCET") = n
		set ^||ML("OUT","VYLUKOVE_TEXTY") = $extract(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).VylukoveTexty(db, sku, lin), 1, 200)
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Plní zákazník atributy spoje, které chce u odjezdů (#13093 bod 2)? Kola = hlava věty spoje pole
/// **19** (legacy `dta20` PrepJiKola), místenkování = pole **6** (`dta6`, znak R), typ místenkování =
/// pole **11** (`dta12`: 1 nutno, 2 možno AMS, 3 nutno AMS). Navíc návaznosti (subnode 32) pro
/// pokračující spoje (bod 3). IN: DB, MAXLIN. Read-only.
ClassMethod ScanAtributySpoju(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, spoju = 0, kola = 0, misten = 0, typMisten = 0, navaz = 0, ukazek = 0
		kill hodnKola, hodnMisten
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for sm = "T", "Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,sm,sp))
						quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						set hlava = $piece($get(^[db]JRL(sku,lin,sm,sp)), "#", 1)
						set vKola = $piece(hlava, ":", 19)
						set vMist = $piece(hlava, ":", 6)
						set vTyp  = $piece(hlava, ":", 11)
						if vKola '= "" {
						set kola = kola + 1, hodnKola(vKola) = $get(hodnKola(vKola)) + 1
						if $get(ukazekKola) < 5 { set ukazekKola = $get(ukazekKola) + 1, ^||ML("OUT","KOLA_UKAZKY",ukazekKola) = "sku="_sku_" lin="_lin_" smer="_sm_" spoj="_sp_" hodnota="_vKola }
					}
						if vMist '= "" { set misten = misten + 1, hodnMisten(vMist) = $get(hodnMisten(vMist)) + 1 }
						if vTyp '= "" { set typMisten = typMisten + 1 }
						if $data(^[db]JRL(sku,lin,sm,sp,32)) {
							set navaz = navaz + 1
							if ukazek < 5 { set ukazek = ukazek + 1, ^||ML("OUT","NAVAZNOST",ukazek) = "sku="_sku_" lin="_lin_" smer="_sm_" spoj="_sp_": "_$extract($get(^[db]JRL(sku,lin,sm,sp,32)),1,90) }
						}
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" spoju="_spoju
		set ^||ML("OUT","KOLA") = "spoju s kolem="_kola
		set ^||ML("OUT","MISTENKOVANI") = "spoju s R="_misten_" s typem mistenkovani="_typMisten
		set ^||ML("OUT","NAVAZNOSTI") = "spoju s navaznosti="_navaz
		set h = "", radek = ""
		for { set h = $order(hodnKola(h)) quit:h=""  set radek = radek_h_"("_hodnKola(h)_"x) " }
		set:radek'="" ^||ML("OUT","KOLA_HODNOTY") = radek
		set h = "", radek = ""
		for { set h = $order(hodnMisten(h)) quit:h=""  set radek = radek_h_"("_hodnMisten(h)_"x) " }
		set:radek'="" ^||ML("OUT","MISTEN_HODNOTY") = radek
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Které linky mají vůbec nastavenou barvu (#12997) - hledá `^JRL(sku,lin,"PATCH","BARVA")`.
/// IN: DB, MAXLIN. Read-only.
ClassMethod NajdiBarvy(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, nalez = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				set barva = $get(^[db]JRL(sku,lin,"PATCH","BARVA"))
				continue:barva=""
				set nalez = nalez + 1
				if nalez <= 10 { set ^||ML("OUT","NALEZ",nalez) = "sku="_sku_" lin="_lin_" barva="_barva }
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" s barvou="_nalez
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Barvy linky po Petrově změně (#12997): co vrací JŘ API a co z toho udělá agenda.
/// IN: DB, SKU, LIN. OUT: seznam barev, naše hodnota, kde to leží. Read-only.
ClassMethod BarvyLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set linIdx = $$cnvCsLin^JRapi(lin)
		set ^||ML("OUT","LIN_INDEX") = linIdx
		set ^||ML("OUT","RAW") = $get(^[db]JRL(sku, linIdx, "PATCH", "BARVA"))

		set seznam = ##class(ALVA.ISY.ISYJR2.API.Linky).GetBarvyLinky(IdFa, db, sku, lin)
		set ^||ML("OUT","POCET_BAREV") = $select(seznam="":0, 1:$listlength(seznam))
		if seznam '= "" {
			for i = 1:1:$listlength(seznam) {
				set ^||ML("OUT","BARVA", i) = $listget(seznam, i)
			}
		}
		// jak to dnes vidí naše agenda (volá GetBarvaLinky bez pořadí)
		set ^||ML("OUT","AGENDA_BARVA_LINKY") = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BarvaLinky(IdFa, db, sku, lin)
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sloupky a označníky jedné stránky - kde má „výčet všech" co zobrazit (víc hodnot dle spojů).
/// IN: DB, SKU, LIN, SMER, PORADI. Read-only.
ClassMethod StrankaSloupky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set smer = $get(^||ML("IN","SMER")) set:smer="" smer = "T"
		set poradi = +$get(^||ML("IN","PORADI")) set:'poradi poradi = 1
		kill ^||ML("OUT"), ^||TiskAd
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, sku, lin, poradi, smer, "")
		if '$isobject(page) {
			set ^||ML("OUT","CHYBA") = "stránka se nesestavila"
			ret $$$OK
		}
		set n = 0, vic = 0, it = page.rows.%Get("ZASTAVKY").%GetIterator()
		while it.%GetNext(.k, .z) {
			set n = n + 1
			set spoju = z.%Get("SLOUPEK_SPOJU")
			if $length(spoju, ",") > 1 { set vic = vic + 1 }
			set ^||ML("OUT","ZASTAVKA",n) = z.%Get("NAZEV_ZASTAVKY")_" | sloupek="_z.%Get("SLOUPEK")_" dle spoju="_spoju_" | oznacnik="_z.%Get("NASTUPISTE")_" dle spoju="_z.%Get("NASTUPISTE_SPOJU")
		}
		set ^||ML("OUT","SOUHRN") = "zastavek="_n_" z toho s vice sloupky dle spoju="_vic
		kill ^||TiskAd
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Atributy zastávek a legenda jedné stránky - kontrola, co GetData reálně vydá.
/// IN: DB, SKU, LIN, SMER, PORADI. Read-only.
ClassMethod StrankaAtributy(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set smer = $get(^||ML("IN","SMER")) set:smer="" smer = "T"
		set poradi = +$get(^||ML("IN","PORADI")) set:'poradi poradi = 1
		kill ^||ML("OUT"), ^||TiskAd
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, sku, lin, poradi, smer, "")
		if '$isobject(page) {
			set ^||ML("OUT","CHYBA") = "stránka se nesestavila (zastávka není obsluhovaná, nebo za ní už nic nejede)"
			ret $$$OK
		}

		set n = 0, it = page.rows.%Get("ZASTAVKY").%GetIterator()
		while it.%GetNext(.k, .z) {
			set atrTxt = "", ai = ""
			set ita = z.%Get("ATRIBUTY").%GetIterator()
			while ita.%GetNext(.ak, .a) { set atrTxt = atrTxt _ a.%Get("ZNAK") }
			continue:atrTxt=""
			set n = n + 1
			set ^||ML("OUT","ZASTAVKA",n) = z.%Get("PORADI")_" "_z.%Get("NAZEV_ZASTAVKY")_" znaky="_atrTxt
		}
		set ^||ML("OUT","ZASTAVEK_SE_ZNACKOU") = n

		set p = 0, itp = page.rows.%Get("POZNAMKY").%GetIterator()
		while itp.%GetNext(.pk, .pz) {
			set p = p + 1
			set ^||ML("OUT","POZNAMKA",p) = "["_pz.%Get("ZNAK")_"] "_pz.%Get("KATEGORIE")_": "_$extract(pz.%Get("TEXT"),1,60)
		}

		// značky u odjezdů - odpověď na „mění ten parametr jen odjezdy, nebo i něco jiného?"
		set d = 0, itd = page.rows.%Get("ODJEZDY").%GetIterator()
		while itd.%GetNext(.dk, .o) {
			set znaky = ""
			set ita2 = o.%Get("ANOTACE").%GetIterator()
			while ita2.%GetNext(.k2, .a2) { set znaky = znaky _ a2.%Get("ZNAK") }
			continue:znaky=""
			set d = d + 1
			if d '> 40 { set ^||ML("OUT","ODJEZD",d) = o.%Get("HODINA")_":"_o.%Get("MINUTA")_" znaky="_znaky }
		}
		set ^||ML("OUT","ODJEZDU_SE_ZNACKOU") = d
		kill ^||TiskAd
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// „Na zavolání / na objednání" - kde je to v datech vyplněné (#13048 bod 1, dotaz zákazníka).
/// Tři různá místa: spoj `Radiobus/jede na objednání` (hlava věty spoje pole 21 = legacy `dta30`),
/// spoj `jede podmínečně` (pole 23 = `dta34`) a **zastávka v trase spoje** (W pozice 5 = atributy
/// trasy spoje, znaky `r` = na objednání / `i` = podmínečně). IN: DB, MAXLIN. Read-only.
ClassMethod ScanNaZavolani(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, spoju = 0, naObj = 0, podm = 0, wrRadku = 0, ukazek = 0
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for sm = "T", "Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,sm,sp))
						quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						set hlava = $piece($get(^[db]JRL(sku,lin,sm,sp)), "#", 1)
						if $piece(hlava, ":", 21) '= "" {
							set naObj = naObj + 1
							if ukazek < 6 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "SPOJ na objednani: sku="_sku_" lin="_lin_" smer="_sm_" spoj="_sp_" hodnota="_$piece(hlava,":",21) }
						}
						if $piece(hlava, ":", 23) '= "" { set podm = podm + 1 }
					}
				}
				// atributy v trase spoje (W pozice 5) - znaky r / i
				set ct = ""
				for {
					set ct = $order(^[db]JRL(sku,lin,"W",ct))
					quit:ct=""
					set w = $get(^[db]JRL(sku,lin,"W",ct))
					for i = 1:1:$length(w,"#") {
						set atr = $piece($piece(w,"#",i), ":", 5)
						continue:atr=""
						if '$find(atr,"r"), '$find(atr,"i") continue
						set wrRadku = wrRadku + 1
						if ukazek < 6 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "ZASTAVKA V TRASE SPOJE: sku="_sku_" lin="_lin_" trasa="_ct_" atributy="_atr }
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" spoju="_spoju_" | spoj na objednani="_naObj_" spoj podminecne="_podm_" zastavka v trase spoje (r/i)="_wrRadku
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kde se v datech vyskytuje daný znak atributu - linka, pořadí a zastávka. Doplněk `ScanAtributy`
/// pro vizuální kontrolu. IN: DB, ZNAK, MAXLIN. Read-only.
ClassMethod NajdiAtribut(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set hledany = $get(^||ML("IN","ZNAK"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if (db = "") || (hledany = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a ZNAK jsou povinné"))

		set linek = 0, nalez = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				set poradi = ""
				for {
					set poradi = $order(^[db]JRL(sku,lin,"J",poradi))
					quit:poradi=""
					set veta = $get(^[db]JRL(sku,lin,"J",poradi))
					set atr = $piece($piece($piece(veta, "#", 1), ":", 2), "#", 1)
					continue:'$find(atr, hledany)
					set nalez = nalez + 1
					continue:nalez>10
					set czst = $piece(veta, ":", 1)
					set ^||ML("OUT","NALEZ",nalez) = "sku="_sku_" lin="_lin_" poradi="_poradi_" czst="_czst_" atributy="_atr_" nazev="_$piece($get(^[db]JRZ(czst)), "$", 2)
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" znak="_hledany_" linek="_linek_" nalezu="_nalez
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Výskyt znaků atributů zastávky (#13048 bod 1): kolikrát se který znak objeví v registru zastávek
/// (`$`-pole 8) a v trase linky (J-věta `#1:2`). Ukáže, které znaky číselníku 16 zákazník reálně
/// používá - tedy jestli má smysl je tisknout. IN: DB, MAXLIN. Read-only.
ClassMethod ScanAtributy(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		// registr zastávek (celý)
		kill pocty
		set czst = ""
		for {
			set czst = $order(^[db]JRZ(czst))
			quit:czst=""
			continue:'$isvalidnum(czst)
			set atr = $piece($get(^[db]JRZ(czst)), "$", 8)
			for i = 1:1:$length(atr) {
				set znak = $extract(atr, i)
				continue:znak=","
				set pocty("REGISTR", znak) = $get(pocty("REGISTR", znak)) + 1
			}
		}

		// trasy linek
		set linek = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				set poradi = ""
				for {
					set poradi = $order(^[db]JRL(sku,lin,"J",poradi))
					quit:poradi=""
					set atr = $piece($piece($piece($get(^[db]JRL(sku,lin,"J",poradi)), "#", 1), ":", 2), "#", 1)
					for i = 1:1:$length(atr) {
						set znak = $extract(atr, i)
						continue:znak=","
						set pocty("TRASA", znak) = $get(pocty("TRASA", znak)) + 1
					}
				}
			}
		}

		set zdroj = ""
		for {
			set zdroj = $order(pocty(zdroj))
			quit:zdroj=""
			set znak = "", radek = ""
			for {
				set znak = $order(pocty(zdroj, znak))
				quit:znak=""
				set radek = radek _ $select(radek="":"", 1:" · ") _ znak _ "=" _ pocty(zdroj, znak)
			}
			set ^||ML("OUT", zdroj) = radek
		}
		set ^||ML("OUT","SOUHRN") = db _ " linek=" _ linek
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Čísla a názvy zastávek jedné stránky - kontrola, že výchozí/cílová sedí na směr stránky.
/// IN: DB, SKU, LIN, SMER, PORADI. Read-only.
ClassMethod StrankaCisla(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set smer = $get(^||ML("IN","SMER")) set:smer="" smer = "T"
		set poradi = +$get(^||ML("IN","PORADI")) set:'poradi poradi = 1
		kill ^||ML("OUT"), ^||TiskAd
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, sku, lin, poradi, smer, "")
		if '$isobject(page) {
			set ^||ML("OUT","CHYBA") = "stránka se nesestavila"
			set ^||ML("OUT","DIAG") = "db="_db_" sku="_sku_" lin="_lin_" poradi="_poradi_" I="_$data(^[db]JRL(sku,lin,"I"))
			ret $$$OK
		}
		set klice = $listbuild("POPIS_ZASTAVKA", "CISLO_ZASTAVKA", "CIS_ZASTAVKA", "CIS_ZASTAVKA_VYCHOZI", "CIS_ZASTAVKA_CILOVA", "KONECNA_ZASTAVKA")
		for i = 1:1:$listlength(klice) {
			set k = $list(klice, i)
			set ^||ML("OUT", k) = page.values.%Get(k)
		}
		kill ^||TiskAd
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kde v datech leží text se zadaným vzorkem (#13088 výluky): hledá v poznámkách linky
/// (`^JRL(sku,lin,"C",key,idx)`) i v komentářích spoje (`^JRL(sku,lin,"MhD","SeT_SpeC",znak)`).
/// IN: DB, PATTERN, MAXLIN (default 500). OUT: počty per zdroj + ukázky.
ClassMethod ScanText(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set pattern = $zconvert($get(^||ML("IN","PATTERN")), "L")
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if (db = "") || (pattern = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a PATTERN jsou povinné"))

		set linek = 0, vPozn = 0, vKomentar = 0, ukazek = 0
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1

				// poznámky linky
				set key = ""
				for {
					set key = $order(^[db]JRL(sku,lin,"C",key))
					quit:key=""
					set idx = ""
					for {
						set idx = $order(^[db]JRL(sku,lin,"C",key,idx))
						quit:idx=""
						set txt = $get(^[db]JRL(sku,lin,"C",key,idx))
						continue:'$find($zconvert(txt,"L"), pattern)
						set vPozn = vPozn + 1
						if ukazek < 8 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "POZNAMKA_LINKY sku="_sku_" lin="_lin_" key="_key_": "_$extract(txt,1,90) }
					}
				}

				// komentáře spoje (SeT_SpeC)
				set znak = ""
				for {
					set znak = $order(^[db]JRL(sku,lin,"MhD","SeT_SpeC",znak))
					quit:znak=""
					set txt = $get(^[db]JRL(sku,lin,"MhD","SeT_SpeC",znak))
					continue:'$find($zconvert(txt,"L"), pattern)
					set vKomentar = vKomentar + 1
					if ukazek < 8 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "KOMENTAR_SPOJE sku="_sku_" lin="_lin_" znak="_znak_": "_$extract(txt,1,90) }
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" nalezu: poznamky linky="_vPozn_" komentare spoje="_vKomentar
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Řádky implementace metody na SERVERU obsahující vzorek - kontrola, že se `.cls` opravdu nasadila
/// (editace mimo hook se na server nedostane a testy pak běží proti staré kopii).
/// IN: CLASS, METHOD, PATTERN. OUT: nalezené řádky.
ClassMethod ZdrojMetody(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set cls = $get(^||ML("IN","CLASS"))
		set met = $get(^||ML("IN","METHOD"))
		set pattern = $get(^||ML("IN","PATTERN"))
		kill ^||ML("OUT")
		if (cls = "") || (met = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "CLASS a METHOD jsou povinné"))

		set def = ##class(%Dictionary.MethodDefinition).%OpenId(cls _ "||" _ met)
		if '$isobject(def) {
			set ^||ML("OUT","CHYBA") = "metoda " _ cls _ "::" _ met _ " na serveru neexistuje"
			ret $$$OK
		}
		set ^||ML("OUT","SIGNATURA") = def.Name _ "(" _ def.FormalSpec _ ") As " _ def.ReturnType
		set impl = def.Implementation
		do impl.Rewind()
		set n = 0, nalez = 0
		while 'impl.AtEnd {
			set radek = impl.ReadLine()
			set n = n + 1
			continue:(pattern '= "")&&('$find(radek, pattern))
			set nalez = nalez + 1
			set ^||ML("OUT","RADEK",n) = radek
		}
		set ^||ML("OUT","SOUHRN") = "řádků=" _ n _ " nálezů=" _ nalez
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kolik linek plní per-spoj sloupek (W pozice 13) a označník (W pozice 7) a u kolika zastávek se
/// hodnoty mezi trasami liší (= má vůbec smysl režim „více hodnot" nad sloupkem). IN: DB, MAXLIN.
ClassMethod ScanPerSpoj(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, linekSl = 0, linekOzn = 0, multiSl = 0, multiOzn = 0
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				kill hodSl, hodOzn
				set maSl = 0, maOzn = 0
				set ct = ""
				for {
					set ct = $order(^[db]JRL(sku,lin,"W",ct))
					quit:ct=""
					set w = $get(^[db]JRL(sku,lin,"W",ct))
					for i = 1:1:$length(w,"#") {
						set seg = $piece(w,"#",i)
						set poradi = $piece(seg,":",1)
						continue:poradi=""
						set sl = $piece(seg,":",13)
						set ozn = $piece(seg,":",7)
						if sl '= "" { set maSl = 1, hodSl(poradi,sl) = 1 }
						if ozn '= "" { set maOzn = 1, hodOzn(poradi,ozn) = 1 }
					}
				}
				set linekSl = linekSl + maSl, linekOzn = linekOzn + maOzn
				set p = ""
				for {
					set p = $order(hodSl(p))
					quit:p=""
					set n = 0, v = "" for { set v = $order(hodSl(p,v)) quit:v=""  set n = n + 1 }
					if n > 1 {
						set multiSl = multiSl + 1
						if multiSl <= 10 set ^||ML("OUT","MULTISL",multiSl) = db_" sku="_sku_" lin="_lin_" poradi="_p
					}
				}
				set p = ""
				for {
					set p = $order(hodOzn(p))
					quit:p=""
					set n = 0, v = "" for { set v = $order(hodOzn(p,v)) quit:v=""  set n = n + 1 }
					set:n>1 multiOzn = multiOzn + 1
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_": linek="_linek_" s per-spoj sloupkem="_linekSl_" s per-spoj označníkem="_linekOzn
		set ^||ML("OUT","MULTI") = "zastávek s víc hodnotami: sloupek="_multiSl_" označník="_multiOzn
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39`: kde se obdobím **potlačí negativní značka** (spoj jede celé období sloupce, takže
/// nemá co vysvětlovat) a kde naopak zůstane (jede jen část). Klíčový požadavek zákazníka z #13045.
/// IN: DB, SKU, LIN, OBDOBI, STRANEK. Read-only.
ClassMethod ZnackyProZkousku(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set obdobi = $get(^||ML("IN","OBDOBI")) set:obdobi="" obdobi = "1.7.-31.8."
		set stranek = +$get(^||ML("IN","STRANEK")) if stranek < 1 set stranek = 6
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set zbytek = """2"",""nadpis"":""Soboty"",""dny"":""6""},{""id"":""3"",""nadpis"":""Nedele a svatky"",""dny"":""7+"""
		set defBez = "{""sloupce"":[{""id"":""1"",""nadpis"":""Pracovni dny"",""dny"":""12345""},{""id"":" _ zbytek _ "}]}"
		set defS = "{""sloupce"":[{""id"":""1"",""nadpis"":""Pracovni dny"",""dny"":""12345"",""obdobi"":""" _ obdobi _ """},{""id"":" _ zbytek _ "}]}"

		do ..ZnackyStranek(.sys, db, sku, lin, defBez, stranek, .bez)
		do ..ZnackyStranek(.sys, db, sku, lin, defS, stranek, .s)
		kill ^||ML("OUT")
		set ^||ML("OUT","A_ZADANI") = "linka "_lin_", sloupec Pracovni dny, obdobi """_obdobi_""""
		set n = 0, cas = ""
		for {
			set cas = $order(bez(cas)) quit:cas=""
			continue:'$data(s(cas))
			continue:bez(cas)=s(cas)
			set n = n + 1
			quit:n>10
			set ^||ML("OUT","B_ZMENA",n) = cas_": znacky bez obdobi """_bez(cas)_""" → s obdobim """_s(cas)_""""
		}
		set ^||ML("OUT","C_SOUHRN") = "odjezdu se zmenenymi znackami (prvnich "_stranek_" stranek) = "_n
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Značky u odjezdů 1. sloupce, klíčované „zastávka čas". Pomocná pro ZnackyProZkousku.
ClassMethod ZnackyStranek(ByRef sys As %String, db As %String, sku As %String, lin As %String, definice As %String, stranek As %Integer, ByRef out)
{
	kill out
	set sys("U","DATA") = db
	kill ^||ML("IN")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	set ^||ML("IN","DEFINICE_SLOUPCU") = definice
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set klic = "", i = 0
	for {
		set klic = $order(^||ML("OUT",klic)) quit:klic=""
		continue:'$isvalidnum(klic)
		set i = i + 1
		quit:i>stranek
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT",klic))
		set zast = page.values.%Get("POPIS_ZASTAVKA")
		set it = page.rows.%Get("ODJEZDY").%GetIterator()
		while it.%GetNext(.k, .o) {
			continue:o.%Get("SKUPINA_DNU_ID")'="1"
			set znaky = "", ita = o.%Get("ANOTACE").%GetIterator()
			while ita.%GetNext(.ak, .a) { set znaky = znaky_a.%Get("ZNAK") }
			set out(zast_" "_o.%Get("HODINA")_":"_$translate($justify(o.%Get("MINUTA"),2)," ",0)) = znaky
		}
	}
	kill ^||ML("OUT")
}

/// Sonda k #12843: jaké linky MHD modul v dané firmě zná (`ALVA.MHD.LSO`) - potřeba k dohledání
/// linky, na které jde vizuálně porovnat značky. IN: DB, IDFA, MAX. Read-only.
ClassMethod MhdLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set fa = $get(^||ML("IN","IDFA")) set:fa="" fa = IdFa
		set max = +$get(^||ML("IN","MAX")) if max < 1 set max = 15
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set n = 0, id = ""
		for {
			set id = $order(^[db]ALVA.MHD.LSO(fa,1,"DTA",id)) quit:id=""
			set n = n + 1
			quit:n>max
			set ^||ML("OUT","LINKA",$translate($justify(n,2)," ",0)) = "id="_id_
				" cislo="_$get(^[db]ALVA.MHD.LSO(fa,1,"DTA",id,"DTA",1,"CisloLinky"))_
				" nazev="_$extract($get(^[db]ALVA.MHD.LSO(fa,1,"DTA",id,"DTA",1,"Nazev")),1,40)_
				" plati="_$get(^[db]ALVA.MHD.LSO(fa,1,"DTA",id,"DTA",1,"PlatiOd"))
		}
		set ^||ML("OUT","SOUHRN") = db_" firma="_fa_" linek(prvnich "_max_")="_$select(n>max:max, 1:n)
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k #12843 bodu 6: které značky MHD vývěska reálně vydá - u zastávek i v legendě - a jaký
/// piktogram jim ve fontu odpovídá. Slouží k porovnání „co je v datech" × „co se nakreslí".
/// IN: DB, LINKA (id linky MHD), MAXZAST. Read-only.
ClassMethod MhdZnackyLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set linka = $get(^||ML("IN","LINKA"))
		set maxZast = +$get(^||ML("IN","MAXZAST")) if maxZast < 1 set maxZast = 12
		kill ^||ML("OUT")
		if (db = "") || (linka = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LINKA jsou povinné"))

		set sys("U","DATA") = db
		kill ^||ML("IN")
		set ^||ML("IN","LINKA_ID") = linka
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
		set chyba = $get(^||ML("OUT","ERR"))
		set prvni = "", klic = "", stranek = 0
		for {
			set klic = $order(^||ML("OUT",klic)) quit:klic=""
			continue:'$isvalidnum(klic)
			set stranek = stranek + 1
			if prvni = "" set prvni = $get(^||ML("OUT",klic))
		}
		kill ^||ML("OUT")
		set ^||ML("OUT","A_SOUHRN") = db_" linka="_linka_" stranek="_stranek
		if chyba '= "" set ^||ML("OUT","A_CHYBA") = chyba
		if prvni = "" ret $$$OK

		set page = ##class(%DynamicObject).%FromJSON(prvni)
		set ^||ML("OUT","B_ZASTAVKA") = page.values.%Get("POPIS_ZASTAVKA")_" · smer "_page.values.%Get("KONECNA_ZASTAVKA")
		// značky u zastávek
		set n = 0, it = page.rows.%Get("ZASTAVKY").%GetIterator()
		while it.%GetNext(.k, .z) {
			set znaky = "", ita = z.%Get("ATRIBUTY").%GetIterator()
			while ita.%GetNext(.ak, .a) { set znaky = znaky_a.%Get("ZNAK")_"("_$ascii(a.%Get("ZNAK"))_") " }
			continue:znaky=""
			set n = n + 1
			quit:n>maxZast
			set ^||ML("OUT","C_ZASTAVKA",n) = $extract(z.%Get("NAZEV_ZASTAVKY"),1,28)_" → "_znaky
		}
		set ^||ML("OUT","C_POCET") = "zastavek se znackou="_n
		// legenda
		set p = 0, itp = page.rows.%Get("POZNAMKY").%GetIterator()
		while itp.%GetNext(.pk, .pz) {
			set p = p + 1
			quit:p>20
			set zn = pz.%Get("ZNAK")
			set ^||ML("OUT","D_LEGENDA",p) = "znak="""_zn_""" ("_$select(zn="":"-",1:$ascii(zn))_") typ="_pz.%Get("TYP")_
				" kat="_pz.%Get("KATEGORIE")_" text="_$extract(pz.%Get("TEXT"),1,45)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k #12843 bodu 6: číselník atributů MHD - jaký `SpecZnak` nese který popis a co z něj
/// udělá `ConvertSpecialniZnak` (kód > 31 se překlápí na znak, tedy na **pozici ve fontu**).
/// Odpovídá na „proč se u ‚prestup na vlak' kreslí kolo". IN: DB, IDFA. Read-only.
ClassMethod MhdAtributyEnums(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set fa = $get(^||ML("IN","IDFA")) set:fa="" fa = IdFa
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd"
		set n = 0, id = ""
		for {
			set id = $order(^[db]ALVA.MHD.ENUMS(fa,"Atribut","DTA",id)) quit:id=""
			set spec = $get(^[db]ALVA.MHD.ENUMS(fa,"Atribut","DTA",id,"SpecZnak"))
			set popis = $get(^[db]ALVA.MHD.ENUMS(fa,"Atribut","DTA",id,"Popis"))
			set nazev = $get(^[db]ALVA.MHD.ENUMS(fa,"Atribut","DTA",id,"Nazev"))
			set n = n + 1
			quit:n>30
			set znak = $classmethod(cls, "ConvertSpecialniZnak", spec)
			set ^||ML("OUT","ATRIBUT",$translate($justify(n,2)," ",0)) =
				"id="_id_" SpecZnak="""_spec_""" → kresli se znak """_znak_"""  popis="_$extract(popis_nazev,1,60)
		}
		set ^||ML("OUT","SOUHRN") = db_" firma="_fa_" atributu="_n
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39`: přesné zadání pro ruční zkoušku období. Pro první stránky vývěsky vypíše časy
/// v 1. sloupci bez období a s ním, plus které zmizí - to je očekávaný výsledek, proti kterému jde
/// v návrháři porovnávat. IN: DB, SKU, LIN, OBDOBI, STRANEK. Read-only.
ClassMethod ZadaniProZkousku(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set obdobi = $get(^||ML("IN","OBDOBI")) set:obdobi="" obdobi = "1.7.-31.8."
		set stranek = +$get(^||ML("IN","STRANEK")) if stranek < 1 set stranek = 3
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set sloupce = """2"",""nadpis"":""Soboty"",""dny"":""6""},{""id"":""3"",""nadpis"":""Nedele a svatky"",""dny"":""7+"""
		set defBez = "{""sloupce"":[{""id"":""1"",""nadpis"":""Pracovni dny"",""dny"":""12345""},{""id"":" _ sloupce _ "}]}"
		set defS = "{""sloupce"":[{""id"":""1"",""nadpis"":""Pracovni dny"",""dny"":""12345"",""obdobi"":""" _ obdobi _ """},{""id"":" _ sloupce _ "}]}"

		do ..CasyStranek(.sys, db, sku, lin, defBez, stranek, .bez)
		do ..CasyStranek(.sys, db, sku, lin, defS, stranek, .s)
		kill ^||ML("OUT")
		set ^||ML("OUT","A_ZADANI") = "linka "_lin_" skupina "_sku_", sloupce Prac/So/Ne, obdobi """_obdobi_""" jen u 1. sloupce"
		for i = 1:1:stranek {
			continue:'$data(bez(i))
			set radek = $get(bez(i,"zast"))
			set ^||ML("OUT","B_STRANKA"_i) = radek
			set ^||ML("OUT","C_STRANKA"_i_"_BEZ") = "  bez obdobi: "_$get(bez(i,"casy"))
			set ^||ML("OUT","D_STRANKA"_i_"_S") = "  s obdobim: "_$get(s(i,"casy"))
			// co zmizelo - jako multimnožina (tentýž čas může mít víc variant trasy)
			kill zbyva
			set casyS = $get(s(i,"casy"))
			for ci = 1:1:$length(casyS,",") {
				set c = $zstrip($piece(casyS,",",ci),"<>W")
				continue:c=""
				set zbyva(c) = $get(zbyva(c)) + 1
			}
			set zmizelo = "", pocet = 0, casy = $get(bez(i,"casy"))
			for ci = 1:1:$length(casy,",") {
				set c = $zstrip($piece(casy,",",ci),"<>W")
				continue:c=""
				if $get(zbyva(c)) > 0 { set zbyva(c) = zbyva(c) - 1  continue }
				set pocet = pocet + 1
				set zmizelo = zmizelo_$select(zmizelo="":"", 1:", ")_c
			}
			set ^||ML("OUT","E_STRANKA"_i_"_ZMIZI") = "  ZMIZI "_pocet_"x: "_$select(zmizelo="":"(nic)", 1:zmizelo)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Časy v 1. sloupci na prvních N stránkách vývěsky. Pomocná pro ZadaniProZkousku.
ClassMethod CasyStranek(ByRef sys As %String, db As %String, sku As %String, lin As %String, definice As %String, stranek As %Integer, ByRef out)
{
	kill out
	set sys("U","DATA") = db
	kill ^||ML("IN")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	set ^||ML("IN","DEFINICE_SLOUPCU") = definice
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set klic = "", i = 0
	for {
		set klic = $order(^||ML("OUT",klic)) quit:klic=""
		continue:'$isvalidnum(klic)
		set i = i + 1
		quit:i>stranek
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT",klic))
		set out(i) = 1
		set out(i,"zast") = "zastavka """_page.values.%Get("POPIS_ZASTAVKA")_""" smer "_page.values.%Get("KONECNA_ZASTAVKA")
		set casy = "", it = page.rows.%Get("ODJEZDY").%GetIterator()
		while it.%GetNext(.k, .o) {
			continue:o.%Get("SKUPINA_DNU_ID")'="1"
			set casy = casy_$select(casy="":"", 1:", ")_o.%Get("HODINA")_":"_$translate($justify(o.%Get("MINUTA"),2)," ",0)
		}
		set out(i,"casy") = $select(casy="":"(zadny)", 1:casy)
	}
	kill ^||ML("OUT")
}

/// Sonda k `B39`: která linka má nejvíc spojů, které v zadaném období vůbec nejedou (= na
/// prázdninové vývěsce zmizí). Slouží k výběru linky pro ruční vyzkoušení, aby byl rozdíl vidět.
/// IN: DB, SKU, OD, DO, MAXLIN. Read-only.
ClassMethod NejlepsiLinkaProObdobi(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set datumOd = $get(^||ML("IN","OD")) set:datumOd="" datumOd = "1.7.2025"
		set datumDo = $get(^||ML("IN","DO")) set:datumDo="" datumDo = "31.8.2025"
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 250
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))
		if sku = "" set sku = $order(^[db]JRL(""))

		set sys("U","DATA") = db
		set hrlOd = ##class(ALVA.GENERALAPI.Date).DateToHor(datumOd)
		set hrlDo = ##class(ALVA.GENERALAPI.Date).DateToHor(datumDo)
		set linek = 0, lin = ""
		for {
			set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
			quit:linek>=maxLin
			continue:'$data(^[db]JRL(sku,lin,"I"))
			set linek = linek + 1
			kill ^||SondaJede, arr
			set arr("PAR","horlogOdDo") = $listbuild(hrlOd, hrlDo)
			set arr("PAR","platnostLin") = 1
			set arr("GLB") = "^||SondaJede"
			do ##class(ALVA.ISY.ISYLinky.API.LinkyStatistika).jede(.sys, $listbuild(sku,"",""), $name(^[db]JRL(sku,lin)), .arr)
			set nejede = 0, jede = 0, smerId = ""
			for {
				set smerId = $order(^||SondaJede("DTA",smerId)) quit:smerId=""
				set spojId = ""
				for {
					set spojId = $order(^||SondaJede("DTA",smerId,spojId)) quit:spojId=""
					if +$listget($get(^||SondaJede("DTA",smerId,spojId)),1) { set jede = jede + 1 } else { set nejede = nejede + 1 }
				}
			}
			continue:nejede<5
			continue:jede<5
			set poradi(nejede, lin) = "jede="_jede_" nejede="_nejede
		}
		kill ^||SondaJede
		set n = 0, klic = ""
		for {
			set klic = $order(poradi(klic), -1) quit:klic=""
			set lin = ""
			for {
				set lin = $order(poradi(klic, lin)) quit:lin=""
				set n = n + 1
				quit:n>8
				set ^||ML("OUT","LINKA",n) = "lin="_lin_" "_poradi(klic,lin)
			}
			quit:n>8
		}
		set ^||ML("OUT","SOUHRN") = db_" skupina="_sku_" obdobi="_datumOd_"-"_datumDo_" prosle linek="_linek
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39`: najde linky vhodné na ruční vyzkoušení období - mají prázdninové rozvržení
/// (dvě pracovní skupiny), vyplněnou NZ konfiguraci a dost spojů, aby byl rozdíl vidět.
/// IN: DB, MAXLIN. Read-only.
ClassMethod LinkyProZkousku(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 20000
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, nalez = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek>=maxLin
				quit:nalez>=8
				set linek = linek + 1
				set ftl = +$get(^[db]JRL(sku,lin,"PATCH","MHD_TYP_R"))
				continue:(ftl'=1)&&(ftl'=3)
				set nz = $get(^[db]JRL(sku,lin,"PATCH","MHD_NZ1_NZ2"))
				continue:$translate(nz,"~")=""
				// kolik spojů má datumové omezení (bez nich není co obdobím filtrovat)
				set spoju = 0, sOmez = 0
				for smerIdx = "T","Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						for omzId = 11,12,13,14,15,22 { if $get(^[db]JRL(sku,lin,smerIdx,sp,omzId)) '= "" set sOmez = sOmez + 1 quit }
					}
				}
				continue:sOmez<3
				set nalez = nalez + 1
				set ^||ML("OUT","LINKA",nalez) = "sku="_sku_" lin="_lin_" rozvrzeni="_ftl_" NZ="_nz_
					" spoju="_spoju_" s datumovym omezenim="_sOmez_" platnost="_$piece($get(^[db]JRL(sku,lin,"PATCH","Plati")),":",1)
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" proslo linek="_linek_" nalezeno="_nalez
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39`: porovná vývěsku **přes všechny stránky** s obdobím u 1. sloupce a bez něj.
/// Odpovídá na „proč vidím upozornění, ale na stránce se nic nezměnilo" - stránka je jedna
/// zastávka × směr, upozornění agreguje celou linku. IN: DB, SKU, LIN, OBDOBI. Read-only.
ClassMethod ObdobiPresStranky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set obdobi = $get(^||ML("IN","OBDOBI"))
		if obdobi = "" set obdobi = "1.7.-31.8."
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		// výchozí vzor Prac / So / Ne+svátky, období jen u 1. sloupce
		set sloupce = """1"",""nadpis"":""Pracovni dny"",""dny"":""12345"""
		set defBez = "{""sloupce"":[{""id"":" _ sloupce _ "}," _
			"{""id"":""2"",""nadpis"":""Soboty"",""dny"":""6""}," _
			"{""id"":""3"",""nadpis"":""Nedele a svatky"",""dny"":""7+""}]}"
		set defS = "{""sloupce"":[{""id"":" _ sloupce _ ",""obdobi"":""" _ obdobi _ """}," _
			"{""id"":""2"",""nadpis"":""Soboty"",""dny"":""6""}," _
			"{""id"":""3"",""nadpis"":""Nedele a svatky"",""dny"":""7+""}]}"

		set t1 = $zhorolog
		do ..SecistVyvesku(.sys, db, sku, lin, defBez, .bez)
		set t2 = $zhorolog
		do ..SecistVyvesku(.sys, db, sku, lin, defS, .s)
		set t3 = $zhorolog
		kill ^||ML("OUT")
		set ^||ML("OUT","A_OBDOBI") = obdobi_" (jen u 1. sloupce, vzor Prac/So/Ne)"
		set ^||ML("OUT","A_CAS") = "GetData bez obdobi="_$fnumber(t2-t1,"",3)_" s · s obdobim="_$fnumber(t3-t2,"",3)_" s"
		set ^||ML("OUT","B_BEZ_OBDOBI") = "stránek="_$get(bez("stranek"))_" odjezdů celkem="_$get(bez("odj"))_
			" · sloupec1="_(+$get(bez("sl",1)))_" sloupec2="_(+$get(bez("sl",2)))_" sloupec3="_(+$get(bez("sl",3)))
		set ^||ML("OUT","C_S_OBDOBIM") = "stránek="_$get(s("stranek"))_" odjezdů celkem="_$get(s("odj"))_
			" · sloupec1="_(+$get(s("sl",1)))_" sloupec2="_(+$get(s("sl",2)))_" sloupec3="_(+$get(s("sl",3)))
		set ^||ML("OUT","D_ROZDIL") = "odjezdů ubylo="_($get(bez("odj")) - $get(s("odj")))_
			" · stránek s upozorněním="_(+$get(s("varovani")))_" · nezařazených hlášení="_(+$get(s("nezarazene")))
		set c = "", ruznychSpoju = 0, seznam = ""
		for { set c = $order(s("spoj", c)) quit:c=""  set ruznychSpoju = ruznychSpoju + 1  set seznam = seznam_$select(seznam="":"", 1:", ")_c }
		set ^||ML("OUT","E_RUZNYCH_SPOJU") = "různých nezařazených spojů = "_ruznychSpoju_" (čísla: "_$extract(seznam,1,60)_")"
		set ^||ML("OUT","F_PRVNI_VAROVANI") = $get(s("prvni"))
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sečte odjezdy vývěsky přes všechny stránky. Pomocná pro ObdobiPresStranky.
ClassMethod SecistVyvesku(ByRef sys As %String, db As %String, sku As %String, lin As %String, definice As %String, ByRef souhrn)
{
	kill souhrn
	set sys("U","DATA") = db
	kill ^||ML("IN")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	set ^||ML("IN","DEFINICE_SLOUPCU") = definice
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set klic = "", souhrn("stranek") = 0, souhrn("odj") = 0
	for {
		set klic = $order(^||ML("OUT",klic)) quit:klic=""
		continue:'$isvalidnum(klic)
		set souhrn("stranek") = souhrn("stranek") + 1
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT",klic))
		set nez = page.values.%Get("NEZARAZENE_SPOJE")
		set cisla = page.values.%Get("NEZARAZENE_SPOJE_CISLA")
		for ci = 1:1:$length(cisla,",") { set c = $piece(cisla,",",ci)  if c '= "" set souhrn("spoj", c) = "" }
		if nez '= "" {
			set souhrn("varovani") = $get(souhrn("varovani")) + 1
			set souhrn("nezarazene") = $get(souhrn("nezarazene")) + $length(nez,",")
			if $get(souhrn("prvni")) = "" {
				set souhrn("prvni") = "zastávka """_page.values.%Get("POPIS_ZASTAVKA")_""" směr "_page.values.%Get("KONECNA_ZASTAVKA")_": "_$extract(nez,1,80)
			}
		}
		set it = page.rows.%Get("ODJEZDY").%GetIterator()
		while it.%GetNext(.k, .o) {
			set souhrn("odj") = souhrn("odj") + 1
			set idS = o.%Get("SKUPINA_DNU_ID")
			set souhrn("sl",idS) = $get(souhrn("sl",idS)) + 1
		}
	}
	kill ^||ML("OUT")
}

/// Sonda k `B39`: týdenní platnosti spojů jedné linky + do kolika sloupců dnes spoj spadne.
/// Hledá spoje, které patří do víc sloupců naráz - tam se dnešní „jedna sada značek na spoj"
/// rozchází s per-sloupcovým počítáním. IN: DB, SKU, LIN. Read-only.
ClassMethod PlkLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
		set ftl = +$get(^[db]JRL(sku,lin,"PATCH","MHD_TYP_R"))
		set ^||ML("OUT","A_FTL") = "rozvržení linky (FTL) = "_ftl
		set kolizi = 0
		for smerIdx = "T","Z" {
			set sp = ""
			for {
				set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
				continue:'$isvalidnum(sp)
				set plk = $piece($piece($get(^[db]JRL(sku,lin,smerIdx,sp)),"#",1),":",7)
				set klicPlk = $select(plk="":"(prázdné)", 1:plk)
				set pocty(klicPlk) = $get(pocty(klicPlk)) + 1
				// spadne do víc sloupců a legacy by mu do všech dalo tutéž sadu značek?
				set skupiny = $classmethod(cls, "ZarazeniSpoje", ftl, plk, "", "")
				set legacy = $classmethod(cls, "DenniZnacky", ftl, plk)
				if ($length(skupiny,",") > 1) && (legacy '= "") {
					set kolizi = kolizi + 1
					if kolizi <= 8 set ^||ML("OUT","C_KOLIZE",kolizi) = "spoj "_sp_" ("_smerIdx_") platnost="""_plk_""" → sloupce "_skupiny_", legacy značky """_legacy_""""
				}
			}
		}
		set plk = "", radek = ""
		for { set plk = $order(pocty(plk)) quit:plk=""  set radek = radek_""""_plk_"""x"_pocty(plk)_" " }
		set ^||ML("OUT","B_PLATNOSTI") = radek
		set ^||ML("OUT","D_SOUHRN") = "spojů ve víc sloupcích se značkou = "_kolizi
		// sdílí oba směry číslování spojů? (klient podle čísel počítá spoje v upozornění)
		set spolecnych = 0, sp = ""
		for {
			set sp = $order(^[db]JRL(sku,lin,"T",sp)) quit:sp=""
			continue:'$isvalidnum(sp)
			if $data(^[db]JRL(sku,lin,"Z",sp)) set spolecnych = spolecnych + 1
		}
		set ^||ML("OUT","E_CISLA_V_OBOU_SMERECH") = "cisel spoju existujicich v obou smerech = "_spolecnych
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39`: nese se „spoj o svátku nejede" týdenní platností, nebo datumovým omezením
/// (= negativní značkou)? Rozhoduje, jestli tu informaci má vysvětlovat denní značka, nebo jestli
/// ji zákazníkovi řekne už NZ. IN: DB, MAXLIN. Read-only.
ClassMethod ScanSvatky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 3000
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, spoju = 0, sMarkerem = 0, sku = ""
		kill kat
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for smerIdx = "T","Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						set veta = $get(^[db]JRL(sku,lin,smerIdx,sp))
						set plk = $piece($piece(veta,"#",1),":",7)
						set cnz = $piece(veta,"#",7)
						// má spoj datumové omezení (to, co nese negativní značku a text do legendy)?
						set maOmez = 0
						for omzId = 11,12,13,14,15,22 { if $get(^[db]JRL(sku,lin,smerIdx,sp,omzId)) '= "" set maOmez = 1 }
						if maOmez set sMarkerem = sMarkerem + 1
						// zajímají nás spoje, které jedou v neděli, ale NE ve svátek
						continue:'(plk["7")
						continue:plk["+"
						set klic = $select(maOmez&&(cnz'=""):"1 s omezením i číslem značky",
							maOmez:"2 s omezením, ale bez čísla značky",
							cnz'="":"3 jen číslo značky, bez omezení",
							1:"4 BEZ čehokoli - nic to nevysvětlí")
						set kat(klic) = $get(kat(klic)) + 1
					}
				}
			}
		}
		set klic = "", celkem = 0
		for { set klic = $order(kat(klic)) quit:klic=""  set celkem = celkem + kat(klic) }
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" spojů="_spoju_" · s datumovým omezením="_sMarkerem
		set ^||ML("OUT","A_CELKEM") = "spojů ""jede v neděli, ale ne ve svátek""="_celkem
		set klic = ""
		for { set klic = $order(kat(klic)) quit:klic=""  set ^||ML("OUT","B_"_$piece(klic," ",1)) = klic_" = "_kat(klic) }
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39` etapě 2: kolika spojům přibude denní značka po sjednocení pravidla „značka jen
/// když spoj nepokrývá celý sloupec". Rozlišuje oddělený víkendový sloupec („Neděle a svátky",
/// vzory 0/3) a slitý („So, Ne a svátky", vzory 1/2) - legacy tam značky netiskne v různém rozsahu.
/// IN: DB, MAXLIN. Read-only.
ClassMethod ScanNoveZnacky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 3000
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
		set linek = 0, spoju = 0, novaNedele = 0, novaSlity = 0, novaPrac = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for smerIdx = "T","Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						set plk = $piece($piece($get(^[db]JRL(sku,lin,smerIdx,sp)), "#", 1), ":", 7)
						set dny = $classmethod(cls, "DnySpoje", plk)
						// oddělený víkendový sloupec „7+" (vzor Prac / So / Ne+svátky)
						set noveNe = $classmethod(cls, "DenniZnackyProSloupec", dny, "7+")
						set legacyNe = ""   // legacy FTL0 víkendové značky netiskne vůbec
						if (noveNe '= legacyNe) && $classmethod(cls, "SedneDoSloupceDny", dny, "7+") set novaNedele = novaNedele + 1
						do ..Smer(.smery, "NEDELE", legacyNe, noveNe, $classmethod(cls, "SedneDoSloupceDny", dny, "7+"))
						// slitý víkendový sloupec „67+" (vzor Prac / víkend)
						set noveSl = $classmethod(cls, "DenniZnackyProSloupec", dny, "67+")
						set legacySl = $classmethod(cls, "DenniZnacky", 2, plk)
						set legacySl = $translate(legacySl, "12345")   // ve víkendovém sloupci jde jen o 6/7/+
						if (noveSl '= legacySl) && $classmethod(cls, "SedneDoSloupceDny", dny, "67+") set novaSlity = novaSlity + 1
						do ..Smer(.smery, "SLITY", legacySl, noveSl, $classmethod(cls, "SedneDoSloupceDny", dny, "67+"))
						// pracovní sloupec „12345"
						set novePr = $classmethod(cls, "DenniZnackyProSloupec", dny, "12345")
						set legacyPr = $translate($classmethod(cls, "DenniZnacky", 0, plk), "67+")
						if (novePr '= legacyPr) && $classmethod(cls, "SedneDoSloupceDny", dny, "12345") set novaPrac = novaPrac + 1
						do ..Smer(.smery, "PRACOVNI", legacyPr, novePr, $classmethod(cls, "SedneDoSloupceDny", dny, "12345"))
						// jmenovatel: kolik spojů do sloupce vůbec spadne (jak hustá ta změna bude)
						if $classmethod(cls, "SedneDoSloupceDny", dny, "7+") set smery("NEDELE","vsech") = $get(smery("NEDELE","vsech")) + 1
						if $classmethod(cls, "SedneDoSloupceDny", dny, "67+") set smery("SLITY","vsech") = $get(smery("SLITY","vsech")) + 1
						if $classmethod(cls, "SedneDoSloupceDny", dny, "12345") set smery("PRACOVNI","vsech") = $get(smery("PRACOVNI","vsech")) + 1
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" spojů="_spoju
		set ^||ML("OUT","A_NEDELE_SVATKY") = "sloupec ""Neděle a svátky"" (vzor 0/3): jinak než dnes u "_novaNedele_" spojů"
		set ^||ML("OUT","B_SLITY_VIKEND") = "sloupec ""So, Ne a svátky"" (vzor 1/2): jinak než dnes u "_novaSlity_" spojů"
		set ^||ML("OUT","C_PRACOVNI") = "sloupec ""Pracovní dny"": jinak než dnes u "_novaPrac_" spojů"
		for typ = "NEDELE", "SLITY", "PRACOVNI" {
			set vsech = +$get(smery(typ,"vsech"))
			set pribude = +$get(smery(typ,"pribude"))
			set podil = $select(vsech:$fnumber(pribude / vsech * 100, "", 1), 1:0)
			set ^||ML("OUT","D_SMER_"_typ) = "ve sloupci je "_vsech_" spojů · značka PŘIBUDE u "_pribude_" ("_podil_" %)"_
				" · ZMIZÍ u "_(+$get(smery(typ,"zmizi")))_" · jiná sada u "_(+$get(smery(typ,"jina")))
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Přibude značka, zmizí, nebo se jen změní sada? (Pomocná pro ScanNoveZnacky.)
ClassMethod Smer(ByRef smery, typ As %String, legacy As %String, nove As %String, sedne As %Boolean)
{
	if 'sedne quit
	if legacy = nove quit
	if legacy = "" { set smery(typ,"pribude") = $get(smery(typ,"pribude")) + 1 quit }
	if nove = "" { set smery(typ,"zmizi") = $get(smery(typ,"zmizi")) + 1 quit }
	set smery(typ,"jina") = $get(smery(typ,"jina")) + 1
}

/// Sonda k `B39` etapě 2: co vyjde z GetData, když má sloupec zadané období. Postaví definici
/// se dvěma sloupci (období vs. bez podmínky), zavolá GetData a shrne, kolik odjezdů kam spadlo
/// a jaké značky u nich jsou. IN: DB, SKU, LIN, OBDOBI. Read-only, po vytěžení smazat.
ClassMethod DefiniceObdobiSonda(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set obdobi = $get(^||ML("IN","OBDOBI"))
		if obdobi = "" set obdobi = "1.7.-31.8."
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set def = "{""sloupce"":[" _
			"{""id"":""1"",""nadpis"":""Obdobi"",""dny"":"""",""obdobi"":""" _ obdobi _ """}," _
			"{""id"":""2"",""nadpis"":""Bez podminky"",""dny"":"""",""obdobi"":""""}]}"
		set ^||ML("OUT","A_DEFINICE") = def

		set sys("U","DATA") = db
		kill ^||ML("IN")
		set ^||ML("IN","SKUPINA_LINEK_ID") = sku
		set ^||ML("IN","LINKA_ID") = lin
		set ^||ML("IN","DEFINICE_SLOUPCU") = def
		set zacatek = $zhorolog
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
		set trvalo = $zhorolog - zacatek

		set stranek = 0, klic = "", prvni = ""
		for {
			set klic = $order(^||ML("OUT",klic)) quit:klic=""
			continue:klic="ERR"
			continue:'$isvalidnum(klic)
			set stranek = stranek + 1
			if prvni = "" set prvni = $get(^||ML("OUT",klic))
		}
		set chyba = $get(^||ML("OUT","ERR"))
		kill ^||ML("OUT")
		set ^||ML("OUT","A_DEFINICE") = def
		set ^||ML("OUT","B_TRVANI") = $fnumber(trvalo,"",3)_" s, stránek="_stranek
		if chyba '= "" set ^||ML("OUT","C_CHYBA") = chyba
		if prvni = "" ret $$$OK

		set page = ##class(%DynamicObject).%FromJSON(prvni)
		set ^||ML("OUT","D_NEZARAZENE") = page.values.%Get("NEZARAZENE_SPOJE")
		set ^||ML("OUT","E_MIMO_PLATNOST") = page.values.%Get("OBDOBI_MIMO_PLATNOST")
		set n = 0, it = page.rows.%Get("ODJEZDY").%GetIterator()
		while it.%GetNext(.k, .o) {
			set idSloupce = o.%Get("SKUPINA_DNU_ID")
			set pocty(idSloupce) = $get(pocty(idSloupce)) + 1
			set znaky = ""
			set ita = o.%Get("ANOTACE").%GetIterator()
			while ita.%GetNext(.ak, .a) { set znaky = znaky _ a.%Get("ZNAK") }
			if znaky '= "" {
				set n = n + 1
				if n <= 12 set ^||ML("OUT","G_ZNACKY",n) = "sloupec="_idSloupce_" "_o.%Get("HODINA")_":"_o.%Get("MINUTA")_" znaky="_znaky
			}
		}
		set idSloupce = "", radek = ""
		for { set idSloupce = $order(pocty(idSloupce)) quit:idSloupce=""  set radek = radek_"sloupec "_idSloupce_"="_pocty(idSloupce)_" odjezdů  " }
		set ^||ML("OUT","F_ODJEZDY") = radek
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39` etapě 2: jaké tvary týdenní platnosti (`plk`, hlava spoje pole 7) data reálně
/// nesou. Rozhoduje, jestli jde denní značky sjednotit pravidlem „spoj pokrývá celý sloupec →
/// značka se netiskne" - u zkratky `X` a prázdné platnosti vyjde stejně jako legacy, u explicitně
/// vypsaného `12345` by se výsledek lišil. IN: DB, MAXLIN. Read-only.
ClassMethod ScanPlk(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 2000
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, spoju = 0, plnyTyden = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku)) quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for smerIdx = "T","Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						set plk = $piece($piece($get(^[db]JRL(sku,lin,smerIdx,sp)), "#", 1), ":", 7)
						set klic = $select(plk="":"(prázdné)", 1:plk)
						set tvary(klic) = $get(tvary(klic)) + 1
						// riziko sjednocení: všech pět pracovních dnů vypsaných jednotlivě
						if (plk["1")&&(plk["2")&&(plk["3")&&(plk["4")&&(plk["5") set plnyTyden = plnyTyden + 1
					}
				}
			}
		}
		set tvar = "", n = 0
		for {
			set tvar = $order(tvary(tvar)) quit:tvar=""
			set n = n + 1
			if n <= 25 set ^||ML("OUT","TVAR",$translate($justify(n,3)," ","0")) = """"_tvar_""" x"_tvary(tvar)
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" spojů="_spoju_" různých tvarů="_n
		set ^||ML("OUT","RIZIKO") = "spojů s vypsanými všemi pracovními dny (1..5 zvlášť)="_plnyTyden
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sonda k `B39` etapě 2 (období): co reálně vrátí `ALVA.ISY.ISYLinky.API.LinkyStatistika.jede`
/// nad linkou AD - je třída nasazená, jak dlouho počítá, jaké má klíče (`smerId` 0/1 vs. naše T/Z)
/// a sedí seznam spojů na `^JRL`. IN: DB, SKU, LIN, OD, DO (dd.mm.rrrr). Read-only, po vytěžení smazat.
ClassMethod JedeSonda(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set datumOd = $get(^||ML("IN","OD"))
		set datumDo = $get(^||ML("IN","DO"))
		kill ^||ML("OUT"), ^||SondaJede
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))
		if datumOd = "" set datumOd = "1.7.2026"
		if datumDo = "" set datumDo = "31.8.2026"

		set jeTrida = ##class(%Dictionary.CompiledClass).%ExistsId("ALVA.ISY.ISYLinky.API.LinkyStatistika")
		set ^||ML("OUT","A_TRIDA") = $select(jeTrida:"LinkyStatistika je nasazená", 1:"LinkyStatistika NA SERVERU NENÍ")
		if 'jeTrida ret $$$OK
		if '$data(^[db]JRL(sku,lin,"I")) {
			set ^||ML("OUT","A_DATA") = "linka "_lin_" v "_db_" neexistuje"
			ret $$$OK
		}

		set hrlOd = ##class(ALVA.GENERALAPI.Date).DateToHor(datumOd)
		set hrlDo = ##class(ALVA.GENERALAPI.Date).DateToHor(datumDo)
		set ^||ML("OUT","B_OBDOBI") = datumOd_" - "_datumDo_" (dní "_(hrlDo-hrlOd+1)_")"

		// data linky předáváme globálem - linka je v našich klíčích už v indexovém tvaru,
		// přes `id` by ji `cisloLinkyDoIndexu` převedla podruhé
		set sys("U","DATA") = db
		kill arrLin
		do ##class(ALVA.ISY.ISYLinky.API.Linky).nacti(.sys, $listbuild(sku,"",""), $name(^[db]JRL(sku,lin)), .arrLin)
		set platiOd = $listget($listget($get(arrLin("DTA")),7),1)
		set platiDo = $listget($listget($get(arrLin("DTA")),7),2)
		set ^||ML("OUT","B_PLATNOST_LINKY") = "od "_$select(platiOd="":"-", 1:##class(ALVA.GENERALAPI.Date).horToDate(platiOd))_" do "_$select(platiDo="":"-", 1:##class(ALVA.GENERALAPI.Date).horToDate(platiDo))

		kill arr
		set arr("PAR","horlogOdDo") = $listbuild(hrlOd, hrlDo)
		set arr("PAR","platnostLin") = +$get(^||ML("IN","PLATNOSTLIN"))
		set arr("GLB") = "^||SondaJede"
		set zacatek = $zhorolog
		do ##class(ALVA.ISY.ISYLinky.API.LinkyStatistika).jede(.sys, $listbuild(sku,"",""), $name(^[db]JRL(sku,lin)), .arr)
		set ^||ML("OUT","C_TRVANI") = $fnumber($zhorolog - zacatek, "", 3)_" s (platnostLin="_(+$get(^||ML("IN","PLATNOSTLIN")))_")"

		// tvar výstupu + ukázky bitmap
		set pocet = 0, ukazek = 0, smerId = ""
		for {
			set smerId = $order(^||SondaJede("DTA",smerId)) quit:smerId=""
			set spojId = ""
			for {
				set spojId = $order(^||SondaJede("DTA",smerId,spojId)) quit:spojId=""
				set pocet = pocet + 1
				set val = $get(^||SondaJede("DTA",smerId,spojId))
				set dnu = $listget(val,1), bitmapa = $listget(val,3)
				set pocty(dnu) = $get(pocty(dnu)) + 1
				if ukazek < 8 {
					set ukazek = ukazek + 1
					set ^||ML("OUT","E_UKAZKA",ukazek) = "smerId="_smerId_" spoj="_spojId_" jede dní="_dnu_" bitmapa("_$length(bitmapa)_")="_$extract(bitmapa,1,45)
				}
			}
		}
		set ^||ML("OUT","D_POCET") = "vrácených spojů="_pocet

		// sedí klíče na naše ^JRL? (jede vrací smerId 0/1, my čteme T/Z)
		set nasich = 0, chybi = 0
		for smerIdx = "T","Z" {
			set smerId = $select(smerIdx="T":0, 1:1)
			set sp = ""
			for {
				set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
				continue:'$isvalidnum(sp)
				set nasich = nasich + 1
				if '$data(^||SondaJede("DTA",smerId,sp)) {
					set chybi = chybi + 1
					if chybi <= 5 set ^||ML("OUT","G_CHYBI",chybi) = "v odpovědi není smer="_smerIdx_" spoj="_sp
				}
			}
		}
		set ^||ML("OUT","F_KLICE") = "spojů v ^JRL="_nasich_" bez odpovědi="_chybi

		// rozložení: kolik spojů v období nejede vůbec / jede každý den / jede jen část
		set dnu = "", nula = 0, plny = 0, castecne = 0, celkemDnu = hrlDo - hrlOd + 1
		for {
			set dnu = $order(pocty(dnu)) quit:dnu=""
			if dnu = 0 { set nula = nula + pocty(dnu) }
			elseif dnu = celkemDnu { set plny = plny + pocty(dnu) }
			else { set castecne = castecne + pocty(dnu) }
		}
		set ^||ML("OUT","H_ROZLOZENI") = "v období nejede vůbec="_nula_" jede každý den="_plny_" jede jen část="_castecne

		// kolik spojů linky má vůbec datumové omezení (kvůli relevanci filtru obdobím)
		set somez = 0
		for smerIdx = "T","Z" {
			set sp = ""
			for {
				set sp = $order(^[db]JRL(sku,lin,smerIdx,sp)) quit:sp=""
				continue:'$isvalidnum(sp)
				set maOmez = 0
				for omzId = 11,12,13,14,15,22 { if $get(^[db]JRL(sku,lin,smerIdx,sp,omzId)) '= "" set maOmez = 1 }
				set somez = somez + maOmez
			}
		}
		set ^||ML("OUT","I_OMEZENI") = "spojů s datumovým omezením="_somez
		kill ^||SondaJede
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Rozpad J-věty trasy linky na `#`-segmenty. IN: DB, SKU, LIN, MAX (default 8 zastávek).
/// OUT: per zastávka jeden řádek "poradi | seg1 | seg2 | ..." pro ruční porovnání s editorem trasy.
ClassMethod DumpJVeta(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set max = +$get(^||ML("IN","MAX")) if max < 1 set max = 8
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set poradi = "", pocet = 0
		for {
			set poradi = $order(^[db]JRL(sku, lin, "J", poradi))
			quit:poradi=""
			quit:pocet>=max
			set veta = $get(^[db]JRL(sku, lin, "J", poradi))
			continue:veta=""
			set pocet = pocet + 1
			set radek = "poradi=" _ poradi
			for i = 1:1:$length(veta, "#") {
				set seg = $piece(veta, "#", i)
				continue:seg=""
				set radek = radek _ "  [" _ i _ "]" _ seg
			}
			set ^||ML("OUT", "J", $translate($justify(pocet, 3), " ", "0")) = radek
		}
		set ^||ML("OUT", "SOUHRN") = db _ " sku=" _ sku _ " lin=" _ lin _ " zastavek=" _ pocet
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kolik linek má v trase atribut „čára pod řádkem" (`p`) nebo „čára nad i pod" (`o`) - podklad
/// k `B172`. Čte jen trasu (`^JRL(sku,lin,"J",poradi)`), žádný tisk se nespouští.
///
/// IN: `LIMIT` (kolik linek projít, 0 = všechny), `MAXOUT` (kolik příkladů vypsat, výchozí 10).
/// OUT: `souhrn` a `linka~<n>` = `sku/lin  p=<počet zastávek>, o=<počet zastávek>`.
ClassMethod KdeJeCaraUZastavky(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s limit = +$g(^||ML("IN","LIMIT")), maxOut = +$g(^||ML("IN","MAXOUT"))
		s:maxOut<1 maxOut = 10

		s projito = 0, sCarou = 0, zastavekP = 0, zastavekO = 0, vypsano = 0
		s s1 = ""
		f {
			s s1 = $o(^[dataDB]JRL(s1))
			q:s1=""
			continue:'$isvalidnum(s1)
			s l1 = ""
			f {
				s l1 = $o(^[dataDB]JRL(s1, l1))
				q:l1=""
				continue:'$d(^[dataDB]JRL(s1, l1, "I"))
				s projito = projito + 1
				i limit, projito > limit q
				s pocetP = 0, pocetO = 0, poradi = ""
				f {
					s poradi = $o(^[dataDB]JRL(s1, l1, "J", poradi))
					q:poradi=""
					continue:'$isvalidnum(poradi)
					s atr = $p($p($g(^[dataDB]JRL(s1, l1, "J", poradi)), ":", 2), "#", 1)
					i atr [ "p" s pocetP = pocetP + 1
					i atr [ "o" s pocetO = pocetO + 1
				}
				continue:'(pocetP + pocetO)
				s sCarou = sCarou + 1
				s zastavekP = zastavekP + pocetP, zastavekO = zastavekO + pocetO
				i vypsano < maxOut {
					s vypsano = vypsano + 1
					s ^||ML("OUT", "linka~"_vypsano) = s1_"/"_l1_"  p="_pocetP_", o="_pocetO
				}
			}
			i limit, projito > limit q
		}
		s ^||ML("OUT", "souhrn") = "projito linek="_projito_", s carou="_sCarou
			_", zastavek p="_zastavekP_", zastavek o="_zastavekO
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// U které zastávky konkrétní linky je čára (`p` / `o`) - podklad k porovnání se starým výtiskem.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID`. OUT: `zastavka~<n>` = `poradi. nazev  atributy=<znaky>`.
ClassMethod CaryLinky(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID")), lin = $g(^||ML("IN","LINKA_ID"))
		s:sku="" sku = 1
		s vypsano = 0, poradi = ""
		f {
			s poradi = $o(^[dataDB]JRL(sku, lin, "J", poradi))
			q:poradi=""
			continue:'$isvalidnum(poradi)
			s veta = $g(^[dataDB]JRL(sku, lin, "J", poradi))
			s atr = $p($p(veta, ":", 2), "#", 1)
			continue:'((atr [ "p") || (atr [ "o"))
			s czst = $p(veta, ":", 1)
			s nazev = $p($g(^[dataDB]JRZ(czst)), "$", 2)
			s vypsano = vypsano + 1
			s ^||ML("OUT", "zastavka~"_vypsano) = poradi_". "_nazev_"  atributy="_atr
		}
		s ^||ML("OUT", "souhrn") = sku_"/"_lin_" - zastavek s carou: "_vypsano
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
