Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci BarcodeElement properties.
///
/// Field key = property path na C# BarcodeElement. Generický ElementEditDialog
/// čte/zapisuje hodnoty přes reflexi — Init jen definuje strukturu + defaults.
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace: manual mirror PropertyDescriptors v
/// `BarcodeElement.cs`. Viz memory `project_edinputform_schema_duplication.md`.
Class ALVA.TemplateDesigner.EDInputForm.BarcodeElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Kód" ─────────────────────────────────────────────────
        d form.AddGroup("code", "Kód")
        d form.AddText("Content", "Obsah")

        s f = form.AddFieldToLastGroup("BarcodeType", "Typ kódu", $$$FieldTypeList, 0, 1)
        d f.AddListItem("QR", "QR kód")
        d f.AddListItem("Code128", "Code 128")
        d f.AddListItem("Ean13", "EAN-13")
        d f.AddListItem("Ean8", "EAN-8")
        d f.AddListItem("Pdf417", "PDF417")
        d f.AddListItem("DataMatrix", "DataMatrix")
        s f.value = "QR"

        // ── Skupina "Vzhled" ──────────────────────────────────────────────
        d form.AddGroup("appearance", "Vzhled")

        s f = form.AddFieldToLastGroup("ForeColor", "Barva kódu", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        s f = form.AddFieldToLastGroup("BackgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 30, 1, "", "mm", "", 1)
        d form.AddDecimal("Height", "Výška", 30, 1, "", "mm", "", 1)
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
