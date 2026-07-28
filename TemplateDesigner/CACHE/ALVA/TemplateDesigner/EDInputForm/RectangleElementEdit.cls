Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci RectangleElement properties.
///
/// Field key = property path na C# RectangleElement. Generický ElementEditDialog
/// čte/zapisuje hodnoty přes reflexi — Init jen definuje strukturu + defaults.
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace: manual mirror PropertyDescriptors v
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

        s f = form.AddFieldToLastGroup("StrokeColor", "Barva okraje", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        d form.AddDecimal("StrokeWidth", "Tloušťka okraje", 0.5, 0, 10, "mm")

        // ── Skupina "Rámeček elementu" ────────────────────────────────────
        // Base element border + rohy (nezávislé na vlastní výplni/okraji obdélníku výše).
        d form.AddGroup("border", "Rámeček elementu")
        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        d form.AddDecimal("BorderWidth", "Tloušťka rámečku", 0, 0, 5, "mm")
        s f = form.AddFieldToLastGroup("CornerStyle", "Rohy", $$$FieldTypeList, 0, 0)
        d f.AddListItem("None", "Ostré"), f.AddListItem("Rounded", "Zaoblené"), f.AddListItem("Beveled", "Zkosené")
        s f.value = "None"
        d form.AddDecimal("CornerSize", "Velikost rohu", 0, 0, 50, "mm")

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 50, 1, "", "mm", "", 1)
        d form.AddDecimal("Height", "Výška", 20, 1, "", "mm", "", 1)
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
