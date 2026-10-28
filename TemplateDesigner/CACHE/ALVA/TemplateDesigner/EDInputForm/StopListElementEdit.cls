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

        // Nahoře skupiny, které má jen tento prvek, pod nimi společný konec všech tabulek
        // (Záhlaví, Datové buňky, Rámeček prvku, Mřížka, Pozice a velikost, Podmínky).

        // ── Co se u zastávky tiskne ───────────────────────────────────────
        // Skupina „Zastávky - specifické" měla 23 polí v jednom seznamu (rozpočet je ~7), takže
        // se rozpadla podle toho, čeho se pole týkají. Klíče polí se NEMĚNÍ - jde jen o to, v jaké
        // skupině se zobrazí, takže uložené šablony ani panel vlastností to nepocítí.
        d form.AddGroup("stopscontent", "Zastávky")
        // Popisek říká, co pole dělá, a odpovídá jeho typu (zaškrtávátko = co zapíná, číslo = Počet…,
        // výběr = co se vybírá) - rozhodnutí Jan 28. 10., ux-conventions §8.
        s f = form.AddBool("ShowStopIndentation", "Odsazení podle trasy", 1)
        s f.helpText = "Název zastávky se odsadí tak, jak je odsazený v trase linky."
        s f = form.AddBool("ShowSignalStopIndicator", "Značka na znamení", 1)
        s f.helpText = "Zastávka, kde spoj staví jen na znamení, dostane značku (výchozí ""×"")"
        d form.AddText("SignalStopSymbol", "Symbol na znamení")
        s f = form.AddBool("ShowStopAttributes", "Atributy zastávky", 1)
        s f.helpText = "Značky u zastávky z jízdního řádu (např. ""S"" - tisk stanoviště, zóny)"
        s f = form.AddBool("ShowTransferAttributes", "Přestupy (vlak/autobus)", 0)
        s f.helpText = "Zobrazí u zastávky symbol přestupu na vlak nebo autobus a k němu řádek v poznámkách. Starý tisk je měl jen u MHD."
        d form.AddBool("ShowStopNotes", "Poznámky zastávky", 1)
        // Strana značek, které zapínají pole nad ním (na znamení, atributy, přestupy, poznámky).
        s f = form.AddFieldToLastGroup("MarkersPosition", "Pozice značek", $$$FieldTypeList, 0, 1)
        d f.AddListItem("AfterName", "Za názvem"), f.AddListItem("BeforeName", "Před názvem")
        s f = form.AddBool("ShowSubMinuteTravel", "Jízda pod minutu jako <1", 0)
        s f.helpText = "Jede-li se na následující zastávku pod minutu, vypíše se ve sloupci časů ""<1"" místo prázdna. Zastávka, kterou spoj neobsluhuje, zůstane prázdná."

        // ── Stanoviště ────────────────────────────────────────────────────
        // Varianty tras se od 8/2026 zapínají jako sloupec v tabu Sloupce, ne zaškrtávátkem tady.
        d form.AddGroup("stopsplatform", "Stanoviště")
        s f = form.AddFieldToLastGroup("MultiPlatformMode", "Výběr stanoviště", $$$FieldTypeList, 0, 1)
        d f.AddListItem("FromRoute", "Podle trasy linky"), f.AddListItem("MostFrequent", "Nejčetnější podle spojů"), f.AddListItem("All", "Všechna podle četnosti")
        d f.AddListItem("RowPerPlatform", "Řádek pro každé stanoviště")
        s f.helpText = "Když spoje jedou z různých stanovišť zastávky: hodnota z trasy / to s nejvíce spoji / výčet všech podle četnosti / nebo řádek pro každé (časy jen variant daného stanoviště). Platí pro údaj zvolený v parametru sestavy Číslo stanoviště podle a zapněte si k tomu jeho sloupec v tabu Sloupce; řádky vyžadují zapnutý sloupec Varianty tras."

        // ── Rozdělení dlouhé trasy ────────────────────────────────────────
        d form.AddGroup("stopsparts", "Rozložení")
        s f = form.AddNumber("PartCount", "Počet oddílů", 1, 1, 3)
        s f.helpText = "1 = bez rozdělení. Seznam se rozřeže na tolik oddílů vedle sebe. Každý oddíl nese celou tabulku znovu (záhlaví i všechny sloupce), jen s další porcí zastávek, takže je prvek nižší. Sloupce se pak musí vejít do šířky jednoho oddílu."
        s f = form.AddDecimal("PartGap", "Mezera mezi oddíly", 3, 0, 50, "mm")

        // ── Aktuální a projeté zastávky ───────────────────────────────────
        d form.AddGroup("stopshighlight", "Aktuální a projeté zastávky")
        // Předpona „Aktuální:" / „Projeté:" a kanonický název vlastnosti, jako zóny Minut odjezdů
        // („Minuta: Velikost"); vysvětlivky prázdné hodnoty v nápovědě (`B194` E10).
        s f = form.AddBool("CurrentStopBold", "Aktuální: Tučné", 1)
        s f.helpText = "Zastávka, pro kterou se vývěska tiskne"
        s f = form.AddBool("CurrentStopUnderline", "Aktuální: Podtržení", 1)
        s f = form.AddDecimal("CurrentStopFontSize", "Aktuální: Velikost", "", 3, 72, "pt")
        s f.helpText = "Prázdné = jako ostatní zastávky"
        s f = form.AddFieldToLastGroup("CurrentStopTextColor", "Aktuální: Barva textu", $$$FieldTypeColor, 0, 0)
        s f.helpText = "Prázdné = jako ostatní zastávky"
        s f = form.AddFieldToLastGroup("CurrentStopBackgroundColor", "Aktuální: Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.helpText = "Prázdné = bez pozadí"
        // Zapnutí tisku projetých zastávek řídí parametr sestavy TISK_PROJETYCH_ZAST; tady je vzhled.
        s f = form.AddFieldToLastGroup("PassedStopsForeColor", "Projeté: Barva textu", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF999999"
        s f.helpText = "Tisk projetých zastávek se zapíná v parametrech sestavy (Tisknout projeté zastávky)"
        s f = form.AddBool("PassedStopsShowTime", "Projeté: Zobrazit čas", 0)
        s f.helpText = "Časy jsou počítané od vývěskové zastávky, u projetých proto vycházejí záporné"

        // ── Šipka směru ───────────────────────────────────────────────────
        d form.AddGroup("stopsmarkers", "Šipka směru")
        // Zapnutí šipky řídí parametr sestavy TISK_SIPKY - jako pole prvku by bylo mrtvé (parametr
        // ho přebije). Tady zůstává jen vzhled šipky.
        // Ve vlastní skupině se název části neopakuje - „Barva", ne „Barva šipky" (ux-conventions §8).
        s f = form.AddFieldToLastGroup("DirectionArrowPosition", "Pozice", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Right", "Vpravo"), f.AddListItem("Left", "Vlevo")
        s f.helpText = "Šipka se kreslí u okraje prvku, ne u sloupců. Tisk šipky se zapíná v parametrech sestavy (Tisknout šipky směru)"
        s f = form.AddFieldToLastGroup("DirectionArrowColor", "Barva", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        d form.AddDecimal("DirectionArrowWidth", "Tloušťka", 0.2, 0.05, 5, "mm")
        d form.AddDecimal("DirectionArrowHeadSize", "Velikost hrotu", 1.2, 0.5, 20, "mm")
        s f = form.AddFieldToLastGroup("DirectionArrowHead", "Tvar hrotu", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Filled", "Plný"), f.AddListItem("Open", "Otevřený"), f.AddListItem("None", "Bez hrotu")
        s f = form.AddFieldToLastGroup("DirectionArrowLineStyle", "Styl čáry", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Solid", "Plná"), f.AddListItem("Dashed", "Čárkovaná"), f.AddListItem("Dotted", "Tečkovaná")

        // ── Záhlaví / Datové buňky ────────────────────────────────────────
        // Skupina části prvku: první pole část zapíná, pak pole platná pro celý řádek (ve sloupci je
        // přebít nejde), pod nimi sdílený balíček polí stylu, který okno sloupců přebíjí po sloupcích.
        // Skupinu „Obecné" prvek nemá - nic neříkala o tom, co v ní je.
        // Pozadí bez výchozí hodnoty - nese ho prvek sám, prázdno znamená bez podbarvení.
        d form.AddGroup("hstyle", "Záhlaví")
        d form.AddBool("ShowHeader", "Záhlaví sloupců", 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Záhlaví", "#FF000000", "", 0, "", 1)
        d form.AddGroup("dstyle", "Datové buňky")
        s f = form.AddDecimal("DataRowHeight", "Výška řádku", "", 1, 50, "mm")
        s f.helpText = "Prázdné = podle velikosti písma."
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Datové buňky", "#FF000000", "#00FFFFFF", 0)

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 50, 50)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

        s f = form.AddText("RowVisibilityExpression", "Podmínka viditelnosti řádku")
        s f.placeholders = 1
        // Pole řádku doplní návrhář podle agendy šablony (ElementFormBinder.DoplnPoleRadku) - pevný seznam lhal.
        s f.helpText = "Zastávka, kde výraz neplatí, se nezobrazí. Např. {NAZEV_ZASTAVKY} != 'Nádraží'."
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
