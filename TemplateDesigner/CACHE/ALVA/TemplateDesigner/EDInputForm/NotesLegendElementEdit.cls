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
        // Zapínadlo záhlaví sedí v Obecném jako u zastávek; jeho vzhled je ve skupině Záhlaví.
        // Klíč HeaderBar, ne ShowHeader: ten u zastávek a tabulek znamená záhlaví SLOUPCŮ, které
        // legenda nemá. Stejný klíč se dvěma popisky hlídá drift test.
        s f = form.AddBool("ShowHeaderBar", "Záhlaví", 1)
        s f.helpText = "Vypnuté záhlaví se nekreslí ani s podbarvením a nezabírá výšku - poznámky pak začínají u horní hrany prvku."

        // ── Skupina "Záhlaví" ─────────────────────────────────────────────
        d form.AddGroup("header", "Záhlaví")
        d form.AddText("HeaderText", "Text záhlaví", "Poznámky")
        // Pozadí BEZ výchozí hodnoty - podbarvení si prvek nese sám, takže pole má ukázat jeho
        // hodnotu. Výchozí barva by se vracela do vymazaného pole a vypadalo by to, že vymazání
        // neplatí (nález Pavel 1. 9.). Prázdno = bez podbarvení.
        s f = form.AddFieldToLastGroup("HeaderStyle.BackgroundColor", "Pozadí záhlaví", $$$FieldTypeColor, 0, 0)
        s f = form.AddFieldToLastGroup("HeaderStyle.TextColor", "Barva textu záhlaví", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        // ComboBox (editovatelný) - klient naplní nainstalovanými fonty (PopulateFontFields, klíč končí "FontFamily")
        d form.AddFieldToLastGroup("HeaderStyle.FontFamily", "Font", $$$FieldTypeComboBox, 0, 0)
        d form.AddFieldToLastGroup("HeaderStyle.FontWeight", "Řez", $$$FieldTypeList, 0, 0)
        // Velikost záhlaví je samostatná; poznámky mají svou ve skupině Rozložení. Prázdno = 9 bodů,
        // což kreslení používalo i dřív - do 16. 9. jen nebylo kde to změnit.
        s f = form.AddDecimal("HeaderStyle.FontSize", "Velikost", "", 3, 24, "pt", "0.#")
        s f.helpText = "Velikost písma v záhlaví prvku. Prázdné = 9 bodů. Výška pruhu záhlaví se počítá z ní."

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

        // ── Skupina "Rozložení" (velikost / sloupce / řazení / zarovnání markeru) ────
        // Velikost a výška řádku patří sem, ne mezi barvy buněk - uživatel je hledá spolu s počtem
        // sloupců (nález Pavel 1. 9.). Popisky i skupina musí souhlasit s C# descriptory (drift test).
        d form.AddGroup("layout", "Rozložení")
        // Linkový JŘ: pořadí, tisk a sloupce po druzích poznámek (#7487) v okně návrháře. Sedí tady,
        // ne v obsahu, aby bylo rozložení na jednom místě, a první, protože je ze skupiny nejdůležitější
        // (rozhodnutí Jan 9. 10.). Návrhář ho ukazuje jen u linkového - druh posílá jen linková agenda.
        s f = form.AddFieldToLastGroup("__skupinyPoznamek", "Druhy poznámek", $$$FieldTypeButton, 0, 0)
        s f.value = "Upravit…"
        s f.helpText = "Pořadí druhů poznámek, které se tisknou, a počet sloupců každého druhu. Každý druh začíná na novém řádku."
        s f = form.AddDecimal("NoteFontSize", "Velikost poznámek", 7, 4, 20, "pt", "0.#")
        s f.helpText = "Velikost písma poznámek. Nevejdou-li se poznámky do prvku, písmo se zmenší a aplikace to oznámí ve stavovém řádku."
        s f = form.AddDecimal("NoteLineHeight", "Výška řádku", "", 2, 15, "mm")
        s f.helpText = "Prázdné = podle velikosti písma. Vlastní hodnotu zadejte, jen když chcete poznámky vzdušnější nebo stlačenější."
        d form.AddNumber("Columns", "Počet sloupců", 1, 1, 4)
        s f = form.AddFieldToLastGroup("WrapSmer", "Směr plnění", $$$FieldTypeList, 0, 0)
        d f.AddListItem("PoSloupcich", "Po sloupcích"), f.AddListItem("PoRadcich", "Po řádcích")
        s f.value = "PoSloupcich"
        s f = form.AddFieldToLastGroup("Razeni", "Řazení poznámek", $$$FieldTypeList, 0, 0)
        // Volby 1:1 s panelem (DleTypu je default elementu - dřív v dialogu úplně chyběla)
        d f.AddListItem("DleTypu", "Dle typu (dny platnosti → značky → atributy → text)"), f.AddListItem("Vychozi", "Výchozí (dle dat)")
        d f.AddListItem("DleDelkyVzestupne", "Délka vzestupně"), f.AddListItem("DleDelkySestupne", "Délka sestupně")
        s f.value = "DleTypu"
        d form.AddDecimal("MarkerSirka", "Šířka buňky značky (0=vedle)", 0, 0, 30, "mm")

        // ── Co blok tiskne ────────────────────────────────────────────────
        // Skupina „Filtrování" míchala tři různé věci a měla 11 položek (rozpočet ~7): které
        // poznámky blok bere, co si bere z poznámek zadaných u linky, a značky u odjezdů (ty
        // uživatelé hledají nejčastěji, protože do července byly parametrem tisku). Klíče polí
        // se NEMĚNÍ - rozpad je jen vizuální.
        // U linkového JŘ návrhář tuhle i obě další skupiny schová celé: přepínače typů nahrazuje
        // okno Druhy poznámek a kategorie poznámek linkový neposílá.
        d form.AddGroup("filter", "Obsah poznámek")
        d form.AddBool("ShowNegativniZnacky", "Negativní značky", 1)
        d form.AddBool("ShowSpecialniZnaky", "Speciální znaky", 1)
        d form.AddBool("ShowTextovePoznamky", "Textové poznámky", 1)

        // ── Poznámky zadané u linky ───────────────────────────────────────
        d form.AddGroup("filterline", "Poznámky linky")
        s f = form.AddFieldToLastGroup("Vyluky", "Poznámky linky - výluky", $$$FieldTypeList, 0, 1)
        d f.AddListItem("Vcetne", "Včetně"), f.AddListItem("Bez", "Bez výluk"), f.AddListItem("Jen", "Jen výluky")
        s f.helpText = "Poznámky linky, které začínají ""Výluka"", lze mít v samostatném bloku: v jednom bloku zvolte Bez výluk a ve druhém Jen výluky."
        // Kategorie poznámek zadaných u linky - klient je chce rozložit do víc bloků, protože mají jiné
        // rozložení sloupců než seznam značek (dlouhé odstavce vs. „značka + text").
        s f = form.AddBool("ShowPoznamkyLinkyTarifni", "Poznámky linky - tarifní", 0)
        s f.helpText = "Které poznámky zadané u linky blok bere. Vypnutím se schová jen tato kategorie - volba Textové poznámky výše vypne i ruční komentáře a rozdíly tras."
        d form.AddBool("ShowPoznamkyLinkyVseobecne", "Poznámky linky - všeobecné", 0)
        d form.AddBool("ShowPoznamkyLinkyRadiobus", "Poznámky linky - radiobus", 0)

        // ── Značky u odjezdů ──────────────────────────────────────────────
        // Do července to byly parametry tisku, takže tady je lidi hledají. Každý přepínač zapíná
        // značku u minuty I její vysvětlivku - nemůže vzniknout značka, kterou legenda nevysvětlí.
        d form.AddGroup("filtermarks", "Značky u odjezdů")
        s fBez = form.AddBool("ShowSouhrnBezbarier", "Souhrn bezbariérových spojů", 1)
        s fBez.helpText = "Souhrnná vysvětlivka ""všechny spoje mají bezbariérově přístupné vozidlo"" místo značky u každého odjezdu."
        s fKola = form.AddBool("ShowZnackaKola", "Přeprava kol u odjezdů", 0)
        s fKola.helpText = "U spojů, které přepravují jízdní kola, se za časem odjezdu vytiskne symbol kola a do legendy přijde vysvětlivka. Zapíná obojí naráz, aby nevznikla značka bez vysvětlení."
        s fPokr = form.AddBool("ShowPokracujiciSpoje", "Pokračující spoje", 0)
        s fPokr.helpText = "Spoje, které pokračují dál po jiné lince, dostanou u odjezdu značku ""u1"", ""u2""… a v legendě vysvětlivku ""pokračuje do zast. … (l. 304)"". Odjezdy se stejným cílem po stejné lince mají tutéž značku."
        s fMist = form.AddBool("ShowZnackaMistenky", "Místenky u odjezdů", 0)
        s fMist.helpText = "U spojů, ke kterým lze zakoupit místenku, se za časem odjezdu vytiskne ""R"" a do legendy přijde vysvětlivka."

        // ── Skupina "Negativní značky" ────────────────────────────────────
        d form.AddGroup("nz", "Negativní značky")
        s f = form.AddFieldToLastGroup("NegativniZnackaBackColor", "Pozadí značky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF000000"
        s f = form.AddFieldToLastGroup("NegativniZnackaForeColor", "Barva textu značky", $$$FieldTypeColor, 0, 0)
        s f.value = "#FFFFFFFF"

        // ── Skupina "Porovnání verzí" (B192b) ─────────────────────────────
        // Jen rozdílový linkový JŘ: legenda nese poznámky obou verzí linky. Data říkají verzi,
        // barvu určuje šablona. Návrhář skupinu jinde schová.
        d form.AddGroup("verze", "Porovnání verzí")
        s f = form.AddFieldToLastGroup("RozdilPuvodniColor", "Původní verze", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF960000"
        s f.helpText = "Barva značky poznámky, která je jen v původní verzi linky. Její text se přeškrtne."
        s f = form.AddFieldToLastGroup("RozdilNovaColor", "Nová verze", $$$FieldTypeColor, 0, 0)
        s f.value = "#FF009600"
        s f.helpText = "Barva značky i textu poznámky, která je jen v nové verzi linky."

        // ── Base pole ─────────────────────────────────────────────────────
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddBorder(form)
        d ##class(ALVA.TemplateDesigner.EDInputForm.BaseElementFields).AddGrid(form, 0)
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
