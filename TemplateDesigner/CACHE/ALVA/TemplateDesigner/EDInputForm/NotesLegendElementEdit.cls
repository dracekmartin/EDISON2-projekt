Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci NotesLegendElement properties (legenda poznámek).
///
/// Field key = property path na C# NotesLegendElement (vč. nested HeaderStyle.X /
/// DataStyle.X — reflexe traverzuje). Init jen definuje strukturu + defaults;
/// generický ElementEditDialog dělá pre-fill přes reflexi.
///
/// Pozn.: NotesLegend NEMÁ per-column kolekci (na rozdíl od StopList/DepartureGrid),
/// proto je čistý generic kandidát — žádný custom dialog potřeba.
///
/// ⚠️ TECHNICKÝ DLUH — Schema duplikace: manual mirror PropertyDescriptors v
/// `NotesLegendElement.cs`. Viz memory `project_edinputform_schema_duplication.md`.
Class ALVA.TemplateDesigner.EDInputForm.NotesLegendElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Obecné" ──────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        s f = form.AddFieldToLastGroup("GridColor", "Barva mřížky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFD0D0D0"

        // ── Skupina "Záhlaví" ─────────────────────────────────────────────
        d form.AddGroup("header", "Záhlaví")
        d form.AddText("HeaderText", "Nadpis", "Poznámky")
        s f = form.AddFieldToLastGroup("HeaderStyle.BackgroundColor", "Pozadí záhlaví", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFF0F0F0"
        s f = form.AddFieldToLastGroup("HeaderStyle.TextColor", "Barva textu záhlaví", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        // ComboBox (editovatelný) — klient naplní nainstalovanými fonty (PopulateFontFields, klíč končí "FontFamily")
        d form.AddFieldToLastGroup("HeaderStyle.FontFamily", "Font", $$$FieldTypeComboBox, 0, 0)
        d form.AddFieldToLastGroup("HeaderStyle.FontWeight", "Řez", $$$FieldTypeList, 0, 0)

        // ── Skupina "Datové buňky" ────────────────────────────────────────
        d form.AddGroup("data", "Datové buňky")
        s f = form.AddFieldToLastGroup("DataStyle.BackgroundColor", "Pozadí buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        s f = form.AddFieldToLastGroup("DataStyle.TextColor", "Barva textu buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        // ComboBox (editovatelný) — klient naplní nainstalovanými fonty (PopulateFontFields, klíč končí "FontFamily")
        d form.AddFieldToLastGroup("DataStyle.FontFamily", "Font", $$$FieldTypeComboBox, 0, 0)
        d form.AddFieldToLastGroup("DataStyle.FontWeight", "Řez", $$$FieldTypeList, 0, 0)
        s f = form.AddText("NoteSeparator", "Oddělovač", " - ")
        s f.helpText = "Vloží se mezi značku a popis u vlastních poznámek linky. U ikonových atributů (na znamení, bezbariér) se nepoužívá."
        d form.AddDecimal("NoteFontSize", "Velikost poznámek", 7, 4, 20, "pt", "0.#")
        d form.AddDecimal("NoteLineHeight", "Výška řádku", 4, 2, 15, "mm")

        // ── Skupina "Rozložení" (sloupce / řazení / zarovnání markeru) ────
        d form.AddGroup("layout", "Rozložení")
        d form.AddNumber("Columns", "Počet sloupců", 1, 1, 4)
        s f = form.AddFieldToLastGroup("WrapSmer", "Směr plnění", $$$FieldTypeList, 0, 0)
        d f.AddListItem("PoSloupcich", "Po sloupcích"), f.AddListItem("PoRadcich", "Po řádcích")
        s f.value = "PoSloupcich"
        s f = form.AddFieldToLastGroup("Razeni", "Řazení poznámek", $$$FieldTypeList, 0, 0)
        d f.AddListItem("Vychozi", "Výchozí"), f.AddListItem("DleDelkyVzestupne", "Délka vzestupně"), f.AddListItem("DleDelkySestupne", "Délka sestupně")
        s f.value = "Vychozi"
        d form.AddDecimal("MarkerSirka", "Šířka buňky značky (0=vedle)", 0, 0, 30, "mm")

        // ── Skupina "Filtrování" ──────────────────────────────────────────
        d form.AddGroup("filter", "Filtrování")
        d form.AddBool("ShowNegativniZnacky", "Negativní značky", 1)
        d form.AddBool("ShowSpecialniZnaky", "Speciální znaky", 1)
        d form.AddBool("ShowTextovePoznamky", "Textové poznámky", 1)

        // ── Skupina "Negativní značky" ────────────────────────────────────
        d form.AddGroup("nz", "Negativní značky")
        s f = form.AddFieldToLastGroup("NegativniZnackaBackColor", "NZ pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        s f = form.AddFieldToLastGroup("NegativniZnackaForeColor", "NZ barva", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFFFFFFF"

        // ── Skupina "Rámeček elementu" ────────────────────────────────────
        d form.AddGroup("border", "Rámeček elementu")
        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        d form.AddDecimal("BorderWidth", "Tloušťka rámečku", 0, 0, 5, "mm")
        s f = form.AddFieldToLastGroup("CornerStyle", "Rohy", $$$FieldTypeList, 0, 0)
        d f.AddListItem("None", "Ostré"), f.AddListItem("Rounded", "Zaoblené"), f.AddListItem("Beveled", "Zkosené")
        s f.value = "None"
        d form.AddDecimal("CornerSize", "Velikost rohu", 0, 0, 50, "mm")

        // ── Skupina "Pozice a velikost" ───────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 100, 1, "", "mm", "", 1)
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
