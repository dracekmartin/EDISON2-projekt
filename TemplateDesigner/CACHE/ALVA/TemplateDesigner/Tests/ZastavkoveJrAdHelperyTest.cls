Include %occInclude

/// %UnitTest pro deterministické helpery ZastavkoveJrAd - čisté funkce bez čtení globálů,
/// syntetické vstupy → přesné asserty. Kryje 1:1 reimplementovanou legacy logiku
/// (KamPrt zařazení spojů, formát datumových omezení, W-mapa časy, markery).
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdHelperyTest Extends ALVA.EDTesting.MLTestCase
{

// ── NastupisteCsv - nástupiště dle spojů, řazení dle četnosti (KOVED #13048 bod 4) ──

Method TestNastupisteCsvRazeniDleCetnosti()
{
	// Nejčetnější první: 18× sloupek 2, 3× sloupek 3 → „2,3" (první položka = režim Nejčetnější)
	kill c
	set c("2") = 18, c("3") = 3
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NastupisteCsv(.c), "2,3", "nejčetnější první")

	// Shoda četností → menší hodnota dřív (numericky: 5 před 10)
	kill c
	set c("10") = 2, c("5") = 2
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NastupisteCsv(.c), "5,10", "shoda četností → numericky vzestupně")

	// Jedna hodnota / prázdný vstup
	kill c
	set c("102") = 4
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NastupisteCsv(.c), "102", "jediná hodnota")
	kill c
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NastupisteCsv(.c), "", "prázdný vstup → prázdný výstup")
}

// ── ZarazeniSpoje - sloupce dnů dle FTL × plk × NZ (1:1 KamPrt^JRapiLinPrtMhd1) ──

Method TestZarazeniSpojeFtl0()
{
	// FTL 0: Prac | So | Ne+svátky
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(0, "", "", ""), "1,2,3", "prázdné plk = pracovní → všechny sloupce")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(0, ".", "", ""), "1,2,3", "tečka = pracovní → všechny sloupce")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(0, "15", "", ""), "1", "dny 1-5 → jen Prac")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(0, "6", "", ""), "2", "sobota → 2")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(0, "7", "", ""), "3", "neděle → 3")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(0, "+", "", ""), "3", "svátek → 3")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(0, "67", "", ""), "2,3", "víkend → 2,3")
}

Method TestZarazeniSpojeFtl2()
{
	// FTL 2: Prac | víkend (So+Ne+svátky)
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(2, "", "", ""), "1,2", "pracovní → oba sloupce")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(2, "6", "", ""), "2", "sobota → víkendový sloupec")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(2, "12345", "", ""), "1", "prac. dny → 1")
}

Method TestZarazeniSpojeFtl1PrazdninoveDeleni()
{
	// FTL 1: Prac mimo prázdniny | Prac o prázdninách | víkend.
	// nzCfg = "MHD_NZ1_NZ2|MHD_NENZ1_NENZ2"; NZ2 (piece 2) = "nejede o prázdninách".
	set cfg = "A~B|"

	// Bez NZ → pracovní spoj do obou prázdninových sloupců + víkend
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(1, "", "", cfg), "1,2,3", "prac bez NZ → oba prázdninové + víkend")
	// CNZ = B (nejede o prázdninách) → jen sloupec "mimo prázdniny" + víkend
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(1, "", "B", cfg), "1,3", "NZ nejede-o-prázd → mimo + víkend")
	// CNZ = A (jede o prázdninách) → jen sloupec "o prázdninách" + víkend
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(1, "", "A", cfg), "2,3", "NZ jede-o-prázd → o prázd + víkend")
	// Víkendový spoj bez NZ konfigurace nedotčen
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(1, "6", "", cfg), "3", "sobota → víkendový sloupec")
}

Method TestZarazeniSpojeFtl3()
{
	// FTL 3: Prac mimo | Prac o prázd | So | Ne+svátky
	// ⚠️ Hrana (dokumentované, gold-ověřené chování - 0320144): s PRÁZDNOU NZ konfigurací se
	// prázdné cnz rovná prázdnému nzftl1 ("" = "") → pracovní spoj jde JEN do sloupce
	// "mimo prázdniny" (1), ne do obou. AD-N15: MHD_NZ1_NZ2 je v praxi prázdné všude.
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(3, "", "", ""), "1,3,4", "pracovní s prázdnou NZ cfg → mimo prázdniny + víkend")
	// S vyplněnou NZ konfigurací a bez CNZ jde pracovní spoj do obou prázdninových sloupců
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(3, "", "", "A~B|"), "1,2,3,4", "pracovní s NZ cfg bez CNZ → všechny 4")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(3, "6", "", ""), "3", "sobota → 3")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZarazeniSpoje(3, "7", "", ""), "4", "neděle → 4")
}

Method TestBitmapNaSkupiny()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BitmapNaSkupiny("1~0~1"), "1,3", "1~0~1 → 1,3")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BitmapNaSkupiny("0~0~0"), "", "nuly → prázdné")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BitmapNaSkupiny("1~1"), "1,2", "1~1 → 1,2")
}

// ── „nevypisovat NZ ve sloupci" (MHD_NEZNZ123) ───────────────────────────────

