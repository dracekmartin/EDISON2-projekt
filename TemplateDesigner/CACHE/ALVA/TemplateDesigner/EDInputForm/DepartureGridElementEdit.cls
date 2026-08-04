Include (GENERAL, EDInputForm)

/// EDInputForm schema pro tab „Obecné" editoru DepartureGridElement (mřížka odjezdů).
/// Field key = property path na C# DepartureGridElement. Stejná 4-skupinová kostra jako
/// StopListElementEdit: Obecné · Styl záhlaví · Styl datových buněk · Odjezdy-specifické (+ Pozice/Podmínky).
/// Stylové balíčky přes ALVA.TemplateDesigner.EDInputForm.StyleFields (jediný zdroj sady polí).
///
/// Element-level styl = prostá pole (element je báze kaskády); per-sloupec/skupina override řeší
/// CellStyleEditor v tabu „Sloupce". Lazy/legacy-scalar nuance (HeaderLine2Style alokace,
/// MinuteStyle/Anotace fallback scaláry, HeaderVerticalAlignment fallback, SetHeaderFontSize cleanup)
/// dořeší C# host (DepartureGroupEditorDialog) v Init/OnOk - viz tam.
///
/// ⚠️ Schema je manual mirror PropertyDescriptors v DepartureGridElement.cs (viz memory
/// project_edinputform_schema_duplication.md).
Class ALVA.TemplateDesigner.EDInputForm.DepartureGridElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Obecné ────────────────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        d form.AddBool("ShowSkupinaDnuHeader", "Záhlaví skupin dnů", 1)
        // Šířka sloupce hodin se edituje v tabu „Sloupce" (řádek Hodiny) - jediný zdroj, ať nekoliduje.
        d form.AddBool("HourColumnPerGroup", "Hodiny u každé skupiny", 0)
        s f = form.AddNumber("MaxGroupCount", "Max skupin (0 = ∞)", 0, 0, 10)
        d form.AddDecimal("MinuteWidth", "Šířka minuty", 6, 3, 15, "mm")
        d form.AddDecimal("GroupSeparatorWidth", "Oddělovač skupin", 0, 0, 5, "mm")
        d form.AddDecimal("AnotaceGap", "Mezera minuta ↔ anotace", "", 0, 5, "mm")
        d form.AddDecimal("AnotaceInterGap", "Mezera anotace ↔ anotace", "", 0, 5, "mm")
        d form.AddDecimal("OdjezdGap", "Mezera mezi odjezdy", "", 0, 10, "mm")
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 50, "mm")
        s f = form.AddFieldToLastGroup("AlternatingRowColor", "Střídání řádků (prázdné = žádné)", $$$FieldTypeColor, 0, 0)
        // Časová osa (rozsah hodin) i „Roztáhnout na výšku" jsou parametry sestavy
        // (CASOVA_OSA/HODINY_OD/DO, ROZTAHNOUT_NA_VYSKU), sdílené a autoritativní - NEduplikovat
        // jako pole prvku, parametr by je přebil a klik by nic nedělal (hlášení B22/B24).

        // ── Styl záhlaví ──────────────────────────────────────────────────
        // PROTOTYP: styl jako tlačítko → pod-formulář (StyleEditDialog). Klíč „style:<prefix>"
        // (ne property path) → ElementFormBinder ho ignoruje; C# host z něj vytáhne prefix.
        d form.AddGroup("hstyle", "Styl záhlaví")
        s f = form.AddFieldToLastGroup("style:HeaderStyle", "Styl záhlaví", $$$FieldTypeButton, 0, 0)
        s f.value = "Upravit styl…"
        d form.AddDecimal("SkupinaDnuHeaderLineHeight", "Min. výška řádku záhlaví", 4, 2, 10, "mm")
        s f = form.AddFieldToLastGroup("style:HeaderLine2Style", "Styl 2. řádku nadpisu", $$$FieldTypeButton, 0, 0)
        s f.value = "Upravit styl…"

        // Styl datových buněk: DataStyle = báze (minuty i hodinová čísla z ní dědí),
        // MinuteStyle = minutový override navrch (prefix „Minuta:").
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF000000", "")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "MinuteStyle", "", "", "", "", 0, "Minuta: ")

        // ── Odjezdy - specifické (anotace = horní indexy/značky u minut) ──
        d form.AddGroup("departure", "Odjezdy - specifické")
        d form.AddBool("ShowAnotace", "Zobrazit anotace", 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceSymbolStyle", "", "", "", "", 0, "Symbol: ")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceTextStyle", "", "", "", "", 0, "Text: ")

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 100, 60)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

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
