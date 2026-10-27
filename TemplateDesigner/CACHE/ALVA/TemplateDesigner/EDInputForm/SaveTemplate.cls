Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro SaveToCacheDialog v TemplateDesigneru.
/// Definuje formulář pro pojmenování šablony před uložením do Caché:
/// - name (Text, Required)
/// - agendaCode (List, jen ke čtení) - jediná položka = agenda šablony; změna jen přes Změnit agendu
///
/// Save metoda je no-op - vlastní Caché uložení šablony zajišťuje
/// `ALVA.TemplateDesigner.API.Sablony.SaveTemplate` (volaná .NET vrstvou přes
/// `CacheTemplateService.SaveAsync` po úspěšném dialogu). Tato třída zajišťuje
/// pouze definici a validaci formuláře.
///
/// IN["name"] / IN["agendaCode"] - initial values (povinné), používá je Init aby
/// předvyplnil formulář. IN["agendaLabel"] = zobrazovaný název agendy (obecná agenda v registru
/// není); bez něj se název dohledá v registru.
Class ALVA.TemplateDesigner.EDInputForm.SaveTemplate Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s initialName  = $g(^||ML("IN","name"))
        s initialAgenda = $g(^||ML("IN","agendaCode"))
        s agendaLabel = $g(^||ML("IN","agendaLabel"))

        // Buduj form
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
        d form.AddGroup("template", "Šablona")

        d form.AddText("name", "Název šablony", initialName, 1)

        // Agenda se jen ukáže (`B194a` C10): změna tady uložila šablonu s prvky a poli jedné agendy pod
        // jinou, i nezveřejněnou. Měnit ji smí jen Změnit agendu, které založí novou šablonu.
        s f = form.AddFieldToLastGroup("agendaCode", "Agenda", $$$FieldTypeList, 0, 0)
        i agendaLabel = "" s agendaLabel = ..NazevAgendy(.sys, initialAgenda)
        d f.AddListItem(initialAgenda, agendaLabel)
        s f.value = initialAgenda
        s f.editable = 0
        s f.helpText = "Agendu změníte příkazem Změnit agendu (Ctrl+Shift+A)."

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

        // Custom server-side validace (kromě Required co se řeší client-side):
        // pokud bychom chtěli např. blokovat duplicate name v Caché, zde.

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Save je no-op - vlastní save provede caller přes CacheTemplateService.SaveAsync
/// (předá Template JSON do `API.Sablony.SaveTemplate`).
ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

/// Nested call do AgendyRegistr.ListAgendy - saves/clears OUT (mirror Instance.cls pattern).
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

/// Název agendy z registru (pro klienta, který název neposlal); neznámá agenda = její kód.
ClassMethod NazevAgendy(ByRef sys As %String, code As %String) As %String
{
    s agendyJson = ..FetchAgendyJson(.sys), nazev = code
    q:agendyJson="" nazev
    s it = ##class(%DynamicArray).%FromJSON(agendyJson).%GetIterator()
    while it.%GetNext(.k, .entry) {
        i $isobject(entry) && (entry.%Get("code") = code) { s nazev = entry.%Get("label") q }
    }
    q nazev
}

}
