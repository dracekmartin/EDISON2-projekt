Include (%occInclude, GENERAL)

/// Read-only sonda AD dat (#13048 bod 4): projde W-trasy linek v `^JRL` a hledá per-spoj
/// nástupiště (W-segment pozice 7 = legacy `trs(8)`, „nástupiště ze spoje" JRapiExp10:1223;
/// trs indexy jsou od W pozic posunuté o 1 - čas/čekání se rozpadá, JRapiSp:234-241). Hlásí:
/// kolik linek pole 8 vůbec plní, a zastávky, kde se na jedné lince×směru liší hodnoty
/// mezi trasami (= kandidát pro režimy „více nástupišť"). Nic nemění.
Class ALVA.TemplateDesigner.Tests.AdWTrasaProbe
{

/// IN: DB (namespace), MAXLIN (volitelně, default 500). OUT: souhrn + MULTI(n) nálezy.
ClassMethod ScanNastupiste(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linekCelkem = 0, linekSPoli8 = 0, multiN = 0
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linekCelkem>=maxLin
				set linekCelkem = linekCelkem + 1

				// per (poradi) posbírej distinct hodnoty pole 8 napříč trasami linky
				kill hodnoty
				set maPole8 = 0
				set ct = ""
				for {
					set ct = $order(^[db]JRL(sku,lin,"W",ct))
					quit:ct=""
					set w = $get(^[db]JRL(sku,lin,"W",ct))
					for i = 1:1:$length(w,"#") {
						set seg = $piece(w,"#",i)
						set poradi = $piece(seg,":",1)
						continue:poradi=""
						set nast = $piece(seg,":",7)
						continue:nast=""
						set maPole8 = 1
						set hodnoty(poradi,nast) = $get(hodnoty(poradi,nast)) + 1
					}
				}
				if maPole8 {
					set linekSPoli8 = linekSPoli8 + 1
					if linekSPoli8 <= 20 {
						// příklad první vyplněné hodnoty pro rychlou orientaci
						set pPor = $order(hodnoty("")), pNast = $select(pPor'="":$order(hodnoty(pPor,"")), 1:"")
						set ^||ML("OUT","POLE8",$translate($justify(linekSPoli8,3)," ","0")) = db_" sku="_sku_" lin="_lin_" napr. poradi="_pPor_" nastupiste="_pNast
					}
				}

				// zastávky s víc než jednou hodnotou
				set poradi = ""
				for {
					set poradi = $order(hodnoty(poradi))
					quit:poradi=""
					set pocetHodnot = 0, vypis = ""
					set nast = ""
					for {
						set nast = $order(hodnoty(poradi,nast))
						quit:nast=""
						set pocetHodnot = pocetHodnot + 1
						set vypis = vypis_$select(vypis="":"",1:", ")_nast_"("_hodnoty(poradi,nast)_"x)"
					}
					if pocetHodnot > 1 {
						set multiN = multiN + 1
						if multiN <= 40 {
							set ^||ML("OUT","MULTI",$translate($justify(multiN,3)," ","0")) = db_" sku="_sku_" lin="_lin_" poradi="_poradi_" hodnoty: "_vypis
						}
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_": linek prohledano="_linekCelkem_" | s per-spoj nastupistem (pole 8)="_linekSPoli8_" | zastavek s VICE hodnotami="_multiN
	} catch (ex) { $$$CatchErrRest }
	quit $$$OK
}

}
