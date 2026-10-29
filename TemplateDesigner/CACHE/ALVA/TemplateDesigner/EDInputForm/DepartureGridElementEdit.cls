Include (GENERAL, EDInputForm)

/// EDInputForm schema pro tab „Obecné" editoru DepartureGridElement (Minuty odjezdů).
/// Field key = property path na C# DepartureGridElement. Kostra jako u ostatních tabulek: nahoře
/// skupiny jen tohoto prvku, pod nimi Záhlaví, Datové buňky, Rámeček prvku, Mřížka, Pozice, Podmínky.
/// Stylové balíčky přes ALVA.TemplateDesigner.EDInputForm.StyleFields (jediný zdroj sady polí).
///
/// Element-level styl = prostá pole (element je báze kaskády); per-sloupec/skupina override řeší
/// CellStyleEditor v tabu „Sloupce". Nullable HeaderLine2Style alokuje až zápis pole
/// (ElementPropertyAccessor), prázdná pole dědí z 1. řádku záhlaví.
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

        // Nahoře skupiny, které má jen tento prvek, pod nimi společný konec všech tabulek
        // (Záhlaví, Datové buňky, Rámeček prvku, Mřížka, Pozice a velikost, Podmínky).

        // ── Skupiny dnů ───────────────────────────────────────────────────
        // Původní „Obecné" mělo 10 polí bez jediné vysvětlivky (rozpočet je ~7). Rozpad podle toho,
        // čeho se pole týkají; klíče polí se NEMĚNÍ, mění se jen zařazení a popisky.
        d form.AddGroup("groups", "Skupiny dnů")
        // Šířka sloupce hodin se edituje v tabu „Sloupce" (řádek Hodiny) - jediný zdroj, ať nekoliduje.
        s f = form.AddBool("HourColumnPerGroup", "Hodiny u každé skupiny", 0)
        s f.helpText = "Sloupec s hodinami se zopakuje u každé skupiny dnů. Vypnuto = jeden sloupec hodin vlevo pro všechny skupiny."
        s f = form.AddFieldToLastGroup("HourColumnHeaderImageBase64", "Obrázek v záhlaví hodin", $$$FieldTypeImage, 0, 0)
        s f.helpText = "Ikona se vykreslí v záhlaví sloupce hodin u každé skupiny dnů. Když je nastavená, text záhlaví sloupce se netiskne."
        s f = form.AddNumber("MaxGroupCount", "Nejvýš skupin dnů", 0, 0, 10)
        s f.helpText = "Kolik skupin dnů se nejvýš vytiskne, další se vynechají. 0 = bez omezení."

        // ── Rozměry ───────────────────────────────────────────────────────
        // Mřížka (čáry) má vlastní skupinu níž; „a mřížka" v názvu skupiny mátlo.
        d form.AddGroup("layout", "Rozměry")
        s f = form.AddDecimal("MinuteWidth", "Šířka minuty", 5, 3, 15, "mm")
        s f.helpText = "Šířka jednoho políčka s minutou - určuje, kolik minut se vejde na řádek"
        s f = form.AddFieldToLastGroup("MinuteSpacing", "Zarovnání minut", $$$FieldTypeList, 0, 1)
        d f.AddListItem("ByContent", "Bez zarovnání"), f.AddListItem("Even", "Podle nejširšího odjezdu"), f.AddListItem("Fixed", "Na pevnou šířku")
        s f.helpText = "Bez zarovnání: každý odjezd zabere jen to, co potřebuje, takže minuty pod sebou nestojí. Podle nejširšího odjezdu: jedna rozteč pro celý prvek podle nejvíc označkovaného spoje - minuty stojí ve svislici, ale jeden takový spoj zředí celou vývěsku. Na pevnou šířku: rozteč je Šířka minuty, nejméně však minuta a jedna značka, takže ji data nezmění; spoj s víc značkami se do ní vejde jejich zmenšením."
        s f = form.AddDecimal("GroupSeparatorWidth", "Mezera mezi skupinami dnů", 0.5, 0, 5, "mm")
        s f.helpText = "Volné místo mezi bloky jednotlivých skupin dnů. 0 = bez mezery."
        s f = form.AddDecimal("OdjezdGap", "Mezera mezi odjezdy", "", 0, 10, "mm")
        s f.helpText = "Vodorovná mezera mezi jednotlivými odjezdy v řádku. Prázdné = podle šířky minuty."
        // Časová osa (rozsah hodin) i „Roztáhnout na výšku" jsou parametry sestavy
        // (CASOVA_OSA/HODINY_OD/DO, ROZTAHNOUT_NA_VYSKU), sdílené a autoritativní - NEduplikovat
        // jako pole prvku, parametr by je přebil a klik by nic nedělal (hlášení B22/B24).

        // ── Značky u odjezdů ──────────────────────────────────────────────
        // „Anotace" bylo interní slovo - uživatel tomu říká značky u odjezdů (písmena a symboly
        // u minuty, které vysvětluje legenda poznámek). Klíče polí zůstaly, přejmenované jsou
        // popisky; tytéž popisky nese i panel vlastností (`ux-conventions §8`).
        d form.AddGroup("departure", "Značky u odjezdů")
        s f = form.AddBool("ShowAnotace", "Značky u odjezdů", 1)
        s f.helpText = "Písmena a symboly u minuty (nízkopodlažní spoj, kolo, jede jen v určité dny…). Co se z nich tiskne, řídí prvek Poznámky - ten je i vysvětluje."
        s f = form.AddDecimal("AnotaceGap", "Mezera mezi značkou a minutou", "", 0, 5, "mm")
        s f.helpText = "Odstup značky od minuty. Prázdné = mezera podle velikosti písma."
        s f = form.AddDecimal("AnotaceInterGap", "Mezera mezi značkami", "", 0, 5, "mm")
        s f.helpText = "Odsazení mezi dvěma značkami u téhož odjezdu"
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceSymbolStyle", "", "", "", "", 0, "Symbol: ", 0, 0)
        // Negativní značka = symbol vykreslený inverzně (znak na plné ploše) - vlastní dvojice barev.
        s f = form.AddFieldToLastGroup("NegativniZnackaBackColor", "Negativní značka: Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        s f.helpText = "Negativní značka = znak vykreslený inverzně, tedy světlý na plné barevné ploše"
        s f = form.AddFieldToLastGroup("NegativniZnackaForeColor", "Negativní značka: Barva textu", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFFFFFFF"
        // „Textová značka", ne „Text": z „Text: Barva textu" nešlo poznat, o jaký text jde (`B194h` H11).
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "AnotaceTextStyle", "", "", "", "", 0, "Textová značka: ", 0, 0)

        // ── Záhlaví / Datové buňky ────────────────────────────────────────
        // Skupina části prvku: první pole část zapíná, pak pole pro celý řádek, pod nimi sada stylu.
        // Styl je rozepsaný jako u ostatních tabulek (do 28. 10. za tlačítkem „Upravit styl…").
        // Samostatné „Zarovnání záhlaví svisle" (HeaderVerticalAlignment) tu záměrně není: dělalo
        // totéž co Zarovnání svisle ve stylu a platilo, jen když bylo prázdné - zůstává jako výchozí Dole.
        d form.AddGroup("hstyle", "Záhlaví")
        s f = form.AddBool("ShowSkupinaDnuHeader", "Záhlaví skupin dnů", 1)
        s f.helpText = "Záhlaví nad každým blokem minut (např. ""Pracovní dny"", ""Sobota"")"
        s f = form.AddDecimal("SkupinaDnuHeaderLineHeight", "Nejmenší výška řádku", 4, 2, 10, "mm")
        s f.helpText = "Nejmenší výška každého řádku záhlaví skupiny dnů (1. i 2. řádku). Když není vyplněná Velikost, písmo záhlaví se z ní dopočítá."
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "hstyle", "Záhlaví", "#FF000000", "", 0, "", 1)
        // 2. řádek záhlaví skupiny (Nadpis2 z dat, např. „den" v „Pracovní / den") je text uvnitř
        // záhlaví: pozadí, zarovnání a okraje mu kreslí 1. řádek, proto jen sada Text.
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderLine2Style", "", "", "", "", 0, "2. řádek: ", 1, 0)

        // DataStyle = báze (minuty i hodinová čísla z ní dědí), MinuteStyle = minutový override navrch.
        d form.AddGroup("dstyle", "Datové buňky")
        s f = form.AddDecimal("DataRowHeight", "Výška řádku", "", 1, 50, "mm")
        s f.helpText = "Prázdné = podle velikosti písma."
        s f = form.AddFieldToLastGroup("AlternatingRowColor", "Střídání řádků: Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.helpText = "Barva, kterou se podbarví každý druhý řádek. Prázdné = bez střídání."
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "dstyle", "Datové buňky", "#FF000000", "", 0)
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "MinuteStyle", "", "", "", "", 0, "Minuta: ", 1, 0)

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
