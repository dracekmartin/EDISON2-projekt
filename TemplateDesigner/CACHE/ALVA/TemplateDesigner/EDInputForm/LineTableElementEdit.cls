Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci LineTableElement properties (knižní JŘ -
/// matice zastávky × spoje).
///
/// Field key = property path na C# LineTableElement (vč. nested HeaderStyle.X / DataStyle.X).
/// Init jen definuje strukturu + defaults; generický ElementEditDialog dělá pre-fill přes reflexi.
///
/// Pozn.: LineTable v prvním řezu NEMÁ per-column kolekci (sloupce spojů vznikají z dat,
/// popisové sloupce jsou pevné properties) → čistý generic kandidát jako NotesLegend.
/// Per-column overridy (2-tab dialog se sdíleným ColumnListEditor) přijdou s R1 extrakcí
/// tabulkového jádra.
///
/// ⚠️ TECHNICKÝ DLUH - Schema duplikace: manual mirror PropertyDescriptors v
/// `LineTableElement.cs`. Viz memory `project_edinputform_schema_duplication.md`.
Class ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // Pozn.: viditelnost / šířky / pořadí / per-column styl jednotlivých sloupců
        // (Zóna, km, Tč, Zastávka, MHD, Spoj) se editují v tabu „Sloupce" (ColumnListEditor),
        // konzistentně se StopList/DepartureGrid. Tady jen element-wide vlastnosti.

        // ── Skupina "Řádky" ───────────────────────────────────────────────
        d form.AddGroup("rows", "Řádky")
        d form.AddBool("ShowGroupHeaders", "Nadpisy skupin dnů", 1)
        d form.AddBool("ShowTripNumbers", "Čísla spojů", 1)
        d form.AddBool("ShowPlatnostRow", "Řádek platnosti", 1)
        d form.AddBool("ShowNzRow", "Řádek negativních značek", 1)
        d form.AddBool("TurnusyKeSpojum", "Turnusy ke spojům", 0)
        d form.AddBool("ShowPripojeRows", "Přípojné řádky (vyčká/navazuje)", 1)
        s f = form.AddDecimal("DataRowHeight", "Výška řádku (prázdné = auto)", "", 1, 20, "mm")
        s f.helpText = "Prázdné = auto podle velikosti písma"
        s f = form.AddDecimal("PomerZnacek", "Velikost značek", 1.5, 1, 3)
        s f.helpText = "Kolikrát větší je písmo značek platnosti a negativních značek než písmo datové buňky. Starý tisk je má jedenapůlkrát větší, aby byly na výtisku čitelné. Řádek značek se tím zvýší."

        // ── Skupina "Šířka sloupců" ───────────────────────────────────────
        // Šířka sloupce spoje je zadané přání a to, co se do sloupce musí vejít. Všechno, co do
        // toho vstupuje - i odstupy mezi značkami a časem - patří pod jeden nadpis; do 8. 9. to
        // bylo roztroušené mezi "Sloupce" a "Datové buňky" a nešlo přečíst jako jedna věc.
        // ⚠️ Ne "Šířka a dělení" - "dělení sloupců" už v UI znamená členění na skupiny dnů.
        d form.AddGroup("width", "Šířka sloupců")
        s f = form.AddDecimal("MezeraMeziZnackamiMm", "Mezera mezi značkami", "", 0, 10, "mm")
        s f.helpText = "Odstup mezi značkami nad časem (například kladívko a vozík vedle sebe). Prázdné = mezera z písma. Dvojice značek bývá ve sloupci to nejširší, takže utažením téhle mezery jde sloupec zúžit až na šířku času."
        s f = form.AddDecimal("MezeraZnackaCasMm", "Mezera mezi značkou a časem", "", 0, 10, "mm")
        s f.helpText = "Odstup značky, která stojí v buňce vlevo od času (návaznost, jen nástup). Prázdné = mezera z písma."
        s f = form.AddDecimal("MezeraHodinaMinutaMm", "Mezera mezi hodinou a minutou", "", 0, 10, "mm")
        s f.helpText = "Odstup mezi hodinou a menší minutou. Prázdné = mezera z písma. Uplatní se jen u formátu s hodinou větším písmem a zužuje i sloupec, protože je uvnitř času."
        s f = form.AddBool("PadEmptyColumns", "Dorovnat prázdnými sloupci", 0)
        s f.helpText = "Co se stane s volným místem vpravo: zapnuté = rozkreslí se prázdnými sloupci, takže tabulka končí rovně, vypnuté = zůstane bílé. Týká se posledního nedopsaného bloku i tabulky, které místo zbylo kvůli největší šířce."
        s f = form.AddBool("JednotnaSirkaSpoju", "Stejně široké sloupce spojů", 0)
        s f.helpText = "Vypnuté = každý sloupec je tak široký, jak ho potřebuje jeho obsah, takže ten se značkami je širší než ostatní (tak to dělá starý tisk) a tabulka působí roztřepeně; do řádku se ale vejde víc spojů. Zapnuté = všechny dostanou šířku toho nejširšího, tabulka je pravidelná a zabere víc místa."
        s f = form.AddDecimal("MezeraMeziSkupinamiMm", "Mezera mezi skupinami sloupců", 0, 0, 20, "mm")
        s f.helpText = "Volné místo za popisným blokem a mezi skupinami dnů. 0 = souvislá mřížka bez mezer. Mezera se bere ze šířky prvku, takže se do řádku vejde méně spojů."

        // ── Skupina "Popisné sloupce" ──────────────────────────────────────
        // Zóna, km, Tč, zastávka a MHD. Viditelnost, šířky a pořadí jednotlivých sloupců se
        // editují v tabu "Sloupce"; sem patří jen volby platné pro celou skupinu.
        d form.AddGroup("columns", "Popisné sloupce")
        s f = form.AddBool("RozdelitZonyDoSloupcu", "Každá zóna ve vlastním sloupci", 0)
        s f.helpText = "Zastávka na hranici zón má víc zón. Vypnuté = všechny v jednom sloupci za sebou, zapnuté = každá ve vlastním sloupci vedle sebe (jako kilometry). Sloupec zón se tím rozšíří o tolik dílů, kolik zón má nejbohatší zastávka."
        // Jeden nadpis nad celou skupinou a jeden slitý blok bez vnitřních čar. Změřeno na vzoru IDZK 940.
        s f = form.AddText("PopisneSloupceNadpis", "Nadpis popisných sloupců")
        s f.helpText = "Text nad zónou, km, Tč, zastávkou a MHD - všechny mají jeden společný nadpis. Prázdné = bez nadpisu."
        s f = form.AddBool("SloucitPopisyRozdvojene", "Sloučit popis u příjezdu a odjezdu", 0)
        s f.helpText = "Zastávka, u které se tiskne příjezd i odjezd, zabírá dva řádky. Vypnuté = km, tarifní číslo, název i MHD se na obou zopakují (tvar windowsového tisku). Zapnuté = vypíšou se jednou přes oba řádky na střed a mezi časy je vodorovná úsečka (tvar postskriptového tisku). Zóna se opakuje vždy."
        s f = form.AddBool("PopisneSloupceBezVnitrnichCar", "Popisné sloupce bez vnitřních čar", 0)
        s f.helpText = "Popisné sloupce se kreslí jako jeden slitý blok. Svislé čáry mezi sloupci spojů zůstanou."

        // ── Skupina "Záhlaví" ─────────────────────────────────────────────
        d form.AddGroup("header", "Záhlaví")
        // Velikost je u tabulky rozměrová volba, ne kosmetika: nese výšku řádku i šířku sloupce
        // spoje. Prázdné pole = dědí se (kaskáda dá 9 pt), per-sloupcová velikost z dialogu vyhrává.
        s f = form.AddDecimal("HeaderStyle.FontSize", "Velikost", 9, 3, 24, "pt", "0.#")
        s f.helpText = "Písmo záhlaví celého prvku. Jednotlivému sloupci se dá přebít v tabu Sloupce."
        // Bez výchozí hodnoty - podbarvení nese prvek sám, prázdno znamená bez podbarvení.
        s f = form.AddFieldToLastGroup("HeaderStyle.BackgroundColor", "Pozadí záhlaví", $$$FieldTypeColor, 0, 0)
        s f = form.AddFieldToLastGroup("HeaderStyle.TextColor", "Barva textu záhlaví", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"

        // ── Skupina "Datové buňky" ────────────────────────────────────────
        d form.AddGroup("data", "Datové buňky")
        s f = form.AddDecimal("DataStyle.FontSize", "Velikost", 8, 3, 24, "pt", "0.#")
        s f.helpText = "Písmo buněk celého prvku. Řídí i výšku řádku, když je prázdná, a šířku sloupce spoje."
        s f = form.AddFieldToLastGroup("DataStyle.BackgroundColor", "Pozadí buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#00FFFFFF"
        s f = form.AddFieldToLastGroup("DataStyle.TextColor", "Barva textu buněk", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        // Vnitřní okraj je u této tabulky rozměrová volba, ne kosmetika: dolní práh sloupce spoje
        // je šířka času plus okraje po stranách, takže rozhoduje, kolik spojů se vejde do bloku
        // a kolikrát se tabulka řízne na šířku. Jen vodorovné - svislé drží pole "Výška řádku".
        // ⚠️ Výchozí hodnota vypsaná, ne prázdné pole: „prázdné = 0,5 mm" nedávalo smysl nikomu,
        // kdo si chtěl sloupce utáhnout, protože z formuláře nebylo poznat, kolik vzduchu tam je
        // (nález Jan 22. 9.). Binder prázdnou hodnotu modelu nechá na tomhle defaultu.
        s f = form.AddDecimal("DataStyle.PaddingLeft", "Vnitřní okraj vlevo", 0.5, 0, 20, "mm")
        s f.helpText = "Odsazení textu od svislé čáry. Větší okraj rozšíří sloupec spoje, takže se do řádku vejde méně spojů."
        s f = form.AddDecimal("DataStyle.PaddingRight", "Vnitřní okraj vpravo", 0.5, 0, 20, "mm")
        s f.helpText = "Odsazení textu od svislé čáry. Je to hlavní páka na vzduch kolem času: větší okraj sloupec rozšíří, nulou se stáhne až k obsahu, ale nikdy tak, aby se číslice dotýkaly čar."
        s f = form.AddFieldToLastGroup("FormatCasu", "Formát času", $$$FieldTypeList, 0, 1)
        d f.AddListItem("HodinaVetsi", "Minuty menším písmem (hh mm)")
        d f.AddListItem("SDvojteckou", "Celý čas stejně (hh:mm)")
        s f.value = "HodinaVetsi"
        s f.helpText = "Starý tisk sází hodinu větším písmem a minutu menším, oddělené mezerou bez dvojtečky. Druhá volba vypíše celý čas jednou velikostí."
        s f = form.AddDecimal("PomerMinuty", "Velikost minut", 0.75, 0.3, 1)
        s f.helpText = "Podíl velikosti minut vůči hodině. 1 = obojí stejně velké. Uplatní se jen u formátu s hodinou větším písmem."
        s f = form.AddBool("VyplnitPrazdneBunkySpoju", "Tečky v prázdných buňkách", 0)
        s f.helpText = "Buňka spoje, ve které není čas, se vyplní tečkami - tabulka pak drží svislé vodicí pruhy jako starý tisk. U linky, kde většina spojů jede jen část trasy, se tím tabulka zaplní a čte se hůř."
        s f = form.AddBool("TucneZastavkyDleTrasy", "Tučné zastávky dle trasy", 1)
        s f.helpText = "V trase linky jde u zastávky zadat atribut ""tučně"" - zapnuté = návrhář ho respektuje, stejně jako to dělá starý tisk. Vypnuté = všechny zastávky stejným písmem."
        s f = form.AddBool("CaryZastavekDleTrasy", "Čáry u zastávek dle trasy", 1)
        s f.helpText = "V trase linky jde u zastávky zadat ""podtrženo"" nebo ""podtrženo nad i pod"" - zapnuté = návrhář nakreslí čáru přes celou šířku tabulky, stejně jako starý tisk. Odděluje se tím úsek linky."
        s f = form.AddBool("TeckyZaNazvemZastavky", "Tečky za názvem zastávky", 0)
        s f.helpText = "Za název zastávky se doplní tečky k pravému okraji sloupce, aby oko dojelo k času. Končí před piktogramem přestupu."
        d form.AddBool("HighlightOddTc", "Podbarvit lichá Tč", 1)
        s f = form.AddFieldToLastGroup("OddTcBackColor", "Barva lichých Tč", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFEEEEEE"

        // ── Skupina "Přípojné řádky" (#8360) ──────────────────────────────
        // Přípoj má jít odlišit od časů spojů - obvykle menším kurzívním písmem, aby v tabulce
        // „nekřičel" stejně jako odjezdy. Prázdné = dědí se z datových buněk.
        d form.AddGroup("pripoj", "Přípojné řádky")
        s f = form.AddFieldToLastGroup("PripojeSlucovani", "Slučovat přípoje", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Ne", "Ne - řádek pro každý spoj")
        d f.AddListItem("PodleSmeru", "Podle směru")
        d f.AddListItem("PodleSmeruALinky", "Podle směru a linky")
        s f.helpText = "Sloučený řádek nese časy víc spojů a v popisku má směr (""ze směru Trhové Sviny""), takže šetří místo. Bez slučování má každý spoj vlastní řádek s číslem spoje a linky."
        s f = form.AddDecimal("PripojStyle.FontSize", "Velikost", "", 4, 200, "pt")
        s f.helpText = "Prázdné = jako datové buňky"
        d form.AddBool("PripojStyle.Bold", "Tučné", 0)
        d form.AddBool("PripojStyle.Italic", "Kurzíva", 0)
        s f = form.AddFieldToLastGroup("PripojStyle.TextColor", "Barva textu", $$$FieldTypeColor, 0, 0)

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form, 1, 1, 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 190)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

        // Kolekce zdrojů dat + řádkové podmínky (element-specifické, do skupiny "Podmínky" výše)
        s f = form.AddText("ZastavkyDataSource", "Kolekce zastávek", "ZASTAVKY")
        s f = form.AddText("SpojeDataSource", "Kolekce spojů", "SPOJE")
        s f = form.AddText("SkupinyDnuDataSource", "Kolekce skupin dnů", "SKUPINY_DNU")
        s f = form.AddText("RowVisibilityExpression", "Viditelnost řádku")
        s f.placeholders = 1
        s f.helpText = "Zastávka, kde výraz neplatí, se nezobrazí (i s přípojnými řádky). Pole řádku: POPIS, TC, ZONA, MHD, TYP, PORADI, NASTUPISTE. Např. {ZONA} == ""1"""
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
