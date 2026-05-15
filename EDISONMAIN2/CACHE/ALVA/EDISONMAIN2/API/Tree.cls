Include GENERAL

Class ALVA.EDISONMAIN2.API.Tree Extends %RegisteredObject
{

/// Vrátí strom modulů hlavního okna.
///
/// Vstup (CVar):
///   "tree" = "main" | "system" | "partners"   (volitelné, default "main")
///                                              "main"     → Hlavní nabídka (modulární agenda)
///                                              "system"   → Správa systému (admin nástroje)
///                                              "partners" → Partnerská řešení (3rd party)
///
/// Formát položek v ^||ML("OUT", i) jako CList ($lb):
///   $lb("FAMILY", nazev, expanded)                         ... složka (expanded: "1"=rozbalena, "0"=sbalena)
///   $lb("EXE", nazev, exe, form, elementId, licensed, inputParam) ... spustitelný modul
///                                                              elementId  = unikátní identifikátor (numerické ID
///                                                                           z EDISFormEnum / EDPackageEnum jako
///                                                                           string); zachovává se i u nelicensovaných
///                                                                           položek pro budoucí použití
///                                                              licensed   = "1" → kontroluje se proti Lic.Elements
///                                                                           "0" → vždy zobrazeno bez ohledu na licenci
///                                                              inputParam = volitelný string předaný formě jako EDParamIn
///                                                                           (např. "AdminMode=1" — legacy navbar input atribut).
///                                                                           Default "" = forma použije svůj default.
///   $lb("FAMILY_END")                                      ... konec složky
///
/// Tři stromy odpovídají top-level uzlům v původním edisonnavbar.xml
/// (id=-2 Hlavní nabídka, id=-3 Správa systému, id=-4 Partnerská řešení).
ClassMethod GetTree(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams

		// Default tree = "main" (zachová zpětnou kompatibilitu s původním voláním bez parametru).
		s tree = $g(^||ML("IN","tree"))
		i tree="" s tree="main"

		i tree="main" d ..FillMain()
		e  i tree="system" d ..FillSystem()
		e  i tree="partners" d ..FillPartners()
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

/// Naplní strom "Hlavní nabídka" (modulární agenda).
///
/// PILOT SCOPE LIMITATION: Currently exposes only the MHD module (id=45 v legacy navbar -2)
/// with its complete sub-tree (Servis, Nastavení a číselníky). Legacy navbar -2 references
/// 29 active top-level modules; 28 (Autobusová doprava, Ekonomický systém, JŘ JIRA, Mzdy,
/// Lístky, Reporting, …) are intentionally omitted for pilot. Post-pilot direction is to
/// parse edisontree.xml directly (server-side) or migrate XML → Caché global — single source
/// of truth, eliminates this hardcoded duplication. See MVP_PLAN.md.
///
/// Top-level MHD moduly mají v původním XML licensed="1" → respektují licenci;
/// položky pod sub-family Servis a Nastavení a číselníky neměly licensed atribut
/// → vždy zobrazeno (uvnitř MHD modulu).
ClassMethod FillMain() [ Private ]
{
	s i=$g(^||ML("OUT"))

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

/// Naplní strom "Správa systému" (admin nástroje).
/// Odpovídá uzlu id=-3 v původním edisonnavbar.xml. Licensing per uzel z edisontree.xml
/// (legacy ApplicationTree.ToXmlEx kompozice navbar + tree).
ClassMethod FillSystem() [ Private ]
{
	s i=$g(^||ML("OUT"))

	// Společná evidence (id=0) — FAMILY v edisontree.xml, licensed="1" na úrovni FAMILY (legacy gate).
	// Naše wire FAMILY licensing nenese → spoléháme na auto-hide kdykoli jsou všechny děti odfiltrované.
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Společná evidence","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","EDISON","","","2002","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Organizační struktura","","","147002","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Uživatelé","","","5001","0")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// Kmenová evidence (id=1) — inline FAMILY v edisonnavbar.xml, licensed="1" na FAMILY úrovni.
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Kmenová evidence","0")
	// 47003 = Archiv dokumentů (edisontree licensed="1")
	s ^||ML("OUT",$i(i))=$lb("EXE","Archiv dokumentů","","","47003","1")
	// 36008 = Návrhář sestav (edisontree bez licensed = vždy zobrazeno)
	s ^||ML("OUT",$i(i))=$lb("EXE","Návrhář sestav","","","36008","0")
	// 149002 = Online aplikace (inline navbar override jména, bez licensing)
	s ^||ML("OUT",$i(i))=$lb("EXE","Online aplikace","","","149002","0")
	// 149003 = Plánované výstupy
	s ^||ML("OUT",$i(i))=$lb("EXE","Plánované výstupy","","","149003","0")
	// 152001 = Služby Rest
	s ^||ML("OUT",$i(i))=$lb("EXE","Služby Rest","","","152001","0")
	// 12001 = Správce stromů
	s ^||ML("OUT",$i(i))=$lb("EXE","Správce stromů","","","12001","0")
	// 40001 = Správce úloh
	s ^||ML("OUT",$i(i))=$lb("EXE","Správce úloh","","","40001","0")
	// 154001 = Události
	s ^||ML("OUT",$i(i))=$lb("EXE","Události","","","154001","0")
	// 2011 = Úlohy na pozadí v admin režimu (inline navbar input="AdminMode=1" → 7. arg).
	// EDISONMAIN.exe je retrofitnuté; formEDISONMAINTaskList parsuje EDParamIn jako string "AdminMode=1".
	s ^||ML("OUT",$i(i))=$lb("EXE","Úlohy na pozadí","EDISONMAIN.exe","ALVA.EDISONMAIN.formEDISONMAINTaskList","2011","0","AdminMode=1")
	// 272001 = Word šablony (inline navbar)
	s ^||ML("OUT",$i(i))=$lb("EXE","Word šablony","","","272001","0")
	// 1.2 = Nastavení a číselníky — sub-FAMILY v edisontree.xml
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Nastavení a číselníky","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Kurzy a měny","","","50004","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Účetní případy","","","66008","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Zahraniční stravné","","","147001","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Způsob zaokrouhlování","","","3014","0")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// Systémové nástroje (id=5) — FAMILY v edisontree.xml, licensed="1" na FAMILY úrovni.
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Systémové nástroje","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Audit systému","","","57001","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","MultiServer","","","48001","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
}

/// Naplní strom "Partnerská řešení" (3rd party agendy).
/// Odpovídá uzlu id=-4 v původním edisonnavbar.xml.
/// Všechny FAMILY mají licensed="1" v edisontree.xml; jednotlivé leaves také.
ClassMethod FillPartners() [ Private ]
{
	s i=$g(^||ML("OUT"))

	// AURIS (inline FAMILY v navbar, id=30; leaf 38001 z edisontree)
	s ^||ML("OUT",$i(i))=$lb("FAMILY","AURIS","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","AURIS","","","38001","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// BC Dopravní systémy (inline FAMILY v navbar, id=31)
	s ^||ML("OUT",$i(i))=$lb("FAMILY","BC Dopravní systémy","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Cena jízdného","","","61001","1")
	s ^||ML("OUT",$i(i))=$lb("EXE","Clearing","","","58001","1")
	s ^||ML("OUT",$i(i))=$lb("EXE","Intenzita provozu","","","79001","1")
	// 98001 = "Přestupy" v edisontree.xml (legacy expanze nahrazuje jméno).
	s ^||ML("OUT",$i(i))=$lb("EXE","Přestupy","","","98001","1")
	// 19 = OPTIM1 — sub-FAMILY v edisontree.xml
	s ^||ML("OUT",$i(i))=$lb("FAMILY","OPTIM1","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Optimalizace","","","60001","1")
	s ^||ML("OUT",$i(i))=$lb("EXE","Statistické výstupy","","","60006","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
	s ^||ML("OUT",$i(i))=$lb("EXE","Standard","","","59001","1")
	// 26 = Výběrová řízení — sub-FAMILY v edisontree.xml
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Výběrová řízení","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Výběrová řízení","","","77001","1")
	s ^||ML("OUT",$i(i))=$lb("EXE","Zadávací dokumentace","","","86001","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// EmTest (inline FAMILY, id=56)
	s ^||ML("OUT",$i(i))=$lb("FAMILY","EmTest","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","EmTest","","","95003","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// FSSOFT (inline FAMILY, id=32)
	s ^||ML("OUT",$i(i))=$lb("FAMILY","FSSOFT","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","FSSOFT","","","87001","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// CHAPS (inline FAMILY, id=33; leaf 95001 = "Export plánu" v edisontree.xml)
	s ^||ML("OUT",$i(i))=$lb("FAMILY","CHAPS","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Export plánu","","","95001","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// Telmax (id=21) — top-level v navbar, ale FAMILY v edisontree.xml
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Telmax","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Import a Export","","","62001","1")
	s ^||ML("OUT",$i(i))=$lb("EXE","Transakce","","","62012","1")
	// 21.1 = Nastavení a číselníky (Telmax sub-FAMILY)
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Nastavení a číselníky","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Formuláře","","","62004","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Karty","","","62007","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Kategorie","","","62010","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Kategorie (nový)","","","274002","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Měny","","","62009","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Měny (nový)","","","274001","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Nastavení","","","62017","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Oblasti","","","62006","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Parametry","","","62003","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Parametry (nový)","","","274004","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Povolené karty","","","62015","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Profily tarifu","","","62013","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Profily zákazníka","","","62014","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Stránky uživatelského rozhraní","","","62002","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Svátky","","","62016","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Svátky (nový)","","","274005","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Tabla","","","62008","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Tabla (nový)","","","274003","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Tarify","","","62011","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Zakázané karty","","","62005","0")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
	// 21.2 = Export a import (Telmax sub-FAMILY)
	s ^||ML("OUT",$i(i))=$lb("FAMILY","Export a import","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Export","","","62019","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Import","","","62020","0")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// T-MAPY (id=44) — FAMILY v edisontree.xml
	s ^||ML("OUT",$i(i))=$lb("FAMILY","T-MAPY","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","Export pro dispečink","","","95002","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")

	// TRANSDEV (id=64) — FAMILY v edisontree.xml s KARAT uvnitř
	s ^||ML("OUT",$i(i))=$lb("FAMILY","TRANSDEV","0")
	s ^||ML("OUT",$i(i))=$lb("EXE","KARAT","","","200001","1")
	s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
}

}