Method TestZnackaSkrytaVeSloupci()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// slot N patří sloupci N (na rozdíl od MHD_NENZ1_NENZ2, kde legacy sloty prohazuje)
	set cfg = "3~7,8~~12"
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 1, 3), 1, "značka 3 skrytá ve sloupci 1")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 2, 3), 0, "ve sloupci 2 se značka 3 tiskne")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 2, 8), 1, "výčet ve slotu: 7,8 → 8 skrytá")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 3, 3), 0, "prázdný slot neskrývá nic")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 4, 12), 1, "čtvrtý sloupec (FTL 3) taky")
	// číslo se porovnává numericky (legacy +NZ), aby „03" sedlo na „3"
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 1, "03"), 1, "vedoucí nula nevadí")
	// dílčí shoda se nesmí chytit - „1" není „12"
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 4, 1), 0, "1 není 12")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", "", 1, 3), 0, "bez konfigurace se netiskne nic navíc")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaVeSloupci", cfg, 1, ""), 0, "spoj bez značky")
}

// ── Časy jízdy z W-mapy ──────────────────────────────────────────────────────

Method TestCasHodnota()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota("27"), 27, "prostý čas")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota("33/4"), 37, "čas/čekání se sčítá")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota(""), "", "prázdné → prázdné")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota("<"), "", "projíždí → prázdné")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota("|"), "", "| (nestaví) → prázdné, ne čas 0")
}

Method TestCasJizdyZMapy()
{
	set wMap(1) = "0", wMap(3) = "12", wMap(5) = "33/4", wMap(7) = "<"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasJizdyZMapy(.wMap, 1, 3), 12, "kumulativní čas 1→3")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasJizdyZMapy(.wMap, 3, 5), 25, "čas s čekáním (37-12)")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasJizdyZMapy(.wMap, 1, 7), 0, "projížděná zastávka → 0")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasJizdyZMapy(.wMap, 1, 99), 0, "mimo trasu → 0")
}

// ── Datumová omezení (1:1 txtNZ^JRapiNZUtl + UprDat^JRapiLinPrtPS3) ─────────

Method TestPrefixOmezeni()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PrefixOmezeni(11), "jede jen ", "11")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PrefixOmezeni(22), "nejede ", "22")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PrefixOmezeni(99), "", "neznámé → prázdné")
}

Method TestUpravDatum()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).UpravDatum("01.05.2026"), "1.5.26", "strip nul + 2místný rok")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).UpravDatum("24.12.26"), "24.12.26", "už 2místný rok beze změny")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).UpravDatum(""), "", "prázdné → prázdné")
}

Method TestFormatDatumyOmezeni()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).FormatDatumyOmezeni("k@01.05.2026:31.08.2026"), "od 1.5.26 do 31.8.26", "rozsah s prefixem k@")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).FormatDatumyOmezeni("24.12.2026#31.12.2026"), "24.12.26, 31.12.26", "jednotlivá data přes #")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).FormatDatumyOmezeni("05.07.2026"), "5.7.26", "jedno datum bez rozsahu")
}

// ── Atributy, markery, normalizace ──────────────────────────────────────────

Method TestJeViditelnyAtributATextAtributu()
{
	// Legacy UVOD tiskne jen "na znamení" (x) - vše ostatní neviditelné
	do $$$AssertTrue(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JeViditelnyAtribut("x"), "x je viditelný")
	do $$$AssertNotTrue(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JeViditelnyAtribut("T"), "T (tučně) není viditelný")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).TextAtributu("x"), "na znamení", "text pro x")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).TextAtributu("?"), "", "neznámý znak → prázdné")
}

Method TestGetZnk()
{
	// Písmenná sada markerů; po vyčerpání zdvojení (1:1 GetZnk^JRapiLinPrtMhd)
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetZnk("BCE", 1), "B", "první písmeno")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetZnk("BCE", 3), "E", "třetí písmeno")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetZnk("BCE", 4), "BB", "po vyčerpání zdvojené")
}

Method TestZiskejMarkerDedupuje()
{
	set m1 = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZiskejMarker(.poznMap, "nejede 24.12.")
	set m2 = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZiskejMarker(.poznMap, "jede jen v so.")
	set m3 = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZiskejMarker(.poznMap, "nejede 24.12.")
	do $$$AssertEquals(m1, 1, "první text → marker 1")
	do $$$AssertEquals(m2, 2, "druhý text → marker 2")
	do $$$AssertEquals(m3, m1, "stejný text → stejný marker (dedup)")
	do $$$AssertEquals($get(poznMap("byMarker",1)), "nejede 24.12.", "reverzní mapa marker→text")
}

// ── Písmena variant tras (KOVED #13048 bod 5) ───────────────────────────────

Method TestPismenoVarianty()
{
	// První varianta bez písmene (default) - hlavní trasa čistá, odchylky A, B…
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PismenoVarianty(1, 1), "", "první sloupec bez písmene")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PismenoVarianty(2, 1), "A", "druhý sloupec → A")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PismenoVarianty(27, 1), "Z", "27. sloupec → Z")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PismenoVarianty(28, 1), "V27", "za Z → nouzový fallback V27")

	// Písmena od prvního sloupce
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PismenoVarianty(1, 0), "A", "první sloupec → A")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PismenoVarianty(26, 0), "Z", "26. sloupec → Z")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PismenoVarianty(27, 0), "V27", "27. sloupec → V27")
}

Method TestNormalizLinku()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NormalizLinku("410101"), "0410101", "doplnění na 7 znaků")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NormalizLinku("0410101"), "0410101", "7 znaků beze změny")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NormalizLinku(""), "", "prázdné → prázdné")
}

