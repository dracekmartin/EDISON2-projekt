Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro SaveToCacheDialog v TemplateDesigneru.
/// Definuje formulář pro pojmenování šablony před uložením do Caché:
/// - name (Text, Required)
/// - agendaCode (List, Required) — položky načteny z ALVA.TemplateDesigner.API.AgendyRegistr
///
/// Save metoda je no-op — vlastní Caché uložení šablony zajišťuje
/// `ALVA.TemplateDesigner.API.Sablony.SaveTemplate` (volaná .NET vrstvou přes
/// `CacheTemplateService.SaveAsync` po úspěšném dialogu). Tato třída zajišťuje
/// pouze definici a validaci formuláře.
///
/// IN["name"] / IN["agendaCode"] — initial values (povinné), používá je Init aby
/// předvyplnil formulář.
Class ALVA.TemplateDesigner.EDInputForm.SaveTemplate Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s initialName  = $g(^||ML("IN","name"))
        s initialAgenda = $g(^||ML("IN","agendaCode"))

        // Načti agendy přes nested call do AgendyRegistr (mirror Instance.cls pattern)
        s agendyJson = ..FetchAgendyJson(.sys)

        // Buduj form
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
        d form.AddGroup("template", "Šablona")

        s f = form.AddFieldToLastGroup("name", "Název šablony", $$$FieldTypeText, 0, 1)
        s f.value = initialName

        s f = form.AddFieldToLastGroup("agendaCode", "Agenda", $$$FieldTypeList, 0, 1)
        d ..AddAgendyItems(f, agendyJson, initialAgenda)

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Validate(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

        // Custom server-side validace (kromě Required co se řeší client-side):
        // pokud bychom chtěli např. blokovat duplicate name v Caché, zde.

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Save je no-op — vlastní save provede caller přes CacheTemplateService.SaveAsync
/// (předá Template JSON do `API.Sablony.SaveTemplate`).
ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

/// Nested call do AgendyRegistr.ListAgendy — saves/clears OUT (mirror Instance.cls pattern).
/// Vrací JSON string s polem agend.
ClassMethod FetchAgendyJson(ByRef sys As %String) As %String
{
    // Cleanup OUT před nested call
    s key = ""
    for { s key = $o(^||ML("OUT",key)) q:key=""
          if key '= "ERR" k ^||ML("OUT",key) }

    d ##class(ALVA.TemplateDesigner.API.AgendyRegistr).ListAgendy(.sys)
    s json = $g(^||ML("OUT","1"))

    // Cleanup OUT po nested call (caller naplní vlastní form data)
    s key = ""
    for { s key = $o(^||ML("OUT",key)) q:key=""
          if key '= "ERR" k ^||ML("OUT",key) }

    q json
}

/// Parsuje JSON pole agend a přidá je jako ListItem do field.
ClassMethod AddAgendyItems(field As ALVA.EDControls.EDInputForm.Field, agendyJson As %String, selectedCode As %String)
{
    q:agendyJson=""
    s arr = ##class(%DynamicArray).%FromJSON(agendyJson)
    s i = 0
    for {
        s entry = arr.%Get(i)
        q:'$isobject(entry)
        s code = entry.%Get("code")
        s label = entry.%Get("label")
        d field.AddListItem(code, label)
        s i = i + 1
    }
    // Pokud caller předal initial code, předvyplň hodnotu
    s:selectedCode'="" field.value = selectedCode
}

}
