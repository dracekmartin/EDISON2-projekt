Include (GENERAL, EDInputForm)

/// EDInputForm schema pro tab „Obecné" editoru DepartureGridElement (mřížka odjezdů).
/// Field key = property path na C# DepartureGridElement. Stejná 4-skupinová kostra jako
/// StopListElementEdit: Obecné · Styl záhlaví · Styl datových buněk · Odjezdy-specifické (+ Pozice/Podmínky).
/// Stylové balíčky přes ALVA.TemplateDesigner.EDInputForm.StyleFields (jediný zdroj sady polí).
///
/// Element-level styl = prostá pole (element je báze kaskády); per-sloupec/skupina override řeší
/// CellStyleEditor v tabu „Sloupce". Lazy/legacy-scalar nuance (HeaderLine2Style alokace,
/// MinuteStyle/Anotace fallback scaláry, HeaderVerticalAlignment fallback, SetHeaderFontSize cleanup)
/// dořeší C# host (DepartureGroupEditorDialog) v Init/OnOk — viz tam.
///
/// ⚠️ Schema je manual mirror PropertyDescriptors v DepartureGridElement.cs (viz memory
/// project_edinputform_schema_duplication.md).
Class ALVA.TemplateDesigner.EDInputForm.DepartureGridElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Obecné ────────────────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        s f = form.AddFieldToLastGroup("GridColor", "Barva mřížky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFD0D0D0"
        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        d form.AddDecimal("BorderWidth", "Šířka rámečku", 0, 0, 5, "mm")
        d form.AddBool("ShowSkupinaDnuHeader", "Záhlaví skupin dnů", 1)
        // Šířka sloupce hodin se edituje v tabu „Sloupce" (řádek Hodiny) — jediný zdroj, ať nekoliduje.
        d form.AddBool("HourColumnPerGroup", "Hodiny u každé skupiny", 0)
        s f = form.AddNumber("MaxGroupCount", "Max skupin (0 = ∞)", 0, 0, 10)
        d form.AddDecimal("MinuteWidth", "Šířka minuty", 6, 3, 15, "mm")
        d form.AddDecimal("GroupSeparatorWidth", "Oddělovač skupin", 0, 0, 5, "mm")
        d form.AddDecimal("AnotaceGap", "Mezera minuta ↔ anotace", "", 0, 5, "mm")
        d form.AddDecimal("AnotaceInterGap", "Mezera anotace ↔ anotace", "", 0, 5, "mm")
        d form.AddDecimal("OdjezdGap", "Mezera mezi odjezdy", "", 0, 10, "mm")
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 50, "mm")
        s f = form.AddFieldToLastGroup("AlternatingRowColor", "Zebra barva (prázdné = žádná)", $$$FieldTypeColor, 0, 0)
        d form.AddBool("AutoExpandToFillHeight", "Roztáhnout na výšku", 0)
        s f = form.AddFieldToLastGroup("TimeAxisMode", "Časová osa", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Compact", "Souvislá bez mezer"), f.AddListItem("DataBounded", "Souvislá s mezerami")
        d f.AddListItem("Range", "Vlastní rozsah"), f.AddListItem("Full", "Celý den (0–23)")
        s f = form.AddNumber("TimeAxisRangeFrom", "Hodiny od", 0, 0, 23)
        s f = form.AddNumber("TimeAxisRangeTo", "Hodiny do", 23, 0, 23)

        // ── Styl záhlaví / dat (sdílený balíček polí) ─────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Styl záhlaví", "#FF000000", "")
        d form.AddDecimal("SkupinaDnuHeaderLineHeight", "Min. výška řádku záhlaví", 4, 2, 10, "mm")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderLine2Style", "", "", "", "", 0, "2. řádek: ")

        // Styl datových buněk: DataStyle = báze (minuty i hodinová čísla z ní dědí),
        // MinuteStyle = minutový override navrch (prefix „Minuta:").
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF000000", "")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "MinuteStyle", "", "", "", "", 0, "Minuta: ")

        // ── Odjezdy — specifické (anotace = horní indexy/značky u minut) ──
        d form.AddGroup("departure", "Odjezdy — specifické")
        d form.AddBool("ShowAnotace", "Zobrazit anotace", 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceSymbolStyle", "", "", "", "", 0, "Symbol: ")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceTextStyle", "", "", "", "", 0, "Text: ")

        // ── Pozice ────────────────────────────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 100, 1, "", "mm", "", 1)
        d form.AddDecimal("Height", "Výška", 60, 1, "", "mm")
        d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
        s f = form.AddNumber("ZIndex", "ZIndex", 0, 0, 100)
        d form.AddBool("IsLocked", "Uzamčeno", 0)

        // ── Podmínky ──────────────────────────────────────────────────────
        d form.AddGroup("advanced", "Podmínky")
        s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
        s f.helpText = "Např. {POCET} > 0 — element se zobrazí jen když podmínka platí"

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