// ── Výčet spojů u dopravce stlačený do rozsahů (KOVED #13008) ────────────────

Method TestRozsahyCisel()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"

	// Vzor od zákazníka: „spoje č. 1-31, 201-204"
	kill c
	for i=1:1:31 { set c(i) = "" }
	for i=201:1:204 { set c(i) = "" }
	do $$$AssertEquals($classmethod(cls, "RozsahyCisel", .c), "1-31, 201-204", "dva rozsahy")

	// Samostatné číslo
	kill c
	set c(51) = ""
	do $$$AssertEquals($classmethod(cls, "RozsahyCisel", .c), "51", "jediný spoj")

	// Dvojice se vypíše výčtem - „1, 2" se čte lépe než „1-2"
	kill c
	set c(1) = "", c(2) = ""
	do $$$AssertEquals($classmethod(cls, "RozsahyCisel", .c), "1, 2", "dvojice výčtem")

	// Mezera v číslování rozdělí rozsahy; pořadí je vzestupné bez ohledu na vkládání
	kill c
	set c(7) = "", c(5) = "", c(6) = "", c(9) = ""
	do $$$AssertEquals($classmethod(cls, "RozsahyCisel", .c), "5-7, 9", "vzestupně a s mezerou")

	kill c
	do $$$AssertEquals($classmethod(cls, "RozsahyCisel", .c), "", "bez spojů prázdné")
}

// ── Licenční linka spoje u souhrnného JŘ (KOVED #13008) ──────────────────────

Method TestLicencniLinkaSpoje()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"

	// Obyčejná linka: „Zdroj spoje" (3. #-segment) prázdný → platí číslo té linky
	set bezZdroje = "1:46:2:460:135:R:6:1::$:0:0#611:2~~~~##103.161##"
	do $$$AssertEquals($classmethod(cls, "LicencniLinkaSpoje", bezZdroje, 822995), 822995, "bez zdroje = číslo linky")

	// Souhrnný JŘ: zdroj „0802170~15" → licenční linka 802170 (index je číslo doplněné nulami na 7)
	set seZdrojem = "1:46:2:460:135:R:6:1::$:0:0#611:2~~~~#0802170~15#103.161##"
	do $$$AssertEquals($classmethod(cls, "LicencniLinkaSpoje", seZdrojem, 999), 802170, "zdroj spoje → licenční linka")

	// Nesmyslný zdroj nesmí vyrobit prázdné číslo - padá se na linku
	set rozbity = "1:46:2:460:135:R:6:1::$:0:0#611:2~~~~#0000000~1#103.161##"
	do $$$AssertEquals($classmethod(cls, "LicencniLinkaSpoje", rozbity, 822995), 822995, "nulový zdroj = číslo linky")
}

// ── Tvar názvu zastávky (KOVED #13089) ───────────────────────────────────────
// V registru je obec a místo oddělené dvojčárkou; z toho se skládají všechny tvary.

Method TestTvarNazvuZkraceny()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// Výchozí (prázdný tvar) i pojmenovaný Zkrácený = dosavadní chování, na kterém stojí gold odtisky
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "Zlín,,aut.nádr.", "ZL", ""), "Zlín,aut.nádr.", "prázdný tvar = zkrácený")
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "Zlín,,aut.nádr.", "ZL", "Zkrácený"), "Zlín,aut.nádr.", "zkrácený")
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "", "ZL", "Zkrácený"), "", "prázdný název")
}

Method TestTvarNazvuPlnyCis()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "Zlín,,aut.nádr.", "ZL", "Plný CIS"), "Zlín,,aut.nádr.", "dvojčárka zůstává")
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "Zlín,,aut.nádr.", "ZL", "Plný CIS s okresem"), "Zlín,,aut.nádr.,ZL", "s okresem")
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "Zlín,,aut.nádr.", "", "Plný CIS s okresem"), "Zlín,,aut.nádr.", "bez okresu v registru se nic nepřidá")
}

Method TestTvarNazvuTypograficky()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "Zlín,,aut.nádr.", "ZL", "Typograficky upravený"), "Zlín, aut. nádr.", "mezera za čárkou i tečkou")
	// Mezera se doplní za KAŽDOU tečku i čárku (kromě před číslicí a na konci) - tak to klient
	// popsal, takže „žel.st." se rozpadne na „žel. st.".
	do $$$AssertEquals($classmethod(cls, "FormatNazvuZastavky", "Otrokovice,Baťov,žel.st.", "", "Typograficky upravený"), "Otrokovice, Baťov, žel. st.", "mezera za každou čárkou i tečkou")
}

/// Mezera se nesmí vložit před číslici - jinak by z „Zlín,č.1" bylo „Zlín, č. 1" a z „1.5" „1. 5".
Method TestMezeryPredCislici()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "MezeryPoInterpunkci", "Zlín,č.1"), "Zlín, č.1", "před číslicí bez mezery")
	do $$$AssertEquals($classmethod(cls, "MezeryPoInterpunkci", "rozc.0.9"), "rozc.0.9", "desetinné číslo beze změny")
	do $$$AssertEquals($classmethod(cls, "MezeryPoInterpunkci", "Zlín, aut. nádr."), "Zlín, aut. nádr.", "už upravený text se nezdvojí")
	// Dvojčárka se do tohoto pravidla nemá dostat (formátuje se až zkrácený název) - kdyby se
	// dostala, vznikne osamocená čárka. Zamčeno, ať je vidět, proč je pořadí kroků důležité.
	do $$$AssertEquals($classmethod(cls, "MezeryPoInterpunkci", "Praha,,Hlavní nádraží."), "Praha, , Hlavní nádraží.", "dvojčárka dává osamocenou čárku - proto se stahuje dřív")
}

