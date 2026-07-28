Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci TextElement properties.
///
/// Field key = property path na C# TextElement (dot notation pro nested,
/// např. "Style.FontSize"). Generický ElementEditDialog čte/zapisuje hodnoty
/// přes reflexi (ElementPropertyAccessor). Init proto NEpotřebuje IN params —
/// jen definuje strukturu + default hodnoty (fallback pro null properties).
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace:
/// Tato class je MANUAL MIRROR PropertyDescriptors v `TextElement.cs`. Stejné schema
/// existuje na 2 místech (C# data model + Caché form def) kvůli Caché patchovatelnosti
/// (server-side change bez .exe redeploy). Při přidání/přejmenování property MUST update
/// obojí. Viz memory `project_edinputform_schema_duplication.md`.
///
/// Save je no-op — TextElement je in-memory .NET model, host aplikuje hodnoty sám.
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
        d form.AddText("Text", "Obsah")
        d form.AddBool("WordWrap", "Zalamovat řádky", 1)

        // ── Skupina "Font" ────────────────────────────────────────────────
        d form.AddGroup("font", "Font")

        // ComboBox (editovatelný + autocomplete) s prázdným seznamem — klient ho naplní nainstalovanými
        // fonty (ElementFormBinder.PopulateFontFields z SKFontManager). Prázdné = dědí výchozí font šablony.
        d form.AddFieldToLastGroup("Style.FontFamily", "Font", $$$FieldTypeComboBox, 0, 0)
        // Řez (váha) — items plní klient dynamicky dle vybrané rodiny (ElementFormBinder.PopulateFontWeightFields,
        // z reálných řezů nainstalovaného fontu). Prázdná položka (nepovinné pole) = auto dle Tučně.
        d form.AddFieldToLastGroup("Style.FontWeight", "Řez", $$$FieldTypeList, 0, 0)

        d form.AddDecimal("Style.FontSize", "Velikost", 10, 4, 200, "pt", "0.#", 1)
        d form.AddBool("Style.Bold", "Tučné", 0)
        d form.AddBool("Style.Italic", "Kurzíva", 0)
        d form.AddBool("Style.Underline", "Podtržení", 0)
        d form.AddBool("Style.Strikethrough", "Přeškrtnutí", 0)
        d form.AddDecimal("Style.LineSpacing", "Řádkování", 1.2, 0.8, 3, "", "0.0#")

        // ── Skupina "Barvy a rámeček" ─────────────────────────────────────
        d form.AddGroup("appearance", "Barvy a rámeček")

        s f = form.AddFieldToLastGroup("Style.TextColor", "Barva textu", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        s f = form.AddFieldToLastGroup("Style.BackgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"

        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        d form.AddDecimal("BorderWidth", "Tloušťka rámečku", 0, 0, 10, "mm")
        s f = form.AddFieldToLastGroup("CornerStyle", "Rohy", $$$FieldTypeList, 0, 0)
        d f.AddListItem("None", "Ostré"), f.AddListItem("Rounded", "Zaoblené"), f.AddListItem("Beveled", "Zkosené")
        s f.value = "None"
        d form.AddDecimal("CornerSize", "Velikost rohu", 0, 0, 50, "mm")

        // ── Skupina "Zarovnání" ───────────────────────────────────────────
        d form.AddGroup("align", "Zarovnání")

        s f = form.AddFieldToLastGroup("Style.HAlign", "Vodorovně", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Left", "Vlevo")
        d f.AddListItem("Center", "Na střed")
        d f.AddListItem("Right", "Vpravo")
        d f.AddListItem("Justify", "Do bloku")
        s f.value = "Left"

        s f = form.AddFieldToLastGroup("Style.VAlign", "Svisle", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Top", "Nahoře")
        d f.AddListItem("Middle", "Na střed")
        d f.AddListItem("Bottom", "Dole")
        s f.value = "Middle"

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 50, 1, "", "mm", "", 1)
        s f = form.AddDecimal("Height", "Výška", "", 0, "", "mm")
        s f.helpText = "Prázdné = dynamická výška (počítá engine)"
        d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
        s f = form.AddNumber("ZIndex", "ZIndex", 0, 0, 100)
        s f.helpText = "Pořadí vrstev (vyšší = nad nižším)"

        // ── Skupina "Pokročilé" ───────────────────────────────────────────
        d form.AddGroup("advanced", "Pokročilé")
        s f = form.AddBool("IsLocked", "Uzamčeno", 0)
        s f.helpText = "Uzamčený element nelze přesunout ani změnit jeho velikost"
        s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
        s f.helpText = "Např. {POCET} > 0 — element se zobrazí jen když podmínka platí"

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
