Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci LineElement properties.
///
/// Field key = property path na C# LineElement. Generický ElementEditDialog
/// čte/zapisuje hodnoty přes reflexi — Init NEpotřebuje IN params, jen definuje
/// strukturu + default hodnoty.
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace: manual mirror PropertyDescriptors v
/// `LineElement.cs` kvůli Caché patchovatelnosti. Viz memory
/// `project_edinputform_schema_duplication.md`.
///
/// Save je no-op — LineElement je in-memory .NET model, host aplikuje hodnoty sám.
Class ALVA.TemplateDesigner.EDInputForm.LineElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
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

        d form.AddDecimal("StrokeWidth", "Tloušťka", 0.5, 0.1, 10, "mm")

        s f = form.AddFieldToLastGroup("Style", "Styl", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Solid", "Plná")
        d f.AddListItem("Dashed", "Čárkovaná")
        d f.AddListItem("Dotted", "Tečkovaná")
        s f.value = "Solid"

        s f = form.AddDecimal("CustomDashMm", "Délka čárky", "", 0.1, 20, "mm")
        s f.helpText = "Prázdné = výchozí (Dashed=3mm, Dotted=1mm)"

        s f = form.AddDecimal("CustomGapMm", "Délka mezery", "", 0.1, 20, "mm")
        s f.helpText = "Prázdné = výchozí (Dashed=2mm, Dotted=1.5mm)"

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 50, 0, "", "mm", "", 1)
        d form.AddDecimal("Height", "Výška", "", 0, "", "mm")
        d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
        s f = form.AddNumber("ZIndex", "ZIndex", 0, 0, 100)
        s f.helpText = "Pořadí vrstev (vyšší = nad nižším)"

        // ── Skupina "Pokročilé" ───────────────────────────────────────────
        d form.AddGroup("advanced", "Pokročilé")
        s f = form.AddBool("IsLocked", "Uzamčeno", 0)
        s f.helpText = "Uzamčený element nelze přesunout ani změnit jeho velikost"
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
