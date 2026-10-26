Include %occInclude

/// `B194a` C9: data v textech omezení linkového JŘ AD. Režim tisku roku je u linky uložený o 1 výš
/// než u firmy a do 26. 10. se neodečítal - 5 tisíc linek s „rokem dvoumístně" tisklo data bez roku.
/// Text dat se porovnává **se starým tiskem** (`gComOddO^JRapiNZUtl2`) ve všech čtyřech režimech,
/// ne s naším odtiskem (R6).
Class ALVA.TemplateDesigner.Tests.LinkovyJrAdDatumyTest Extends ALVA.EDTesting.MLTestCase
{

/// Data omezení jako v `^JRL(sku,lin,smer,sp,omzId)`: rozsahy, jednotlivá data, nuly navíc, shodné
/// „od" a „do", rozsah přes konec roku.
Parameter VZORY = "01.09.2026:30.06.2027|24.12.2026#25.12.2026|1.5.2026:31.8.2026#24.12.2026|05.07.2026:05.07.2026|28.12.2026:2.1.2027|3.4.2026";

/// Režim u linky = uložená hodnota - 1; bez nastavení u linky platí nastavení firmy.
Method TestRezimLinkyPosunutyOJedna()
{
	set db = $namespace, sku = "TDTEST"_$translate($system.Util.CreateGUID(), "-"), lin = "0999999"
	set ad = "ALVA.TemplateDesigner.Agendy.LinkovyJrAd"
	try {
		set ^[db]JRL(sku, lin, "I") = ""
		do $$$AssertEquals($classmethod(ad, "TiskDtm", db, sku, lin), +$get(^[db]JRSET("Definice","TiskDtm")), "bez nastavení u linky → firma")
		for ulozeno = 1, 2, 4 {
			set ^[db]JRL(sku, lin, "PATCH", "TiskDtm") = ulozeno
			do $$$AssertEquals($classmethod(ad, "TiskDtm", db, sku, lin), ulozeno - 1, "uloženo "_ulozeno)
		}
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]JRL(sku)
}

/// Text dat shodný se starým tiskem v režimech 0-3 (mezery na krajích se nepočítají - starý tisk
/// dává mezeru před „od", naše skládání ji přidá až za předponou omezení).
Method TestDatumyJakoStaryTisk()
{
	if $text(gComOddO^JRapiNZUtl2) = "" {
		do $$$AssertSkipped("starý modul JŘ (JRapiNZUtl2) není na tomto serveru")
		quit
	}
	// Slova „od"/„do" bere starý tisk z číselníku firmy (my napevno) - porovnává se proto nad firmou,
	// která je má v číselníku ve výchozím tvaru; testovací namespace ho má prázdný.
	set db = "CB_CSADAD", rec = "CZ"
	if '##class(%SYS.Namespace).Exists(db) || ($get(^[db]JRSET("NCOME", rec, "ODDO")) '= "od~do") {
		do $$$AssertSkipped("firma "_db_" s číselníkem od/do na tomto serveru není")
		quit
	}
	for rezim = 0:1:3 {
		kill set
		set set("SET") = 1, set("SET", 1, "arr", 7, 29) = rezim
		for v = 1:1:$length(..#VZORY, "|") {
			set vzor = $piece(..#VZORY, "|", v)
			set legacy = ""
			set ok = $$gComOddO^JRapiNZUtl2("", db, rec, vzor, .legacy, .set)
			set nas = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).FormatDatumyOmezeni(vzor, rezim)
			do $$$AssertEquals(nas, $zstrip(legacy, "<>W"), "režim "_rezim_", "_vzor_": náš „"_nas_""" × starý „"_legacy_"""")
		}
	}
}

}
