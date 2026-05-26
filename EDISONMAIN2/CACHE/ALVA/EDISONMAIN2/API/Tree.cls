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
/// Formát položek v ^||ML("OUT", i) jako CList ($lb). Konstruují je helpery
/// <see cref="AddFamily">..AddFamily</see>, <see cref="AddFamilyEnd">..AddFamilyEnd</see>
/// a <see cref="AddExe">..AddExe</see>; přímý $lb se v Fill* metodách už nepoužívá.
///
///   $lb("FAMILY", nazev, expanded)                                          ... složka
///   $lb("EXE", nazev, exe, form, elementId, licensed, inputParam, hidden, color) ... spustitelný modul
///       elementId  = numerické ID z EDISFormEnum / EDPackageEnum jako string;
///                    zachovává se i u nelicensovaných položek (licensed="0").
///       licensed   = "1" → kontroluje se proti Lic.Elements; "0" → vždy zobrazeno.
///       inputParam = volitelný string předaný formě jako EDParamIn (např. "AdminMode=1").
///       hidden     = "1" → modul se odfiltruje v UI (legacy `hidden="1"` atribut).
///       color      = hex "#RRGGBB" pro Foreground v stromu; "" = výchozí barva tématu.
///                    Konvence: "#FFA500" (oranžová) = modul je stub bez retrofitnuté .exe.
///   $lb("FAMILY_END")                                                       ... konec složky
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

/// Helper — přidá FAMILY uzel (složka). isExpanded=1 → strom defaultně rozbalený.
ClassMethod AddFamily(name As %String, isExpanded As %Boolean = 0) [ Private ]
{
	s ^||ML("OUT", $i(^||ML("OUT"))) = $lb("FAMILY", name, $s(isExpanded:"1", 1:"0"))
}

/// Helper — uzavře FAMILY uzel.
ClassMethod AddFamilyEnd() [ Private ]
{
	s ^||ML("OUT", $i(^||ML("OUT"))) = $lb("FAMILY_END")
}

/// Helper — přidá EXE uzel (modul). Viz wire format v doc GetTree pro popis polí.
ClassMethod AddExe(name As %String, exePath As %String, formName As %String, elementId As %String, isLicensed As %Boolean = 1, inputParam As %String = "", hidden As %Boolean = 0, color As %String = "") [ Private ]
{
	s ^||ML("OUT", $i(^||ML("OUT"))) = $lb("EXE", name, exePath, formName, elementId, $s(isLicensed:"1", 1:"0"), inputParam, $s(hidden:"1", 1:"0"), color)
}

