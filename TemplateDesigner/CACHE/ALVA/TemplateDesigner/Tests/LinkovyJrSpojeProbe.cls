Include (%occInclude, GENERAL)

/// Read-only sonda: **co GetData linkového JŘ reálně pošle za spoje** - číslo, směr a skupinu dnů,
/// v pořadí, v jakém to klient dostane.
///
/// **K čemu to je.** Uživateli chyběly v tabulce spoje 501-509 (`B120`, `CB_GWBUS 2 / 0320144`),
/// zatímco server je posílá. Rozhodnout, kde se ztrácejí, nejde bez toho, aby bylo vidět **směr
/// a skupinu** každého spoje: klient spoje dělí na hlavní a zrcadlený směr a na bloky sloupců,
/// takže spoj s cizí hodnotou v těchto polích může vypadnout na jiném místě než v datech.
///
/// Sonda **nic nemění**, jen zavolá `GetData` a vypíše z odpovědi tři pole per spoj.
Class ALVA.TemplateDesigner.Tests.LinkovyJrSpojeProbe
{

/// Vypíše spoje, které `GetData` poslal pro jednu linku.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID` (+ volitelně kterýkoli parametr tisku - předá se dál).
/// OUT: řádek `strana~<n>` se souhrnem stránky a pak `spoj~<n>~<i>` s `číslo:směr:skupina`.
ClassMethod Spoje(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
		s lin = $g(^||ML("IN","LINKA_ID"))

		// Vstup si odložíme - GetData zapisuje do téhož ^||ML a IN by si přepsalo samo sebe.
		m vstup = ^||ML("IN")
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)

		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")
		s ^||ML("OUT","linka") = dataDB_" "_sku_" "_lin
		s ^||ML("OUT","chyba") = $g(odpoved("ERR"))

		s stranek = 0, spojuCelkem = 0, key = ""
		f {
			s key = $o(odpoved(key))
			q:key=""
			continue:key="ERR"
			s stranek = stranek + 1
			s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
			s spoje = page.rows.%Get("SPOJE")
			s zast = page.rows.%Get("ZASTAVKY")
			s pocetSpoju = $s($isobject(spoje):spoje.%Size(), 1:0)
			s pocetZast = $s($isobject(zast):zast.%Size(), 1:0)
			s spojuCelkem = spojuCelkem + pocetSpoju
			s ^||ML("OUT","strana~"_key) = "smer="_page.values.%Get("SMER")
				_" spoju="_pocetSpoju_" zastavek="_pocetZast
			// Číslo, směr a skupina per spoj - v pořadí odpovědi, ať jde srovnat s tím, co uživatel vidí.
			f si=0:1:pocetSpoju-1 {
				s sp = spoje.%Get(si)
				// Kolik časů spoj reálně nese: sloupec bez jediného času se sice nakreslí, ale je
				// prázdný - a uživatel prázdný sloupec nahlásí jako chybějící spoj.
				s casy = sp.%Get("CASY"), casu = 0, neprazdnych = 0
				i $isobject(casy) {
					s casu = casy.%Size()
					f ci=0:1:casu-1 i casy.%Get(ci) '= "" s neprazdnych = neprazdnych + 1
				}
				// NZ a PLATNOST rozhodují o zařazení do vlastních sloupců - spoj, který do žádného
				// nesedne, server tiše zahodí, takže právě v těchhle dvou polích se hledá rozdíl.
				s pl = sp.%Get("PLATNOST"), plText = ""
				i $isobject(pl) f pi=0:1:pl.%Size()-1 s plText = plText_pl.%Get(pi)
				s ^||ML("OUT","spoj~"_key_"~"_$tr($j(si+1,3)," ","0")) =
					sp.%Get("CISLO_SPOJE")_":"_sp.%Get("SMER")_":"_sp.%Get("SKUPINA")
					_" casy="_neprazdnych_"/"_casu_" nz="_sp.%Get("NZ")_" platnost="_plText
			}
		}
		s ^||ML("OUT","souhrn") = "stranek="_stranek_" spoju="_spojuCelkem
		k ^||ML("IN")
		m ^||ML("IN") = vstup
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Vypíše **texty poznámek** první stránky, jak je `GetData` pošle - hlavně kvůli jazyku textů
/// (#9088: slovenská šablona na Nitře tiskla české poznámky).
///
/// IN: `LINKA_ID` (+ `SKUPINA_LINEK_ID`, `JAZYK` a další parametry tisku), `MAX` (výchozí 8).
/// OUT: `poznamka~<i>` = `druh:znak text`.
ClassMethod Poznamky(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s max = +$g(^||ML("IN","MAX"))
		s:max<1 max = 8
		m vstup = ^||ML("IN")
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)
		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")
		s ^||ML("OUT","chyba") = $g(odpoved("ERR"))
		s key = $o(odpoved(""))
		i (key '= "") && (key '= "ERR") {
			s pozn = ##class(%DynamicObject).%FromJSON(odpoved(key)).rows.%Get("POZNAMKY")
			i $isobject(pozn) {
				f pi=0:1:pozn.%Size()-1 {
					q:pi'<max
					s p = pozn.%Get(pi)
					s ^||ML("OUT","poznamka~"_(pi+1)) = p.%Get("DRUH")_":"_p.%Get("ZNAK")_" "_$e(p.%Get("TEXT"),1,80)
				}
			}
		}
		k ^||ML("IN")
		m ^||ML("IN") = vstup
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Vypíše **přípoje per spoj** tak, jak je `GetData` pošle - u kterého spoje a na které zastávce.
///
/// Proč: `#11300` (KORID) je o prázdném přípojném řádku na tabulce, kde jsou pracovní dny a víkend
/// oddělené. Ukázat to jde jen na lince, kde přípoj **nemají všechny spoje** a zároveň spoje spadají
/// do víc skupin dnů - z výpisu spojů ani z globálů to vidět není.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID` (+ volitelně parametry tisku). OUT: řádek `spoj~<n>~<i>`
/// s číslem spoje, skupinou dnů a počtem přípojů; `pripoj~...` s tarifním číslem a typem.
ClassMethod Pripoje(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
		s lin = $g(^||ML("IN","LINKA_ID"))

		m vstup = ^||ML("IN")
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)

		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")
		s ^||ML("OUT","linka") = dataDB_" "_sku_" "_lin
		s ^||ML("OUT","chyba") = $g(odpoved("ERR"))

		s sPripoji = 0, bezPripoju = 0, key = ""
		f {
			s key = $o(odpoved(key))
			q:key=""
			continue:key="ERR"
			s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
			s skupiny = page.rows.%Get("SKUPINY_DNU")
			s ^||ML("OUT","strana~"_key) = "smer="_page.values.%Get("SMER")
				_" skupin="_$s($isobject(skupiny):skupiny.%Size(), 1:0)
			s spoje = page.rows.%Get("SPOJE")
			continue:'$isobject(spoje)
			f si=0:1:spoje.%Size()-1 {
				s sp = spoje.%Get(si)
				s pr = sp.%Get("PRIPOJE"), pocet = $s($isobject(pr):pr.%Size(), 1:0)
				i pocet { s sPripoji = sPripoji + 1 } else { s bezPripoju = bezPripoju + 1 }
				s ^||ML("OUT","spoj~"_key_"~"_$tr($j(si+1,3)," ","0")) =
					sp.%Get("CISLO_SPOJE")_" skupina="_sp.%Get("SKUPINA")_" pripoju="_pocet
				continue:'pocet
				f pi=0:1:pocet-1 {
					s p = pr.%Get(pi)
					// RADEK rozhoduje, jestli přípoj dostane řádek v tabulce, nebo jde jen do
					// poznámek pod ní. Bez něj vypadá chybějící řádek jako vada vykreslení.
					s ^||ML("OUT","pripoj~"_key_"~"_$tr($j(si+1,3)," ","0")_"~"_(pi+1)) =
						"tc="_p.%Get("PORADI")_" typ="_p.%Get("TYP")_" radek="_p.%Get("RADEK")
						_" cas="_p.%Get("CAS")_" text="_p.%Get("TEXT")
						_" linka="_p.%Get("LINKA")_" spoj="_p.%Get("SPOJ")
				}
			}
		}
		s ^||ML("OUT","souhrn") = "spoju s pripoji="_sPripoji_", bez pripoju="_bezPripoju
		k ^||ML("IN")
		m ^||ML("IN") = vstup
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Najde linku se **zónami u zastávek** a vypíše, jak vypadají - s písmenem přepravního systému,
/// nebo bez něj.
///
/// Proč: nastavení „odstranit písmena ze zóny" (`ZonaIdsDelZnk`) jde ověřit jen na lince, která
/// zóny vůbec má. Který kraj je eviduje, z globálů vidět není - a roadmapa mířila na Vsetín, kde
/// vzorek není.
///
/// IN: `LIMIT` (kolik linek projít, výchozí 60). OUT: první nalezená linka, hodnoty zón u prvních
/// zastávek a obě nastavení (globální i u linky).
///
/// ⚠️ Prochází **jen firmu, do které je volající přihlášený** - `GetData` si dataDB bere z `sys`.
/// Jinou firmu ověříš dalším voláním, ne parametrem.
ClassMethod Zony(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s limit = +$g(^||ML("IN","LIMIT"))
		s:limit<1 limit = 60
		s ^||ML("OUT","nastaveni~globalni") = $g(^[dataDB]JRSET("Definice","ZonaIdsDelZnk"))

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
				// Zóny se tisknou jen u linek s `linIDS` 1 nebo 2; u ostatních posílá agenda
				// prázdno, takže by je nemělo smysl zkoumat a jen by spotřebovaly limit.
				s linIds = +$g(^[dataDB]JRL(s1, l1, "PATCH", "linIDS"))
				s sLinIds = $g(sLinIds) + $s((linIds=1)||(linIds=2):1, 1:0)
				continue:'((linIds = 1) || (linIds = 2))
				s projito = projito + 1
				i projito > limit {
					s ^||ML("OUT","souhrn") = "prozkoumano linek s IDS="_(projito-1)_", zadna se zonami"
					ret $$$OK
				}
				i ..VypisZony(dataDB, s1, l1, .sys) ret $$$OK
			}
		}
		s ^||ML("OUT","souhrn") = "projito linek="_projito_", zadna se zonami"
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Když linka má u zastávek zóny, vypíše je do OUT a vrátí 1. Helper - volá se z metody s catch.
/// ⚠️ `dataDB` se předává parametrem: helper nemá `$$$FillParams`, takže by tu jinak nebylo.
ClassMethod VypisZony(dataDB As %String, sku As %String, lin As %String, ByRef sys As %String) As %Boolean
{
	s naslo = 0
	try {
		k ^||ML("IN")
		s ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
		s ^||ML("IN","OBOUSMERNY_TISK") = "false"
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)
		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")
		i $g(odpoved("ERR"))'="" ret 0

		s key = $o(odpoved(""))
		i key="ERR" s key = $o(odpoved(key))
		i key="" ret 0
		s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
		s zast = page.rows.%Get("ZASTAVKY")
		i '$isobject(zast) ret 0
		s vypsano = 0
		f zi=0:1:zast.%Size()-1 {
			// ⚠️ Pole se jmenuje ZONY (množné), i když nese jednu hodnotu - je to shoda se
			// zastávkovým JŘ, kde je to pole zón.
			s z = zast.%Get(zi), zona = z.%Get("ZONY")
			// „-" znamená zapnutý tisk zón, ale prázdnou hodnotu v datech - to není vzorek.
			continue:(zona="")||(zona="-")
			s vypsano = vypsano + 1
			i vypsano '> 8 {
				s ^||ML("OUT","zona~"_$tr($j(vypsano,2)," ","0")) =
					"tc="_z.%Get("TC")_" "_z.%Get("POPIS")_" -> """_zona_""""
			}
		}
		i 'vypsano ret 0
		s naslo = 1
		s ^||ML("OUT","linka") = dataDB_" "_sku_" / "_lin_"  zastavek se zonou="_vypsano
		s ^||ML("OUT","nastaveni~globalni") = $g(^[dataDB]JRSET("Definice","ZonaIdsDelZnk"))
		s ^||ML("OUT","nastaveni~u linky") = $g(^[dataDB]JRL(sku,lin,"PATCH","ZonaIdsDelZnk"))
	}
	catch (ex) { s naslo = 0 }
	q naslo
}

/// Najde linky, na kterých jde předvést **prázdný přípojný řádek** (`#11300`).
///
/// Hledá se linka, kde spoje spadají do **víc skupin dnů** a zároveň aspoň jeden spoj nese přípoj
/// tištěný jako **řádek tabulky** (`RADEK=1`), zatímco spoje jiné skupiny přípoj nemají. Teprve
/// taková linka dá list, na kterém by ve starém tisku zůstal prázdný přípojný řádek.
///
/// ⚠️ Volá `GetData` na každou zkoumanou linku, takže je to **pomalé** - proto `LIMIT`.
///
/// IN: `LIMIT` (kolik linek projít, výchozí 150), `MAX` (kolik nálezů vypsat, výchozí 10),
/// `MIN_SPOJU` (přeskočit linky s méně spoji, výchozí 4).
/// OUT: `nalez~<n>` = `sku / lin  skupin=<n> spoju s radkovym pripojem=<n>`.
ClassMethod NajdiProUkazku(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s limit = +$g(^||ML("IN","LIMIT")), max = +$g(^||ML("IN","MAX"))
		s minSpoju = +$g(^||ML("IN","MIN_SPOJU"))
		s:limit<1 limit = 150
		s:max<1 max = 10
		s:minSpoju<1 minSpoju = 4

		s projito = 0, nalezu = 0, s1 = ""
		f {
			s s1 = $o(^[dataDB]JRL(s1))
			q:s1=""
			continue:'$isvalidnum(s1)
			s l1 = ""
			f {
				s l1 = $o(^[dataDB]JRL(s1, l1))
				q:l1=""
				continue:'$d(^[dataDB]JRL(s1, l1, "I"))
				continue:+$g(^[dataDB]JRL(s1, l1, "T"))<minSpoju
				s projito = projito + 1
				i projito > limit ret ..DopisSouhrn(projito - 1, nalezu, .statistika, .nalez)
				s popis = ..ProzkoumejLinku(s1, l1, .sys, .statistika)
				continue:popis=""
				s nalezu = nalezu + 1
				// ⚠️ Nálezy se hromadí stranou - `GetData` uvnitř helperu maže `^||ML("OUT")`,
				// takže zapsané by je přepsala hned další zkoumaná linka.
				s nalez(nalezu) = s1_" / "_l1_"  "_popis
				i nalezu '< max ret ..DopisSouhrn(projito, nalezu, .statistika, .nalez)
			}
		}
		d ..DopisSouhrn(projito, nalezu, .statistika, .nalez)
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Popis linky, když na ní jde ukázku postavit; jinak prázdno. Helper bez `ByRef sys` - volá se
/// jen z metody, která catch má.
ClassMethod ProzkoumejLinku(sku As %String, lin As %String, ByRef sys As %String, ByRef statistika) As %String
{
	s vysledek = ""
	try {
		k ^||ML("IN")
		s ^||ML("IN","SKUPINA_LINEK_ID") = sku
		s ^||ML("IN","LINKA_ID") = lin
		s ^||ML("IN","OBOUSMERNY_TISK") = "false"
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)
		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")
		i $g(odpoved("ERR"))'="" ret ""

		s key = ""
		f {
			s key = $o(odpoved(key))
			q:key=""
			continue:key="ERR"
			s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
			s skupiny = page.rows.%Get("SKUPINY_DNU")
			continue:'$isobject(skupiny)
			s statistika("stranek") = $g(statistika("stranek")) + 1
			i skupiny.%Size()>1 s statistika("vic skupin") = $g(statistika("vic skupin")) + 1
			continue:skupiny.%Size()<2
			s spoje = page.rows.%Get("SPOJE")
			continue:'$isobject(spoje)
			// skupinaSRadkem = skupina dnů, jejíž spoj nese přípoj tištěný jako řádek
			k maRadek, vsechny
			f si=0:1:spoje.%Size()-1 {
				s sp = spoje.%Get(si), vsechny(sp.%Get("SKUPINA")) = ""
				s pr = sp.%Get("PRIPOJE")
				continue:'$isobject(pr)
				f pi=0:1:pr.%Size()-1 i +pr.%Get(pi).%Get("RADEK") s maRadek(sp.%Get("SKUPINA")) = ""
			}
			// Ukázka dává smysl jen tehdy, když jedna skupina řádkový přípoj má a jiná ne.
			continue:'$d(maRadek)
			s statistika("vic skupin + radkovy pripoj") = $g(statistika("vic skupin + radkovy pripoj")) + 1
			s sRadkem = 0, k1 = "" f  s k1 = $o(maRadek(k1)) q:k1=""  s sRadkem = sRadkem + 1
			s celkem = 0, k1 = "" f  s k1 = $o(vsechny(k1)) q:k1=""  s celkem = celkem + 1
			continue:sRadkem'<celkem
			s vysledek = "smer="_page.values.%Get("SMER")_" skupin="_skupiny.%Size()
				_" spoju="_spoje.%Size()
			q
		}
	}
	catch (ex) {
		s vysledek = ""
		s statistika("chyba: "_ex.DisplayString()) = $g(statistika("chyba: "_ex.DisplayString())) + 1
	}
	q vysledek
}

/// Zapíše souhrn průchodu do OUT. Statistika říká, na které podmínce hledání padá, když nic nenajde.
ClassMethod DopisSouhrn(projito As %Integer, nalezu As %Integer, ByRef statistika, ByRef nalez) As %Status
{
	s ^||ML("OUT","souhrn") = "projito linek="_projito_", nalezu="_nalezu
	s k1 = ""
	f  s k1 = $o(statistika(k1)) q:k1=""  s ^||ML("OUT","statistika~"_k1) = statistika(k1)
	s k1 = ""
	f  s k1 = $o(nalez(k1)) q:k1=""  s ^||ML("OUT","nalez~"_$tr($j(k1,2)," ","0")) = nalez(k1)
	q $$$OK
}

/// Vypíše kilometrické sloupce linky - **společné i per směr** - a přepínače, které o nich rozhodují.
///
/// Proč: starý tisk bere buď dva směrové seznamy (`PATCH,KmsSmer,T`/`Z`), nebo jeden **společný**
/// (`^JRL(sku,lin,"K")`), a rozhoduje o tom přepínač `PrintKmsSmerNo` u linky, resp.
/// `PS_PrintKmsSmerNo` globálně. My čteme vždy ty směrové, takže bez tohohle měření nejde říct,
/// odkud se bere rozdíl v počtu sloupců proti výtisku.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID`. OUT: počty a obsah seznamů + hodnoty přepínačů.
ClassMethod KmSloupce(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
		s lin = $g(^||ML("IN","LINKA_ID"))
		k ^||ML("OUT")
		s ^||ML("OUT","linka") = dataDB_" "_sku_" "_lin

		// Přepínače: u linky se ukládá jen přítomnost klíče, globálně dtto.
		s ^||ML("OUT","prepinac~linka.PrintKmsSmerNo") =
			$s($d(^[dataDB]JRL(sku,lin,"PATCH","PrintKmsSmerNo")):"nastaveno (smerove seznamy se NEtisknou)", 1:"neni")
		s ^||ML("OUT","prepinac~global.PS_PrintKmsSmerNo") =
			$s($d(^[dataDB]JRSET("Definice","PS_PrintKmsSmerNo")):"nastaveno", 1:"neni")
		// Prahy, kterými se seznamy generují (ne tisknou) - u linky, jinak globálně, výchozí 10.
		s ^||ML("OUT","prah~linka.Kms.Filtr") = $g(^[dataDB]JRL(sku,lin,"PATCH","Kms","Filtr"), "neni")
		s ^||ML("OUT","prah~linka.Kms.MaxFiltr") = $g(^[dataDB]JRL(sku,lin,"PATCH","Kms","MaxFiltr"), "neni")
		s ^||ML("OUT","prah~global.Kms.Filtr") = $g(^[dataDB]JRSET("Definice","Kms","Filtr"), "neni")
		s ^||ML("OUT","prah~global.Kms.MaxFiltr") = $g(^[dataDB]JRSET("Definice","Kms","MaxFiltr"), "neni")

		// Společný seznam
		s ^||ML("OUT","pocet~spolecny") = +$g(^[dataDB]JRL(sku,lin,"K"))
		s i = ""
		f {
			s i = $o(^[dataDB]JRL(sku,lin,"K",i))
			q:i=""
			continue:'$isvalidnum(i)
			s ^||ML("OUT","spolecny~"_$tr($j(i,2)," ","0")) = $e($g(^[dataDB]JRL(sku,lin,"K",i)),1,200)
		}
		// Směrové seznamy
		f smer = "T","Z" {
			s ^||ML("OUT","pocet~"_smer) = +$g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer))
			s i = ""
			f {
				s i = $o(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer,i))
				q:i=""
				continue:'$isvalidnum(i)
				s ^||ML("OUT",smer_"~"_$tr($j(i,2)," ","0")) =
					$e($g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer,i)),1,200)
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Vypíše u šablon dané agendy, jestli nesou **vlastní definici sloupců** (`customColumns`).
///
/// Proč: server spoj, který nesedne do žádného vlastního sloupce, **tiše zahodí**
/// (`ZarazeniSpojeDleDefiniceLinky`, §16.1) - a definice jde ze šablony, ne z parametrů, takže
/// volání „naprázdno" ji nemá odkud vzít a vrátí všechny spoje. Šablona na disku přitom nemusí
/// odpovídat té v databázi, ze které uživatel tiskne.
///
/// IN: `AGENDA` (default `LINKOVY_JR_AD`). OUT: řádek per šablona s délkou blobu a nálezem.
ClassMethod Sablony(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s agenda = $g(^||ML("IN","AGENDA"))
		s:agenda="" agenda = "LINKOVY_JR_AD"
		k ^||ML("OUT")
		s ^||ML("OUT","agenda") = agenda_" IdFa="_IdFa
		s id = "", pocet = 0
		f {
			s id = $o(^ALVA.TemplateDesigner.Templates(IdFa,"IDX","ByAgenda",agenda,id))
			q:id=""
			s pocet = pocet + 1
			s nazev = $g(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",id,"Metadata","Name"))
			// Blob může být rozsekaný na kusy - slepíme, ať se hledá v celém JSON.
			s blob = $g(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",id,"Data"))
			i blob = +blob {
				s cely = ""
				f ci=1:1:blob s cely = cely_$g(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",id,"Data",ci))
				s blob = cely
			}
			s maDefinici = $s(blob["customColumns":"ANO", 1:"ne")
			s ^||ML("OUT","sablona~"_id) = nazev_" | customColumns="_maDefinici_" | delka="_$l(blob)
		}
		s ^||ML("OUT","souhrn") = "sablon="_pocet
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Vypíše **persistované parametry tisku** uložené uživatelům pro danou agendu.
///
/// Proč: náhled i tisk skládají IN z uložených hodnot, takže volání „naprázdno" může vracet jiná
/// data než aplikace u uživatele. Bez tohoto výpisu se hádá, s čím se to má přehrát
/// (TemplateDesigner/CLAUDE.md, diagnostika „UI ukazuje jiné nastavení, než se tiskne").
///
/// IN: `AGENDA` (default `LINKOVY_JR_AD`). OUT: řádek `<uživatel>` s uloženým blobem.
ClassMethod Parametry(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s agenda = $g(^||ML("IN","AGENDA"))
		s:agenda="" agenda = "LINKOVY_JR_AD"
		k ^||ML("OUT")
		s ^||ML("OUT","agenda") = agenda_" IdFa="_IdFa_" dataDB="_dataDB
		// Globál hledáme v obou namespace - kde UserData reálně leží, se z kódu nepozná.
		s nalezeno = 0
		f ns=1:1:2 {
			s fa = ""
			f {
				s fa = $s(ns=1:$o(^ALVA.EDControls.UserData(fa)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa)))
				q:fa=""
				s user = ""
				f {
					s user = $s(ns=1:$o(^ALVA.EDControls.UserData(fa, user)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa, user)))
					q:user=""
					s klic = ""
					f {
						s klic = $s(ns=1:$o(^ALVA.EDControls.UserData(fa, user, klic)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa, user, klic)))
						q:klic=""
						continue:klic'["PrintParams"
						s ag = ""
						f {
							s ag = $s(ns=1:$o(^ALVA.EDControls.UserData(fa, user, klic, ag)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa, user, klic, ag)))
							q:ag=""
							s blob = $s(ns=1:$g(^ALVA.EDControls.UserData(fa, user, klic, ag)), 1:$g(^[dataDB]ALVA.EDControls.UserData(fa, user, klic, ag)))
							continue:blob=""
							s nalezeno = nalezeno + 1
							s ^||ML("OUT","ns"_ns_"~"_fa_"~"_user_"~"_ag) = $e(blob,1,900)
						}
					}
				}
			}
		}
		s ^||ML("OUT","souhrn") = "zaznamu s ulozenymi parametry="_nalezeno
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
