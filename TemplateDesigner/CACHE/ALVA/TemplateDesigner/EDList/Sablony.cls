Include GENERAL

/// Seznam tiskových šablon uložených v TemplateDesigneru — metadata všech šablon
/// per aktuální IDFa (firmu).
///
/// Šablona = uložený layout tiskové sestavy (zastávkový JŘ MHD, faktura, ...) s odkazem
/// na agendu odkud čerpá data. Sloupce seznamu: název, agenda, datum změny, autor.
///
/// Data:  ^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Metadata", ...)
/// CRUD:  ALVA.TemplateDesigner.API.Sablony (tato třída pouze čte)
/// Konzument: CacheTemplatePickerDialog v TD (.NET)
Class ALVA.TemplateDesigner.EDList.Sablony Extends %RegisteredObject
{

/// Vrátí definici sloupců pro EDList.
ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s list = ##class(ALVA.EDControls.EDList.List).%New()
        d list.AddColumn("name",       "Název",        "TEXT",     190, "", 1)
        d list.AddColumn("zdroj",      "Zdroj",        "TEXT",      70, "", 1)
        d list.AddColumn("agenda",     "Agenda",       "TEXT",     110, "", 1)
        d list.AddColumn("varianta",   "Varianta",     "TEXT",      60, "", 1)
        d list.AddColumn("modifiedAt", "Změněno",      "DATETIME", 130, "", 1, "dd.MM.yyyy HH:mm")
        d list.AddColumn("modifiedBy", "Kým",          "TEXT",      70, "", 1)
        d list.AddColumn("author",     "Autor",        "TEXT",      70, "", 1)
        d list.WriteColumnsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Vrátí řádky šablon. Volitelný filter (sub-subscript pod "filter"):
///   IN["filter","agendaCode"] = "MHD" | "FAKTURY" | "GENERAL" | "" (všechny)
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s filterAgenda = $g(^||ML("IN","filter","agendaCode"))
        s list = ##class(ALVA.EDControls.EDList.List).%New()

        // Systémové (MLINE) první — sdílené šablony od M-line, na klientovi read-only.
        // Nezdvojovat, když session firma sama je MLINE (autorování systémových).
        if IdFa '= "MLINE" d ..AddBranchRows(list, "MLINE", filterAgenda, 1)
        // Vlastní (firma)
        d ..AddBranchRows(list, IdFa, filterAgenda, 0)

        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Přidá řádky jedné větve (firma nebo MLINE), s volitelným filtrem agendy.
ClassMethod AddBranchRows(list As ALVA.EDControls.EDList.List, sourceIdFa As %String, filterAgenda As %String, isSystem As %Boolean)
{
    if filterAgenda '= "" {
        s templateId = ""
        for {
            s templateId = $o(^ALVA.TemplateDesigner.Templates(sourceIdFa,"IDX","ByAgenda",filterAgenda,templateId))
            q:templateId=""
            d ..AddTemplateRow(list, sourceIdFa, templateId, isSystem)
        }
    } else {
        s templateId = ""
        for {
            s templateId = $o(^ALVA.TemplateDesigner.Templates(sourceIdFa,"DTA",templateId))
            q:templateId=""
            d ..AddTemplateRow(list, sourceIdFa, templateId, isSystem)
        }
    }
}

/// Sestaví jeden řádek z metadat šablony. isSystem=1 → systémová (MLINE) větev.
ClassMethod AddTemplateRow(list As ALVA.EDControls.EDList.List, IdFa As %String, templateId As %String, isSystem As %Boolean = 0)
{
    if '$d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId)) q
    s base = $name(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata"))

    s row = list.AddRow(templateId)
    s row.name       = $g(@base@("Name"))
    d ..ResolveAgendaVariant($g(@base@("AgendaCode")), .agendaLabel, .variantaText)
    s row.agenda     = agendaLabel
    s row.varianta   = variantaText
    s row.modifiedAt = $g(@base@("ModifiedAt"))
    s row.modifiedBy = $g(@base@("ModifiedBy"))
    s row.author     = $g(@base@("Author"))
    s row.zdroj      = $s(isSystem:"M-line", 1:"vlastní")
}

/// Rozloží kombinovaný kód (metadata AgendaCode) na zobrazovací dvojici Agenda + Varianta
/// dle parametrů agenda třídy (dvouosý model). Cache per request v ^||TDSablonyAgendy —
/// GetClassForCode dělá SQL scan, per-řádkové volání by bylo zbytečně drahé.
/// Neznámý kód (třída neexistuje) → agenda = syrový kód, varianta prázdná; GENERAL → "Obecná".
ClassMethod ResolveAgendaVariant(code As %String, ByRef agendaLabel As %String, ByRef variantaText As %String)
{
    s agendaLabel = code, variantaText = ""
    q:code=""
    if code = "GENERAL" { s agendaLabel = "Obecná"  q }

    if '$d(^||TDSablonyAgendy(code)) {
        s className = ##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(code)
        s ^||TDSablonyAgendy(code) = $s(className'="":$lb($parameter(className,"AgendaLabel"), $parameter(className,"Variant")), 1:$lb("",""))
    }
    s cached = ^||TDSablonyAgendy(code)
    s:$lg(cached,1)'="" agendaLabel = $lg(cached,1)
    s variantaText = $lg(cached,2)
}

}
