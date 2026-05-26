Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci TextElement properties.
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace:
/// Tato class je MANUAL MIRROR PropertyDescriptors v `TextElement.cs`. Stejné schema
/// existuje na 2 místech (C# + Caché) kvůli Caché patchovatelnosti (server-side change
/// bez .exe redeploy). Při změně property MUST update obojí.
///
/// Long-term refaktor: schema jako data v Caché global → auto-gen C# model + form.
/// Viz memory `project_edinputform_schema_duplication.md`.
///
/// IN parametry pro pre-fill viz Init metoda. Save je no-op — host extrahuje hodnoty
/// z Items po úspěšné validaci a aplikuje je na TextElement v Template modelu.
Class ALVA.TemplateDesigner.EDInputForm.TextElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        // Read initial values (všechny optional)
        s initText        = $g(^||ML("IN","text"))
        s initWordWrap    = $g(^||ML("IN","wordWrap"))
        s initLineSpacing = $g(^||ML("IN","lineSpacing"))
        s initFontFamily  = $g(^||ML("IN","fontFamily"))
        s initFontSize    = $g(^||ML("IN","fontSize"))
        s initBold        = $g(^||ML("IN","bold"))
        s initItalic      = $g(^||ML("IN","italic"))
        s initUnderline   = $g(^||ML("IN","underline"))
        s initStrike      = $g(^||ML("IN","strikethrough"))
        s initTextColor   = $g(^||ML("IN","textColor"))
        s initBgColor     = $g(^||ML("IN","backgroundColor"))
        s initBorderColor = $g(^||ML("IN","borderColor"))
        s initBorderWidth = $g(^||ML("IN","borderWidth"))
        s initHAlign      = $g(^||ML("IN","hAlign"))
        s initVAlign      = $g(^||ML("IN","vAlign"))
        s initX           = $g(^||ML("IN","x"))
        s initY           = $g(^||ML("IN","y"))
        s initWidth       = $g(^||ML("IN","width"))
        s initHeight      = $g(^||ML("IN","height"))
        s initRotation    = $g(^||ML("IN","rotation"))
        s initZIndex      = $g(^||ML("IN","zIndex"))
        s initLocked      = $g(^||ML("IN","isLocked"))
        s initVisExpr     = $g(^||ML("IN","visibilityExpression"))

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Text" ────────────────────────────────────────────────
        d form.AddGroup("text", "Text")
        d form.AddText("text", "Obsah", initText)
        d form.AddBool("wordWrap", "Zalamovat řádky", $s(initWordWrap'="":initWordWrap,1:1))

        // ── Skupina "Font" ────────────────────────────────────────────────
        d form.AddGroup("font", "Font")

        s f = form.AddFieldToLastGroup("fontFamily", "Písmo", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Arial", "Arial")
        d f.AddListItem("Calibri", "Calibri")
        d f.AddListItem("Times New Roman", "Times New Roman")
        d f.AddListItem("Verdana", "Verdana")
        d f.AddListItem("Courier New", "Courier New")
        d f.AddListItem("Consolas", "Consolas")
        s f.value = $s(initFontFamily'="":initFontFamily,1:"Arial")

        d form.AddNumber("fontSize", "Velikost", $s(initFontSize'="":initFontSize,1:9), 4, 200, "pt", 1)
        d form.AddBool("bold", "Tučně", $s(initBold'="":initBold,1:0))
        d form.AddBool("italic", "Kurzíva", $s(initItalic'="":initItalic,1:0))
        d form.AddBool("underline", "Podtržené", $s(initUnderline'="":initUnderline,1:0))
        d form.AddBool("strikethrough", "Přeškrtnuté", $s(initStrike'="":initStrike,1:0))
        d form.AddDecimal("lineSpacing", "Řádkování", $s(initLineSpacing'="":initLineSpacing,1:1.2), 0.8, 3, "", "0.0#")

        // ── Skupina "Barvy a rámeček" ─────────────────────────────────────
        d form.AddGroup("appearance", "Barvy a rámeček")

        s f = form.AddFieldToLastGroup("textColor", "Barva textu", $$$FieldTypeColor, 0, 0)
        s f.value = $s(initTextColor'="":initTextColor,1:"#FF000000")

        s f = form.AddFieldToLastGroup("backgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = $s(initBgColor'="":initBgColor,1:"#00FFFFFF")

        s f = form.AddFieldToLastGroup("borderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = $s(initBorderColor'="":initBorderColor,1:"#FF000000")

        d form.AddDecimal("borderWidth", "Tloušťka rámečku", $s(initBorderWidth'="":initBorderWidth,1:0), 0, 10, "mm")

        // ── Skupina "Zarovnání" ───────────────────────────────────────────
        d form.AddGroup("align", "Zarovnání")

        s f = form.AddFieldToLastGroup("hAlign", "Horizontálně", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Left", "Vlevo")
        d f.AddListItem("Center", "Na střed")
        d f.AddListItem("Right", "Vpravo")
        d f.AddListItem("Justify", "Do bloku")
        s f.value = $s(initHAlign'="":initHAlign,1:"Left")

        s f = form.AddFieldToLastGroup("vAlign", "Vertikálně", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Top", "Nahoru")
        d f.AddListItem("Middle", "Na střed")
        d f.AddListItem("Bottom", "Dolů")
        s f.value = $s(initVAlign'="":initVAlign,1:"Middle")

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("x", "X", $s(initX'="":initX,1:0), 0, "", "mm", "", 1)
        d form.AddDecimal("y", "Y", $s(initY'="":initY,1:0), 0, "", "mm", "", 1)
        d form.AddDecimal("width", "Šířka", $s(initWidth'="":initWidth,1:50), 1, "", "mm", "", 1)
        s f = form.AddDecimal("height", "Výška", initHeight, 0, "", "mm")
        s f.helpText = "Prázdné = dynamická výška (počítá engine)"
        d form.AddDecimal("rotation", "Rotace", $s(initRotation'="":initRotation,1:0), -360, 360, "°")
        s f = form.AddNumber("zIndex", "ZIndex", $s(initZIndex'="":initZIndex,1:0), 0, 100)
        s f.helpText = "Pořadí vrstev (vyšší = nad nižším)"

        // ── Skupina "Pokročilé" ───────────────────────────────────────────
        d form.AddGroup("advanced", "Pokročilé")
        s f = form.AddBool("isLocked", "Uzamčeno", $s(initLocked'="":initLocked,1:0))
        s f.helpText = "Uzamčený element nelze přesunout ani změnit jeho velikost"
        s f = form.AddText("visibilityExpression", "Podmínka viditelnosti", initVisExpr)
        s f.helpText = "Např. {POCET} > 0 — element se zobrazí jen když podmínka platí"

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Validate(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

        // Declarative validace (Min/Max/Required) řeší client side. Custom server-side
        // checks bychom zde — pro PoC žádné dodatečné.

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Save je no-op — TextElement je in-memory .NET model, host extrahuje hodnoty
/// z Items po úspěšné validaci a aplikuje je sám.
ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

}