// ── Zóny: trasa linky, záskok na registr zastávek, odstranění písmen (KOVED #13091 bod 4) ──
// Písmeno v kódu zóny nese přepravní systém, ne zónu - klient chtěl číslo z číselníku.

Method TestZonaBezPismenVKodu()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "ZonaBezPismenVKodu", "100J"), "100", "písmeno přepravního systému pryč")
	do $$$AssertEquals($classmethod(cls, "ZonaBezPismenVKodu", "O12"), "12", "písmeno na začátku (ODIS)")
	do $$$AssertEquals($classmethod(cls, "ZonaBezPismenVKodu", "101"), "101", "čisté číslo beze změny")
	do $$$AssertEquals($classmethod(cls, "ZonaBezPismenVKodu", ""), "", "prázdné → prázdné")
}

/// Zóna z trasy linky (9. `#`-segment J-věty) - záskok na registr se neuplatní, takže test
/// nečte globály. Čárka uvnitř je oddělovač zón, ne součást hodnoty (1:1 `JRapiLin` dta11).
Method TestZonyZTrasyLinky()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// J-věta se zónou 101 v 9. #-segmentu (reálný tvar CB_GWBUS)
	set veta = "4842:TAS##16:#2#0##0:0#:#101#0:0##2:99##0#"
	do $$$AssertEquals($classmethod(cls, "ZonyZastavky", "", veta, "", 0), "101", "zóna z trasy linky")

	// Vícezónová (hraniční) zastávka - čárka v datech = dvě zóny
	set vetaVice = "4842:TAS##16:#2#0##0:0#:#11,12#0:0##2:99##0#"
	do $$$AssertEquals($classmethod(cls, "ZonyZastavky", "", vetaVice, "", 0), "11;12", "čárka = oddělovač dvou zón")

	// Odstranění písmen zapnuté
	set vetaPismeno = "4842:TAS##16:#2#0##0:0#:#100J#0:0##2:99##0#"
	do $$$AssertEquals($classmethod(cls, "ZonyZastavky", "", vetaPismeno, "", 1), "100", "zapnuté odstranění písmen")
	do $$$AssertEquals($classmethod(cls, "ZonyZastavky", "", vetaPismeno, "", 0), "100J", "vypnuté = hodnota jak je v datech")

	// „-" u linky = zóny se netisknou vůbec
	set vetaBez = "4842:TAS##16:#2#0##0:0#:#-#0:0##2:99##0#"
	do $$$AssertEquals($classmethod(cls, "ZonyZastavky", "", vetaBez, "", 0), "", "pomlčka = netisknout zóny")
}

// ── Denní značky týdenní platnosti dle rozvržení sloupců (KOVED #13045) ──────
// Značka smí nést jen to, co nedává nadpis sloupce. Klient hlásil „6" u odjezdů ve sloupci,
// který sobotu už jmenuje, a u spojů „6+" i v nedělním sloupci (tam značka rovnou lhala).

Method TestDenniZnackyOddeleneSloupce()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// FTL 0 a 3 mají sobotu i neděli ve vlastním sloupci → pro 6, 7 a „+" se značka netiskne
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 0, "6"), "", "sobotní spoj ve vlastním sloupci - bez značky")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 0, "6+"), "", "sobota + svátek - bez značky (hlášení klienta)")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 0, "7"), "", "nedělní spoj - bez značky")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 3, "6+"), "", "totéž ve čtyřsloupcovém rozvržení")
	// konkrétní pracovní dny se značkují vždy - sloupec pracovních dnů je nerozlišuje
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 0, "5"), "5", "jede jen v pátek - značka zůstává")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 0, "15"), "15", "pondělí a pátek")
}

Method TestDenniZnackySloucenyVikend()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// FTL 1 a 2 mají víkend v jednom sloupci → značka rozlišuje uvnitř sloupce,
	// ale jen když spoj nejede i v ostatní víkendové dny (1:1 legacy JRapiLinPrtMhd1)
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 2, "6"), "6", "jen sobota - značka odlišuje")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 2, "7"), "7", "jen neděle")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 2, "+"), "+", "jen svátek")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 2, "6+"), "", "sobota i svátek - nic neodlišuje")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 2, "67"), "", "sobota i neděle")
	// Neděle + svátek dostane obě značky (1:1 legacy) - vylučuje se jen proti sobotě
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 1, "7+"), "7+", "neděle a svátek - obě značky jako v legacy")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 1, "5"), "5", "pracovní den beze změny")
}

Method TestDenniZnackyBezPlatnosti()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// Prázdná / „." platnost = jede denně, „X" = skupinový vzor → značka nikde
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 0, ""), "", "bez platnosti")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 2, "."), "", "tečka = jede denně")
	do $$$AssertEquals($classmethod(cls, "DenniZnacky", 0, "X"), "", "skupinový vzor X")
}

Method TestTextDneVTydnuSvatek()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "TextDneVTydnu", "+"), "jede ve svátek", "svátek má text do legendy")
	do $$$AssertEquals($classmethod(cls, "TextDneVTydnu", "6"), "jede v sobotu", "dny beze změny")
}

