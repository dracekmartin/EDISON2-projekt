Include (GENERAL, EDInputForm)

/// EDInputForm schema pro tab „Obecné" editoru StopListElement (zastávkový seznam).
/// Field key = property path na C# StopListElement. Embeduje se do StopColumnsEditorDialog
/// vedle custom editoru sloupců (tab „Sloupce"). Validate jen echo - žádná cross-field logika.
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
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Obecné ────────────────────────────────────────────────────────
        d form.AddGroup("general", "Obecné")
        s f = form.AddFieldToLastGroup("GridColor", "Barva mřížky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFD0D0D0"
        s f = form.AddDecimal("GridLineWidth", "Tloušťka mřížky", "", 0, 5, "mm")
        s f.helpText = "Prázdné = dle tloušťky rámečku elementu"
        s f = form.AddFieldToLastGroup("GridLineStyle", "Styl mřížky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Solid", "Plná"), f.AddListItem("Dashed", "Čárkovaná"), f.AddListItem("Dotted", "Tečkovaná")
        d form.AddBool("ShowHeader", "Záhlaví sloupců", 1)
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 50, "mm")
        d form.AddBool("ShowRowSeparators", "Oddělovací čáry", 0)
        s f = form.AddBool("ShowColumnSeparators", "Svislé čáry mezi sloupci", 0)
        s f.helpText = "Čáry mezi sloupci ve tvaru mřížky (barva, tloušťka a styl výše)"

        // ── Styl záhlaví / dat (sdílený balíček polí) ─────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Styl záhlaví", "#FF000000", "#FFE0E0E0")
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF000000", "#00FFFFFF")

        // ── Zastávky - specifické (značky, směr, zvýraznění) ──────────────
        d form.AddGroup("stops", "Zastávky - specifické")
        d form.AddBool("ShowStopIndentation", "Odsazení", 1)
        d form.AddBool("ShowSignalStopIndicator", "Na znamení", 1)
        d form.AddText("SignalStopSymbol", "Symbol na znamení")
        d form.AddBool("ShowStopAttributes", "Atributy", 1)
        s f = form.AddBool("ShowTransferAttributes", "Přestupy (vlak/autobus)", 0)
        s f.helpText = "Zobrazí u zastávky symbol přestupu na vlak (L) / autobus (A). Původní JŘ je netiskl."
        s f = form.AddFieldToLastGroup("MultiPlatformMode", "Více označníků", $$$FieldTypeList, 0, 1)
        d f.AddListItem("FromRoute", "Z trasy linky"), f.AddListItem("MostFrequent", "Nejčetnější dle spojů"), f.AddListItem("All", "Výčet všech")
        d f.AddListItem("RowPerPlatform", "Řádek pro každý označník")
        s f.helpText = "Sloupec Označník, když spoje jedou z různých označníků zastávky: hodnota z trasy / označník s nejvíce spoji / výčet všech dle četnosti / nebo řádek pro každý označník (časy jen variant daného označníku). Zapněte si k tomu sloupec Označník v tabu Sloupce; řádky vyžadují zapnuté Všechny varianty tras."
        d form.AddBool("ShowStopNotes", "Poznámky zastávky", 1)
        d form.AddBool("ShowAllRouteVariants", "Všechny varianty tras", 1)
        s f = form.AddNumber("PartCount", "Rozdělit na více oddílů", 1, 1, 3)
        s f.helpText = "Seznam se rozřeže na tolik oddílů vedle sebe. Každý oddíl nese celou tabulku znovu (záhlaví i všechny sloupce), jen s další porcí zastávek, takže je prvek nižší. Sloupce se pak musí vejít do šířky jednoho oddílu."
        s f = form.AddDecimal("PartGap", "Mezera mezi oddíly", 3, 0, 50, "mm")
        s f = form.AddFieldToLastGroup("MarkersPosition", "Pozice značek", $$$FieldTypeList, 0, 1)
        d f.AddListItem("AfterName", "Za zastávkou"), f.AddListItem("BeforeName", "Před zastávkou")
        // Zapnutí šipky řídí parametr sestavy TISK_SIPKY - jako pole prvku by bylo mrtvé (parametr
        // ho přebije). Tady zůstává jen vzhled šipky.
        s f = form.AddFieldToLastGroup("DirectionArrowPosition", "Pozice šipky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Right", "Vpravo (u pravého okraje elementu)"), f.AddListItem("Left", "Vlevo (u levého okraje elementu)")
        s f.helpText = "Tisk šipky se zapíná v parametrech sestavy (Tisknout šipky směru)"
        s f = form.AddFieldToLastGroup("DirectionArrowColor", "Barva šipky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        d form.AddDecimal("DirectionArrowWidth", "Tloušťka šipky", 0.2, 0.05, 5, "mm")
        d form.AddDecimal("DirectionArrowHeadSize", "Velikost hrotu", 1.2, 0.5, 20, "mm")
        s f = form.AddFieldToLastGroup("DirectionArrowHead", "Hrot šipky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Filled", "Plný"), f.AddListItem("Open", "Otevřený"), f.AddListItem("None", "Bez hrotu")
        s f = form.AddFieldToLastGroup("DirectionArrowLineStyle", "Styl čáry šipky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Solid", "Plná"), f.AddListItem("Dashed", "Čárkovaná"), f.AddListItem("Dotted", "Tečkovaná")
        d form.AddBool("CurrentStopUnderline", "Podtržení aktuální zast.", 1)
        d form.AddBool("CurrentStopBold", "Tučně aktuální zast.", 1)
        s f = form.AddDecimal("CurrentStopFontSize", "Velikost aktuální (prázdné = dle buňky)", "", 4, 200, "pt")
        s f = form.AddFieldToLastGroup("CurrentStopBackgroundColor", "Pozadí aktuální (prázdné = žádné)", $$$FieldTypeColor, 0, 0)
        // Zapnutí tisku projetých zastávek řídí parametr sestavy TISK_PROJETYCH_ZAST; tady je vzhled.
        s f = form.AddFieldToLastGroup("PassedStopsForeColor", "Barva projetých", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF999999"
        s f.helpText = "Tisk projetých zastávek se zapíná v parametrech sestavy (Tisknout projeté zastávky)"
        s f = form.AddFieldToLastGroup("PassedStopsTime", "Čas u projetých", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Hide", "Nezobrazovat"), f.AddListItem("Show", "Zobrazit")
        s f.helpText = "Časy jsou počítané od vývěskové zastávky, u projetých proto vycházejí záporné"

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 50, 50)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

        s f = form.AddText("RowVisibilityExpression", "Viditelnost řádku")
        s f.helpText = "Zastávka, kde výraz neplatí, se nezobrazí. Pole řádku: POPIS, TC, CAS_JIZDY, PORADI, JE_AKTUALNI. Např. {JE_AKTUALNI} == 1"
        s f = form.AddFieldToLastGroup("__highlightRules", "Zvýraznění řádků", $$$FieldTypeButton, 0, 0)
        s f.value = "Upravit pravidla…"
        s f.helpText = "Seznam pravidel: podmínka → barva pozadí řádku. První splněné vyhrává."

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
