Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci LineElement properties.
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace:
/// Tato class je MANUAL MIRROR PropertyDescriptors v `LineElement.cs`. Stejné schema
/// existuje na 2 místech (C# + Caché) kvůli Caché patchovatelnosti. Při změně property
/// MUST update obojí. Viz memory `project_edinputform_schema_duplication.md`.
///
/// IN parametry pro pre-fill viz Init metoda. Save je no-op — host extrahuje hodnoty
/// z Items po úspěšné validaci a aplikuje je na LineElement v Template modelu.
Class ALVA.TemplateDesigner.EDInputForm.LineElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        // Read initial values (všechny optional)
        s initOrientation = $g(^||ML("IN","orientation"))
        s initColor       = $g(^||ML("IN","color"))
        s initStrokeWidth = $g(^||ML("IN","strokeWidth"))
        s initStyle       = $g(^||ML("IN","style"))
        s initDashMm      = $g(^||ML("IN","dashMm"))
        s initGapMm       = $g(^||ML("IN","gapMm"))
        s initX           = $g(^||ML("IN","x"))
        s initY           = $g(^||ML("IN","y"))
        s initWidth       = $g(^||ML("IN","width"))
        s initHeight      = $g(^||ML("IN","height"))
        s initRotation    = $g(^||ML("IN","rotation"))
        s initZIndex      = $g(^||ML("IN","zIndex"))
        s initLocked      = $g(^||ML("IN","isLocked"))
        s initVisExpr     = $g(^||ML("IN","visibilityExpression"))

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Čára" ────────────────────────────────────────────────
        d form.AddGroup("line", "Čára")

        s f = form.AddFieldToLastGroup("orientation", "Orientace", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Horizontal", "Vodorovná")
        d f.AddListItem("Vertical", "Svislá")
        s f.value = $s(initOrientation'="":initOrientation,1:"Horizontal")

        s f = form.AddFieldToLastGroup("color", "Barva", $$$FieldTypeColor, 0, 0)
        s f.value = $s(initColor'="":initColor,1:"#FF000000")

        d form.AddDecimal("strokeWidth", "Tloušťka", $s(initStrokeWidth'="":initStrokeWidth,1:0.5), 0.1, 10, "mm")

        s f = form.AddFieldToLastGroup("style", "Styl", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Solid", "Plná")
        d f.AddListItem("Dashed", "Čárkovaná")
        d f.AddListItem("Dotted", "Tečkovaná")
        s f.value = $s(initStyle'="":initStyle,1:"Solid")

        s f = form.AddDecimal("dashMm", "Délka čárky", initDashMm, 0.1, 20, "mm")
        s f.helpText = "Prázdné = výchozí (Dashed=3mm, Dotted=1mm)"

        s f = form.AddDecimal("gapMm", "Délka mezery", initGapMm, 0.1, 20, "mm")
        s f.helpText = "Prázdné = výchozí (Dashed=2mm, Dotted=1.5mm)"

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("x", "X", $s(initX'="":initX,1:0), 0, "", "mm", "", 1)
        d form.AddDecimal("y", "Y", $s(initY'="":initY,1:0), 0, "", "mm", "", 1)
        d form.AddDecimal("width", "Šířka", $s(initWidth'="":initWidth,1:50), 0, "", "mm", "", 1)
        d form.AddDecimal("height", "Výška", $s(initHeight'="":initHeight,1:0), 0, "", "mm")
        d form.AddDecimal("rotation", "Rotace", $s(initRotation'="":initRotation,1:0), -360, 360, "°")
        s f = form.AddNumber("zIndex", "ZIndex", $s(initZIndex'="":initZIndex,1:0), 0, 100)
        s f.helpText = "Pořadí vrstev (vyšší = nad nižším)"

        // ── Skupina "Pokročilé" ───────────────────────────────────────────
        d form.AddGroup("advanced", "Pokročilé")
        s f = form.AddBool("isLocked", "Uzamčeno", $s(initLocked'="":initLocked,1:0))
        s f.helpText = "Uzamčený element nelze přesunout ani změnit jeho velikost"
        s f = form.AddText("visibilityExpression", "Podmínka viditelnosti", initVisExpr)
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

/// Save je no-op — LineElement je in-memory .NET model, host řeší aplikaci.
ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

}