// ── Poznámky linky a výluky (KOVED #13093 bod 1) ──

/// Výluky se poznají prefixem „Výluka" - konvence zákazníka, tak to posílají i do CIS.
/// Kdyby detekce chytala víc, spadl by do výlukového bloku i místenkový text ze stejné kategorie.
Method TestJeVyluka()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertTrue($classmethod(cls, "JeVyluka", "Výluka: Z důvodu uzavírky I/50…"), "prefix Výluka:")
	do $$$AssertTrue($classmethod(cls, "JeVyluka", "Výluka bez dvojtečky taky projde"), "prefix bez dvojtečky")
	do $$$AssertTrue('$classmethod(cls, "JeVyluka", "R - k jízdence je možné zakoupit místenku"), "místenkový text není výluka")
	do $$$AssertTrue('$classmethod(cls, "JeVyluka", "Na lince platí Tarif IDZK"), "tarifní text není výluka")
	do $$$AssertTrue('$classmethod(cls, "JeVyluka", "Kvůli výluce jezdí jinak"), "slovo uvnitř textu nestačí")
	do $$$AssertTrue('$classmethod(cls, "JeVyluka", ""), "prázdný text není výluka")
}

/// Text poznámky se pro vývěsku čistí: legacy značka „$$" pryč, „#" (oddělovač odstavců) na mezeru -
/// legenda text zalamuje sama, takže znak křížku by v ní jen svítil.
Method TestTextPoznamkyLinky()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "TextPoznamkyLinky", "Text s $$TrNz"), "Text s TrNz", "$$ se odstraní")
	do $$$AssertEquals($classmethod(cls, "TextPoznamkyLinky", "První#Druhý"), "První Druhý", "# je mezera")
	do $$$AssertEquals($classmethod(cls, "TextPoznamkyLinky", "  Text  "), "Text", "okrajové mezery pryč")
	do $$$AssertEquals($classmethod(cls, "TextPoznamkyLinky", "#"), "", "jen oddělovač → prázdno (poznámka se přeskočí)")
}

/// Kola posíláme markerem „K", který si .NET přeloží na glyf kola (`L`, 0x4C). Poslat rovnou „L"
/// nejde - to je v číselníku vlak a přeložilo by se na lokomotivu, tedy přesně ta záměna piktogramů,
/// kterou jsme 3. 8. opravovali u přestupů.
Method TestZnakKolaJeMarkerNeGlyf()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "ZnakKola"), "K", "kola jdou markerem K, glyf dosadí render")
	do $$$AssertNotEquals($classmethod(cls, "ZnakKola"), "L", "L je v číselníku vlak → lokomotiva")
}

/// Zákazník si značku vysvětluje sám vlastní poznámkou u linky („R - k jízdence…"), takže naši
/// generovanou vysvětlivku pro týž znak zahodíme - jinak stály v legendě dvě pod sebou (nahlášeno
/// 4. 8. z reálné vývěsky KOVED). Pomlčka za znakem je povinná, jinak by běžný text začínající
/// jednoznakovým slovem umlčel legendu značky, kterou nikdo nevysvětlil.
Method TestZnakVysvetlivkyLinky()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "ZnakVysvetlivky", "R - k jízdence je možné zakoupit místenku"), "R", "vysvětlivka značky R")
	do $$$AssertEquals($classmethod(cls, "ZnakVysvetlivky", "Na lince platí tarif Zlínského kraje"), "", "běžný text není vysvětlivka")
	do $$$AssertEquals($classmethod(cls, "ZnakVysvetlivky", "IDS DSZO: na všech spojích"), "", "text s dvojtečkou není vysvětlivka")
	do $$$AssertEquals($classmethod(cls, "ZnakVysvetlivky", "V úseku Zlín - Otrokovice"), "", "bez pomlčky HNED za znakem to není vysvětlivka")
	do $$$AssertEquals($classmethod(cls, "ZnakVysvetlivky", ""), "", "prázdný text")
}

/// Vysvětlivka pokračování nese **poslední trojčíslí** linky, ne celé číslo - klient to tak má
/// v požadavku („l. 304") a v jízdním řádu se linky takhle zkráceně označují (#13093 bod 3).
Method TestTextPokracovani()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "TextPokracovani", "824532", "Podhradí"), "pokračuje do zast. Podhradí (l. 532)", "trojčíslí z konce čísla linky")
	do $$$AssertEquals($classmethod(cls, "TextPokracovani", "773165", "Otrokovice,žel.st."), "pokračuje do zast. Otrokovice,žel.st. (l. 165)", "cíl je celý název zastávky")
	do $$$AssertEquals($classmethod(cls, "TextPokracovani", "42", "Kvasice,nám."), "pokračuje do zast. Kvasice,nám. (l. 42)", "krátké číslo linky se neřeže")
}

// ── JeViditelnyAtribut - co z číselníku 16 je informace pro cestujícího (KOVED #13048 bod 1) ──

/// Znaky pro cestujícího se tisknou. Kdyby některý vypadl, tiše zmizí z vývěsky - to byla
/// podstata hlášení #13048 bodu 1, takže je vyjmenované v testu, ne jen v kódu.
Method TestViditelneAtributyProCestujiciho()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	for znak = "x", "y", "z", "e", "f", "g", "O", "D", "C", "G", "a", "b", "c" {
		do $$$AssertTrue($classmethod(cls, "JeViditelnyAtribut", znak), "atribut pro cestujícího se tiskne: "_znak)
	}
}

