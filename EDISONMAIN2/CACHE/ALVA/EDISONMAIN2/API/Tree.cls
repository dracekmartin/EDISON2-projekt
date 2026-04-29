Include GENERAL

Class ALVA.EDISONMAIN2.API.Tree Extends %RegisteredObject
{

/// Vrátí strom modulů hlavního okna.
/// Formát položek v ^||ML("OUT", i) jako CList ($lb):
///   $lb("FAMILY", nazev, expanded)                         ... složka (expanded: "1"=rozbalena, "0"=sbalena)
///   $lb("EXE", nazev, exe, form, elementId, licensed)      ... spustitelný modul
///                                                              elementId  = unikátní identifikátor (numerické ID
///                                                                           z EDISFormEnum / EDPackageEnum jako
///                                                                           string); zachovává se i u nelicensovaných
///                                                                           položek pro budoucí použití
///                                                              licensed   = "1" → kontroluje se proti Lic.Elements
///                                                                           "0" → vždy zobrazeno bez ohledu na licenci
///   $lb("FAMILY_END")                                      ... konec složky
///
/// Položky pod MHD odpovídají starému EDISON XML stromu (edisontree.xml).
/// Top-level moduly mají v původním XML licensed="1" → respektují licenci;
/// položky pod sub-family Servis a Nastavení a číselníky neměly licensed atribut
/// → vždy zobrazeno (uvnitř MHD modulu).
ClassMethod GetTree(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams
		s i=0

		s ^||ML("OUT",$i(i))=$lb("FAMILY","MHD","1")

		// Top-level moduly — licensed="1"
		// 230009 = formMHD2LinkyHlavniOkno
		s ^||ML("OUT",$i(i))=$lb("EXE","Linky a oběhy","MHD2Linky.exe","ALVA.MHD2.Linky.formMHD2LinkyHlavniOkno","230009","1")
		// 260002 = formMHD2PlanDopravyDispecink
		s ^||ML("OUT",$i(i))=$lb("EXE","Dispečink","MHD2PlanDopravy.exe","ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyDispecink","260002","1")
		// 260005 = formMHD2PlanDopravyHromadneUpravyDokladu
		s ^||ML("OUT",$i(i))=$lb("EXE","Hromadné úpravy dokladů","MHD2PlanDopravy.exe","ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyHromadneUpravyDokladu","260005","1")
		// 230005 = formMHD2LinkySkupinyChronometrazi
		s ^||ML("OUT",$i(i))=$lb("EXE","Chronometráže","MHD2Linky.exe","ALVA.MHD2.Linky.formMHD2LinkySkupinyChronometrazi","230005","1")
		// 131018 = Importy (mimo MHD2 package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Importy","","","131018","1")
		// 250001 = Jízda bez nehod (samostatný package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Jízda bez nehod","","","250001","1")
		// 165001 = Oběhy a optimalizace (samostatný package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Oběhy a optimalizace","","","165001","1")
		// 260003 = formMHD2PlanDopravyObjednavky
		s ^||ML("OUT",$i(i))=$lb("EXE","Objednávky","MHD2PlanDopravy.exe","ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyObjednavky","260003","1")
		// 260001 = formMHD2PlanDopravyMain
		s ^||ML("OUT",$i(i))=$lb("EXE","Plán dopravy","MHD2PlanDopravy.exe","ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyMain","260001","1")
		// 241001 = formMHD2RidiciList
		s ^||ML("OUT",$i(i))=$lb("EXE","Řidiči","MHD2Ridici.exe","ALVA.MHD2.Ridici.formMHD2RidiciList","241001","1")
		// 241002 = formMHD2RidiciPremie
		s ^||ML("OUT",$i(i))=$lb("EXE","Řidiči - prémie","MHD2Ridici.exe","ALVA.MHD2.Ridici.formMHD2RidiciPremie","241002","1")
		// 241004 = formMHD2RidiciDlouhodobeNepritomnosti
		s ^||ML("OUT",$i(i))=$lb("EXE","Řidiči - dlouhodobé nepřítomnosti","MHD2Ridici.exe","ALVA.MHD2.Ridici.formMHD2RidiciDlouhodobeNepritomnosti","241004","1")
		// 230002 = formMHD2LinkySmeny
		s ^||ML("OUT",$i(i))=$lb("EXE","Směny","MHD2Linky.exe","ALVA.MHD2.Linky.formMHD2LinkySmeny","230002","1")
		// 241003 = formMHD2RidiciStavyElektromeru
		s ^||ML("OUT",$i(i))=$lb("EXE","Stavy elektroměrů","MHD2Ridici.exe","ALVA.MHD2.Ridici.formMHD2RidiciStavyElektromeru","241003","1")
		// 230008 = formMHD2LinkyTurnusoveSkupiny
		s ^||ML("OUT",$i(i))=$lb("EXE","Turnusové skupiny","MHD2Linky.exe","ALVA.MHD2.Linky.formMHD2LinkyTurnusoveSkupiny","230008","1")
		// 131017 = Výstupy (mimo MHD2 package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Výstupy","","","131017","1")
		// 210011 = formMHD2CiselnikyZastavky
		s ^||ML("OUT",$i(i))=$lb("EXE","Zastávky","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyZastavky","210011","1")

		// Servis sub-family — položky v originálním XML neměly licensed atribut → licensed="0"
		s ^||ML("OUT",$i(i))=$lb("FAMILY","Servis","0")
		// 210022 = formMHD2CiselnikyPrechodNaObdobi
		s ^||ML("OUT",$i(i))=$lb("EXE","Přechod na období","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyPrechodNaObdobi","210022","0")
		s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

		// Nastavení a číselníky sub-family — položky v originálním XML neměly licensed atribut → licensed="0"
		s ^||ML("OUT",$i(i))=$lb("FAMILY","Nastavení a číselníky","0")
		// 210012 = formMHD2CiselnikyNastaveni
		s ^||ML("OUT",$i(i))=$lb("EXE","Nastavení","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyNastaveni","210012","0")
		// 210001 = formMHD2CiselnikyCile
		s ^||ML("OUT",$i(i))=$lb("EXE","Cíle","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyCile","210001","0")
		// 66001 = Číselné řady (samostatný package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Číselné řady","","","66001","0")
		// 210020 = formMHD2CiselnikyDruhyUsecek
		s ^||ML("OUT",$i(i))=$lb("EXE","Druhy úseček, činnosti","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyDruhyUsecek","210020","0")
		// 210008 = formMHD2CiselnikyDuvodyPreobsazeniVozu
		s ^||ML("OUT",$i(i))=$lb("EXE","Důvody přeobsazení vozu","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyDuvodyPreobsazeniVozu","210008","0")
		// 146019 = Fiktivní řidiči (v původním XML licensed="0" — explicitně vždy zobrazeno)
		s ^||ML("OUT",$i(i))=$lb("EXE","Fiktivní řidiči","","","146019","0")
		// 210002 = formMHD2CiselnikyIDS
		s ^||ML("OUT",$i(i))=$lb("EXE","IDS","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyIDS","210002","0")
		// 210017 = formMHD2CiselnikyZalohaDat (XML name "Import a export dat")
		s ^||ML("OUT",$i(i))=$lb("EXE","Import a export dat","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyZalohaDat","210017","0")
		// 210009 = formMHD2CiselnikyKalendare
		s ^||ML("OUT",$i(i))=$lb("EXE","Kalendáře","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyKalendare","210009","0")
		// 210023 = formMHD2CiselnikyKategorieVD
		s ^||ML("OUT",$i(i))=$lb("EXE","Kategorie výlukové dopravy","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyKategorieVD","210023","0")
		// 76002 = Návrhář sestav JŘ (samostatný package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Návrhář sestav JŘ","","","76002","0")
		// 70003 = Návrhář sestav JŘ MHD (samostatný package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Návrhář sestav JŘ MHD","","","70003","0")
		// 210010 = formMHD2CiselnikyPoznamky
		s ^||ML("OUT",$i(i))=$lb("EXE","Poznámky","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyPoznamky","210010","0")
		// 210007 = formMHD2CiselnikyPracoviste
		s ^||ML("OUT",$i(i))=$lb("EXE","Pracoviště","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyPracoviste","210007","0")
		// 163011 = Přepravní systémy (samostatný package)
		s ^||ML("OUT",$i(i))=$lb("EXE","Přepravní systémy","","","163011","0")
		// 210024 = formMHD2CiselnikyPriplatky
		s ^||ML("OUT",$i(i))=$lb("EXE","Příplatky","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyPriplatky","210024","0")
		// 210003 = formMHD2CiselnikySkupinyLinek
		s ^||ML("OUT",$i(i))=$lb("EXE","Skupiny linek","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikySkupinyLinek","210003","0")
		// 210021 = formMHD2CiselnikyTypyListku
		s ^||ML("OUT",$i(i))=$lb("EXE","Typy lístků","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyTypyListku","210021","0")
		// 210005 = formMHD2CiselnikyTypyProvozu
		s ^||ML("OUT",$i(i))=$lb("EXE","Typy provozů","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyTypyProvozu","210005","0")
		// 210013 = formMHD2CiselnikyTypySkupin
		s ^||ML("OUT",$i(i))=$lb("EXE","Typy skupin","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyTypySkupin","210013","0")
		// 210006 = formMHD2CiselnikyVariantyChronometrazi
		s ^||ML("OUT",$i(i))=$lb("EXE","Varianty chronometráží","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyVariantyChronometrazi","210006","0")
		// 210004 = formMHD2CiselnikyZony
		s ^||ML("OUT",$i(i))=$lb("EXE","Zóny","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyZony","210004","0")
		// 210019 = formMHD2CiselnikyZpravyProPP
		s ^||ML("OUT",$i(i))=$lb("EXE","Zprávy pro palubní počítač","MHD2Ciselniky.exe","ALVA.MHD2.Ciselniky.formMHD2CiselnikyZpravyProPP","210019","0")
		s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

		s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

}
