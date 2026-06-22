Include (GENERAL, EDInputForm)

/// EDInputForm schema pro tab „Obecné" editoru StopListElement (zastávkový seznam).
/// Field key = property path na C# StopListElement. Embeduje se do StopColumnsEditorDialog
/// vedle custom editoru sloupců (tab „Sloupce"). Validate jen echo — žádná cross-field logika.
///
/// Element-level styl záhlaví/buněk je zde jako prostá pole (element je báze kaskády, tri-state
/// „dědit" tu netřeba); per-sloupec override řeší CellStyleEditor v tabu „Sloupce".
///
/// ⚠️ Schema je manual mirror PropertyDescriptors v StopListElement.cs (viz memory
/// project_edinputform_schema_duplication.md).
Class ALVA.TemplateDesigner.EDInputForm.StopListElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Obecné ────────────────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        s f = form.AddFieldToLastGroup("GridColor", "Barva mřížky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFD0D0D0"
        s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        d form.AddDecimal("BorderWidth", "Šířka rámečku", 0, 0, 5, "mm")
        d form.AddBool("ShowHeader", "Záhlaví sloupců", 1)
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 50, "mm")
        d form.AddBool("ShowRowSeparators", "Oddělovací čáry", 0)

        // ── Styl záhlaví / dat (sdílený balíček polí) ─────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Styl záhlaví", "#FF000000", "#FFE0E0E0")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF000000", "#00FFFFFF")

        // ── Zastávky — specifické (značky, směr, zvýraznění) ──────────────
        d form.AddGroup("stops", "Zastávky — specifické")
        d form.AddBool("ShowStopIndentation", "Odsazení", 1)
        d form.AddBool("ShowSignalStopIndicator", "Na znamení", 1)
        d form.AddText("SignalStopSymbol", "Symbol na znamení")
        d form.AddBool("ShowStopAttributes", "Atributy", 1)
        d form.AddBool("ShowStopNotes", "Poznámky zastávky", 1)
        s f = form.AddFieldToLastGroup("MarkersPosition", "Pozice značek", $$$FieldTypeList, 0, 1)
        d f.AddListItem("AfterName", "Za zastávkou"), f.AddListItem("BeforeName", "Před zastávkou")
        d form.AddBool("ShowDirectionArrow", "Šipka směru", 0)
        s f = form.AddFieldToLastGroup("DirectionArrowPosition", "Pozice šipky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Right", "Vpravo"), f.AddListItem("Left", "Vlevo")
        s f = form.AddFieldToLastGroup("DirectionArrowColor", "Barva šipky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        d form.AddBool("DirectionArrowBold", "Tučná šipka", 0)
        d form.AddBool("CurrentStopUnderline", "Podtržení aktuální zast.", 1)
        d form.AddBool("CurrentStopBold", "Tučně aktuální zast.", 1)
        s f = form.AddDecimal("CurrentStopFontSize", "Font aktuální (prázdné = dle buňky)", "", 4, 200, "pt")
        s f = form.AddFieldToLastGroup("CurrentStopBackgroundColor", "Pozadí aktuální (prázdné = žádné)", $$$FieldTypeColor, 0, 0)
        d form.AddBool("ShowPassedStops", "Projeté zastávky", 0)
        s f = form.AddFieldToLastGroup("PassedStopsForeColor", "Barva projetých", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF999999"

        // ── Pozice ────────────────────────────────────────────────────────
        d form.AddGroup("position", "Pozice a velikost")
        d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
        d form.AddDecimal("Width", "Šířka", 50, 1, "", "mm", "", 1)
        d form.AddDecimal("Height", "Výška", 50, 1, "", "mm")
        d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
        s f = form.AddNumber("ZIndex", "ZIndex", 0, 0, 100)
        d form.AddBool("IsLocked", "Uzamčeno", 0)

        // ── Podmínky ──────────────────────────────────────────────────────
        d form.AddGroup("advanced", "Podmínky")
        s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
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
        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

}
