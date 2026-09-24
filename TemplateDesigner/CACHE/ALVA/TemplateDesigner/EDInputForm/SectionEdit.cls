Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci vlastností sekce (dialog "Vlastnosti sekce").
///
/// Field key = property path na C# Section. Generický ElementEditDialog čte/zapisuje
/// hodnoty přes reflexi (ElementPropertyAccessor) - Init jen definuje strukturu + defaults.
///
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace: manual mirror PropertyDescriptorů z
/// `BuildSectionDescriptors` v `ElementPropertiesWindowViewModel.cs`. Viz memory
/// `project_edinputform_schema_duplication.md`.
///
/// Pozn.: legacy panel zobrazoval "Datový zdroj" + "Výška řádku" jen pro PrintBehavior=PerDataRow
/// (podmíněně). EDInputForm nemá conditional fields a PerDataRow je legacy - proto se tyto dvě
/// pole vynechávají (běžné sekce vypadají 1:1 jako dřív).
Class ALVA.TemplateDesigner.EDInputForm.SectionEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Sekce" ───────────────────────────────────────────────
        d form.AddGroup("section", "Sekce")

        d form.AddText("Name", "Název")

        s f = form.AddDecimal("DesignerHeight", "Výška", 30, 5, 9999, "mm")
        s f.helpText = "Nejmenší výška - obsah ji může zvětšit. Prázdné = výška podle obsahu"

        s f = form.AddFieldToLastGroup("PrintBehavior", "Tisk", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Once", "Jednou")
        d f.AddListItem("OnEveryPage", "Každá stránka")
        d f.AddListItem("OnFirstPageOnly", "Jen první")
        d f.AddListItem("OnLastPageOnly", "Jen poslední")
        d f.AddListItem("OnEveryPageExceptFirst", "Kromě první")
        s f.value = "Once"

        s f = form.AddFieldToLastGroup("Role", "Umístění", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Body", "Obsah")
        d f.AddListItem("BelowContent", "Pod obsahem")
        d f.AddListItem("Header", "Hlavička")
        d f.AddListItem("Footer", "Patička")
        s f.value = "Body"
        s f.helpText = "Pod obsahem = na každé stránce hned za obsahem té stránky"

        s f = form.AddFieldToLastGroup("OverflowBehavior", "Přetečení", $$$FieldTypeList, 0, 1)
        d f.AddListItem("KeepTogether", "Nepřerušovat")
        d f.AddListItem("SplitAcrossPages", "Přes stránky")
        d f.AddListItem("AllowSplit", "Povolit rozdělení")
        s f.value = "KeepTogether"

        // ── Skupina "Vzhled" ──────────────────────────────────────────────
        d form.AddGroup("appearance", "Vzhled")

        d form.AddBool("ShowBorder", "Rámeček", 0)

        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        s f = form.AddDecimal("BorderWidth", "Tloušťka rámečku", 0.3, 0, 5, "mm")
        s f.helpText = "Jen když je zapnutý Rámeček. Prázdné nebo 0 = bez rámečku"

        // ── Skupina "Podmínky" ────────────────────────────────────────────
        d form.AddGroup("conditions", "Podmínky")

        s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
        s f.placeholders = 1
        s f.helpText = "Např. {STRANA} > 1 - sekce se vytiskne jen když podmínka platí"

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
