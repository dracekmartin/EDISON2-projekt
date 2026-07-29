Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci ImageElement properties.
///
/// Field key = property path na C# ImageElement. Generický ElementEditDialog
/// čte/zapisuje hodnoty přes reflexi - Init jen definuje strukturu + defaults.
/// Obrázek (ImageBase64) je field typu IMAGE - náhled + upload řeší EDInputForm
/// přes host hook PickImageImpl (file dialog + downscale → Base64).
///
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace: manual mirror PropertyDescriptors v
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
        s f.helpText = "Volitelné - název pole agendy, ze kterého se obrázek načte při tisku (místo statického)"

        s f = form.AddFieldToLastGroup("ScaleMode", "Škálování", $$$FieldTypeList, 0, 0)
        d f.AddListItem("Fit", "Vejít celý")
        d f.AddListItem("Fill", "Vyplnit (oříznout)")
        d f.AddListItem("Stretch", "Roztáhnout")
        d f.AddListItem("None", "Bez úpravy")
        s f.value = "Fit"

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        // Pozadí elementu patří k rámečku (prosvítá pod obrázkem, který nevyplní celý box).
        s f = form.AddFieldToLastGroup("BackgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"

        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 30, 30, 1)
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
