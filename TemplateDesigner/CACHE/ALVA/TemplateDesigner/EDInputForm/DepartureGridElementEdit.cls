Include (GENERAL, EDInputForm)

/// EDInputForm schema pro tab „Obecné" editoru DepartureGridElement (mřížka odjezdů).
/// Field key = property path na C# DepartureGridElement. Stejná 4-skupinová kostra jako
/// StopListElementEdit: Obecné · Styl záhlaví · Styl datových buněk · Odjezdy-specifické (+ Pozice/Podmínky).
/// Stylové balíčky přes ALVA.TemplateDesigner.EDInputForm.StyleFields (jediný zdroj sady polí).
///
/// Element-level styl = prostá pole (element je báze kaskády); per-sloupec/skupina override řeší
/// CellStyleEditor v tabu „Sloupce". Lazy/legacy-scalar nuance (HeaderLine2Style alokace,
/// MinuteStyle/Anotace fallback scaláry, HeaderVerticalAlignment fallback, SetHeaderFontSize cleanup)
/// dořeší C# host (DepartureGroupEditorDialog) v Init/OnOk - viz tam.
///
/// ⚠️ Schema je manual mirror PropertyDescriptors v DepartureGridElement.cs (viz memory
/// project_edinputform_schema_duplication.md).
Class ALVA.TemplateDesigner.EDInputForm.DepartureGridElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupiny dnů ───────────────────────────────────────────────────
        // Původní „Obecné" mělo 10 polí bez jediné vysvětlivky (rozpočet je ~7). Rozpad podle toho,
        // čeho se pole týkají; klíče polí se NEMĚNÍ, mění se jen zařazení a popisky.
        d form.AddGroup("groups", "Skupiny dnů")
        s f = form.AddBool("ShowSkupinaDnuHeader", "Záhlaví skupin dnů", 1)
        s f.helpText = "Nadpis nad každým blokem minut (např. ""Pracovní dny"", ""Sobota"")"
        // Šířka sloupce hodin se edituje v tabu „Sloupce" (řádek Hodiny) - jediný zdroj, ať nekoliduje.
        s f = form.AddBool("HourColumnPerGroup", "Hodiny u každé skupiny", 0)
        s f.helpText = "Sloupec s hodinami se zopakuje u každé skupiny dnů. Vypnuto = jeden sloupec hodin vlevo pro všechny skupiny."
        s f = form.AddFieldToLastGroup("HourColumnHeaderImageBase64", "Obrázek v záhlaví hodin", $$$FieldTypeImage, 0, 0)
        s f.helpText = "Ikona se vykreslí v záhlaví sloupce hodin u každé skupiny dnů. Když je nastavená, nadpis sloupce se netískne."
        s f = form.AddNumber("MaxGroupCount", "Max skupin (0 = ∞)", 0, 0, 10)
        s f.helpText = "Kolik skupin dnů se vejde do prvku; další se nevykreslí. 0 = bez omezení."

        // ── Rozměry a mřížka ──────────────────────────────────────────────
        d form.AddGroup("layout", "Rozměry a mřížka")
        s f = form.AddDecimal("MinuteWidth", "Šířka minuty", 5, 3, 15, "mm")
        s f = form.AddFieldToLastGroup("MinuteSpacing", "Rozestup odjezdů", $$$FieldTypeList, 0, 1)
        d f.AddListItem("ByContent", "Podle obsahu"), f.AddListItem("Even", "Rovnoměrný")
        s f.helpText = "Podle obsahu: minuta se značkou zabere víc místa a odsune odjezdy za sebou. Rovnoměrný: jeden rozestup pro celou skupinu podle nejširšího odjezdu, takže minuty stojí ve svislici a značka nerozhodí zarovnání ostatních (Šířka minuty je pak spodní mez rastru)."
        s f.helpText = "Šířka jednoho políčka s minutou - určuje, kolik minut se vejde na řádek"
        s f = form.AddDecimal("GroupSeparatorWidth", "Oddělovač skupin", 0.5, 0, 5, "mm")
        s f.helpText = "Mezera mezi bloky jednotlivých skupin dnů. 0 = bez mezery."
        s f = form.AddDecimal("OdjezdGap", "Mezera mezi odjezdy", "", 0, 10, "mm")
        s f.helpText = "Vodorovná mezera mezi jednotlivými odjezdy v řádku. Prázdné = podle šířky minuty."
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 50, "mm")
        s f = form.AddFieldToLastGroup("AlternatingRowColor", "Střídání řádků (prázdné = žádné)", $$$FieldTypeColor, 0, 0)
        // Časová osa (rozsah hodin) i „Roztáhnout na výšku" jsou parametry sestavy
        // (CASOVA_OSA/HODINY_OD/DO, ROZTAHNOUT_NA_VYSKU), sdílené a autoritativní - NEduplikovat
        // jako pole prvku, parametr by je přebil a klik by nic nedělal (hlášení B22/B24).

        // ── Styl záhlaví ──────────────────────────────────────────────────
        // PROTOTYP: styl jako tlačítko → pod-formulář (StyleEditDialog). Klíč „style:<prefix>"
        // (ne property path) → ElementFormBinder ho ignoruje; C# host z něj vytáhne prefix.
        d form.AddGroup("hstyle", "Styl záhlaví")
        s f = form.AddFieldToLastGroup("style:HeaderStyle", "Styl záhlaví", $$$FieldTypeButton, 0, 0)
        s f.value = "Upravit styl…"
        d form.AddDecimal("SkupinaDnuHeaderLineHeight", "Min. výška řádku záhlaví", 4, 2, 10, "mm")
        s f = form.AddFieldToLastGroup("HeaderVerticalAlignment", "Zarovnání záhlaví", $$$FieldTypeList, 0, 0)
        d f.AddListItem("Top", "Nahoře"), f.AddListItem("Middle", "Na střed"), f.AddListItem("Bottom", "Dole")
        s f.value = "Bottom"
        s f = form.AddFieldToLastGroup("style:HeaderLine2Style", "Styl 2. řádku nadpisu", $$$FieldTypeButton, 0, 0)
        s f.value = "Upravit styl…"

        // Styl datových buněk: DataStyle = báze (minuty i hodinová čísla z ní dědí),
        // MinuteStyle = minutový override navrch (prefix „Minuta:").
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF000000", "")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "MinuteStyle", "", "", "", "", 0, "Minuta: ", 1)

        // ── Značky u odjezdů ──────────────────────────────────────────────
        // „Anotace" bylo interní slovo - uživatel tomu říká značky u odjezdů (písmena a symboly
        // u minuty, které vysvětluje legenda poznámek). Klíče polí zůstaly, přejmenované jsou
        // popisky; tytéž popisky nese i panel vlastností (`ux-conventions §8`).
        d form.AddGroup("departure", "Značky u odjezdů")
        s f = form.AddBool("ShowAnotace", "Značky u odjezdů", 1)
        s f.helpText = "Písmena a symboly u minuty (nízkopodlažní spoj, kolo, jede jen v určité dny…). Co se z nich tiskne, řídí prvek Legenda poznámek - ta je i vysvětluje."
        s f = form.AddDecimal("AnotaceGap", "Mezera minuta ↔ značka", "", 0, 5, "mm")
        s f.helpText = "Odsazení značky od minuty. Prázdné = automaticky podle velikosti písma."
        s f = form.AddDecimal("AnotaceInterGap", "Mezera mezi značkami", "", 0, 5, "mm")
        s f.helpText = "Odsazení mezi dvěma značkami u téhož odjezdu"
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceSymbolStyle", "", "", "", "", 0, "Symbol: ")
        // Negativní značka = symbol vykreslený inverzně (znak na plné ploše) - vlastní dvojice barev.
        s f = form.AddFieldToLastGroup("NegativniZnackaBackColor", "Pozadí negativní značky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        s f.helpText = "Negativní značka = znak vykreslený inverzně, tedy světlý na plné barevné ploše"
        s f = form.AddFieldToLastGroup("NegativniZnackaForeColor", "Barva negativní značky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFFFFFFF"
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceTextStyle", "", "", "", "", 0, "Text: ")

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form, 1, 1, 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 100, 60)
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