/// Tiskové direktivy legacy se jako značka tisknout NESMÍ - na AD datech jsou jich tisíce
/// (T 2724×, E 2111×, P 843× jen u KOVED), takže by vývěsku zaplevelily.
Method TestTiskoveDirektivyNejsouZnacka()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	for znak = "T", "P", "p", "o", "N", "V", "J", "S", ">", "<", "E", "H" {
		do $$$AssertTrue('$classmethod(cls, "JeViditelnyAtribut", znak), "tisková direktiva není značka: "_znak)
	}
	// Přestupy mají vlastní přepínač, takže do obecné viditelnosti nepatří.
	do $$$AssertTrue('$classmethod(cls, "JeViditelnyAtribut", "A"), "autobus jde přes přepínač přestupů")
	do $$$AssertTrue('$classmethod(cls, "JeViditelnyAtribut", "L"), "vlak jde přes přepínač přestupů")
	do $$$AssertTrue('$classmethod(cls, "JeViditelnyAtribut", ""), "prázdný znak není atribut")
}

// ── SloupekZTrasy - sloupek z J-věty #12 dle směru (KOVED #13088) ──

/// Vstupy jsou skutečné J-věty ze ZL_KOVED linky 0822995 (zastávky 1 a 3), aby test hlídal
/// tvar reálných dat, ne jen syntetický vzor.
Method TestSloupekZTrasyDleSmeru()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set veta = "41994:TA##2:V#2###0:0#:##0:0##2:100##0#"
	do $$$AssertEquals($classmethod(cls, "SloupekZTrasy", veta, 1), "2", "směr tam = první hodnota segmentu 12")
	do $$$AssertEquals($classmethod(cls, "SloupekZTrasy", veta, 0), "100", "směr zpět = druhá hodnota")
	set veta2 = "42023:##:#2###0:0#:##0:0##3:1##0#"
	do $$$AssertEquals($classmethod(cls, "SloupekZTrasy", veta2, 1), "3", "jiná zastávka, směr tam")
	do $$$AssertEquals($classmethod(cls, "SloupekZTrasy", veta2, 0), "1", "jiná zastávka, směr zpět")
}

/// Sloupek a označník jsou dvě různá pole (segment 12 vs. 3). Na téhle zastávce je označník
/// prázdný, sloupek vyplněný - kdyby se čtení pletlo, vývěska by tiskla prázdno.
Method TestSloupekNeniOznacnik()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set veta = "42001:V##:#2###0:0#:##0:0##2:1##0#"
	set oznacnik = $piece($piece(veta, "#", 3), ":", 1)
	do $$$AssertEquals(oznacnik, "", "označník (segment 3) je na téhle zastávce prázdný")
	do $$$AssertEquals($classmethod(cls, "SloupekZTrasy", veta, 1), "2", "sloupek (segment 12) je vyplněný")
}

/// Pozice sloupku a označníku v datech drží jedna funkce - kdyby se rozešly, agregace dle spojů
/// by počítala jiné pole než sloupec a vývěska by tiskla dvě různá čísla pod jedním názvem.
Method TestPoziceStanoviste()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "PoziceStanoviste", "OZN"), "7~3", "označník = W 7, J-věta 3")
	do $$$AssertEquals($classmethod(cls, "PoziceStanoviste", "SL"), "13~12", "sloupek = W 13, J-věta 12")
	do $$$AssertEquals($classmethod(cls, "PoziceStanoviste", ""), "7~3", "neznámý zdroj → označník (dosavadní chování)")
}

/// Linka bez evidence sloupků (kratší J-věta) nesmí spadnout ani vymyslet hodnotu.
Method TestSloupekChybejiciSegment()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "SloupekZTrasy", "4842:x##11", 1), "", "krátká J-věta → prázdno")
	do $$$AssertEquals($classmethod(cls, "SloupekZTrasy", "", 1), "", "prázdná J-věta → prázdno")
}

// ── Vlastní sloupce vývěsky (dny + značky) ───────────────────────────────────

Method TestDnySpoje()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	do $$$AssertEquals($classmethod(cls, "DnySpoje", ""), "", "prázdná platnost = jede každý den")
	do $$$AssertEquals($classmethod(cls, "DnySpoje", "."), "", "tečka = jede každý den")
	do $$$AssertEquals($classmethod(cls, "DnySpoje", "X"), "12345", "X = legislativní zkratka pracovních dnů")
	do $$$AssertEquals($classmethod(cls, "DnySpoje", "X6"), "123456", "X + sobota")
	do $$$AssertEquals($classmethod(cls, "DnySpoje", "15"), "15", "konkrétní dny beze změny")
	do $$$AssertEquals($classmethod(cls, "DnySpoje", "67+"), "67+", "víkend + svátek")
	do $$$AssertEquals($classmethod(cls, "DnySpoje", "1X"), "12345", "duplicitní dny se neopakují")
	do $$$AssertEquals($classmethod(cls, "DnySpoje", "abc"), "-", "nerozpoznaná platnost → žádný den (spoj se nikam nezařadí)")
}

