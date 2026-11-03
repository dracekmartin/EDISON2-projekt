Include GENERAL

/// Seznam tiskových šablon uložených v TemplateDesigneru - metadata všech šablon, které firma vidí
/// (vlastní a M-line, `API.Sablony.VetveFirmy`).
///
/// Šablona = uložený layout tiskové sestavy (zastávkový JŘ MHD, faktura, ...) s odkazem
/// na agendu odkud čerpá data. Sloupce seznamu: název, agenda, zdroj, šablona pro tisk, datum změny, autor.
///
/// Data:  ^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Metadata", ...)
/// CRUD:  ALVA.TemplateDesigner.API.Sablony (tato třída pouze čte)
/// Konzument: CacheTemplatePickerDialog v TD (.NET) - seznam vpravo od stromu `API.StromSablon`
Class ALVA.TemplateDesigner.EDList.Sablony Extends %RegisteredObject
{

/// Vrátí definici sloupců pro EDList.
ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        // Agenda + Varianta jako dvojice hned za názvem - pár drží pohromadě ve výchozím layoutu.
        s list = ##class(ALVA.EDControls.EDList.List).%New()
        // Ikona před názvem: šablona M-line má logo M-line (jen ke čtení, otevře se jako kopie), vlastní dokument -
        // jako na úvodní obrazovce, ať je odlišná šablona vidět na první pohled. Samostatný sloupec ikony
        // bez názvu vypadal divně (Jan 3. 11.), proto u názvu.
        s col = list.AddColumn("name", "Název",        "TEXT",     190, "", 1)
        // iconKey přišel s projektem ALVA_EDControls 3. 11. a ten jezdí s balíčkem shellu, ne TD: na serveru se starší
        // verzí by zápis spadl na <PROPERTY DOES NOT EXIST> a s ním celý seznam šablon. Bez něj je seznam jen bez ikon.
        try { s col.iconKey = "ikona" } catch { }
        d list.AddColumn("agenda",     "Agenda",       "TEXT",     110, "", 1)
        d list.AddColumn("varianta",   "Varianta",     "TEXT",      60, "", 1)
        d list.AddColumn("zdroj",      "Zdroj",        "TEXT",      70, "", 1)
        // Do vydání tisku z okna jízdních řádů skryté: uživatel ho nemá a informace o něm mátla (Jan 3. 11.).
        s col = list.AddColumn("proTisk", "Pro tisk",  "TEXT",      95, "", 1)
        s col.visible = 0
        d list.AddColumn("modifiedAt", "Změněno",      "DATETIME", 130, "", 1, "dd.MM.yyyy HH:mm")
        // Kdo šablonu založil a změnil, se eviduje, ale uživateli v přehledu nic neříká (Jan 3. 11.).
        s col = list.AddColumn("modifiedBy", "Kým",    "TEXT",      70, "", 1)
        s col.visible = 0
        s col = list.AddColumn("author",     "Autor",  "TEXT",      70, "", 1)
        s col.visible = 0
        // Skryté: návrhář podle nich zvýrazní šablonu, kterou tisk vezme, a nastaví výchozí šablonu agendy.
        s col = list.AddColumn("agendaCode", "Kód agendy", "TEXT")
        s col.visible = 0
        s col = list.AddColumn("vychozi", "Výchozí pro tisk", "TEXT")
        s col.visible = 0
        d list.WriteColumnsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Vrátí řádky šablon. Volitelný filtr (sub-subscript pod "filter"):
///   IN["filter","agendaCodes"] = kódy agend čárkou - uzel stromu šablon (oblast, agenda nebo varianta)
///   IN["filter","agendaCode"]  = jeden kód agendy (starší tvar); oba prázdné = všechny šablony
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s kody = $g(^||ML("IN","filter","agendaCodes"))
        s:kody="" kody = $g(^||ML("IN","filter","agendaCode"))
        s list = ##class(ALVA.EDControls.EDList.List).%New()

        s vetve = ##class(ALVA.TemplateDesigner.API.Sablony).VetveFirmy(IdFa)
        for iVetev = 1:1:$ll(vetve) {
            s vetev = $lg(vetve, iVetev)
            k ids
            if kody = "" {
                d ##class(ALVA.TemplateDesigner.API.Sablony).IdSablon(vetev, "", .ids)
            } else {
                for iKod = 1:1:$l(kody, ",") {
                    s kod = $p(kody, ",", iKod)
                    d:kod'="" ##class(ALVA.TemplateDesigner.API.Sablony).IdSablon(vetev, kod, .ids)
                }
            }
            // Šablony M-line jsou na klientovi jen ke čtení; firma MLINE (autorování) je má jako vlastní.
            s templateId = ""
            for {
                s templateId = $o(ids(templateId))
                q:templateId=""
                d ..AddTemplateRow(list, vetev, templateId, vetev'=IdFa, IdFa, .vychozi)
            }
        }

        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Sestaví jeden řádek z metadat šablony. isSystem=1 → systémová (MLINE) větev.
/// `vychozi(agendaCode)` = $lb(templateId, zdroj) šablony, kterou tisk agendy vezme; plní se jednou za agendu.
ClassMethod AddTemplateRow(list As ALVA.EDControls.EDList.List, vetev As %String, templateId As %String, isSystem As %Boolean, IdFa As %String, ByRef vychozi)
{
    if '$d(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId)) q
    s base = $name(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId,"Metadata"))
    s agendaCode = $g(@base@("AgendaCode"))

    s row = list.AddRow(templateId)
    s row.name       = $g(@base@("Name"))
    d ..ResolveAgendaVariant(agendaCode, .agendaLabel, .variantaText)
    s row.agenda     = agendaLabel
    s row.varianta   = variantaText
    s row.modifiedAt = $g(@base@("ModifiedAt"))
    s row.modifiedBy = $g(@base@("ModifiedBy"))
    s row.author     = $g(@base@("Author"))
    s row.zdroj      = $s(isSystem:"M-line", 1:"vlastní")
    s row.ikona      = $s(isSystem:"EDIcons.MlineLogo", 1:"EDIcons.Document")
    s row.agendaCode = agendaCode

    s:'$d(vychozi(agendaCode)) vychozi(agendaCode) = ..SablonaProTisk(IdFa, agendaCode)
    s zdroj = $s($lg(vychozi(agendaCode),1)=templateId:$lg(vychozi(agendaCode),2), 1:"")
    s row.vychozi = zdroj
    s row.proTisk = $case(zdroj, "firma":"výchozí firmy", "mline":"výchozí M-line", :"")
}

/// Šablona, kterou tisk agendy vezme bez volby volajícího, jako $lb(templateId, zdroj); agenda bez třídy
/// (Obecná, smazaná agenda) šablonu pro tisk nemá.
ClassMethod SablonaProTisk(IdFa As %String, agendaCode As %String) As %List
{
    q:##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(agendaCode)="" $lb("","")
    s sablona = ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaProTisk(IdFa, agendaCode)
    q $lb(sablona.templateId, sablona.zdroj)
}

/// Rozloží kombinovaný kód (metadata AgendaCode) na zobrazovací dvojici Agenda + Varianta
/// dle parametrů agenda třídy (dvouosý model). Cache per request v ^||TDSablonyAgendy -
/// GetClassForCode dělá SQL scan, per-řádkové volání by bylo zbytečně drahé.
/// Neznámý kód (třída neexistuje) → agenda = syrový kód, varianta prázdná; GENERAL → "Obecná (bez dat)"
/// jako v návrháři. Varianta je popisek („AD - porovnání verzí"), kód jen bez popisku.
ClassMethod ResolveAgendaVariant(code As %String, ByRef agendaLabel As %String, ByRef variantaText As %String)
{
    s agendaLabel = code, variantaText = ""
    q:code=""
    if code = "GENERAL" { s agendaLabel = "Obecná (bez dat)"  q }

    if '$d(^||TDSablonyAgendy(code)) {
        s className = ##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(code)
        s varianta = $s(className'="":$parameter(className,"VariantLabel"), 1:"")
        s:(varianta="")&&(className'="") varianta = $parameter(className,"Variant")
        s ^||TDSablonyAgendy(code) = $s(className'="":$lb($parameter(className,"AgendaLabel"), varianta), 1:$lb("",""))
    }
    s cached = ^||TDSablonyAgendy(code)
    s:$lg(cached,1)'="" agendaLabel = $lg(cached,1)
    s variantaText = $lg(cached,2)
}

}
