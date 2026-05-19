Include GENERAL

/// EDList backing pro InstancePickerDialog v TemplateDesigneru.
/// Vrací sloupce + řádky dostupných instancí pro danou agendu (např. linky pro MHD).
///
/// Routuje na agenda-specifickou metodu GetAvailableInstances přes $classmethod.
/// Wire formát: CVar named IN + JSON multi-slot OUT (standard Styl 3 viz DOCS/01-wire-formats.md).
Class ALVA.TemplateDesigner.API.InstancesList Extends %RegisteredObject
{

/// Vrátí definici sloupců pro EDList. Společné pro obě varianty.
ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s list = ##class(ALVA.EDControls.EDList.List).%New()
        d list.AddColumn("value",    "ID",     "TEXT",  80, "",  1)
        d list.AddColumn("display",  "Název",  "TEXT", 250, "",  1)
        d list.AddColumn("subtitle", "Detail", "TEXT", 220, "",  0)
        d list.WriteColumnsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Filter (sub-subscript pod "filter"):
///   IN["filter","agendaCode"] = kód agendy (např. "MHD")
/// OUT[1..N] = JSON objekt per řádek
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s agendaCode = $g(^||ML("IN","filter","agendaCode"))
        s instances = ..FetchInstances(.sys, agendaCode)

        s list = ##class(ALVA.EDControls.EDList.List).%New()
        s count = instances.%Size()
        for i=0:1:count-1 {
            s inst = instances.%Get(i)
            if '$isobject(inst) continue
            s value    = inst.%Get("value")
            s display  = inst.%Get("display")
            s subtitle = inst.%Get("subtitle")
            s row = list.AddRow(value)
            d row.%Set("value",    value)
            d row.%Set("display",  display)
            d row.%Set("subtitle", subtitle)
        }
        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Sdílený fetch — routuje na agenda-specifickou GetAvailableInstances přes $classmethod.
/// Vrací %DynamicArray objektů {value, display, subtitle}.
ClassMethod FetchInstances(ByRef sys As %String, agendaCode As %String) As %DynamicArray
{
    s arr = ##class(%DynamicArray).%New()
    s cacheClass = ..GetClassForAgenda(agendaCode)
    q:cacheClass="" arr

    ; Volání agenda metody — sdílí stejné ^||ML globály.
    ; Save and clear OUT, set IN params, call, parse, restore.
    s json = ""
    s ^||ML("IN","search") = ""
    s ^||ML("IN","limit") = 200

    ; Cleanup OUT before nested call (klíče "1".."N")
    s key = ""
    for {
        s key = $o(^||ML("OUT",key))
        q:key=""
        if key '= "ERR" k ^||ML("OUT",key)
    }

    d $classmethod(cacheClass, "GetAvailableInstances", .sys)
    s json = $g(^||ML("OUT","1"))

    ; Cleanup OUT after nested call so caller's OUT writes start fresh
    s key = ""
    for {
        s key = $o(^||ML("OUT",key))
        q:key=""
        if key '= "ERR" k ^||ML("OUT",key)
    }

    if json '= "" {
        s parsed = ##class(%DynamicArray).%FromJSON(json)
        s i = 0
        for {
            s inst = parsed.%Get(i)
            q:'$isobject(inst)
            d arr.%Push(inst)
            s i = i + 1
        }
    }
    q arr
}

/// Mapuje agenda kód na FQN Caché třídy implementující AgendaAPI.
/// Aktuálně hardcoded — odpovídá agendas[] v templatedesigner.appsettings.json.
/// TODO: po Wave 2 přechod na AgendaRegistryAPI z Caché.
ClassMethod GetClassForAgenda(agendaCode As %String) As %String
{
    if agendaCode = "MHD"     q "ALVA.TemplateDesigner.Agendas.ZastavkoveJrMhdAPI"
    if agendaCode = "FAKTURY" q "ALVA.TemplateDesigner.Agendas.TemplateDesignerFAKTURY"
    q ""
}

}