Method TestZarazeniSpojeDleDefiniceKaskada()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// 2 sloupce: pracovní jen se značkami 5,6; víkend bez značky 7
	kill def
	set def = 2
	set def(1,"id") = "A", def(1,"dny") = "12345", def(1,"jen") = ",5,6,", def(1,"bez") = ""
	set def(2,"id") = "B", def(2,"dny") = "67+",   def(2,"jen") = "",     def(2,"bez") = ",7,"

	// dny: spoj bez omezení sedí do obou, ale „jen" ho z A vyřadí (nemá značku)
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "", ""), "B", "spoj bez značky neprojde přes seznam „jen s těmito“")
	// „jen": spoj se značkou 5 projde do A; do B taky (B „jen" nemá)
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "", "5"), "A,B", "značka v seznamu „jen“ → sedí")
	// „bez": značka 7 vyřadí z B, v A není v „jen"? je 7 mimo ",5,6," → vyřazen i z A
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "", "7"), "", "vyloučená značka → nikam")
	// dny rozhodují před značkami: sobotní spoj se značkou 5 do A nesedí dny
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "6", "5"), "B", "dny mají přednost před značkami")
	// normalizace čísel značek: „05" == 5
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "", "05"), "A,B", "číslo značky se porovnává numericky")

	// sloupec bez dnů neomezuje (kontrakt „nevyplněná podmínka neomezuje") - jen značky filtrují
	kill def
	set def = 1
	set def(1,"id") = "F", def(1,"dny") = "", def(1,"jen") = ",5,", def(1,"bez") = ""
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "6", "5"), "F", "sobotní spoj se značkou sedí do sloupce bez dnů")
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "6", ""), "", "spoj bez značky přes „jen“ neprojde")
}

Method TestZnackaSkrytaDleDefinice()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	kill def
	set def = 2
	set def(1,"id") = "P", def(2,"id") = "V"
	set def("nevyp","P") = ",52,53,", def("nevyp","V") = ""
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaDleDefinice", .def, "P", "52"), 1, "značka v per-sloupcovém seznamu → potlačit")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaDleDefinice", .def, "V", "52"), 0, "jiný sloupec bez seznamu → tisknout")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaDleDefinice", .def, "P", "99"), 0, "značka mimo seznam → tisknout")
	do $$$AssertEquals($classmethod(cls, "ZnackaSkrytaDleDefinice", .def, "P", ""), 0, "bez značky → nic k potlačení")
}

