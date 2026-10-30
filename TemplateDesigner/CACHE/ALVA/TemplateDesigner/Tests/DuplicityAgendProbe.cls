Include (%occInclude, GENERAL)

/// Sonda k `B194g` R2 (kód agend opsaný pod jinými jmény): kolik linek reálně narazí na místa, kde se
/// kopie liší. Diagnostika přes `cache_query_cvar`, ne test - výsledek závisí na datech dev serveru.
Class ALVA.TemplateDesigner.Tests.DuplicityAgendProbe Extends %RegisteredObject
{

Parameter FIRMY = "CB_CSADAD,CB_GWBUS,KV_DP,VS_CSAD,ZL_KOVED,SKNI_MHDNITRA";

/// Firmy s daty MHD (číselník poznámek ke spojům).
Parameter FIRMYMHD = "CB_DP,KV_DP";

/// Platnost linky s příponou za dvojtečkou (`PATCH,"Plati"` = „od~do:…"). Kopie, které příponu
/// neodříznou, ji pošlou v datu „do". OUT: souhrn, firma~db, priklad~n.
ClassMethod PlatnostSPriponou(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML, pocet
		set linek = 0, sPriponou = 0, prikladu = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					continue:'$data(^[db]JRL(sku, lin, "I"))
					set linek = linek + 1
					set plati = $get(^[db]JRL(sku, lin, "PATCH", "Plati"))
					continue:plati'[":"
					set sPriponou = sPriponou + 1, pocet(db) = $get(pocet(db)) + 1
					continue:prikladu'<10
					set prikladu = prikladu + 1
					set ^||ML("OUT", "priklad~"_prikladu) = db_" "_sku_"/"_lin_" Plati="_plati
				}
			}
		}
		set ^||ML("OUT", "souhrn") = "linek="_linek_" sPriponou="_sPriponou
		set db = ""
		for {
			set db = $order(pocet(db))
			quit:db=""
			set ^||ML("OUT", "firma~"_db) = pocet(db)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Spoje s omezením 14 / 15 („jede jen v sudých / lichých týdnech"), u kterých z omezení nevyjde
/// žádné datum. Linkový JŘ AD větu vytiskl i bez data, vývěska AD ji do 30. 10. přeskočila (`B56` S8
/// sjednotil podle linkového). OUT: souhrn, priklad~n.
ClassMethod OmezeniBezDat(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML
		set agenda = "ALVA.TemplateDesigner.Agendy.Jr.DataAd"
		set sOmezenim = 0, bezDat = 0, prikladu = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					continue:'$data(^[db]JRL(sku, lin, "I"))
					set tiskDtm = $classmethod(agenda, "TiskDtm", db, sku, lin)
					for smer = "T", "Z" {
						set sp = ""
						for {
							set sp = $order(^[db]JRL(sku, lin, smer, sp))
							quit:sp=""
							continue:'$isvalidnum(sp)
							for omzId = 14, 15 {
								set omz = $get(^[db]JRL(sku, lin, smer, sp, omzId))
								continue:omz=""
								set sOmezenim = sOmezenim + 1
								continue:$classmethod(agenda, "FormatDatumyOmezeni", omz, tiskDtm)'=""
								set bezDat = bezDat + 1
								continue:prikladu'<10
								set prikladu = prikladu + 1
								set ^||ML("OUT", "priklad~"_prikladu) = db_" "_sku_"/"_lin_" "_smer_" spoj "_sp_" omezeni "_omzId_" = """_omz_""""
							}
						}
					}
				}
			}
		}
		set ^||ML("OUT", "souhrn") = "omezeni14a15="_sOmezenim_" bezDat="_bezDat
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Černá „0;0;0" v barvách: barva spoje AD (`#`-pole 12 spoje; vývěska i linkový ji berou jako
/// nenastavenou) a barvy poznámek ke spojům MHD (vývěska MHD ji bere jako černou). OUT: ad~…, mhd~db~pole.
ClassMethod CernaBarva(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML
		set spoju = 0, sBarvou = 0, cernych = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					continue:'$data(^[db]JRL(sku, lin, "I"))
					for smer = "T", "Z" {
						set sp = ""
						for {
							set sp = $order(^[db]JRL(sku, lin, smer, sp))
							quit:sp=""
							continue:'$isvalidnum(sp)
							set spoju = spoju + 1
							set barva = $piece($get(^[db]JRL(sku, lin, smer, sp)), "#", 12)
							continue:barva=""
							set sBarvou = sBarvou + 1
							set:barva="0;0;0" cernych = cernych + 1
						}
					}
				}
			}
		}
		set ^||ML("OUT", "ad") = "spoju="_spoju_" sBarvou="_sBarvou_" cerna0;0;0="_cernych
		for fi = 1:1:$length(..#FIRMYMHD, ",") {
			set db = $piece(..#FIRMYMHD, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			for pole = "TiskJRMHDVozoveBarvaZnakuRGB", "TiskJRMHDVozoveBarvaPozadiRGB" {
				set poznamek = 0, sBarvou = 0, cernych = 0, id = ""
				for {
					set id = $order(^[db]ALVA.MHD.ENUMS(db, "PoznamkaSpoj", "DTA", id))
					quit:id=""
					set poznamek = poznamek + 1
					set barva = $get(^[db]ALVA.MHD.ENUMS(db, "PoznamkaSpoj", "DTA", id, pole))
					continue:barva=""
					set sBarvou = sBarvou + 1
					set:barva="0;0;0" cernych = cernych + 1
				}
				set ^||ML("OUT", "mhd~"_db_"~"_pole) = "poznamek="_poznamek_" sBarvou="_sBarvou_" cerna0;0;0="_cernych
			}
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Tři převody data platnosti linky na `$h` (`B56` S8): `ALVA.GENERALAPI.Date.DateToHor` (začátek
/// platnosti pro zastávky k datu), kontrola tvaru + `DatumNaHorolog` (období sloupců vývěsky AD)
/// a `$zdateh` v `DatumNaDh` (turnusy linkového JŘ AD). Vyjdou na reálných datech stejně?
/// OUT: souhrn, tvar~vzor = počet, rozdil~n = příklad.
ClassMethod PrevodyPlatnosti(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML, tvary
		set zastavkovy = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
		set linkovy = "ALVA.TemplateDesigner.Agendy.LinkovyJrAd"
		set dat = 0, rozdilu = 0, prazdnych = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					continue:'$data(^[db]JRL(sku, lin, "I"))
					set plati = $piece($get(^[db]JRL(sku, lin, "PATCH", "Plati")), ":", 1)
					for kus = 1, 2 {
						set text = $piece(plati, "~", kus)
						if text = "" { set prazdnych = prazdnych + 1  continue }
						set dat = dat + 1
						set vzor = $translate(text, "0123456789", "9999999999")
						set tvary(vzor) = $get(tvary(vzor)) + 1
						set a = ""
						try { set a = ##class(ALVA.GENERALAPI.Date).DateToHor(text) } catch (ex2) { set a = "" }
						set b = ""
						if $classmethod(zastavkovy, "JeDatumTvar", text) set b = $classmethod(zastavkovy, "DatumNaHorolog", text, $piece(text, ".", 3))
						set c = $classmethod(linkovy, "DatumNaDh", text)
						continue:(a=b)&&(b=c)
						set rozdilu = rozdilu + 1
						continue:rozdilu>20
						set ^||ML("OUT", "rozdil~"_rozdilu) = db_" "_sku_"/"_lin_" "_text_" DateToHor="_a_" DatumNaHorolog="_b_" DatumNaDh="_c
					}
				}
			}
		}
		set ^||ML("OUT", "souhrn") = "dat="_dat_" prazdnych="_prazdnych_" rozdilu="_rozdilu
		set vzor = ""
		for {
			set vzor = $order(tvary(vzor))
			quit:vzor=""
			set ^||ML("OUT", "tvar~"_vzor) = tvary(vzor)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
