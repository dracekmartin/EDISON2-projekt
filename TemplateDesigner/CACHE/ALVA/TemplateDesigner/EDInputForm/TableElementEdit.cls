Include (GENERAL, EDInputForm)

/// EDInputForm schema pro dialog pevné (ručně vyplňované) tabulky. Field key = property path
/// na C# TableElement.
///
/// ⚠️ Dialog má **jediný tab** a **žádné tlačítko na sloupce ani řádky** (rozhodnutí Jan 12. 8.):
/// struktura i obsah tabulky se dělají přímo na plátně (Alt+klik do buňky, pravý klik na řádky
/// a sloupce). V dialogu zůstávají jen obecné vlastnosti prvku - kdyby sem někdo vrátil editor
/// sloupců, vzniknou dvě cesty k témuž a rozejdou se.
///
/// Zdroj dat ani řazení tu nejsou schválně - řádky se u tohoto prvku negenerují z dat
/// (na to je DynamicTableElement).
///
/// ⚠️ Schema je manual mirror PropertyDescriptors v TableElement.cs (viz memory
/// project_edinputform_schema_duplication.md).
Class ALVA.TemplateDesigner.EDInputForm.TableElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Obecné ────────────────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        s f = form.AddDecimal("RowHeight", "Výška řádku", 6, 1, 50, "mm")
        s f.helpText = "Výchozí výška řádku. Roztažením prvku do výšky se řádky natáhnou, písmo zůstane."
        s f = form.AddFieldToLastGroup("AlternatingRowColor", "Střídání řádků (prázdné = žádné)", $$$FieldTypeColor, 0, 0)

        // Řádek záhlaví je u ručně vyplňované tabulky zbytečný pojem (záhlaví = první řádek, který
        // si uživatel udělá tučný). Zůstává kvůli šablonám, které ho už mají - proto je pole
        // schované za nápovědou, ne odstraněné.
        // Label je kanonický (týž klíč má stejný popisek i u ostatních tabulek) - proč se u ručně
        // vyplňované tabulky nehodí, vysvětluje nápověda.
        s f = form.AddBool("ShowHeader", "Záhlaví sloupců", 0)
        s f.helpText = "Nové tabulky ho nepotřebují - záhlaví si udělejte z prvního řádku (tučně). Zůstává kvůli starším šablonám."
        d form.AddDecimal("HeaderHeight", "Výška záhlaví", 8, 2, 50, "mm")

        // ── Styl buněk (sdílený balíček polí) ─────────────────────────────
        // ⚠️ Popisek skupiny říká „všech": tenhle formulář je výchozí styl CELÉ tabulky, zatímco
        // panel u vybrané buňky mění jen tu jednu. Bez toho stály proti sobě dvě sady týchž
        // vlastností a nešlo poznat, která kam sahá (nález Jan 17. 8.).
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl všech buněk", "#FF222222", "#00FFFFFF")
        // Pozadí bez výchozí hodnoty - nese ho prvek sám, prázdno znamená bez podbarvení.
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Styl řádku záhlaví", "#FF333333", "", 1, "", 1)

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form, 1, "#FF999999", 0.19)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form, 1, 1, 1, "#FFCCCCCC")
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 50)
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
