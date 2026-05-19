Include GENERAL

/// EDList backing pro CacheTemplatePickerDialog v TemplateDesigneru.
/// Vrací sloupce + řádky uložených šablon iterací nad ^ALVA.TemplateDesigner.Templates.
///
/// Storage je v třídě ALVA.TemplateDesigner.API.Sablony (CRUD); tato třída ho jen čte.
Class ALVA.TemplateDesigner.EDList.Sablony Extends %RegisteredObject
{

/// Vrátí definici sloupců pro EDList.
ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s list = ##class(ALVA.EDControls.EDList.List).%New()
        d list.AddColumn("name",       "Název",        "TEXT",     200, "", 1)
        d list.AddColumn("agenda",     "Agenda",       "TEXT",      90, "", 1)
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

        // Pokud je filter, použij sekundární index, jinak iteruj všechny TemplateID.
        if filterAgenda '= "" {
            s templateId = ""
            for {
                s templateId = $o(^ALVA.TemplateDesigner.Templates(IdFa,"IDX","ByAgenda",filterAgenda,templateId))
                q:templateId=""
                d ..AddTemplateRow(list, IdFa, templateId)
            }
        } else {
            s templateId = ""
            for {
                s templateId = $o(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId))
                q:templateId=""
                d ..AddTemplateRow(list, IdFa, templateId)
            }
        }

        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Sestaví jeden řádek z metadat šablony.
ClassMethod AddTemplateRow(list As ALVA.EDControls.EDList.List, IdFa As %String, templateId As %String)
{
    if '$d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId)) q
    s base = $name(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata"))

    s row = list.AddRow(templateId)
    s row.name       = $g(@base@("Name"))
    s row.agenda     = $g(@base@("AgendaCode"))
    s row.modifiedAt = $g(@base@("ModifiedAt"))
    s row.modifiedBy = $g(@base@("ModifiedBy"))
    s row.author     = $g(@base@("Author"))
}

}
