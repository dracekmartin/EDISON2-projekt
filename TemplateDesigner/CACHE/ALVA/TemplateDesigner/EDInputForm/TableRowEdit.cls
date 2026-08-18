Include (GENERAL, EDInputForm)

/// EDInputForm schema pro **vybraný řádek** pevné tabulky. Field key = property path na C#
/// TableRow, takže se čte i zapisuje reflexí jako u prvků a buněk.
///
/// Podmínka viditelnosti se sem dostává **stejným balíčkem jako u prvků** (BaseElementFields) -
/// výraz se vyhodnocuje toutéž funkcí, takže žádná druhá gramatika nevzniká; do 18. 8. jen nebylo
/// odkud ji u řádku zadat, přestože tisk ji respektoval.
///
/// ⚠️ Výška řádku sem nepatří - mění se tažením vodorovné hranice na plátně (dvě cesty k témuž
/// rozměru by se rozešly). Styl textu patří buňce (TableCellEdit), tady je jen pozadí řádku.
Class ALVA.TemplateDesigner.EDInputForm.TableRowEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Vybraný řádek ─────────────────────────────────────────────────
        d form.AddGroup("row", "Vybraný řádek")
        s f = form.AddFieldToLastGroup("BackgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.helpText = "Prázdné = podle střídání řádků nastaveného na tabulce."

        // ── Podmínky (sdílený balíček) ────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form, "řádek")

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
