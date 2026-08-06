Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci RectangleElement properties.
///
/// Field key = property path na C# RectangleElement. Generický ElementEditDialog
/// čte/zapisuje hodnoty přes reflexi - Init jen definuje strukturu + defaults.
///
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace: manual mirror PropertyDescriptors v
/// `RectangleElement.cs`. Viz memory `project_edinputform_schema_duplication.md`.
Class ALVA.TemplateDesigner.EDInputForm.RectangleElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Vzhled" ──────────────────────────────────────────────
        d form.AddGroup("appearance", "Vzhled")

        s f = form.AddFieldToLastGroup("BackgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        s f.helpText = "Lze zadat i {BARVA_LINKY} nebo {BARVA_LINKY_2} - barvu vezme z jízdního řádu, kde linka může mít víc barev (nenastavená = bílá)"

        s f = form.AddFieldToLastGroup("StrokeColor", "Barva okraje", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        d form.AddDecimal("StrokeWidth", "Tloušťka okraje", 0.5, 0, 10, "mm")

        // Base pole (rámeček elementu je nezávislý na vlastní výplni/okraji obdélníku výše).
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 50, 20, 1)
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
