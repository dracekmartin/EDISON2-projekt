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

}
