Include (GENERAL, EDInputForm)

/// EDInputForm schema pro styl **vybraného sloupce** na záložce Sloupce oken tabulek (Seznam zastávek,
/// Minuty odjezdů, Tabulka spojů, Tabulka z dat). Field key = property path na C# ColumnVisualStyle
/// (Header.*, Data.*), takže se čte i zapisuje reflexí jako u prvků. Sada stylu je táž jako
/// ve formuláři prvku (StyleFields), aby pořadí, popisky i rozsahy byly na obou místech stejné.
///
/// ⚠️ Výchozí hodnoty polí se z tohoto schématu **nikdy nezobrazí**: klient je před vyplněním
/// přepíše tím, co pro sloupec vydá kaskáda (styl prvku → zapečené výchozí). Formulář ukazuje,
/// jak se sloupec vytiskne, a zapíše jen to, co uživatel opravdu změnil.
Class ALVA.TemplateDesigner.EDInputForm.TableColumnEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "Header", "header", "Záhlaví")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "Data", "data", "Datové buňky")
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
