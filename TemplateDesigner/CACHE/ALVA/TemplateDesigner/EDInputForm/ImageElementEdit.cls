Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci ImageElement properties.
///
/// Field key = property path na C# ImageElement. Generický ElementEditDialog
/// čte/zapisuje hodnoty přes reflexi — Init jen definuje strukturu + defaults.
/// Obrázek (ImageBase64) je field typu IMAGE — náhled + upload řeší EDInputForm
/// přes host hook PickImageImpl (file dialog + downscale → Base64).
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace: manual mirror PropertyDescriptors v
/// `ImageElement.cs`. Viz memory `project_edinputform_schema_duplication.md`.
Class ALVA.TemplateDesigner.EDInputForm.ImageElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Obrázek" ─────────────────────────────────────────────
        d form.AddGroup("image", "Obrázek")

        s f = form.AddFieldToLastGroup("ImageBase64", "Obrázek", $$$FieldTypeImage, 0, 0)
        s f.helpText = "Nahraj obrázek ze souboru (PNG/JPG). Velké soubory se automaticky zmenší."

        s f = form.AddText("DataField", "Datové pole")
        s f.helpText = "Volitelné — název pole agendy, ze kterého se obrázek načte při tisku (místo statického)"

        s f = form.AddFieldToLastGroup("ScaleMode", "Škálování", $$$FieldTypeList, 0, 0)
        d f.AddListItem("Fit", "Vejít celý")
        d f.AddListItem("Fill", "Vyplnit (oříznout)")
        d f.AddListItem("Stretch", "Roztáhnout")
        d f.AddListItem("None", "Bez úpravy")
        s f.value = "Fit"

        // ── Skupina "Rámeček elementu" ────────────────────────────────────
        d form.AddGroup("border", "Rámeček elementu")
        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        d form.AddDecimal("BorderWidth", "Tloušťka rámečku", 0, 0, 5, "mm")
        s f = form.AddFieldToLastGroup("BackgroundColor", "Pozadí", $$$FieldTypeColor, 0, 0)
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
