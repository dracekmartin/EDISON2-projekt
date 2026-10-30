Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci LineElement properties.
///
/// Field key = property path na C# LineElement. Generický ElementEditDialog
/// čte/zapisuje hodnoty přes reflexi - Init NEpotřebuje IN params, jen definuje
/// strukturu + default hodnoty.
///
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace: manual mirror PropertyDescriptors v
/// `LineElement.cs` kvůli Caché patchovatelnosti. Viz memory
/// `project_edinputform_schema_duplication.md`.
///
/// Save je no-op - LineElement je in-memory .NET model, host aplikuje hodnoty sám.
Class ALVA.TemplateDesigner.EDInputForm.LineElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Čára" ────────────────────────────────────────────────
        d form.AddGroup("line", "Čára")

        s f = form.AddFieldToLastGroup("Orientation", "Orientace", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Horizontal", "Vodorovná")
        d f.AddListItem("Vertical", "Svislá")
        s f.value = "Horizontal"

        s f = form.AddFieldToLastGroup("Color", "Barva", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        s f.helpText = "Lze zadat i {BARVA_LINKY} nebo {BARVA_LINKY_2} - barvu vezme z jízdního řádu, kde linka může mít víc barev (nenastavená = bílá)"

        d form.AddDecimal("StrokeWidth", "Tloušťka", 0.5, 0.1, 10, "mm")

        s f = form.AddFieldToLastGroup("Style", "Styl", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Solid", "Plná")
        d f.AddListItem("Dashed", "Čárkovaná")
        d f.AddListItem("Dotted", "Tečkovaná")
        s f.value = "Solid"

        s f = form.AddDecimal("CustomDashMm", "Délka čárky", "", 0.1, 20, "mm")
        s f.helpText = "Prázdné = výchozí (čárkovaná 3 mm, tečkovaná 1 mm)"

        s f = form.AddDecimal("CustomGapMm", "Délka mezery", "", 0.1, 20, "mm")
        s f.helpText = "Prázdné = výchozí (čárkovaná 2 mm, tečkovaná 1,5 mm)"

        // ── Base pole (čára nemá plochu → bez rohů; nulová šířka je u svislé legitimní) ────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form, 0)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 50, "", 0, 0, 0)
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
