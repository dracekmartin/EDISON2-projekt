Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci LineTableElement properties (knižní JŘ —
/// matice zastávky × spoje).
///
/// Field key = property path na C# LineTableElement (vč. nested HeaderStyle.X / DataStyle.X).
/// Init jen definuje strukturu + defaults; generický ElementEditDialog dělá pre-fill přes reflexi.
///
/// Pozn.: LineTable v prvním řezu NEMÁ per-column kolekci (sloupce spojů vznikají z dat,
/// popisové sloupce jsou pevné properties) → čistý generic kandidát jako NotesLegend.
/// Per-column overridy (2-tab dialog se sdíleným ColumnListEditor) přijdou s R1 extrakcí
/// tabulkového jádra.
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace: manual mirror PropertyDescriptors v
/// `LineTableElement.cs`. Viz memory `project_edinputform_schema_duplication.md`.
Class ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // Pozn.: viditelnost / šířky / pořadí / per-column styl jednotlivých sloupců
        // (Zóna, km, Tč, Zastávka, MHD, Spoj) se editují v tabu „Sloupce" (ColumnListEditor),
        // konzistentně se StopList/DepartureGrid. Tady jen element-wide vlastnosti.

        // ── Skupina "Řádky" ───────────────────────────────────────────────
        d form.AddGroup("rows", "Řádky")
        d form.AddBool("ShowGroupHeaders", "Nadpisy skupin dnů", 1)
        d form.AddBool("ShowPlatnostRow", "Řádek platnosti", 1)
        d form.AddBool("ShowNzRow", "Řádek negativních značek", 1)
        d form.AddBool("ShowPripojeRows", "Přípojné řádky (vyčká/navazuje)", 1)
        s f = form.AddDecimal("DataRowHeight", "Výška řádku", "", 1, 20, "mm")
        s f.helpText = "Prázdné = auto podle velikosti písma"

        // ── Skupina "Záhlaví" ─────────────────────────────────────────────
        d form.AddGroup("header", "Záhlaví")
        s f = form.AddFieldToLastGroup("HeaderStyle.BackgroundColor", "Pozadí záhlaví", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFE0E0E0"
        s f = form.AddFieldToLastGroup("HeaderStyle.TextColor", "Barva textu záhlaví", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        // ── Skupina "Datové buňky" ────────────────────────────────────────
        d form.AddGroup("data", "Datové buňky")
        s f = form.AddFieldToLastGroup("DataStyle.BackgroundColor", "Pozadí buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        s f = form.AddFieldToLastGroup("DataStyle.TextColor", "Barva textu buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        d form.AddBool("HighlightOddTc", "Podbarvit lichá Tč", 1)
        s f = form.AddFieldToLastGroup("OddTcBackColor", "Barva lichých Tč", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFEEEEEE"
        s f = form.AddFieldToLastGroup("GridColor", "Barva mřížky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFD0D0D0"

        // ── Skupina "Rámeček elementu" ────────────────────────────────────
        d form.AddGroup("border", "Rámeček elementu")
        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        d form.AddDecimal("BorderWidth", "Tloušťka rámečku", 0, 0, 5, "mm")

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 190, 1, "", "mm", "", 1)
        s f = form.AddDecimal("Height", "Výška", "", 0, "", "mm")
        s f.helpText = "Prázdné = dynamická výška (počítá engine)"
        d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
        s f = form.AddNumber("ZIndex", "ZIndex", 0, 0, 100)
        s f.helpText = "Pořadí vrstev (vyšší = nad nižším)"

        // ── Skupina "Pokročilé" ───────────────────────────────────────────
        d form.AddGroup("advanced", "Pokročilé")
        s f = form.AddText("ZastavkyDataSource", "Kolekce zastávek", "ZASTAVKY")
        s f = form.AddText("SpojeDataSource", "Kolekce spojů", "SPOJE")
        s f = form.AddText("SkupinyDnuDataSource", "Kolekce skupin dnů", "SKUPINY_DNU")
        s f = form.AddBool("IsLocked", "Uzamčeno", 0)
        s f.helpText = "Uzamčený element nelze přesunout ani změnit jeho velikost"
        s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
        s f.helpText = "Např. {POCET} > 0 — element se zobrazí jen když podmínka platí"
        s f = form.AddText("RowHighlightExpression", "Zvýraznit řádek (podmínka)")
        s f.helpText = "Zastávka splňující podmínku (např. {ZONA} == ""1"") dostane pozadí ""Barva zvýraznění"""
        s f = form.AddFieldToLastGroup("RowHighlightColor", "Barva zvýraznění (prázdné = žádné)", $$$FieldTypeColor, 0, 0)

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Validate(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()
        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

}
