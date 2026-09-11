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
        d form.AddBool("ShowHeader", "Záhlaví sloupců", 1)
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 50, "mm")

        // ── Styl záhlaví / dat (sdílený balíček polí) ─────────────────────
        // Pozadí bez výchozí hodnoty - nese ho prvek sám, prázdno znamená bez podbarvení.
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Styl záhlaví", "#FF000000", "", 1, "", 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Styl datových buněk", "#FF000000", "#00FFFFFF")

        // ── Co se u zastávky tiskne ───────────────────────────────────────
        // Skupina „Zastávky - specifické" měla 23 polí v jednom seznamu (rozpočet je ~7), takže
        // se rozpadla podle toho, čeho se pole týkají. Klíče polí se NEMĚNÍ - jde jen o to, v jaké
        // skupině se zobrazí, takže uložené šablony ani panel vlastností to nepocítí.
        d form.AddGroup("stopscontent", "Zastávky - obsah")
        d form.AddBool("ShowStopIndentation", "Odsazení", 1)
        s f = form.AddBool("ShowSignalStopIndicator", "Na znamení", 1)
        s f.helpText = "Zastávka, kde spoj staví jen na znamení, dostane značku (výchozí ""×"")"
        d form.AddText("SignalStopSymbol", "Symbol na znamení")
        s f = form.AddBool("ShowStopAttributes", "Atributy", 1)
        s f.helpText = "Značky u zastávky z jízdního řádu (např. ""S"" - tisk stanoviště, zóny)"
        s f = form.AddBool("ShowTransferAttributes", "Přestupy (vlak/autobus)", 0)
        s f.helpText = "Zobrazí u zastávky symbol přestupu na vlak (L) / autobus (A). Původní JŘ je netiskl."
        d form.AddBool("ShowStopNotes", "Poznámky zastávky", 1)
        s f = form.AddBool("ShowSubMinuteTravel", "Jízda pod minutu jako <1", 0)
        s f.helpText = "Jede-li se na následující zastávku pod minutu, vypíše se ve sloupci časů ""<1"" místo prázdna. Zastávka, kterou spoj neobsluhuje, zůstane prázdná."

        // ── Stanoviště ────────────────────────────────────────────────────
        // Varianty tras se od 8/2026 zapínají jako sloupec v tabu Sloupce, ne zaškrtávátkem tady.
        d form.AddGroup("stopsplatform", "Stanoviště")
        s f = form.AddFieldToLastGroup("MultiPlatformMode", "Více stanovišť", $$$FieldTypeList, 0, 1)
        d f.AddListItem("FromRoute", "Z trasy linky"), f.AddListItem("MostFrequent", "Nejčetnější dle spojů"), f.AddListItem("All", "Výčet všech")
        d f.AddListItem("RowPerPlatform", "Řádek pro každé stanoviště")
        s f.helpText = "Když spoje jedou z různých stanovišť zastávky: hodnota z trasy / to s nejvíce spoji / výčet všech dle četnosti / nebo řádek pro každé (časy jen variant daného stanoviště). Platí pro údaj zvolený v parametru sestavy Číslo stanoviště podle a zapněte si k tomu jeho sloupec v tabu Sloupce; řádky vyžadují zapnutý sloupec Varianty tras."

        // ── Rozdělení dlouhé trasy ────────────────────────────────────────
        d form.AddGroup("stopsparts", "Rozdělení na oddíly")
        s f = form.AddNumber("PartCount", "Rozdělit na více oddílů", 1, 1, 3)
        s f.helpText = "Seznam se rozřeže na tolik oddílů vedle sebe. Každý oddíl nese celou tabulku znovu (záhlaví i všechny sloupce), jen s další porcí zastávek, takže je prvek nižší. Sloupce se pak musí vejít do šířky jednoho oddílu."
        s f = form.AddDecimal("PartGap", "Mezera mezi oddíly", 3, 0, 50, "mm")

        // ── Značky a šipka směru ──────────────────────────────────────────
        d form.AddGroup("stopsmarkers", "Značky a šipka směru")
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

        // ── Aktuální a projeté zastávky ───────────────────────────────────
        d form.AddGroup("stopshighlight", "Aktuální a projeté zastávky")
        d form.AddBool("CurrentStopUnderline", "Podtržení aktuální zast.", 1)
        d form.AddBool("CurrentStopBold", "Tučně aktuální zast.", 1)
        s f = form.AddDecimal("CurrentStopFontSize", "Velikost aktuální (prázdné = dle buňky)", "", 4, 200, "pt")
        s f = form.AddFieldToLastGroup("CurrentStopBackgroundColor", "Pozadí aktuální (prázdné = žádné)", $$$FieldTypeColor, 0, 0)
        s f = form.AddFieldToLastGroup("CurrentStopTextColor", "Barva textu aktuální (prázdné = dle buňky)", $$$FieldTypeColor, 0, 0)
        // Zapnutí tisku projetých zastávek řídí parametr sestavy TISK_PROJETYCH_ZAST; tady je vzhled.
        s f = form.AddFieldToLastGroup("PassedStopsForeColor", "Barva projetých", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF999999"
        s f.helpText = "Tisk projetých zastávek se zapíná v parametrech sestavy (Tisknout projeté zastávky)"
        s f = form.AddFieldToLastGroup("PassedStopsTime", "Čas u projetých", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Hide", "Nezobrazovat"), f.AddListItem("Show", "Zobrazit")
        s f.helpText = "Časy jsou počítané od vývěskové zastávky, u projetých proto vycházejí záporné"

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 50, 50)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

        s f = form.AddText("RowVisibilityExpression", "Viditelnost řádku")
        s f.placeholders = 1
        s f.helpText = "Zastávka, kde výraz neplatí, se nezobrazí. Pole řádku: NAZEV_ZASTAVKY, CAS_JIZDY, PORADI, AKTUALNI_ZASTAVKA, NA_ZNAMENI. Např. {NA_ZNAMENI} == 'false'"
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
