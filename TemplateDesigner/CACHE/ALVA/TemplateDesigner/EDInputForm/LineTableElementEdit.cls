Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci LineTableElement properties (knižní JŘ -
/// matice zastávky × spoje).
///
/// Field key = property path na C# LineTableElement (vč. nested HeaderStyle.X / DataStyle.X).
/// Init jen definuje strukturu + defaults; generický ElementEditDialog dělá pre-fill přes reflexi.
///
/// Kostra jako u ostatních tabulek: nahoře skupiny jen tohoto prvku, pod nimi Záhlaví, Datové
/// buňky, Rámeček prvku, Mřížka, Pozice, Podmínky. Per-sloupcové styly jsou v okně prvku (Sloupce).
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

        // Nahoře skupiny, které má jen tento prvek, pod nimi společný konec všech tabulek
        // (Záhlaví, Datové buňky, Rámeček prvku, Mřížka, Pozice a velikost, Podmínky).

        // ── Skupina "Řádky nad časy" ──────────────────────────────────────
        // Řádky v hlavičce sloupců spojů mezi čísly spojů a časy. Samotné „Řádky" neříkalo, o jaké jde,
        // a hledání ho pletlo s Přípojnými řádky (`B194h` H10). Záhlaví skupin dnů a čísla spojů jsou
        // v Záhlaví, přípoje ve svých řádcích.
        d form.AddGroup("rows", "Řádky nad časy")
        s f = form.AddBool("ShowPlatnostRow", "Řádek platnosti", 1)
        s f.helpText = "Nad časy spojů se vytiskne řádek se značkami spoje: dny platnosti (X, 6, 7, +), místenka a atributy spoje (bezbariérový, kola…)."
        s f = form.AddBool("ShowNzRow", "Řádek negativních značek", 1)
        s f.helpText = "Nad časy spojů se vytiskne řádek s čísly negativních značek spoje."
        s f = form.AddBool("TurnusyKeSpojum", "Řádky turnusů", 0)
        s f.helpText = "Pod čísly spojů se vytisknou řádky s označením turnusu. Je to provozní údaj dopravce; vypnuté = turnusy se ani nenačítají."
        s f = form.AddDecimal("PomerZnacek", "Poměr velikosti značek", 1.5, 1, 3, "×")
        s f.helpText = "Kolikrát větší je písmo značek platnosti a negativních značek než písmo datové buňky. Starý tisk je má jedenapůlkrát větší, aby byly na výtisku čitelné. Řádek značek se tím zvýší."

        // ── Skupina "Časy" ────────────────────────────────────────────────
        // Co se tiskne v buňkách spojů. Do 28. 10. ve skupině Datové buňky, která má mít jen vzhled.
        d form.AddGroup("times", "Časy")
        s f = form.AddFieldToLastGroup("FormatCasu", "Formát času", $$$FieldTypeList, 0, 1)
        d f.AddListItem("HodinaVetsi", "Minuty menším písmem (hh mm)")
        d f.AddListItem("SDvojteckou", "Celý čas stejně (hh:mm)")
        d f.AddListItem("STeckou", "Celý čas stejně (hh.mm)")
        s f.value = "HodinaVetsi"
        s f.helpText = "Starý tisk sází hodinu větším písmem a minutu menším, oddělené mezerou bez dvojtečky. Ostatní volby vypíší celý čas jednou velikostí: s dvojtečkou (04:20), nebo s tečkou a hodinou bez nuly (4.20)."
        s f = form.AddDecimal("PomerMinuty", "Poměr velikosti minut", 0.75, 0.3, 1, "×")
        s f.helpText = "Podíl velikosti minut vůči hodině. 1 = obojí stejně velké. Uplatní se jen u formátu s hodinou větším písmem."
        s f = form.AddBool("VyplnitPrazdneBunkySpoju", "Tečky v prázdných buňkách", 0)
        s f.helpText = "Buňka spoje, ve které není čas, se vyplní tečkami - tabulka pak drží svislé vodicí pruhy jako starý tisk. U linky, kde většina spojů jede jen část trasy, se tím tabulka zaplní a čte se hůř."

        // ── Skupina "Popisné sloupce" ──────────────────────────────────────
        // Zóna, km, Tč, zastávka a MHD. Viditelnost, šířky a pořadí jednotlivých sloupců se
        // editují v tabu "Sloupce"; sem patří jen volby platné pro celou skupinu.
        d form.AddGroup("columns", "Popisné sloupce")
        s f = form.AddBool("RozdelitZonyDoSloupcu", "Každá zóna ve vlastním sloupci", 0)
        s f.helpText = "Zastávka na hranici zón má víc zón. Vypnuté = všechny v jednom sloupci za sebou, zapnuté = každá ve vlastním sloupci vedle sebe (jako kilometry). Sloupec zón se tím rozšíří o tolik dílů, kolik zón má nejbohatší zastávka."
        s f = form.AddBool("JednoZahlaviKm", "Jedno záhlaví nad km sloupci", 0)
        s f.helpText = "Vypnuté = nad každým km sloupcem je vlastní záhlaví ""km"", zapnuté = jedno společné přes všechny. Kilometry zůstávají v samostatných sloupcích."
        s f = form.AddNumber("MaxKmSloupcu", "Nejvýš km sloupců", "", 1, 20)
        s f.helpText = "Kolik km sloupců se nejvýš vytiskne. Prázdné = všechny. Vezmou se první v pořadí, ve kterém je linka má, ostatní se netisknou - jejich spoje pak v tabulce kilometry nemají. Zúží to tabulku za cenu té informace."
        // Jedno záhlaví nad celou skupinou a jeden slitý blok bez vnitřních čar. Změřeno na vzoru IDZK 940.
        s f = form.AddText("PopisneSloupceNadpis", "Text záhlaví popisných sloupců")
        s f.helpText = "Text nad zónou, km, Tč, zastávkou a MHD - všechny mají jedno společné záhlaví. Prázdné = bez záhlaví."
        s f = form.AddBool("SloucitPopisyRozdvojene", "Sloučit popis u příjezdu a odjezdu", 0)
        s f.helpText = "Zastávka, u které se tiskne příjezd i odjezd, zabírá dva řádky. Vypnuté = km, tarifní číslo, název i MHD se na obou zopakují (tvar windowsového tisku). Zapnuté = vypíšou se jednou přes oba řádky na střed a mezi časy je vodorovná úsečka (tvar postskriptového tisku). Zóna se opakuje vždy."
        s f = form.AddBool("PopisneSloupceBezVnitrnichCar", "Popisné sloupce bez vnitřních čar", 0)
        s f.helpText = "Popisné sloupce se kreslí jako jeden slitý blok. Svislé čáry mezi sloupci spojů zůstanou."

        // ── Skupina "Zastávky" ────────────────────────────────────────────
        // Jak se tiskne název zastávky. Do 28. 10. ve skupině Datové buňky.
        d form.AddGroup("stops", "Zastávky")
        s f = form.AddBool("TucneZastavkyDleTrasy", "Tučné zastávky podle trasy", 1)
        s f.helpText = "V trase linky jde u zastávky zadat atribut ""tučně"" - zapnuté = návrhář ho respektuje, stejně jako to dělá starý tisk. Vypnuté = všechny zastávky stejným písmem."
        s f = form.AddBool("CaryZastavekDleTrasy", "Čáry u zastávek podle trasy", 1)
        s f.helpText = "V trase linky jde u zastávky zadat ""podtrženo"" nebo ""podtrženo nad i pod"" - zapnuté = návrhář nakreslí čáru přes celou šířku tabulky, stejně jako starý tisk. Odděluje se tím úsek linky."
        s f = form.AddBool("TeckyZaNazvemZastavky", "Tečky za názvem zastávky", 0)
        s f.helpText = "Za název zastávky se doplní tečky k pravému okraji sloupce, aby oko dojelo k času. Končí před piktogramem přestupu."

        // ── Skupina "Přípojné řádky" (#8360) ──────────────────────────────
        // Obsah tabulky, proto před Šířkou sloupců (`ux §4`: obsah, rozložení, zvláštní stavy; `B194h` H10).
        // Přípoj má jít odlišit od časů spojů - obvykle menším kurzívním písmem, aby v tabulce
        // „nekřičel" stejně jako odjezdy. Prázdné = dědí se z datových buněk. Přípojný řádek je
        // řádek buněk (popisek přes CellRenderer, časy po úsecích), proto celá sada Buňka.
        d form.AddGroup("pripoj", "Přípojné řádky")
        s f = form.AddBool("ShowPripojeRows", "Přípojné řádky", 1)
        s f.helpText = "Pod zastávkou se vytisknou řádky o přípojích (vyčká / navazuje)."
        s f = form.AddFieldToLastGroup("PripojeSlucovani", "Slučování přípojů", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Ne", "Řádek pro každý spoj")
        d f.AddListItem("PodleSmeru", "Podle směru")
        d f.AddListItem("PodleSmeruALinky", "Podle směru a linky")
        s f.helpText = "Sloučený řádek nese časy víc spojů a v popisku má směr (""ze směru Trhové Sviny""), takže šetří místo. Bez slučování má každý spoj vlastní řádek s číslem spoje a linky."
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "PripojStyle", "", "", "", "", 0)
        s f = form.GetField("PripojStyle.FontSize")
        s f.helpText = "Prázdné = jako datové buňky"
        // Pozadí celého řádku - s bílým písmem dá inverzní řádek, o který si KU HK řekl (#8360).
        s f = form.GetField("PripojStyle.BackgroundColor")
        s f.helpText = "Podbarví celý přípojný řádek. S bílým písmem dá inverzní řádek. Prázdné = jako datové buňky."

        // ── Skupina "Šířka sloupců" ───────────────────────────────────────
        // Šířka sloupce spoje je zadané přání a to, co se do sloupce musí vejít. Všechno, co do
        // toho vstupuje - i odstupy mezi značkami a časem - patří pod jeden nadpis; do 8. 9. to
        // bylo roztroušené mezi "Sloupce" a "Datové buňky" a nešlo přečíst jako jedna věc.
        // ⚠️ Ne "Šířka a dělení" - "dělení sloupců" už v UI znamená členění na skupiny dnů.
        d form.AddGroup("width", "Šířka sloupců")
        s f = form.AddDecimal("MezeraMeziZnackamiMm", "Mezera mezi značkami", "", 0, 10, "mm")
        s f.helpText = "Odstup mezi značkami nad časem (například kladívko a vozík vedle sebe). Prázdné = mezera podle velikosti písma. Dvojice značek bývá ve sloupci to nejširší, takže utažením téhle mezery jde sloupec zúžit až na šířku času."
        s f = form.AddDecimal("MezeraZnackaCasMm", "Mezera mezi značkou a časem", "", 0, 10, "mm")
        s f.helpText = "Odstup značky, která stojí v buňce vlevo od času (návaznost, jen nástup). Prázdné = mezera podle velikosti písma."
        s f = form.AddDecimal("MezeraHodinaMinutaMm", "Mezera mezi hodinou a minutou", "", 0, 10, "mm")
        s f.helpText = "Odstup mezi hodinou a menší minutou. Prázdné = mezera podle velikosti písma. Uplatní se jen u formátu s hodinou větším písmem a zužuje i sloupec, protože je uvnitř času."
        s f = form.AddBool("PadEmptyColumns", "Dorovnat prázdnými sloupci", 0)
        s f.helpText = "Co se stane s volným místem vpravo: zapnuté = rozkreslí se prázdnými sloupci, takže tabulka končí rovně, vypnuté = zůstane bílé. Týká se posledního nedopsaného bloku i tabulky, které místo zbylo kvůli největší šířce."
        s f = form.AddBool("JednotnaSirkaSpoju", "Stejně široké sloupce spojů", 0)
        s f.helpText = "Vypnuté = každý sloupec je tak široký, jak ho potřebuje jeho obsah, takže ten se značkami je širší než ostatní (tak to dělá starý tisk) a tabulka působí roztřepeně; do řádku se ale vejde víc spojů. Zapnuté = všechny dostanou šířku toho nejširšího, tabulka je pravidelná a zabere víc místa."
        s f = form.AddDecimal("MezeraMeziSkupinamiMm", "Mezera mezi skupinami dnů", 0, 0, 20, "mm")
        s f.helpText = "Volné místo mezi skupinami dnů a také za popisnými sloupci. 0 = souvislá mřížka bez mezer. Mezera se bere ze šířky prvku, takže se do řádku vejde méně spojů."

        // ── Skupina "Porovnání verzí" (B192) ──────────────────────────────
        // Jen rozdílový linkový JŘ: změněný spoj stojí dvakrát vedle sebe a odlišné časy se podbarví.
        // Data říkají, která buňka se liší, barvu určuje šablona. Návrhář skupinu jinde schová.
        d form.AddGroup("verze", "Porovnání verzí")
        // Barvy mají v popisku „Barva", jinak je hledání „barva" nenajde (ux-conventions §8).
        s f = form.AddFieldToLastGroup("RozdilPuvodniBackColor", "Původní verze: Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFF08080"
        s f.helpText = "Podbarvení časů původní verze spoje, které se v nové verzi změnily."
        s f = form.AddFieldToLastGroup("RozdilNovaBackColor", "Nová verze: Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF92D36E"
        s f.helpText = "Podbarvení časů nové verze spoje, které se proti původní změnily, a všech časů nového spoje."
        s f = form.AddFieldToLastGroup("RozdilPuvodniZnackaColor", "Původní verze: Barva značek", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF960000"
        s f.helpText = "Barva značky, kterou spoj měl jen v původní verzi. Platí pro spoj, kterému se změnily jen značky - ten zůstává v jednom sloupci."
        s f = form.AddFieldToLastGroup("RozdilNovaZnackaColor", "Nová verze: Barva značek", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF009600"
        s f.helpText = "Barva značky, kterou spoj má jen v nové verzi."

        // ── Záhlaví / Datové buňky ────────────────────────────────────────
        // Skupina části prvku: zapnutí, pole pro celý řádek, pod nimi sada stylu (rozhodnutí Jan 28. 10.).
        // Velikost a vnitřní okraje jsou u této tabulky rozměrové volby, ne kosmetika: nesou výšku řádku
        // i šířku sloupce spoje, proto si nesou vlastní nápovědy.
        d form.AddGroup("header", "Záhlaví")
        d form.AddBool("ShowGroupHeaders", "Záhlaví skupin dnů", 1)
        d form.AddBool("ShowTripNumbers", "Čísla spojů", 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "HeaderStyle", "header", "Záhlaví", "#FF000000", "", 0, "", 1)
        s f = form.GetField("HeaderStyle.FontSize")
        s f.helpText = "Písmo záhlaví celého prvku, prázdné = 9 bodů. Jednotlivému sloupci se dá přebít v záložce Sloupce."

        d form.AddGroup("data", "Datové buňky")
        s f = form.AddDecimal("DataRowHeight", "Výška řádku", "", 1, 50, "mm")
        s f.helpText = "Prázdné = podle velikosti písma."
        // Podbarvení řádku zastávky podle tarifního čísla - obdoba Střídání řádků u ostatních tabulek,
        // jen barva (prázdná = bez), jako Střídání. Výchozí hodnota schválně chybí: klient by ji ukázal
        // u vypnutého podbarvení (prázdná hodnota prvku) a vypadalo by to zapnuté.
        s f = form.AddFieldToLastGroup("OddTcBackColor", "Lichá Tč: Barva pozadí", $$$FieldTypeColor, 0, 0)
        s f.helpText = "Řádek zastávky s lichým tarifním číslem dostane tuto barvu místo Barvy pozadí. Řídí se tarifním číslem, ne pořadím řádku, takže příjezd a odjezd jedné zastávky mají barvu stejnou. Prázdné = bez podbarvení."
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, "DataStyle", "data", "Datové buňky", "#FF000000", "#00FFFFFF", 0)
        s f = form.GetField("DataStyle.FontSize")
        s f.helpText = "Písmo buněk celého prvku, prázdné = 8 bodů. Řídí i výšku řádku, když je prázdná, a šířku sloupce spoje."
        // ⚠️ Výchozí hodnota vypsaná, ne prázdné pole: „prázdné = 0,5 mm" nedávalo smysl nikomu,
        // kdo si chtěl sloupce utáhnout, protože z formuláře nebylo poznat, kolik vzduchu tam je
        // (nález Jan 22. 9.). Binder prázdnou hodnotu modelu nechá na tomhle defaultu.
        s f = form.GetField("DataStyle.PaddingLeft")
        s f.value = 0.5
        s f.helpText = "Odsazení textu od svislé čáry. Větší okraj rozšíří sloupec spoje, takže se do řádku vejde méně spojů."
        s f = form.GetField("DataStyle.PaddingRight")
        s f.value = 0.5
        s f.helpText = "Odsazení textu od svislé čáry. Je to hlavní páka na vzduch kolem času: větší okraj sloupec rozšíří, nulou se stáhne až k obsahu, ale nikdy tak, aby se číslice dotýkaly čar."

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form, 1, 1, 1)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddPosition(form, 190)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddConditions(form)

        // Řádkové podmínky (do skupiny "Podmínky" výše). Zdroje dat (ZastavkyDataSource / SpojeDataSource /
        // SkupinyDnuDataSource) formulář nenabízí: všechny agendy posílají ZASTAVKY / SPOJE / SKUPINY_DNU
        // a jiná hodnota tabulku jen vyprázdní; v šabloně vlastnost zůstává (`B194` E14).
        s f = form.AddText("RowVisibilityExpression", "Podmínka viditelnosti řádku")
        s f.placeholders = 1
        // Pole řádku doplní návrhář podle agendy šablony (ElementFormBinder.DoplnPoleRadku) - pevný seznam lhal.
        s f.helpText = "Zastávka, kde výraz neplatí, se nezobrazí (i s přípojnými řádky). Např. {NAZEV_ZASTAVKY} != 'Nádraží'."
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
