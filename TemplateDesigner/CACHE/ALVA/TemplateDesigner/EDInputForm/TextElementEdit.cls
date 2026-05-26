Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci TextElement properties — PoC validace EDInputForm
/// na komplexnějším formuláři (groups + víc field typů + declarative validation).
///
/// Schema typografie: Text content + Font properties + Zarovnání.
/// 8 polí ve 3 skupinách.
///
/// IN parametry pro pre-fill (všechny optional):
///   text, fontFamily, fontSize, bold, italic, textColor, hAlign, vAlign
///
/// Save je no-op — host extrahuje hodnoty z Items po úspěšné validaci a aplikuje
/// je na TextElement v Template modelu.
Class ALVA.TemplateDesigner.EDInputForm.TextElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        // Read initial values (všechny optional)
        s initText        = $g(^||ML("IN","text"))
        s initWordWrap    = $g(^||ML("IN","wordWrap"))
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
        s initLocked      = $g(^||ML("IN","isLocked"))
        s initVisExpr     = $g(^||ML("IN","visibilityExpression"))

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Text" ────────────────────────────────────────────────
        d form.AddGroup("text", "Text")

        s f = form.AddFieldToLastGroup("text", "Obsah", $$$FieldTypeText, 0, 0)
        s f.value = initText

        s f = form.AddFieldToLastGroup("wordWrap", "Zalamovat řádky", $$$FieldTypeBool, 0, 0)
        s f.value = $s(initWordWrap'="":initWordWrap,1:1)

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

        s f = form.AddFieldToLastGroup("fontSize", "Velikost", $$$FieldTypeNumber, 0, 1)
        s f.value = $s(initFontSize'="":initFontSize,1:9)
        s f.min = 4
        s f.max = 200
        s f.unit = "pt"

        s f = form.AddFieldToLastGroup("bold", "Tučně", $$$FieldTypeBool, 0, 0)
        s f.value = $s(initBold'="":initBold,1:0)

        s f = form.AddFieldToLastGroup("italic", "Kurzíva", $$$FieldTypeBool, 0, 0)
        s f.value = $s(initItalic'="":initItalic,1:0)

        s f = form.AddFieldToLastGroup("underline", "Podtržené", $$$FieldTypeBool, 0, 0)
        s f.value = $s(initUnderline'="":initUnderline,1:0)

        s f = form.AddFieldToLastGroup("strikethrough", "Přeškrtnuté", $$$FieldTypeBool, 0, 0)
        s f.value = $s(initStrike'="":initStrike,1:0)

        // ── Skupina "Barvy a rámeček" ─────────────────────────────────────
        d form.AddGroup("appearance", "Barvy a rámeček")

        s f = form.AddFieldToLastGroup("textColor", "Barva textu", $$$FieldTypeColor, 0, 0)
        s f.value = $s(initTextColor'="":initTextColor,1:"#FF000000")

        s f = form.AddFieldToLastGroup("backgroundColor", "Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = $s(initBgColor'="":initBgColor,1:"#00FFFFFF")

        s f = form.AddFieldToLastGroup("borderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = $s(initBorderColor'="":initBorderColor,1:"#FF000000")

        s f = form.AddFieldToLastGroup("borderWidth", "Tloušťka rámečku", $$$FieldTypeDecimal, 0, 0)
        s f.value = $s(initBorderWidth'="":initBorderWidth,1:0)
        s f.min = 0
        s f.max = 10
        s f.unit = "mm"

        // ── Skupina "Zarovnání" ───────────────────────────────────────────
        d form.AddGroup("align", "Zarovnání")

        s f = form.AddFieldToLastGroup("hAlign", "Horizontálně", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Left", "Vlevo")
        d f.AddListItem("Center", "Na střed")
        d f.AddListItem("Right", "Vpravo")
        s f.value = $s(initHAlign'="":initHAlign,1:"Left")

        s f = form.AddFieldToLastGroup("vAlign", "Vertikálně", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Top", "Nahoru")
        d f.AddListItem("Middle", "Na střed")
        d f.AddListItem("Bottom", "Dolů")
        s f.value = $s(initVAlign'="":initVAlign,1:"Middle")

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")

        s f = form.AddFieldToLastGroup("x", "X", $$$FieldTypeDecimal, 0, 1)
        s f.value = $s(initX'="":initX,1:0)
        s f.min = 0
        s f.unit = "mm"

        s f = form.AddFieldToLastGroup("y", "Y", $$$FieldTypeDecimal, 0, 1)
        s f.value = $s(initY'="":initY,1:0)
        s f.min = 0
        s f.unit = "mm"

        s f = form.AddFieldToLastGroup("width", "Šířka", $$$FieldTypeDecimal, 0, 1)
        s f.value = $s(initWidth'="":initWidth,1:50)
        s f.min = 1
        s f.unit = "mm"

        s f = form.AddFieldToLastGroup("height", "Výška", $$$FieldTypeDecimal, 0, 0)
        s f.value = initHeight
        s f.min = 0
        s f.unit = "mm"
        s f.helpText = "Prázdné = dynamická výška (počítá engine)"

        // ── Skupina "Pokročilé" ───────────────────────────────────────────
        d form.AddGroup("advanced", "Pokročilé")

        s f = form.AddFieldToLastGroup("isLocked", "Uzamčeno", $$$FieldTypeBool, 0, 0)
        s f.value = $s(initLocked'="":initLocked,1:0)
        s f.helpText = "Uzamčený element nelze přesunout ani změnit jeho velikost"

        s f = form.AddFieldToLastGroup("visibilityExpression", "Podmínka viditelnosti", $$$FieldTypeText, 0, 0)
        s f.value = initVisExpr
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
