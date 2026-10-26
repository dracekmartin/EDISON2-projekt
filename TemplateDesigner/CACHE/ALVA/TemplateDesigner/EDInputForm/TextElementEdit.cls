Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci TextElement properties.
///
/// Field key = property path na C# TextElement (dot notation pro nested,
/// např. "Style.FontSize"). Generický ElementEditDialog čte/zapisuje hodnoty
/// přes reflexi (ElementPropertyAccessor). Init proto NEpotřebuje IN params -
/// jen definuje strukturu + default hodnoty (fallback pro null properties).
///
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace:
/// Tato class je MANUAL MIRROR PropertyDescriptors v `TextElement.cs`. Stejné schema
/// existuje na 2 místech (C# data model + Caché form def) kvůli Caché patchovatelnosti
/// (server-side change bez .exe redeploy). Při přidání/přejmenování property MUST update
/// obojí. Viz memory `project_edinputform_schema_duplication.md`.
///
/// Save je no-op - TextElement je in-memory .NET model, host aplikuje hodnoty sám.
Class ALVA.TemplateDesigner.EDInputForm.TextElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Text" ────────────────────────────────────────────────
        d form.AddGroup("text", "Text")
        // Placeholdery {POLE}: příznak říká, že sem patří datová pole - jejich SEZNAM dodá klient
        // (Caché agendu šablony nezná). Vedle pole se pak ukáže „vložit pole".
        s f = form.AddText("Text", "Obsah")
        s f.placeholders = 1
        d form.AddBool("WordWrap", "Zalamovat řádky", 0)

        // ── Skupina "Font" ────────────────────────────────────────────────
        d form.AddGroup("font", "Font")

        // ComboBox (editovatelný + autocomplete) s prázdným seznamem - klient ho naplní nainstalovanými
        // fonty (ElementFormBinder.PopulateFontFields z SKFontManager). Prázdné = dědí výchozí font šablony.
        d form.AddFieldToLastGroup("Style.FontFamily", "Font", $$$FieldTypeComboBox, 0, 0)
        // Řez (váha) - items plní klient dynamicky dle vybrané rodiny (ElementFormBinder.PopulateFontWeightFields,
        // z reálných řezů nainstalovaného fontu). Prázdná položka (nepovinné pole) = auto dle Tučně.
        d form.AddFieldToLastGroup("Style.FontWeight", "Řez", $$$FieldTypeList, 0, 0)

        d form.AddDecimal("Style.FontSize", "Velikost", 10, 4, 200, "pt", "0.#", 1)
        d form.AddBool("Style.Bold", "Tučné", 0)
        d form.AddBool("Style.Italic", "Kurzíva", 0)
        d form.AddBool("Style.Underline", "Podtržení", 0)
        d form.AddBool("Style.Strikethrough", "Přeškrtnutí", 0)
        d form.AddDecimal("Style.LineSpacing", "Řádkování", 1.2, 0.8, 3, "", "0.0#")

        // ── Skupina "Barvy" (rámeček elementu má vlastní skupinu ze sdíleného balíčku) ─────
        d form.AddGroup("appearance", "Barvy")

        s f = form.AddFieldToLastGroup("Style.TextColor", "Barva textu", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        s f = form.AddFieldToLastGroup("Style.BackgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        s f.helpText = "Lze zadat i {BARVA_LINKY} nebo {BARVA_LINKY_2} - barvu vezme z jízdního řádu, kde linka může mít víc barev (nenastavená = bílá)"

        // ── Skupina "Zarovnání" ───────────────────────────────────────────
        d form.AddGroup("align", "Zarovnání")

        s f = form.AddFieldToLastGroup("Style.HAlign", "Zarovnání vodorovně", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Left", "Vlevo")
        d f.AddListItem("Center", "Na střed")
        d f.AddListItem("Right", "Vpravo")
        d f.AddListItem("Justify", "Do bloku")
        s f.value = "Center"

        s f = form.AddFieldToLastGroup("Style.VAlign", "Zarovnání svisle", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Top", "Nahoře")
        d f.AddListItem("Middle", "Na střed")
        d f.AddListItem("Bottom", "Dole")
        s f.value = "Middle"

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
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
