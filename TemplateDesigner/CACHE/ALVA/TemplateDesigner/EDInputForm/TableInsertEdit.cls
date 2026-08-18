Include (GENERAL, EDInputForm)

/// EDInputForm schema pro **rozměr nově vkládané pevné tabulky**. Field key = property path na C#
/// TableGridSize (jen rozměr, žádný prvek) - dokud tohle nebylo, vkládala se natvrdo mřížka 3 × 3
/// a na 12 × 10 se uživatel musel proklikat devatenácti vloženími sloupce a řádku.
///
/// ⚠️ Výchozí 3 × 3 musí souhlasit s C# (hlídá EDInputFormSchemaDriftTests) - je to velikost,
/// se kterou tabulka vzniká, takže kdo si rozměr neřeší, jen potvrdí a má, co míval.
Class ALVA.TemplateDesigner.EDInputForm.TableInsertEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        d form.AddGroup("size", "Rozměr tabulky")
        // Popisky drží kanon - „Počet sloupců" má týž klíč i u legendy poznámek (ux-conventions §8).
        s f = form.AddNumber("Columns", "Počet sloupců", 3, 1, 30)
        s f = form.AddNumber("Rows", "Počet řádků", 3, 1, 50)
        s f.helpText = "Řádky i sloupce jde přidávat i později přes pravé tlačítko v tabulce."

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