Method TestParseDefiniceSloupcu()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// validní definice
	set ^||ML("IN","DEFINICE_SLOUPCU") = "{""sloupce"":[{""id"":""1"",""nadpis"":""Pracovní dny"",""nadpis2"":""mimo prázdniny"",""dny"":""12345"",""znackyJen"":"""",""znackyBez"":""21, 32"",""nevypisovatZnacky"":""52""},{""id"":""2"",""nadpis"":""Soboty"",""dny"":""6""}]}"
	kill def
	do $$$AssertEquals($classmethod(cls, "ParseDefiniceSloupcu", .def), 2, "dva sloupce")
	do $$$AssertEquals($get(def(1,"nadpis")), "Pracovní dny", "nadpis 1. sloupce")
	do $$$AssertEquals($get(def(1,"nadpis2")), "mimo prázdniny", "druhý řádek nadpisu")
	do $$$AssertEquals($get(def(1,"bez")), ",21,32,", "seznam „bez“ normalizovaný (ořez mezer)")
	do $$$AssertEquals($get(def("nevyp","1")), ",52,", "per-sloupcové potlačení značek")
	do $$$AssertEquals($get(def(2,"dny")), "6", "dny 2. sloupce")

	// nevalidní vstupy → 0 = režim automaticky
	set ^||ML("IN","DEFINICE_SLOUPCU") = "tohle není json"
	do $$$AssertEquals($classmethod(cls, "ParseDefiniceSloupcu", .def), 0, "nevalidní JSON → automaticky")
	set ^||ML("IN","DEFINICE_SLOUPCU") = "{""neco"":1}"
	do $$$AssertEquals($classmethod(cls, "ParseDefiniceSloupcu", .def), 0, "chybí sloupce → automaticky")
	kill ^||ML("IN","DEFINICE_SLOUPCU")
	do $$$AssertEquals($classmethod(cls, "ParseDefiniceSloupcu", .def), 0, "bez parametru → automaticky")
}

/// Parita presetů: definice odpovídající čtyřem dnešním rozvržením musí zařadit spoj do týchž
/// sloupců jako legacy ZarazeniSpoje - hlavní pojistka, že presety jsou 1:1 náhrada.
Method TestDefiniceParitaPresetFtl0()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	kill def
	set def = 3
	set def(1,"id") = 1, def(1,"dny") = "12345", def(1,"jen") = "", def(1,"bez") = ""
	set def(2,"id") = 2, def(2,"dny") = "6",     def(2,"jen") = "", def(2,"bez") = ""
	set def(3,"id") = 3, def(3,"dny") = "7+",    def(3,"jen") = "", def(3,"bez") = ""
	for plk = "", ".", "12345", "X", "15", "6", "7", "+", "67", "16", "67+" {
		do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, plk, ""), $classmethod(cls, "ZarazeniSpoje", 0, plk, "", ""), "FTL0 parita, plk="_plk)
	}
}

Method TestDefiniceParitaPresetFtl2()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	kill def
	set def = 2
	set def(1,"id") = 1, def(1,"dny") = "12345", def(1,"jen") = "", def(1,"bez") = ""
	set def(2,"id") = 2, def(2,"dny") = "67+",   def(2,"jen") = "", def(2,"bez") = ""
	for plk = "", ".", "12345", "X", "15", "6", "7", "+", "67", "16", "67+" {
		do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, plk, ""), $classmethod(cls, "ZarazeniSpoje", 2, plk, "", ""), "FTL2 parita, plk="_plk)
	}
}

/// FTL1 (prázdninové dělení): preset se staví z NZ konfigurace linky - „mimo prázdniny" vylučuje
/// značku „jede o prázdninách" + poziční seznam 1, „o prázdninách" vylučuje „nejede o prázdninách"
/// + poziční seznam 2. nzCfg = "21~22|31~32" → nzftl2=21 (jede o prázd), nzftl1=22 (nejede),
/// nenzftl1=32, nenzftl2=31 (legacy sloty prohozené).
Method TestDefiniceParitaPresetFtl1()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set nzCfg = "21~22|31~32"
	kill def
	set def = 3
	set def(1,"id") = 1, def(1,"dny") = "12345", def(1,"jen") = "", def(1,"bez") = ",21,32,"
	set def(2,"id") = 2, def(2,"dny") = "12345", def(2,"jen") = "", def(2,"bez") = ",22,31,"
	set def(3,"id") = 3, def(3,"dny") = "67+",   def(3,"jen") = "", def(3,"bez") = ""
	for plk = "", ".", "12345", "X", "6", "16", "67+" {
		for cnz = "", "21", "22", "31", "32", "99" {
			do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, plk, cnz), $classmethod(cls, "ZarazeniSpoje", 1, plk, cnz, nzCfg), "FTL1 parita, plk="_plk_" cnz="_cnz)
		}
	}
}

Method TestDefiniceParitaPresetFtl3()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set nzCfg = "21~22|31~32"
	kill def
	set def = 4
	set def(1,"id") = 1, def(1,"dny") = "12345", def(1,"jen") = "", def(1,"bez") = ",21,32,"
	set def(2,"id") = 2, def(2,"dny") = "12345", def(2,"jen") = "", def(2,"bez") = ",22,31,"
	set def(3,"id") = 3, def(3,"dny") = "6",     def(3,"jen") = "", def(3,"bez") = ""
	set def(4,"id") = 4, def(4,"dny") = "7+",    def(4,"jen") = "", def(4,"bez") = ""
	for plk = "", ".", "12345", "X", "6", "7", "16", "67+" {
		for cnz = "", "21", "22", "31", "32", "99" {
			do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, plk, cnz), $classmethod(cls, "ZarazeniSpoje", 3, plk, cnz, nzCfg), "FTL3 parita, plk="_plk_" cnz="_cnz)
		}
	}
}

/// Vědomá odchylka od legacy: linka BEZ NZ konfigurace dává v legacy FTL1/3 pracovní spoj jen do
/// 1. sloupce (prázdné nzftl1 == prázdné cnz je „exact match"); preset s prázdnými seznamy značek
/// ho dá do obou pracovních sloupců. Degenerovaný případ (prázdninové rozvržení bez prázdninových
/// značek nedává smysl) - test odchylku dokumentuje, aby nezanikla mlčky.
Method TestDefiniceFtl1BezNzKonfigurace()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	kill def
	set def = 3
	set def(1,"id") = 1, def(1,"dny") = "12345", def(1,"jen") = "", def(1,"bez") = ""
	set def(2,"id") = 2, def(2,"dny") = "12345", def(2,"jen") = "", def(2,"bez") = ""
	set def(3,"id") = 3, def(3,"dny") = "67+",   def(3,"jen") = "", def(3,"bez") = ""
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpoje", 1, "", "", ""), "1,3", "legacy: prázdná NZ cfg → jen 1. pracovní sloupec")
	do $$$AssertEquals($classmethod(cls, "ZarazeniSpojeDleDefinice", .def, "", ""), "1,2,3", "definice: bez vyloučení → oba pracovní sloupce (vědomě jinak)")
}

/// Denní značky u vlastních sloupců - parita s legacy DenniZnacky: oddělené víkendové sloupce
/// (jako FTL 0/3) → víkend bez značek; slitý víkend 6+7+svátek (jako FTL 1/2) → podmíněné značky.
Method TestDenniZnackyDleDefinice()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	// oddělené: So | Ne+svátky (žádný sloupec neslévá 6,7,+)
	kill sep
	set sep = 3
	set sep(1,"dny") = "12345", sep(2,"dny") = "6", sep(3,"dny") = "7+"
	for plk = "", ".", "X", "15", "6", "7", "+", "67", "67+", "16" {
		do $$$AssertEquals($classmethod(cls, "DenniZnackyDleDefinice", .sep, plk), $classmethod(cls, "DenniZnacky", 0, plk), "oddělený víkend = parita FTL0, plk="_plk)
	}
	// slitý víkend: Prac | So+Ne+svátky
	kill mrg
	set mrg = 2
	set mrg(1,"dny") = "12345", mrg(2,"dny") = "67+"
	for plk = "", ".", "X", "15", "6", "7", "+", "67", "67+", "16", "7+" {
		do $$$AssertEquals($classmethod(cls, "DenniZnackyDleDefinice", .mrg, plk), $classmethod(cls, "DenniZnacky", 2, plk), "slitý víkend = parita FTL2, plk="_plk)
	}
}

}
