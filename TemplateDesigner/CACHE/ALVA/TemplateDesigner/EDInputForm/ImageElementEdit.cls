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
        s f.helpText = "Nahrajte obrázek ze souboru (PNG/JPG). Velké soubory se automaticky zmenší."

        s f = form.AddText("DataField", "Datové pole")
        s f.helpText = "Volitelné - název pole agendy, ze kterého se obrázek načte při tisku (místo statického)"

        // „Škálování" a „(oříznout)" ve volbě nebyly podle pravidel popisků - co se ořízne, říká nápověda.
        s f = form.AddFieldToLastGroup("ScaleMode", "Přizpůsobení velikosti", $$$FieldTypeList, 0, 0)
        d f.AddListItem("Fit", "Vejít celý")
        d f.AddListItem("Fill", "Vyplnit")
        d f.AddListItem("Stretch", "Roztáhnout")
        d f.AddListItem("None", "Bez úpravy")
        s f.value = "Fit"
        s f.helpText = "Vejít celý = celý obrázek, kolem může zbýt místo. Vyplnit = obrázek vyplní prvek a co přečnívá, se ořízne. Roztáhnout = vyplní prvek a změní poměr stran. Bez úpravy = původní velikost."

        // Pozadí prosvítá pod obrázkem, který nevyplní celý prvek - patří k obrázku, ne k rámečku.
        s f = form.AddFieldToLastGroup("BackgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        s f.helpText = "Lze zadat i {BARVA_LINKY} nebo {BARVA_LINKY_2} - barvu vezme z jízdního řádu, kde linka může mít víc barev (nenastavená = bílá)"

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)

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
