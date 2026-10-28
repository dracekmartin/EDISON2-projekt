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

        // Typ + Stránky jsou pohled na PrintBehavior a Role modelu (Section.Typ / Section.Stranky) -
        // do šablony se zapisují ty dvě, formát se nemění. Stránky a Přetečení skrývá podle typu host.
        s f = form.AddFieldToLastGroup("Typ", "Typ sekce", $$$FieldTypeList, 0, 1)
        d f.AddListItem("HlavickaStranky", "Hlavička stránky")
        d f.AddListItem("Obsah", "Obsah")
        d f.AddListItem("PodObsahemStranky", "Pod obsahem stránky")
        d f.AddListItem("PatickaStranky", "Patička stránky")
        s f.value = "Obsah"
        s f.helpText = "Hlavička stránky - nahoře na stránce. Obsah - jednou, v pořadí podle šablony (tabulka, poznámky za ní). Pod obsahem stránky - na každé stránce hned pod tím, co na ní je (poznámky pod každou částí tabulky). Patička stránky - dole na stránce."

        s f = form.AddFieldToLastGroup("Stranky", "Tisknout na", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Vsechny", "Každé stránce")
        d f.AddListItem("JenPrvni", "Jen první stránce")
        d f.AddListItem("JenPosledni", "Jen poslední stránce")
        s f.value = "Vsechny"
        s f.helpText = "Na kterých listech sestavy se hlavička nebo patička vytiskne. Kde na listu leží, určuje Typ sekce"

        s f = form.AddFieldToLastGroup("OverflowBehavior", "Rozdělení na stránky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("KeepTogether", "Držet pohromadě")
        d f.AddListItem("SplitAcrossPages", "Rozdělit přes stránky")
        s f.value = "KeepTogether"
        s f.helpText = "Co se stane, když se sekce nevejde na zbytek stránky"

        s f = form.AddBool("ZacitNaNoveStrance", "Začít na nové stránce", 0)
        s f.helpText = "Sekce začne na novém listu - třeba poznámky na samostatné stránce za tabulkou. Na listu, kde ještě nic není, se nový nezakládá"

        // ── Skupina "Rámeček sekce" ───────────────────────────────────────
        // Jako „Rámeček prvku" u prvků; „Vzhled" nic neříkal a hledání „rámeček" našlo jen přepínač.
        d form.AddGroup("appearance", "Rámeček sekce")

        d form.AddBool("ShowBorder", "Rámeček", 0)

        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        s f.helpText = "Lze zadat i {BARVA_LINKY} nebo {BARVA_LINKY_2} - barvu vezme z jízdního řádu, kde linka může mít víc barev (nenastavená = bílá)"

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
