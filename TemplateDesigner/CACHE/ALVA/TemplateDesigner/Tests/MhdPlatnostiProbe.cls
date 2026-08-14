Include (%occInclude, GENERAL)

/// Read-only sonda nad MHD platnostmi spojů (`B84`): co reálně vyleze z kalendáře, když se
/// bitmapa spoje liší od bitmapy typu provozu. Odpovídá na dvě otázky:
///
///   1. **Které denní značky MHD vůbec produkuje.** `ALVA.MHD.KalendarFce.MakeRozpisTplProTypProvozuID`
///      plní `arr("arr",1,id)` jen pro `id` 1-7; „pracovní dny" rozepíše na 1-5 a „neděle a svátky"
///      na 7. Znaky `+` a `X` z vyhlášky 122/2014 tedy z MHD **nemůžou** vyjít.
///   2. **Jestli se týdenní platnost vůbec donese do tisku.** `PorovnatBitmapyPlatnosti` čte výstup
///      z proměnné, do které kalendář nezapisuje - sonda ukáže obojí vedle sebe.
///
/// Nic nemění (jen čte globály a volá kalendář).
Class ALVA.TemplateDesigner.Tests.MhdPlatnostiProbe
{

/// Pro linku projde spoje a u těch, kde se bitmapa spoje liší od bitmapy typu provozu, vypíše,
/// které dny kalendář vrátí a co z toho dnes agenda přečte.
/// IN: LINKA_ID (povinné), MAXSPOJ (default 40). Read-only.
ClassMethod PlatnostiLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set LinkaID = $get(^||ML("IN","LINKA_ID"))
		set maxSpoj = +$get(^||ML("IN","MAXSPOJ")) if maxSpoj < 1 set maxSpoj = 40
		kill ^||ML("OUT")
		if LinkaID = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID je povinné"))

		set autoPozn = $get(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju"))
		set ^||ML("OUT","A_LINKA") = "linka=" _ LinkaID _ " automatickePoznamkovaniPlatnosti=" _ +autoPozn

		set spoju = 0, rucnich = 0, odlisnych = 0, ukazek = 0
		kill znaky
		set SpojID = ""
		for {
			set SpojID = $order(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,LinkaID,SpojID))
			quit:SpojID=""
			quit:spoju'<maxSpoj
			set spoju = spoju + 1
			continue:'$get(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"RucniZadani"))
			set rucnich = rucnich + 1

			set TypProvozuID = $get(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"TypProvozuID"))
			set ProvozBitMapa = $get(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"ProvozBitMapa"))
			set bitmapSpoje = $piece(ProvozBitMapa,"~",3)
			kill arr
			set isok = ##class(ALVA.MHD.KalendarFce).GetJede(IdFa,dataDB,$piece(ProvozBitMapa,"~",1),$piece(ProvozBitMapa,"~",2),TypProvozuID,"","",.arr)
			continue:'isok
			set bitmapTypuProvozu = $listget($get(arr("dta",1)),3)
			kill arr
			continue:bitmapTypuProvozu=bitmapSpoje
			set odlisnych = odlisnych + 1

			// Rozpis dnů, jak ho vrátí kalendář (plní se DO vstupního pole, ne do druhého)
			kill arri
			set arri("dta",1) = $piece(ProvozBitMapa,"~",1)
			set arri("dta",2) = $piece(ProvozBitMapa,"~",2)
			set arri("dta",3) = bitmapSpoje
			set arri("dta",4) = bitmapTypuProvozu
			set isok = ##class(ALVA.MHD.KalendarFce).MakeRozpisTplProTypProvozuID(IdFa,dataDB,TypProvozuID,.arri)
			continue:'isok

			// Které klíče rozpisu vůbec existují (odpověď na „může vyjít + nebo X?")
			set klice = "", id = ""
			for {
				set id = $order(arri("arr",1,id))
				quit:id=""
				set klice = klice _ $select(klice="":"", 1:",") _ id
			}
			set znaky(klice) = $get(znaky(klice)) + 1

			// Dny, které z rozpisu reálně vycházejí (bitmapa daného dne obsahuje aspoň jednu jedničku)
			set dny = ""
			for id = 1:1:7 {
				set bmp = $get(arri("arr",1,id))
				continue:'$find(bmp,1)
				set dny = dny _ id
			}
			continue:ukazek'<8
			set ukazek = ukazek + 1
			set ^||ML("OUT","C_SPOJ",ukazek) = "spoj=" _ SpojID _ " typProvozu=" _ TypProvozuID _ " dnyZKalendare=""" _ dny _ """ klice=""" _ klice _ """"
		}

		set ^||ML("OUT","B_SOUHRN") = "spoju=" _ spoju _ " sRucnimZadanim=" _ rucnich _ " sOdlisnouBitmapou=" _ odlisnych
		set k = "", n = 0
		for {
			set k = $order(znaky(k))
			quit:k=""
			set n = n + 1
			set ^||ML("OUT","D_KLICE",n) = "rozpis vraci klice [" _ k _ "] x" _ znaky(k)
		}
	}
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

/// Které linky firmy mají zapnuté automatické poznámkování odlišných platností - tedy kde se
/// větev denních značek vůbec spouští. IN: MAXLIN (default 300). Read-only.
ClassMethod LinkySAutoPoznamkami(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 300
		kill ^||ML("OUT")

		set linek = 0, zapnutych = 0, ukazek = 0, LinkaID = ""
		for {
			set LinkaID = $order(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID))
			quit:LinkaID=""
			quit:linek'<maxLin
			set linek = linek + 1
			continue:'$get(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju"))
			set zapnutych = zapnutych + 1
			continue:ukazek'<10
			set ukazek = ukazek + 1
			set ^||ML("OUT","B_LINKA",ukazek) = "linka=" _ LinkaID _ " cislo=" _ $get(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"Cislo"))
		}
		set ^||ML("OUT","A_SOUHRN") = "firma=" _ IdFa _ " linek=" _ linek _ " sAutomatickymPoznamkovanim=" _ zapnutych
	}
	catch (ex) { kill ^||ML("OUT")  $$$CatchErrRest }
	ret $$$OK
}

}
