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
/// žádné datum. Linkový JŘ AD větu vytiskne i bez data, vývěska AD ji přeskočí. OUT: souhrn, priklad~n.
ClassMethod OmezeniBezDat(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML
		set agenda = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
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

}
