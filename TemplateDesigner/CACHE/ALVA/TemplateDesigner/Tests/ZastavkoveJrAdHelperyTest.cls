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

}
