Include (GENERAL, EDInputForm)

/// EDInputForm schema pro **vybranou buňku** pevné tabulky. Field key = property path na C#
/// TableCell (Format, Style.*), takže se čte i zapisuje reflexí jako u prvků.
///
/// ⚠️ Výchozí hodnoty polí stylu se z tohoto schématu **nikdy nezobrazí**: klient je před
/// vyplněním přepíše hodnotami, které pro tu buňku vydá kaskáda (styl sloupce → „Styl všech
/// buněk" prvku → zapečené výchozí). Buňka je totiž překryv, ne samostatné nastavení - formulář
/// musí ukázat, jak buňka reálně vypadá, a přitom zapsat jen to, co uživatel opravdu změnil.
/// Hlídá to test „vlastnosti buňky mají zděděnou hodnotu".
///
/// ⚠️ Pozice, rámeček, uzamčení ani podmínka viditelnosti sem nepatří - geometrii buňky dává
/// řádek a sloupec, rámeček je mřížka tabulky a viditelnost patří řádku.
Class ALVA.TemplateDesigner.EDInputForm.TableCellEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Vybraná buňka ─────────────────────────────────────────────────
        d form.AddGroup("cell", "Vybraná buňka")
        s f = form.AddFieldToLastGroup("__tableSettings", "Nastavení celé tabulky", $$$FieldTypeButton, 0, 0)
        s f.helpText = "Písmo, barvy a mřížku pro všechny buňky najednou nastavíte na prvku."

        s f = form.AddText("Format", "Formát hodnoty")
        s f.helpText = "Jen pro buňku s datovým polem - třeba dd.MM.yyyy nebo N2. Prázdné = podle sloupce."

        // ── Styl buňky (sdílený balíček polí) ─────────────────────────────
        // Prázdné pole = dědí se z nastavení celé tabulky.
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "Style", "cellstyle", "Styl buňky")

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
