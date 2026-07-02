Include (GENERAL, EDInputForm)

/// Helper pro EDInputForm schémata tabulkových elementů: vyplivne standardní „balíček"
/// polí stylu textu (TextStyle) pro daný property-path prefix. Jediný zdroj pravdy pro sadu
/// polí stylu → každá zóna (HeaderStyle/DataStyle/MinuteStyle/…) napříč všemi elementy má
/// identická pole ve stejném pořadí; nové pole stylu stačí přidat zde a propíše se všude.
///
/// Klíče polí = property path na C# element (prefix.FontFamily, prefix.FontSize, …) — čte/zapisuje
/// je ElementFormBinder přes reflexi. Hodnoty HAlign/VAlign = názvy enumu (Left/Center/Right…).
Class ALVA.TemplateDesigner.EDInputForm.StyleFields
{

/// <var>newGroup</var>=0 → pole se přidají do POSLEDNÍ skupiny (ne nová) — pro víc stylových zón
/// v jedné business skupině (anotace symboly/text). <var>labelPrefix</var> se předřadí labelům
/// (např. "Symbol: ") pro odlišení zón ve stejné skupině.
ClassMethod Add(form As ALVA.EDControls.EDInputForm.Form, prefix As %String, groupKey As %String, groupLabel As %String, defTextColor As %String = "", defBgColor As %String = "", newGroup As %Boolean = 1, labelPrefix As %String = "")
{
    i newGroup d form.AddGroup(groupKey, groupLabel)
    // ComboBox (editovatelný) s prázdným seznamem — klient naplní nainstalovanými fonty
    // (ElementFormBinder.PopulateFontFields). Prázdné = dědit z rodiče kaskády.
    d form.AddFieldToLastGroup(prefix_".FontFamily", labelPrefix_"Font", $$$FieldTypeComboBox, 0, 0)
    d form.AddDecimal(prefix_".FontSize", labelPrefix_"Velikost", "", 4, 200, "pt")
    d form.AddBool(prefix_".Bold", labelPrefix_"Tučné", 0)
    d form.AddBool(prefix_".Italic", labelPrefix_"Kurzíva", 0)
    d form.AddBool(prefix_".Underline", labelPrefix_"Podtržení", 0)
    d form.AddBool(prefix_".Strikethrough", labelPrefix_"Přeškrtnutí", 0)
    s f = form.AddFieldToLastGroup(prefix_".HAlign", labelPrefix_"Zarovnání", $$$FieldTypeList, 0, 0)
    d f.AddListItem("Left", "Vlevo"), f.AddListItem("Center", "Na střed"), f.AddListItem("Right", "Vpravo")
    s f = form.AddFieldToLastGroup(prefix_".VAlign", labelPrefix_"Svisle", $$$FieldTypeList, 0, 0)
    d f.AddListItem("Top", "Nahoře"), f.AddListItem("Middle", "Na střed"), f.AddListItem("Bottom", "Dole")
    s f = form.AddFieldToLastGroup(prefix_".TextColor", labelPrefix_"Barva textu", $$$FieldTypeColor, 0, 0)
    s:defTextColor'="" f.value = defTextColor
    s f = form.AddFieldToLastGroup(prefix_".BackgroundColor", labelPrefix_"Pozadí", $$$FieldTypeColor, 0, 0)
    s:defBgColor'="" f.value = defBgColor
    d form.AddDecimal(prefix_".PaddingTop", labelPrefix_"Vnitřní okraj nahoře", "", 0, 20, "mm")
    d form.AddDecimal(prefix_".PaddingRight", labelPrefix_"Vnitřní okraj vpravo", "", 0, 20, "mm")
    d form.AddDecimal(prefix_".PaddingBottom", labelPrefix_"Vnitřní okraj dole", "", 0, 20, "mm")
    d form.AddDecimal(prefix_".PaddingLeft", labelPrefix_"Vnitřní okraj vlevo", "", 0, 20, "mm")
    d form.AddDecimal(prefix_".LineSpacing", labelPrefix_"Řádkování", "", 0.5, 3, "")
}

}
