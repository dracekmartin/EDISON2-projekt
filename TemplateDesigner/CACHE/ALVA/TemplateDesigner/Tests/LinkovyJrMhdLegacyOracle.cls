Include %occInclude

/// Oracle linkového JŘ MHD: spustí **starý tisk** (`ALVA.MHD.LinkyPrintFormatJR.MakeData`, větev
/// `VolanoZ="MHD"` v `ALVA.TISKJR.Data`) a náš `LinkovyJrMhd.GetData` nad toutéž linkou.
///
/// Zatím porovnává **značky spojů**: číslo spoje, negativní značku, značky a texty v řádcích
/// platnosti a k tomu legendu druhů 1, 2, 4, 5 a značek zastávky. Časy, zastávky a pořadí spojů
/// přijdou s `B191c`.
///
/// Spoje se porovnávají jako multimnožina řádků `směr|číslo|NZ|značky|texty`, ne podle pozice:
/// starý modul řadí spoje jinak (bublinkově po zastávkách, legacy :951-990) a pořadí tu měřit nechceme.
///
/// Vstup starého modulu: všechny typy provozu a oba směry zapnuté (bez `arr,11` nevytiskne nic,
/// legacy :534, :538), legenda za každý směr (`arr,4` = 1, jako náš model stránky na směr)
/// a záměrně obrovská plocha, aby vyšel jeden blok na typ provozu.
Class ALVA.TemplateDesigner.Tests.LinkovyJrMhdLegacyOracle
{

/// Globál, do kterého píše starý modul. Process-private, uklízí se před každým během.
Parameter GLB = "^||TdMhdLinkovyOracle";

ClassMethod OracleDostupny() As %Boolean
{
	quit ##class(%Dictionary.CompiledClass).%ExistsId("ALVA.MHD.LinkyPrintFormatJR")
}

/// Spustí starý tisk nad linkou. 0 = starý modul nebo firma na serveru není, nebo skončil chybou.
ClassMethod SpustLegacy(idfa As %String, db As %String, lin As %String) As %Boolean
{
	quit:'..OracleDostupny() 0
	quit:'##class(%SYS.Namespace).Exists(db) 0
	kill @..#GLB
	kill in, out
	set n = 0, tp = ""
	for  {
		set tp = $order(^[db]ALVA.MHD.ENUMS(idfa,"TypyProvozu","DTA",tp))
		quit:tp=""
		set in("A",2,"arr",11,$increment(n)) = "TypProvozuID#"_tp_"~1"
	}
	set in("A",2,"arr",11,$increment(n)) = "SmerID#0~1"
	set in("A",2,"arr",11,$increment(n)) = "SmerID#1~1"
	set in("A",2,"arr",4) = 1
	set in("A",2,"arr",12) = 0
	set in("A",2,"dta",1) = 1000000
	set in("A",2,"dta",2) = 1000000
	set ok = ##class(ALVA.MHD.LinkyPrintFormatJR).MakeData(idfa, db, lin, ..#GLB, .in, .out)
	quit (ok = 1)
}

/// Id ze starého číselníku znaků → klíč, který posílá naše agenda. Číselník je týž jako u AD
/// (`GSId^JRapiLinPrtWin`); neznámé id zůstane, jak je, ať je rozdíl vidět.
ClassMethod KlicZnaku(id As %String) As %String
{
	set znak = ##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).ZnakZId(id)
	quit $select(znak'="":znak, 1:id)
}

/// Porovnává se tahle položka legendy? Druhy 1, 2, 4, 5 a značky zastávky (druh 7, jen N/V/x/A).
ClassMethod PorovnavanaPoznamka(druh As %String, znak As %String) As %Boolean
{
	quit (",1,2,4,5," [ (","_druh_",")) || ((druh = 7) && (",N,V,x,A," [ (","_znak_",")))
}

/// Starý tisk → leg("spoj", řádek) = počet, leg("pozn", směr|druh|znak|text) = 1.
/// ⚠️ Hodnota musí být neprázdná: `Porovnej` srovnává hodnoty, takže s "" by položka chybějící
/// na jedné straně vypadala jako shoda (tak to 12. 10. vypadalo, než to ukázala KV_DP 1073).
ClassMethod NactiLegacy(idfa As %String, db As %String, lin As %String, ByRef leg) As %Boolean
{
	kill leg
	quit:'..SpustLegacy(idfa, db, lin) 0
	set g = ..#GLB, idobj = ""
	for  {
		set idobj = $order(@g@(idobj))
		quit:idobj=""
		set typ = $get(@g@(idobj,"arr",1)), smer = $get(@g@(idobj,"arr",2))
		if typ = 2 {
			// Blok = jeden typ provozu (legacy :1112-1125). Seznam zastávek bloku: název se značkami
			// a pásmo, řádky v pořadí tisku.
			set (colZst, colZon) = "", col = ""
			for  {
				set col = $order(@g@(idobj,"column",col))
				quit:col=""
				set ct = $get(@g@(idobj,"column",col,"arr",1))
				if ct = 4 set colZst = col
				if ct = 1 set colZon = col
			}
			set zast = "", row = ""
			for  {
				set row = $order(@g@(idobj,"row",row))
				quit:row=""
				continue:$get(@g@(idobj,"row",row,"arr",1))'=2
				set zast = zast_";"_..BunkaLegacy(idobj, row, colZst)_"["_..BunkaLegacy(idobj, row, colZon)_"]"
			}
			set zast = smer_"|"_$extract(zast,2,$length(zast))
			do ..Pridej(.leg, "zast", zast)
			set blok = ""
			set col = ""
			for  {
				set col = $order(@g@(idobj,"column",col))
				quit:col=""
				continue:$get(@g@(idobj,"column",col,"arr",1))'=7
				set blok = blok_","_$get(@g@(idobj,"column",col,"arr",3))
				set casy = "", row = ""
				for  {
					set row = $order(@g@(idobj,"row",row))
					quit:row=""
					continue:$get(@g@(idobj,"row",row,"arr",1))'=2
					set casy = casy_";"_..BunkaLegacy(idobj, row, col)
				}
				set radek = smer_"|"_$get(@g@(idobj,"column",col,"arr",3))_"|"_$extract(casy,2,$length(casy))
				do ..Pridej(.leg, "cas", radek)
				set (nz, znaky, texty) = ""
				set row = ""
				for  {
					set row = $order(@g@(idobj,"row",row))
					quit:row=""
					continue:$get(@g@(idobj,"row",row,"arr",1))'=1
					set i = ""
					for  {
						set i = $order(@g@(idobj,"dta",row,col,i))
						quit:i=""
						set t = $get(@g@(idobj,"dta",row,col,i,"arr",1))
						if t = 3 set nz = $get(@g@(idobj,"dta",row,col,i,"dta",3))
						if t = 2 set znaky = znaky_","_..KlicZnaku($get(@g@(idobj,"dta",row,col,i,"dta",2)))
						if t = 1 {
							set x = $get(@g@(idobj,"dta",row,col,i,"dta",1))
							if x '= "" set texty = texty_","_x
						}
					}
				}
				set radek = smer_"|"_$get(@g@(idobj,"column",col,"arr",3))_"|"_nz_"|"_$extract(znaky,2,$length(znaky))_"|"_$extract(texty,2,$length(texty))
				set leg("spoj",radek) = $get(leg("spoj",radek)) + 1
			}
			set leg("bloky",smer) = $get(leg("bloky",smer))_"|"_$extract(blok,2,$length(blok))
		}
		if typ = 3 {
			set i = ""
			for  {
				set i = $order(@g@(idobj,"dta",i))
				quit:i=""
				set druh = $get(@g@(idobj,"dta",i,"arr",1))
				set t = $get(@g@(idobj,"dta",i,"arr",2))
				set znak = $get(@g@(idobj,"dta",i,"dta",t))
				if t = 2 set znak = ..KlicZnaku(znak)
				// Vědomá odchylka: značky zastávky jsou u nás stálé značky (druh 7) jako u AD,
				// starý MHD tisk je řadí k atributům (druh 5, legacy :1366). Druh řídí skupinu legendy.
				if (druh = 5) && (",N,V,x,A," [ (","_znak_",")) set druh = 7
				continue:'..PorovnavanaPoznamka(druh, znak)
				do ..Pridej(.leg, "pozn", smer_"|"_druh_"|"_znak_"|"_$get(@g@(idobj,"dta",i,"txt",1)), 1)
			}
		}
	}
	kill @g
	quit 1
}

/// arr(kat, klíč) = počet výskytů (`mnozina` = jen 1), arr(kat, klíč, "t") = celý text pro výpis.
/// ⚠️ Dlouhý text se do klíče nevejde (limit 511 bajtů - seznam 40 zastávek, časy dlouhé linky),
/// proto klíč = začátek textu a otisk celku.
ClassMethod Pridej(ByRef arr, kat As %String, text As %String, mnozina As %Boolean = 0, kolik As %Integer = 1)
{
	set klic = text
	if $length(text) > 150 set klic = $extract(text,1,100)_"#"_$system.Encryption.Base64Encode($system.Encryption.MD5Hash($zconvert(text,"O","UTF8")))
	if mnozina { set arr(kat,klic) = 1 }
	else { set arr(kat,klic) = $get(arr(kat,klic)) + kolik }
	set arr(kat,klic,"t") = text
}

/// Obsah buňky starého tisku ve tvaru, ve kterém ho skládá `NactiNase`: text, značky ve složených
/// závorkách (`{|}` projíždí, `{x}` na znamení), čas. Prázdný sloupec ("") = "".
ClassMethod BunkaLegacy(idobj As %String, row As %String, col As %String) As %String
{
	quit:col="" ""
	set g = ..#GLB, out = "", i = ""
	for  {
		set i = $order(@g@(idobj,"dta",row,col,i))
		quit:i=""
		set t = $get(@g@(idobj,"dta",row,col,i,"arr",1))
		if t = 1 set out = out_$get(@g@(idobj,"dta",row,col,i,"dta",1))
		if t = 2 set out = out_"{"_..KlicZnaku($get(@g@(idobj,"dta",row,col,i,"dta",2)))_"}"
		if t = 4 set out = out_..NormCas($get(@g@(idobj,"dta",row,col,i,"dta",4)))
	}
	quit out
}

/// Čas na společný tvar `H:MM` (bez vodicí nuly hodiny), ať se neliší jen formátem.
ClassMethod NormCas(cas As %String) As %String
{
	quit:cas'[":" cas
	quit (+$piece(cas,":",1))_":"_$piece(cas,":",2)
}

/// Spoje bloků ve tvaru "c1,c2|c3,c4" → bloky seřazené uvnitř i mezi sebou, ať jde porovnat,
/// jestli jsou spoje rozdělené stejně, bez ohledu na pořadí.
ClassMethod RozdeleniBloku(bloky As %String) As %String
{
	kill serazene
	for b=1:1:$length(bloky,"|") {
		set blok = $piece(bloky,"|",b)
		continue:blok=""
		kill cisla
		for c=1:1:$length(blok,",") { set x = $piece(blok,",",c), cisla(x) = $get(cisla(x)) + 1 }
		set klic = "", x = ""
		for  {
			set x = $order(cisla(x))
			quit:x=""
			for n=1:1:cisla(x) set klic = klic_","_x
		}
		// Blok dlouhé linky se do klíče nevejde - klíč je jen pro pořadí, text se drží zvlášť.
		set text = $extract(klic,2,$length(klic))
		set poradi = $extract(text,1,100)_"#"_$system.Encryption.Base64Encode($system.Encryption.MD5Hash(text))
		set serazene(poradi) = $get(serazene(poradi)) + 1, serazene(poradi,"t") = text
	}
	set out = "", k = ""
	for  {
		set k = $order(serazene(k))
		quit:k=""
		for n=1:1:serazene(k) set out = out_"|"_serazene(k,"t")
	}
	quit $extract(out,2,$length(out))
}

/// Náš výstup v témž tvaru jako `NactiLegacy`.
ClassMethod NactiNase(ByRef sys, lin As %String, ByRef nase)
{
	kill nase, ^||ML
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrMhd).GetData(.sys)
	set key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set smer = page.values.%Get("SMER")
		// Seznam zastávek; řádek příjezdu nese název zastávky pod sebou (starý tisk ho má u obou).
		set zastavky = page.rows.%Get("ZASTAVKY"), zast = ""
		for j=0:1:zastavky.%Size()-1 {
			set z = zastavky.%Get(j)
			set nazev = $select(z.%Get("TYP")="prijezd":zastavky.%Get(j+1).%Get("NAZEV_ZASTAVKY"), 1:z.%Get("NAZEV_ZASTAVKY"))
			set atributy = z.%Get("ATRIBUTY"), znacky = ""
			if $isobject(atributy) { for a=0:1:atributy.%Size()-1 set znacky = znacky_"{"_atributy.%Get(a)_"}" }
			set zast = zast_";"_nazev_znacky_"["_z.%Get("ZONY")_"]"
		}
		set zast = smer_"|"_$extract(zast,2,$length(zast))
		set (bloky, posledni) = "", pocetBloku = 0
		set it = page.rows.%Get("SPOJE").%GetIterator()
		while it.%GetNext(.k, .spoj) {
			set sk = spoj.%Get("SKUPINA")
			if sk '= posledni { set bloky = bloky_"|", posledni = sk, pocetBloku = pocetBloku + 1 }
			else { set bloky = bloky_"," }
			set bloky = bloky_spoj.%Get("CISLO_SPOJE")
			set casy = "", hodnoty = spoj.%Get("CASY")
			for j=0:1:hodnoty.%Size()-1 {
				set v = hodnoty.%Get(j)
				set casy = casy_";"_$select((v="|")||(v="<"):"{"_v_"}", 1:..NormCas(v))
			}
			set radek = smer_"|"_spoj.%Get("CISLO_SPOJE")_"|"_$extract(casy,2,$length(casy))
			do ..Pridej(.nase, "cas", radek)
			set texty = spoj.%Get("TEXTOVE_ZNACKY")
			set pocetTextu = $select($isobject(texty):texty.%Size(), 1:0)
			set platnost = spoj.%Get("PLATNOST"), znaky = "", txt = ""
			for j=0:1:platnost.%Size()-1 {
				if j < (platnost.%Size() - pocetTextu) { set znaky = znaky_","_platnost.%Get(j) }
				else { set txt = txt_","_platnost.%Get(j) }
			}
			set radek = smer_"|"_spoj.%Get("CISLO_SPOJE")_"|"_spoj.%Get("NZ")_"|"_$extract(znaky,2,$length(znaky))_"|"_$extract(txt,2,$length(txt))
			set nase("spoj",radek) = $get(nase("spoj",radek)) + 1
		}
		// Starý tisk opakuje seznam zastávek v každém bloku, my ho máme jednou na stránku.
		do ..Pridej(.nase, "zast", zast, 0, pocetBloku)
		set nase("bloky",smer) = $get(nase("bloky",smer))_bloky
		set it = page.rows.%Get("POZNAMKY").%GetIterator()
		while it.%GetNext(.k, .pozn) {
			continue:'..PorovnavanaPoznamka(pozn.%Get("DRUH"), pozn.%Get("ZNAK"))
			do ..Pridej(.nase, "pozn", smer_"|"_pozn.%Get("DRUH")_"|"_pozn.%Get("ZNAK")_"|"_pozn.%Get("TEXT"), 1)
		}
	}
	kill ^||ML
}

/// Porovná obě strany. res("spoj") / res("pozn") = 1 shoda, res(...,"rozdil") = prvních pár rozdílů,
/// res("pocet") = počet spojů starého tisku, res("sNz"/"sZnaky"/"sTexty"/"sPozn") = kolik z nich
/// něco nese. 0 = starý modul nejde spustit (test linku přeskočí).
ClassMethod Porovnej(ByRef sys, idfa As %String, db As %String, lin As %String, ByRef res) As %Boolean
{
	kill res
	quit:'..NactiLegacy(idfa, db, lin, .leg) 0
	do ..NactiNase(.sys, lin, .nase)
	// Co porovnání vůbec vidí - shoda nad prázdnými značkami by nic nedokazovala.
	set (res("pocet"), res("sNz"), res("sZnaky"), res("sTexty"), res("sPozn")) = 0, k = ""
	for  {
		set k = $order(leg("spoj",k))
		quit:k=""
		set res("pocet") = res("pocet") + leg("spoj",k)
		if $piece(k,"|",3) '= "" set res("sNz") = res("sNz") + leg("spoj",k)
		if $piece(k,"|",4) '= "" set res("sZnaky") = res("sZnaky") + leg("spoj",k)
		if $piece(k,"|",5) '= "" set res("sTexty") = res("sTexty") + leg("spoj",k)
	}
	set k = ""
	for  { set k = $order(leg("pozn",k)) quit:k=""  set res("sPozn") = res("sPozn") + 1 }
	// Rozdělení spojů do bloků (typů provozu) a jejich pořadí - dvě otázky, proto dvě kategorie:
	// „bloky" = stejné skupiny spojů bez ohledu na pořadí, „poradi" = i stejné pořadí.
	for smer = "T", "Z" {
		set l = $get(leg("bloky",smer)), o = $get(nase("bloky",smer))
		continue:(l="")&&(o="")
		do ..Pridej(.leg, "blok", smer_"|"_..RozdeleniBloku(l), 1), ..Pridej(.nase, "blok", smer_"|"_..RozdeleniBloku(o), 1)
		do ..Pridej(.leg, "poradi", smer_"|"_l, 1), ..Pridej(.nase, "poradi", smer_"|"_o, 1)
	}
	for kat = "spoj", "pozn", "cas", "zast", "blok", "poradi" {
		set rozdil = "", pocet = 0
		for strana = 1, 2 {
			set k = ""
			for  {
				if strana = 1 { set k = $order(leg(kat,k)) } else { set k = $order(nase(kat,k)) }
				quit:k=""
				set l = $get(leg(kat,k)), o = $get(nase(kat,k))
				continue:l=o
				continue:(strana = 2)&&$data(leg(kat,k))
				set pocet = pocet + 1
				set text = $select(strana=1:$get(leg(kat,k,"t"),k), 1:$get(nase(kat,k,"t"),k))
				if pocet <= 5 set rozdil = rozdil_$select(rozdil="":"",1:" ; ")_"leg="_l_" nase="_o_" ["_text_"]"
			}
		}
		set res(kat) = (pocet = 0), res(kat,"rozdil") = rozdil, res(kat,"pocet") = pocet
	}
	quit 1
}

/// Hromadné porovnání přes MLRest. IN: IDFA, DB, LINKY (čárkou). OUT(linka) = "ok" nebo kategorie
/// s rozdílem a počtem rozdílů; neporovnatelná linka „přeskočena".
ClassMethod SouhrnSady(ByRef sys As %String) As %Status
{
	set idfa = $get(^||ML("IN","IDFA")), db = $get(^||ML("IN","DB")), linky = $get(^||ML("IN","LINKY"))
	kill tmp
	set tmp("U","IdFa") = idfa, tmp("U","DATA") = db, tmp("U","SystemDB") = $namespace
	set tmp("U","delimiter") = "~", tmp("U","MLP") = 1, tmp("U","loginName") = "test", tmp("U","fullName") = "test"
	kill vysledky
	for i=1:1:$length(linky,",") {
		set lin = $piece(linky,",",i)
		continue:lin=""
		if '..Porovnej(.tmp, idfa, db, lin, .res) { set vysledky(lin) = "přeskočena" continue }
		set out = ""
		for kat = "spoj", "pozn", "cas", "zast", "blok", "poradi" {
			if 'res(kat) set out = out_kat_"("_res(kat,"pocet")_") "
		}
		set vysledky(lin) = $select(out="":"ok", 1:out)_" ["_res("pocet")_" spojů]"
	}
	kill ^||ML("OUT")
	merge ^||ML("OUT") = vysledky
	quit $$$OK
}

/// Diagnostika přes MLRest. IN: IDFA, DB, LINKA_ID. OUT: SPOJE, POZN, POCET.
ClassMethod Souhrn(ByRef sys As %String) As %Status
{
	set idfa = $get(^||ML("IN","IDFA")), db = $get(^||ML("IN","DB")), lin = $get(^||ML("IN","LINKA_ID"))
	kill tmp
	set tmp("U","IdFa") = idfa, tmp("U","DATA") = db, tmp("U","SystemDB") = $namespace
	set tmp("U","delimiter") = "~", tmp("U","MLP") = 1, tmp("U","loginName") = "test", tmp("U","fullName") = "test"
	set ok = ..Porovnej(.tmp, idfa, db, lin, .res)
	kill ^||ML("OUT")
	if 'ok { set ^||ML("OUT","SKIP") = "starý tisk nejde spustit" quit $$$OK }
	set ^||ML("OUT","POCET") = res("pocet")_" spojů, s NZ "_res("sNz")_", se značkou "_res("sZnaky")_", s textem "_res("sTexty")_", položek legendy "_res("sPozn")
	set ^||ML("OUT","SPOJE") = res("spoj")_" ("_res("spoj","pocet")_") "_res("spoj","rozdil")
	set ^||ML("OUT","POZN") = res("pozn")_" ("_res("pozn","pocet")_") "_res("pozn","rozdil")
	for kat = "cas", "zast", "blok", "poradi" {
		set ^||ML("OUT",$zconvert(kat,"U")) = res(kat)_" ("_res(kat,"pocet")_") "_$extract(res(kat,"rozdil"),1,3000)
	}
	do ..NactiLegacy(idfa, db, lin, .leg)
	set k = ""
	for  { set k = $order(leg("pozn",k)) quit:k=""  set ^||ML("OUT","LEGENDA",$increment(n)) = $extract($get(leg("pozn",k,"t"),k),1,300) }
	quit $$$OK
}

}
