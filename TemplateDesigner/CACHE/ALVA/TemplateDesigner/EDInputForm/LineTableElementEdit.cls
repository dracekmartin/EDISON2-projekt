Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci LineTableElement properties (knižní JŘ -
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
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace: manual mirror PropertyDescriptors v
/// `LineTableElement.cs`. Viz memory `project_edinputform_schema_duplication.md`.
Class ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
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
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 20, "mm")
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

        // ── Skupina "Přípojné řádky" (#8360) ──────────────────────────────
        // Přípoj má jít odlišit od časů spojů - obvykle menším kurzívním písmem, aby v tabulce
        // „nekřičel" stejně jako odjezdy. Prázdné = dědí se z datových buněk.
        d form.AddGroup("pripoj", "Přípojné řádky")
        s f = form.AddDecimal("PripojStyle.FontSize", "Velikost", "", 4, 200, "pt")
        s f.helpText = "Prázdné = jako datové buňky"
        d form.AddBool("PripojStyle.Bold", "Tučné", 0)
        d form.AddBool("PripojStyle.Italic", "Kurzíva", 0)
        s f = form.AddFieldToLastGroup("PripojStyle.TextColor", "Barva textu", $$$FieldTypeColor, 0, 0)

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form, 1, 1, 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 190)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

        // Kolekce zdrojů dat + řádkové podmínky (element-specifické, do skupiny "Podmínky" výše)
        s f = form.AddText("ZastavkyDataSource", "Kolekce zastávek", "ZASTAVKY")
        s f = form.AddText("SpojeDataSource", "Kolekce spojů", "SPOJE")
        s f = form.AddText("SkupinyDnuDataSource", "Kolekce skupin dnů", "SKUPINY_DNU")
        s f = form.AddText("RowVisibilityExpression", "Viditelnost řádku")
        s f.placeholders = 1
        s f.helpText = "Zastávka, kde výraz neplatí, se nezobrazí (i s přípojnými řádky). Pole řádku: POPIS, TC, ZONA, MHD, TYP, PORADI, NASTUPISTE. Např. {ZONA} == ""1"""
        s f = form.AddFieldToLastGroup("__highlightRules", "Zvýraznění řádků", $$$FieldTypeButton, 0, 0)
        s f.value = "Upravit pravidla…"
        s f.helpText = "Seznam pravidel: podmínka → barva pozadí řádku. První splněné vyhrává."

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Validate(ByRef sys As %String) As %Status
{
    s err = ""
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