/// Naplní strom "Hlavní nabídka" (modulární agenda).
///
/// Top-level layout odpovídá legacy `edisonnavbar.xml` id=-2 (29 aktivních agend, abecedně).
/// MHD má kompletní sub-tree (Servis, Nastavení a číselníky); ostatních 28 agend jsou
/// **stuby** — pouze top-level placeholder s name + elementId, prázdné exe/form, orange color
/// (#FFA500) per konvenci v PLAN.md (= "modul nemá retrofitnutou .exe"). Sub-tree každé
/// stub agendy přijde až s jejím portováním. `licensed="0"` pro stuby v dev prostředí, ať
/// jsou vidět bez ohledu na licenci; v produkci se nastaví "1" jakmile bude agenda připravená.
///
/// MHD sub-tree: top-level moduly licensed="1" (respektují licenci), položky pod sub-family
/// Servis a Nastavení a číselníky licensed="0" (vždy zobrazeno uvnitř MHD modulu).
ClassMethod FillMain() [ Private ]
{
	// 28 stub agend kolem MHD — name + elementId z legacy edisonnavbar.xml id=-2.
	d ..AddExe("Autobusová doprava", "", "", "8", 0, "", 0, "#FFA500")
	d ..AddExe("Autoopravna", "", "", "12", 0, "", 0, "#FFA500")
	d ..AddExe("Autoopravna 2", "", "", "65", 0, "", 0, "#FFA500")
	d ..AddExe("Dispečink", "", "", "29", 0, "", 0, "#FFA500")
	d ..AddExe("Doplňková evidence k zaměstnanci", "", "", "51", 0, "", 0, "#FFA500")
	d ..AddExe("Dotace", "", "", "4", 0, "", 0, "#FFA500")
	d ..AddExe("Ekonomický systém", "", "", "10", 0, "", 0, "#FFA500")
	d ..AddExe("Evidence majetku", "", "", "23", 0, "", 0, "#FFA500")
	d ..AddExe("FakturaceEx", "", "", "48", 0, "", 0, "#FFA500")
	d ..AddExe("ISY BUS", "", "", "60", 0, "", 0, "#FFA500")
	d ..AddExe("Jízdní řády JIRA", "", "", "2", 0, "", 0, "#FFA500")
	d ..AddExe("Lístky", "", "", "6", 0, "", 0, "#FFA500")
	d ..AddExe("Manažerské výstupy", "", "", "59", 0, "", 0, "#FFA500")

	d ..AddFamily("MHD", 1)

	// Top-level moduly — licensed="1"
	d ..AddExe("Linky a oběhy", "MHD2Linky.exe", "ALVA.MHD2.Linky.formMHD2LinkyHlavniOkno", "230009", 1)
	d ..AddExe("Dispečink", "MHD2PlanDopravy.exe", "ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyDispecink", "260002", 1)
	d ..AddExe("Hromadné úpravy dokladů", "MHD2PlanDopravy.exe", "ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyHromadneUpravyDokladu", "260005", 1)
	d ..AddExe("Chronometráže", "MHD2Linky.exe", "ALVA.MHD2.Linky.formMHD2LinkySkupinyChronometrazi", "230005", 1)
	d ..AddExe("Importy", "", "", "131018", 1)
	d ..AddExe("Jízda bez nehod", "", "", "250001", 1)
	d ..AddExe("Oběhy a optimalizace", "", "", "165001", 1)
	d ..AddExe("Objednávky", "MHD2PlanDopravy.exe", "ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyObjednavky", "260003", 1)
	d ..AddExe("Plán dopravy", "MHD2PlanDopravy.exe", "ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyMain", "260001", 1)
	d ..AddExe("Řidiči", "MHD2Ridici.exe", "ALVA.MHD2.Ridici.formMHD2RidiciList", "241001", 1)
	d ..AddExe("Řidiči - prémie", "MHD2Ridici.exe", "ALVA.MHD2.Ridici.formMHD2RidiciPremie", "241002", 1)
	d ..AddExe("Řidiči - dlouhodobé nepřítomnosti", "MHD2Ridici.exe", "ALVA.MHD2.Ridici.formMHD2RidiciDlouhodobeNepritomnosti", "241004", 1)
	d ..AddExe("Směny", "MHD2Linky.exe", "ALVA.MHD2.Linky.formMHD2LinkySmeny", "230002", 1)
	d ..AddExe("Stavy elektroměrů", "MHD2Ridici.exe", "ALVA.MHD2.Ridici.formMHD2RidiciStavyElektromeru", "241003", 1)
	d ..AddExe("Turnusové skupiny", "MHD2Linky.exe", "ALVA.MHD2.Linky.formMHD2LinkyTurnusoveSkupiny", "230008", 1)
	d ..AddExe("Výstupy", "", "", "131017", 1)
	d ..AddExe("Zastávky", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyZastavky", "210011", 1)

	// Servis sub-family — položky v originálním XML neměly licensed atribut → licensed="0"
	d ..AddFamily("Servis", 0)
	d ..AddExe("Přechod na období", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyPrechodNaObdobi", "210022", 0)
	d ..AddFamilyEnd()

	// Nastavení a číselníky sub-family — licensed="0" (vždy zobrazeno uvnitř MHD modulu)
	d ..AddFamily("Nastavení a číselníky", 0)
	d ..AddExe("Nastavení", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyNastaveni", "210012", 0)
	d ..AddExe("Cíle", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyCile", "210001", 0)
	d ..AddExe("Číselné řady", "", "", "66001", 0)
	d ..AddExe("Druhy úseček, činnosti", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyDruhyUsecek", "210020", 0)
	d ..AddExe("Důvody přeobsazení vozu", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyDuvodyPreobsazeniVozu", "210008", 0)
	d ..AddExe("Fiktivní řidiči", "", "", "146019", 0)
	d ..AddExe("IDS", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyIDS", "210002", 0)
	d ..AddExe("Import a export dat", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyZalohaDat", "210017", 0)
	d ..AddExe("Kalendáře", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyKalendare", "210009", 0)
	d ..AddExe("Kategorie výlukové dopravy", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyKategorieVD", "210023", 0)
	d ..AddExe("Návrhář sestav JŘ", "", "", "76002", 0)
	d ..AddExe("Návrhář sestav JŘ MHD", "", "", "70003", 0)
	d ..AddExe("Poznámky", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyPoznamky", "210010", 0)
	d ..AddExe("Pracoviště", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyPracoviste", "210007", 0)
	d ..AddExe("Přepravní systémy", "", "", "163011", 0)
	d ..AddExe("Příplatky", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyPriplatky", "210024", 0)
	d ..AddExe("Skupiny linek", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikySkupinyLinek", "210003", 0)
	d ..AddExe("Typy lístků", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyTypyListku", "210021", 0)
	d ..AddExe("Typy provozů", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyTypyProvozu", "210005", 0)
	d ..AddExe("Typy skupin", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyTypySkupin", "210013", 0)
	d ..AddExe("Varianty chronometráží", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyVariantyChronometrazi", "210006", 0)
	d ..AddExe("Zóny", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyZony", "210004", 0)
	d ..AddExe("Zprávy pro palubní počítač", "MHD2Ciselniky.exe", "ALVA.MHD2.Ciselniky.formMHD2CiselnikyZpravyProPP", "210019", 0)
	d ..AddFamilyEnd()

	d ..AddFamilyEnd()

	// 15 zbývajících stub agend (po MHD v abecedním pořadí dle legacy navbar -2).
	d ..AddExe("Mzdy a personalistika", "", "", "53", 0, "", 0, "#FFA500")
	d ..AddExe("Nákladní doprava", "", "", "38", 0, "", 0, "#FFA500")
	d ..AddExe("Online aplikace", "", "", "72", 0, "", 0, "#FFA500")
	d ..AddExe("Prodejní terminál", "", "", "40", 0, "", 0, "#FFA500")
	d ..AddExe("Provozní hlášení", "", "", "50", 0, "", 0, "#FFA500")
	d ..AddExe("Provozní pokladna", "", "", "13", 0, "", 0, "#FFA500")
	d ..AddExe("Přepravní pokladna", "", "", "16", 0, "", 0, "#FFA500")
	d ..AddExe("Převody", "", "", "11", 0, "", 0, "#FFA500")
	d ..AddExe("Reporting", "", "", "3", 0, "", 0, "#FFA500")
	d ..AddExe("Skladové hospodářství", "", "", "22", 0, "", 0, "#FFA500")
	d ..AddExe("Sklady Mtz", "", "", "199", 0, "", 0, "#FFA500")
	d ..AddExe("Vlaky 2", "", "", "66", 0, "", 0, "#FFA500")
	d ..AddExe("Vozidla 2", "", "", "57", 0, "", 0, "#FFA500")
	d ..AddExe("Zákazníci 2", "", "", "54", 0, "", 0, "#FFA500")
	d ..AddExe("Zaměstnanci", "", "", "61", 0, "", 0, "#FFA500")
}

/// Naplní strom "Správa systému" (admin nástroje).
/// Odpovídá uzlu id=-3 v původním edisonnavbar.xml. Licensing per uzel z edisontree.xml.
ClassMethod FillSystem() [ Private ]
{
	// Společná evidence (id=0)
	d ..AddFamily("Společná evidence", 0)
	d ..AddExe("EDISON", "", "", "2002", 0)
	d ..AddExe("Organizační struktura", "", "", "147002", 0)
	d ..AddExe("Uživatelé", "", "", "5001", 0)
	d ..AddFamilyEnd()

	// Kmenová evidence (id=1)
	d ..AddFamily("Kmenová evidence", 0)
	d ..AddExe("Archiv dokumentů", "", "", "47003", 1)
	d ..AddExe("Návrhář sestav", "", "", "36008", 0)
	d ..AddExe("Online aplikace", "", "", "149002", 0)
	d ..AddExe("Plánované výstupy", "", "", "149003", 0)
	d ..AddExe("Služby Rest", "", "", "152001", 0)
	d ..AddExe("Správce stromů", "", "", "12001", 0)
	d ..AddExe("Správce úloh", "", "", "40001", 0)
	d ..AddExe("Události", "", "", "154001", 0)
	// Úlohy na pozadí v admin režimu (inline navbar input="AdminMode=1" → 7. arg)
	d ..AddExe("Úlohy na pozadí", "EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINTaskList", "2011", 0, "AdminMode=1")
	d ..AddExe("Word šablony", "", "", "272001", 0)
	d ..AddFamily("Nastavení a číselníky", 0)
	d ..AddExe("Kurzy a měny", "", "", "50004", 0)
	d ..AddExe("Účetní případy", "", "", "66008", 0)
	d ..AddExe("Zahraniční stravné", "", "", "147001", 0)
	d ..AddExe("Způsob zaokrouhlování", "", "", "3014", 0)
	d ..AddFamilyEnd()
	d ..AddFamilyEnd()

	// Systémové nástroje (id=5)
	d ..AddFamily("Systémové nástroje", 0)
	d ..AddExe("Audit systému", "", "", "57001", 0)
	d ..AddExe("MultiServer", "", "", "48001", 1)
	d ..AddFamilyEnd()
}

/// Naplní strom "Partnerská řešení" (3rd party agendy).
/// Odpovídá uzlu id=-4 v původním edisonnavbar.xml. Všechny FAMILY i leaves licensed="1".
ClassMethod FillPartners() [ Private ]
{
	d ..AddFamily("AURIS", 0)
	d ..AddExe("AURIS", "", "", "38001", 1)
	d ..AddFamilyEnd()

	d ..AddFamily("BC Dopravní systémy", 0)
	d ..AddExe("Cena jízdného", "", "", "61001", 1)
	d ..AddExe("Clearing", "", "", "58001", 1)
	d ..AddExe("Intenzita provozu", "", "", "79001", 1)
	d ..AddExe("Přestupy", "", "", "98001", 1)
	d ..AddFamily("OPTIM1", 0)
	d ..AddExe("Optimalizace", "", "", "60001", 1)
	d ..AddExe("Statistické výstupy", "", "", "60006", 1)
	d ..AddFamilyEnd()
	d ..AddExe("Standard", "", "", "59001", 1)
	d ..AddFamily("Výběrová řízení", 0)
	d ..AddExe("Výběrová řízení", "", "", "77001", 1)
	d ..AddExe("Zadávací dokumentace", "", "", "86001", 1)
	d ..AddFamilyEnd()
	d ..AddFamilyEnd()

	d ..AddFamily("EmTest", 0)
	d ..AddExe("EmTest", "", "", "95003", 1)
	d ..AddFamilyEnd()

	d ..AddFamily("FSSOFT", 0)
	d ..AddExe("FSSOFT", "", "", "87001", 1)
	d ..AddFamilyEnd()

	d ..AddFamily("CHAPS", 0)
	d ..AddExe("Export plánu", "", "", "95001", 1)
	d ..AddFamilyEnd()

	d ..AddFamily("Telmax", 0)
	d ..AddExe("Import a Export", "", "", "62001", 1)
	d ..AddExe("Transakce", "", "", "62012", 1)
	d ..AddFamily("Nastavení a číselníky", 0)
	d ..AddExe("Formuláře", "", "", "62004", 0)
	d ..AddExe("Karty", "", "", "62007", 0)
	d ..AddExe("Kategorie", "", "", "62010", 0)
	d ..AddExe("Kategorie (nový)", "", "", "274002", 0)
	d ..AddExe("Měny", "", "", "62009", 0)
	d ..AddExe("Měny (nový)", "", "", "274001", 0)
	d ..AddExe("Nastavení", "", "", "62017", 0)
	d ..AddExe("Oblasti", "", "", "62006", 0)
	d ..AddExe("Parametry", "", "", "62003", 0)
	d ..AddExe("Parametry (nový)", "", "", "274004", 0)
	d ..AddExe("Povolené karty", "", "", "62015", 0)
	d ..AddExe("Profily tarifu", "", "", "62013", 0)
	d ..AddExe("Profily zákazníka", "", "", "62014", 0)
	d ..AddExe("Stránky uživatelského rozhraní", "", "", "62002", 0)
	d ..AddExe("Svátky", "", "", "62016", 0)
	d ..AddExe("Svátky (nový)", "", "", "274005", 0)
	d ..AddExe("Tabla", "", "", "62008", 0)
	d ..AddExe("Tabla (nový)", "", "", "274003", 0)
	d ..AddExe("Tarify", "", "", "62011", 0)
	d ..AddExe("Zakázané karty", "", "", "62005", 0)
	d ..AddFamilyEnd()
	d ..AddFamily("Export a import", 0)
	d ..AddExe("Export", "", "", "62019", 0)
	d ..AddExe("Import", "", "", "62020", 0)
	d ..AddFamilyEnd()
	d ..AddFamilyEnd()

	d ..AddFamily("T-MAPY", 0)
	d ..AddExe("Export pro dispečink", "", "", "95002", 1)
	d ..AddFamilyEnd()

	d ..AddFamily("TRANSDEV", 0)
	d ..AddExe("KARAT", "", "", "200001", 1)
	d ..AddFamilyEnd()
}

}
