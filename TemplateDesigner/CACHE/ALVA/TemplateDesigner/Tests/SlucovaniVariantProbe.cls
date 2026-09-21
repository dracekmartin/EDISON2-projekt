Include (%occInclude, GENERAL)

/// Read-only sonda: **na kterých linkách je vidět slučování variant tras** (`B157`).
///
/// Zavolá `GetData` dvakrát - bez slučování a s ním - a porovná počet variantních sloupců.
/// Linka, kde se počet liší, je ta, na které jde změna prohlédnout v návrháři; z globálů to
/// vidět není, protože varianty vznikají až seskupením spojů podle sledu zastávek.
///
/// ⚠️ Volá `GetData` **dvakrát na linku**, takže je to pomalé - proto `LIMIT`.
Class ALVA.TemplateDesigner.Tests.SlucovaniVariantProbe
{

/// Konkrétní vývěsky jedné linky, na kterých je vidět rozdíl mezi novým a starým pravidlem
/// slučování (`B157`). Hledá stránku, kde se zapnutým slučováním něco zmizí (jede jen část jiné
/// trasy) **a zároveň** zůstane varianta, kterou by staré pravidlo slilo (zacházka uprostřed).
/// Na takové vývěsce jde obojí ukázat najednou.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID`, `MAXOUT` (kolik vypsat, výchozí 8).
/// OUT: `zastavka~<n>` = `<směr> <název>  variant=<n>, slije se nove=<n>, stare by slilo=<n>`.
ClassMethod RozdilPoZastavkach(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID")), lin = $g(^||ML("IN","LINKA_ID"))
		s maxOut = +$g(^||ML("IN","MAXOUT"))
		s:maxOut<1 maxOut = 8

		k ^||ML("IN")
		s ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
		s ^||ML("IN","SLUCOVAT_PODMNOZINY") = "false"
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
			s vt = page.values.%Get("VARIANTY_TRAS")
			continue:'$isobject(vt)
			s pocetVar = vt.%Size()
			continue:pocetVar<3

			k sig
			s zastavky = page.rows.%Get("ZASTAVKY")
			continue:'$isobject(zastavky)
			f v=1:1:pocetVar { s sig(v) = "" }
			f z=0:1:(zastavky.%Size()-1) {
				s tm = zastavky.%Get(z).%Get("TRASA_MIN")
				f v=1:1:pocetVar {
					s hodnota = $s($isobject(tm)&&(v'>tm.%Size()):tm.%Get(v-1), 1:"")
					s sig(v) = sig(v)_$s(sig(v)="":"", 1:"~")_$s($isvalidnum(hodnota):"S", 1:$g(hodnota))
				}
			}

			s nove = ..PocetSlitych(pocetVar, .sig, 0)
			s stare = ..PocetSlitych(pocetVar, .sig, 1)
			// Zajímavá je jen stránka, kde je vidět obojí: něco se slije a něco si sloupec nechá,
			// ačkoli by ho staré pravidlo vzalo.
			continue:'nove
			continue:stare'>nove
			s nalez(stare - nove, nove, stranek) =
				page.values.SMER_" "_page.values."POPIS_ZASTAVKA"_
				"  variant="_pocetVar_", slije se nove="_nove_", stare by slilo="_stare
		}

		s ^||ML("OUT","souhrn") = "linka "_sku_"/"_lin_", stranek="_stranek
		s vypsano = 0, r = ""
		f {
			s r = $o(nalez(r), -1)
			q:(r="")||(vypsano'<maxOut)
			s n = ""
			f {
				s n = $o(nalez(r, n), -1)
				q:(n="")||(vypsano'<maxOut)
				s st = ""
				f {
					s st = $o(nalez(r, n, st))
					q:(st="")||(vypsano'<maxOut)
					s vypsano = vypsano + 1
					s ^||ML("OUT","zastavka~"_$tr($j(vypsano,2)," ","0")) = nalez(r, n, st)
				}
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Linky, kde slučování variant něco ubere, seřazené od největšího úbytku.
///
/// IN: `LIMIT` (kolik linek projít, výchozí 80), `MAX` (kolik vypsat, výchozí 10),
/// `MIN_VARIANT` (přeskočit linky s méně variantami, výchozí 3).
/// OUT: `nalez~<n>` = `sku / lin  variant bez slucovani=<n>, se slucovanim=<n>`.
ClassMethod KdeJeVidet(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s limit = +$g(^||ML("IN","LIMIT")), max = +$g(^||ML("IN","MAX"))
		s minVar = +$g(^||ML("IN","MIN_VARIANT"))
		s:limit<1 limit = 80
		s:max<1 max = 10
		s:minVar<2 minVar = 3

		s projito = 0, nalezu = 0, hotovo = 0, s1 = ""
		f {
			s s1 = $o(^[dataDB]JRL(s1))
			q:s1=""
			q:hotovo
			continue:'$isvalidnum(s1)
			s l1 = ""
			f {
				s l1 = $o(^[dataDB]JRL(s1, l1))
				q:l1=""
				q:hotovo
				continue:'$d(^[dataDB]JRL(s1, l1, "I"))
				continue:'+$g(^[dataDB]JRL(s1, l1, "T"))
				s projito = projito + 1
				i projito > limit s hotovo = 1 q

				s bez = ..PocetVariant(dataDB, s1, l1, "false", .sys)
				continue:bez<minVar
				s se = ..PocetVariant(dataDB, s1, l1, "true", .sys)
				continue:se'<bez
				s nalezu = nalezu + 1
				// Nálezy stranou: `GetData` uvnitř maže ^||ML("OUT"), takže zapsané by zmizely.
				s nalez(bez - se, nalezu) = s1_" / "_l1_"  variant bez slucovani="_bez_", se slucovanim="_se
				i nalezu '< max s hotovo = 1
			}
		}

		s ^||ML("OUT","souhrn") = "projito linek="_projito_", nalezu="_nalezu
		s vypsano = 0, ubytek = ""
		f {
			s ubytek = $o(nalez(ubytek), -1)
			q:ubytek=""
			s idx = ""
			f {
				s idx = $o(nalez(ubytek, idx))
				q:idx=""
				s vypsano = vypsano + 1
				s ^||ML("OUT","nalez~"_$tr($j(vypsano,2)," ","0")) = nalez(ubytek, idx)_"  (ubylo "_ubytek_")"
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Linky, kde se **nové pravidlo liší od starého** - tedy kde je varianta se zacházkou uprostřed
/// trasy. Ta je podmnožinou delší varianty, ale ne jejím souvislým úsekem, takže si po `B157`
/// sloupec nechá, kdežto před opravou zmizel. To je případ, který zákazník reklamoval.
///
/// Průběhy variant se skládají zpátky z pole `TRASA_MIN` vývěskové stránky (číslo = staví,
/// „<" = projíždí, prázdno = mimo trasu) - z globálů je přímo přečíst nejde.
///
/// IN: `LIMIT` (kolik linek projít, výchozí 60), `MAX` (kolik vypsat, výchozí 10).
ClassMethod KdeSeLisiPravidla(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s limit = +$g(^||ML("IN","LIMIT")), max = +$g(^||ML("IN","MAX"))
		s:limit<1 limit = 60
		s:max<1 max = 10

		s projito = 0, nalezu = 0, hotovo = 0, s1 = ""
		f {
			s s1 = $o(^[dataDB]JRL(s1))
			q:s1=""
			q:hotovo
			continue:'$isvalidnum(s1)
			s l1 = ""
			f {
				s l1 = $o(^[dataDB]JRL(s1, l1))
				q:l1=""
				q:hotovo
				continue:'$d(^[dataDB]JRL(s1, l1, "I"))
				continue:'+$g(^[dataDB]JRL(s1, l1, "T"))
				s projito = projito + 1
				i projito > limit s hotovo = 1 q

				k sig
				s pocetVar = ..NactiSignatury(s1, l1, .sys, .sig)
				continue:pocetVar<2
				s novych = ..PocetSlitych(pocetVar, .sig, 0)
				s starych = ..PocetSlitych(pocetVar, .sig, 1)
				continue:starych'>novych
				s nalezu = nalezu + 1
				s nalez(starych - novych, nalezu) = s1_" / "_l1_"  variant="_pocetVar_
					", slito nove="_novych_", slito stare="_starych
				i nalezu '< max s hotovo = 1
			}
		}

		s ^||ML("OUT","souhrn") = "projito linek="_projito_", linek se zachazkou="_nalezu
		s vypsano = 0, rozdil = ""
		f {
			s rozdil = $o(nalez(rozdil), -1)
			q:rozdil=""
			s idx = ""
			f {
				s idx = $o(nalez(rozdil, idx))
				q:idx=""
				s vypsano = vypsano + 1
				s ^||ML("OUT","zachazka~"_$tr($j(vypsano,2)," ","0")) = nalez(rozdil, idx)
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Rozpis jedné linky: seznam zastávek, průběh každé varianty a co se s kterou stane.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID`.
/// OUT: `A~*` zastávky, `B~*` varianty bez slučování (písmeno + kde staví),
/// `C~*` varianty se slučováním, `D~*` které varianty se slijí podle nového a starého pravidla.
ClassMethod RozpisLinky(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID")), lin = $g(^||ML("IN","LINKA_ID"))

		k sig, pism, nazev
		s pocetVar = ..NactiSignatury(sku, lin, .sys, .sig, .pism, .nazev)
		// Výstup se skládá stranou: `NactiSignatury` volá `GetData`, a ten maže ^||ML("OUT").
		s vystup("souhrn") = "linka "_sku_" / "_lin_", variant bez slucovani="_pocetVar

		s poradiZ = ""
		f {
			s poradiZ = $o(nazev(poradiZ))
			q:poradiZ=""
			s vystup("A~zastavka~"_$tr($j(poradiZ,2)," ","0")) = nazev(poradiZ)
		}
		f v=1:1:pocetVar {
			s vystup("B~varianta~"_$tr($j(v,2)," ","0")) =
				"pismeno="_$s($g(pism(v))="":"(zadne)", 1:$g(pism(v)))_
				", zastaveni="_##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni($g(sig(v)))_
				", prubeh="_$tr($g(sig(v)), "~", "")
		}

		k pismSe, nazevSe, sigSe
		s pocetSe = ..NactiSignatury(sku, lin, .sys, .sigSe, .pismSe, .nazevSe, "true")
		s vystup("souhrn~slouceno") = "variant se slucovanim="_pocetSe
		f v=1:1:pocetSe {
			s vystup("C~varianta~"_$tr($j(v,2)," ","0")) =
				"pismeno="_$s($g(pismSe(v))="":"(zadne)", 1:$g(pismSe(v)))_
				", zastaveni="_##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni($g(sigSe(v)))_
				", prubeh="_$tr($g(sigSe(v)), "~", "")
		}

		f a=1:1:pocetVar {
			s cilNovy = ..CilSlouceni(pocetVar, .sig, a, 0)
			s cilStary = ..CilSlouceni(pocetVar, .sig, a, 1)
			continue:(cilNovy=0)&&(cilStary=0)
			s vystup("D~slouceni~"_$tr($j(a,2)," ","0")) =
				"varianta "_a_" ("_$s($g(pism(a))="":"bez pismene", 1:$g(pism(a)))_")"_
				"  nove -> "_$s(cilNovy:cilNovy, 1:"zustava")_
				",  stare -> "_$s(cilStary:cilStary, 1:"zustava")
		}

		s klic = ""
		f {
			s klic = $o(vystup(klic))
			q:klic=""
			s ^||ML("OUT",klic) = vystup(klic)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Do které varianty by se `a` slila podle zvoleného pravidla; 0 = zůstává vlastním sloupcem.
/// Helper - volá se jen z metody, která catch má.
ClassMethod CilSlouceni(pocetVar As %Integer, ByRef sig As %String, a As %Integer, stare As %Boolean) As %Integer
{
	s cil = 0, cilStaveni = 0
	s staveniA = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni($g(sig(a)))
	f b=1:1:pocetVar {
		continue:b=a
		s staveniB = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni($g(sig(b)))
		continue:staveniB'>staveniA
		i stare {
			continue:'##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaProSrovnani($g(sig(a)), $g(sig(b)))
		}
		else {
			continue:'##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JeUsekemVarianty($g(sig(a)), $g(sig(b)))
		}
		i (cil = 0) || (staveniB > cilStaveni) s cil = b, cilStaveni = staveniB
	}
	q cil
}

/// Průběhy variant linky poskládané z `TRASA_MIN`. Vrací počet variant, `sig(i)` = signatura.
/// Helper - volá se jen z metody, která catch má.
ClassMethod NactiSignatury(sku As %String, lin As %String, ByRef sys As %String, ByRef sig As %String, ByRef pism As %String, ByRef nazev As %String, slucovat As %String = "false") As %Integer
{
	s pocet = 0
	try {
		k ^||ML("IN")
		s ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
		s ^||ML("IN","SLUCOVAT_PODMNOZINY") = slucovat
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")

		s key = $o(odpoved(""))
		i key = "ERR" s key = $o(odpoved(key))
		i key = "" ret 0
		s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
		s vt = page.values.%Get("VARIANTY_TRAS")
		i '$isobject(vt) ret 0
		s pocet = vt.%Size()
		f v=1:1:pocet { s pism(v) = vt.%Get(v - 1) }
		ret:pocet<2 pocet

		s zastavky = page.rows.%Get("ZASTAVKY")
		i '$isobject(zastavky) ret 0
		f v=1:1:pocet { s sig(v) = "" }
		f z=0:1:(zastavky.%Size() - 1) {
			s nazev(z + 1) = zastavky.%Get(z).%Get("NAZEV_ZASTAVKY")
			s tm = zastavky.%Get(z).%Get("TRASA_MIN")
			f v=1:1:pocet {
				s hodnota = $s($isobject(tm) && (v '> tm.%Size()):tm.%Get(v - 1), 1:"")
				s sig(v) = sig(v)_$s(sig(v)="":"", 1:"~")_$s($isvalidnum(hodnota):"S", 1:$g(hodnota))
			}
		}
	}
	catch (ex) { s pocet = 0 }
	q pocet
}

/// Kolik variant by se slilo do větší podle zvoleného pravidla (`stare` = 0 úsek, 1 podmnožina).
/// Helper - volá se jen z metody, která catch má.
ClassMethod PocetSlitych(pocetVar As %Integer, ByRef sig As %String, stare As %Boolean) As %Integer
{
	s slito = 0
	f a=1:1:pocetVar {
		s staveniA = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni($g(sig(a)))
		s nalezeno = 0
		f b=1:1:pocetVar {
			continue:b=a
			s staveniB = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni($g(sig(b)))
			continue:staveniB'>staveniA
			i stare {
				continue:'##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaProSrovnani($g(sig(a)), $g(sig(b)))
			}
			else {
				continue:'##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JeUsekemVarianty($g(sig(a)), $g(sig(b)))
			}
			s nalezeno = 1
			q
		}
		s slito = slito + nalezeno
	}
	q slito
}

/// Ve které databázi leží data linek - projde jmenné prostory serveru a nahlásí ty,
/// kde `^JRL` obsahuje skupiny linek. Bez toho nejde sonda zavolat na správná data,
/// protože `dataDB` se do volání předává zvenčí.
ClassMethod KdeJsouData(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		s rs = ##class(%ResultSet).%New("%SYS.Namespace:List")
		d rs.Execute()
		s poradi = 0
		while rs.Next() {
			s ns = rs.Get("Nsp")
			continue:ns=""
			s pocet = 0
			try {
				s s1 = ""
				f {
					s s1 = $o(^[ns]JRL(s1))
					q:s1=""
					continue:'$isvalidnum(s1)
					q:$o(^[ns]JRL(s1,""))=""
					s pocet = pocet + 1
				}
			}
			catch (vnitrni) { s pocet = 0 }
			continue:'pocet
			s poradi = poradi + 1
			s ^||ML("OUT","databaze~"_poradi) = ns_"  skupin linek="_pocet
		}
		s ^||ML("OUT","souhrn") = "databazi s linkami="_poradi
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kolik variantních sloupců má první stránka linky. 0 = linka nejde načíst nebo varianty nemá.
/// Helper - volá se jen z metody, která catch má.
ClassMethod PocetVariant(dataDB As %String, sku As %String, lin As %String, slucovat As %String, ByRef sys As %String) As %Integer
{
	s pocet = 0
	try {
		k ^||ML("IN")
		s ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
		s ^||ML("IN","SLUCOVAT_PODMNOZINY") = slucovat
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")
		i $g(odpoved("ERR")) '= "" ret 0

		s key = $o(odpoved(""))
		i key = "ERR" s key = $o(odpoved(key))
		i key = "" ret 0
		s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
		s vt = page.values.%Get("VARIANTY_TRAS")
		i $isobject(vt) s pocet = vt.%Size()
	}
	catch (ex) { s pocet = 0 }
	q pocet
}

}
