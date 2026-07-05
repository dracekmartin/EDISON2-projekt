Include (GENERAL, EDInputForm)

/// EDInputForm schema pro tab „Obecné" editoru DynamicTableElement (dynamická tabulka).
/// Field key = property path na C# DynamicTableElement. Embeduje se do DynamicTableEditorDialog
/// vedle editoru sloupců (tab „Sloupce"). DataSource = ComboBox naplněný klientsky kolekcemi
/// aktuální agendy (viz DynamicTableEditorDialog.PopulateDataSourceField).
///
/// Element-level styl záhlaví/buněk je zde jako prostá pole (element je báze kaskády);
/// per-sloupec override řeší CellStyleEditor v tabu „Sloupce".
///
/// ⚠️ Schema je manual mirror PropertyDescriptors v DynamicTableElement.cs (viz memory
/// project_edinputform_schema_duplication.md).
Class ALVA.TemplateDesigner.EDInputForm.DynamicTableElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Obecné ────────────────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        // Zdroj dat = List pole; položky (kolekce agendy) plní klient (PopulateDataSourceField) — jsou
        // dynamické per agenda, Caché je nezná. Formulář se re-Inituje při každém návratu na tab
        // (EDInputForm auto-Init na Loaded), takže dialog po Prefillu znovu aplikuje vybraný zdroj
        // z dialog-scoped stavu (viz DynamicTableEditorDialog).
        d form.AddFieldToLastGroup("DataSource", "Zdroj dat", $$$FieldTypeList, 0, 0)
        d form.AddBool("ShowHeader", "Záhlaví sloupců", 1)
        d form.AddDecimal("RowHeight", "Výška řádku", 6, 2, 50, "mm")
        d form.AddDecimal("HeaderHeight", "Výška záhlaví", 8, 2, 50, "mm")
        s f = form.AddFieldToLastGroup("BorderStyle", "Rámeček", $$$FieldTypeList, 0, 0)
        d f.AddListItem("None", "Žádný"), f.AddListItem("Outer", "Vnější"), f.AddListItem("All", "Vše")
        d f.AddListItem("RowsOnly", "Řádky"), f.AddListItem("ColumnsOnly", "Sloupce")
        s f = form.AddFieldToLastGroup("AlternatingRowColor", "Střídání řádků (prázdné = žádné)", $$$FieldTypeColor, 0, 0)

        // ── Styl záhlaví / dat (sdílený balíček polí) ─────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Styl záhlaví", "#FF333333", "#FFE8E8E8")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF222222", "#00FFFFFF")

        // ── Pozice ────────────────────────────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 50, 1, "", "mm", "", 1)
        d form.AddDecimal("Height", "Výška (prázdné = auto)", "", 1, "", "mm")
        d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
        s f = form.AddNumber("ZIndex", "ZIndex", 0, 0, 100)
        d form.AddBool("IsLocked", "Uzamčeno", 0)

        // ── Podmínky ──────────────────────────────────────────────────────
        d form.AddGroup("advanced", "Podmínky")
        s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
        s f.helpText = "Např. {POCET} > 0 — element se zobrazí jen když podmínka platí"
        s f = form.AddText("RowVisibilityExpression", "Viditelnost řádku")
        s f.helpText = "Podmínka nad daty řádku, např. {CENA} > 0 — řádky, kde neplatí, se nevykreslí"

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
