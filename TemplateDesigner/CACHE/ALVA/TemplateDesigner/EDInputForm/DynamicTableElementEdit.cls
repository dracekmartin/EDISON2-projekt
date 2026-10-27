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
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Obecné ────────────────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        // Zdroj dat = List pole; položky (kolekce agendy) plní klient (PopulateDataSourceField) - jsou
        // dynamické per agenda, Caché je nezná. Formulář se re-Inituje při každém návratu na tab
        // (EDInputForm auto-Init na Loaded), takže dialog po Prefillu znovu aplikuje vybraný zdroj
        // z dialog-scoped stavu (viz DynamicTableEditorDialog).
        d form.AddFieldToLastGroup("DataSource", "Zdroj dat", $$$FieldTypeList, 0, 0)
        d form.AddBool("ShowHeader", "Záhlaví sloupců", 1)
        d form.AddDecimal("RowHeight", "Výška řádku", 6, 1, 50, "mm")
        d form.AddDecimal("HeaderHeight", "Výška záhlaví", 8, 2, 50, "mm")
        // Řazení řádků: sloupce jsou per element, Caché je nezná - položky plní klient
        // (PopulateSortFields), stejně jako u „Zdroj dat". Prázdné = pořadí z agendy.
        s f = form.AddFieldToLastGroup("SortField", "Řadit podle", $$$FieldTypeList, 0, 0)
        s f.helpText = "Prázdné = pořadí, v jakém řádky vrátí databáze. Číslo, datum i text se poznají z hodnot."
        s f = form.AddFieldToLastGroup("SortDescending", "Směr", $$$FieldTypeList, 0, 0)
        d f.AddListItem("0", "Vzestupně"), f.AddListItem("1", "Sestupně")
        s f.value = "0"
        s f = form.AddFieldToLastGroup("ThenByField", "Potom podle", $$$FieldTypeList, 0, 0)
        s f = form.AddFieldToLastGroup("ThenByDescending", "Směr (druhý klíč)", $$$FieldTypeList, 0, 0)
        d f.AddListItem("0", "Vzestupně"), f.AddListItem("1", "Sestupně")
        s f.value = "0"
        s f = form.AddFieldToLastGroup("AlternatingRowColor", "Střídání řádků (prázdné = žádné)", $$$FieldTypeColor, 0, 0)
        s f = form.AddNumber("MinRows", "Min. řádků (0 = ne)", 0, 0, 200)
        s f.helpText = "Doplní prázdné řádky do tohoto počtu - pevná výška boxu (např. faktura). 0 = bez minima"
        s f = form.AddNumber("MaxRows", "Max. řádků (0 = bez limitu)", 0, 0, 500)
        s f.helpText = "Ořízne přebytečné datové řádky. 0 = bez omezení"

        // ── Styl záhlaví / dat (sdílený balíček polí) ─────────────────────
        // Pozadí bez výchozí hodnoty - nese ho prvek sám, prázdno znamená bez podbarvení.
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Styl záhlaví", "#FF333333", "", 1, "", 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF222222", "#00FFFFFF")

        // ── Base pole (rámeček elementu formulář dřív neměl vůbec - šel nastavit jen v panelu) ─
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form, 1, "#FF999999", 0.19)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form, 1, 0, 0, "#FFCCCCCC")
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 50)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

        s f = form.AddText("RowVisibilityExpression", "Viditelnost řádku")
        s f.placeholders = 1
        s f.helpText = "Podmínka nad daty řádku - řádky, kde neplatí, se nevykreslí. Použijte názvy polí ze sloupců (záložka Sloupce). Např. {CENA} > 0"
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
