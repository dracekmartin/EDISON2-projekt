Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci NotesLegendElement properties (legenda poznámek).
///
/// Field key = property path na C# NotesLegendElement (vč. nested HeaderStyle.X /
/// DataStyle.X - reflexe traverzuje). Init jen definuje strukturu + defaults;
/// generický ElementEditDialog dělá pre-fill přes reflexi.
///
/// Pozn.: NotesLegend NEMÁ per-column kolekci (na rozdíl od StopList/DepartureGrid),
/// proto je čistý generic kandidát - žádný custom dialog potřeba.
///
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace: manual mirror PropertyDescriptors v
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
        // ComboBox (editovatelný) - klient naplní nainstalovanými fonty (PopulateFontFields, klíč končí "FontFamily")
        d form.AddFieldToLastGroup("HeaderStyle.FontFamily", "Font", $$$FieldTypeComboBox, 0, 0)
        d form.AddFieldToLastGroup("HeaderStyle.FontWeight", "Řez", $$$FieldTypeList, 0, 0)

        // ── Skupina "Datové buňky" ────────────────────────────────────────
        d form.AddGroup("data", "Datové buňky")
        s f = form.AddFieldToLastGroup("DataStyle.BackgroundColor", "Pozadí buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        s f = form.AddFieldToLastGroup("DataStyle.TextColor", "Barva textu buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        // ComboBox (editovatelný) - klient naplní nainstalovanými fonty (PopulateFontFields, klíč končí "FontFamily")
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
        // Volby 1:1 s panelem (DleTypu je default elementu - dřív v dialogu úplně chyběla)
        d f.AddListItem("DleTypu", "Dle typu (značky → atributy → text)"), f.AddListItem("Vychozi", "Výchozí (dle dat)")
        d f.AddListItem("DleDelkyVzestupne", "Délka vzestupně"), f.AddListItem("DleDelkySestupne", "Délka sestupně")
        s f.value = "DleTypu"
        d form.AddDecimal("MarkerSirka", "Šířka buňky značky (0=vedle)", 0, 0, 30, "mm")

        // ── Skupina "Filtrování" ──────────────────────────────────────────
        d form.AddGroup("filter", "Filtrování")
        d form.AddBool("ShowNegativniZnacky", "Negativní značky", 1)
        d form.AddBool("ShowSpecialniZnaky", "Speciální znaky", 1)
        d form.AddBool("ShowTextovePoznamky", "Textové poznámky", 1)
        s f = form.AddFieldToLastGroup("Vyluky", "Poznámky linky - výluky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Vcetne", "Včetně"), f.AddListItem("Bez", "Bez výluk"), f.AddListItem("Jen", "Jen výluky")
        s f.helpText = "Poznámky linky, které začínají ""Výluka"", lze mít v samostatném bloku: v jednom bloku zvolte Bez výluk a ve druhém Jen výluky. Vyžaduje zapnutý parametr sestavy Poznámky zadané u linky."
        // Kategorie poznámek zadaných u linky - klient je chce rozložit do víc bloků, protože mají jiné
        // rozložení sloupců než seznam značek (dlouhé odstavce vs. „značka + text").
        s f = form.AddBool("ShowPoznamkyLinkyTarifni", "Poznámky linky - tarifní", 0)
        s f.helpText = "Které poznámky zadané u linky blok bere. Vypnutím se schová jen tato kategorie - volba Textové poznámky výše vypne i ruční komentáře a rozdíly tras. Vyžaduje zapnutý parametr sestavy Poznámky zadané u linky."
        d form.AddBool("ShowPoznamkyLinkyVseobecne", "Poznámky linky - všeobecné", 0)
        d form.AddBool("ShowPoznamkyLinkyRadiobus", "Poznámky linky - radiobus", 0)
        s fBez = form.AddBool("ShowSouhrnBezbarier", "Souhrn bezbariérových spojů", 1)
        s fBez.helpText = "Souhrnná vysvětlivka ""všechny spoje mají bezbariérově přístupné vozidlo"" místo značky u každého odjezdu. Dřív parametr sestavy."

        // ── Skupina "Negativní značky" ────────────────────────────────────
        d form.AddGroup("nz", "Negativní značky")
        s f = form.AddFieldToLastGroup("NegativniZnackaBackColor", "NZ pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        s f = form.AddFieldToLastGroup("NegativniZnackaForeColor", "NZ barva", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFFFFFFF"

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 100)
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
