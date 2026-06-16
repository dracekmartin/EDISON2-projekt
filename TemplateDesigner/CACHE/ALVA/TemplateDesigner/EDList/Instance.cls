Include GENERAL

/// Seznam instancí pro aktuální agendu — konkrétní záznamy, nad kterými se bude tisknout
/// šablona (uživatel jednu/víc vybere v Instance Pickeru, výběr se předá do GetData).
///
/// Co je "instance" závisí na agendě:
/// - **MHD** (zastávkový JŘ)  → linky MHD (např. "42 — Praha–Brno", "15 — Linka přes centrum")
/// - **FAKTURY**             → jednotlivé faktury (např. "2024001 — ACME s.r.o.", "2024002 — Beta a.s.")
/// - **Meziměstský JŘ**       → linky meziměstské dopravy (až bude implementováno)
/// - **Vozový JŘ**            → oběhy vozidel (až bude implementováno)
///
/// Sloupce seznamu: value (opaque ID předaný do GetData), display (hlavní popis), subtitle (volitelný detail).
///
/// Data: routuje přes $classmethod na agenda-specifickou GetAvailableInstances
///       v ALVA.TemplateDesigner.Agendy.<konkrétní> (každá agenda iteruje vlastní doménovou strukturu —
///       MHD: ^ALVA.MHD.LSO(IdFa,1,"IDX",...); faktury: TBD).
/// Wire formát: CVar named IN + JSON multi-slot OUT (Styl 3 viz EDControls/DOCS/01-wire-formats.md).
/// Konzument: InstancePickerDialog v TD (.NET)
Class ALVA.TemplateDesigner.EDList.Instance Extends %RegisteredObject
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

/// Mapuje agenda kód na FQN Caché třídy implementující BaseAgenda.
/// Aktuálně hardcoded mirror Wave 2 AgendyRegistr — TODO: refactor na volání
/// ALVA.TemplateDesigner.API.AgendyRegistr.ListAgendy a lookup z výsledku.
ClassMethod GetClassForAgenda(agendaCode As %String) As %String
{
    if agendaCode = "ZASTAVKOVY_JR_MHD" q "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd"
    if agendaCode = "ZASTAVKOVY_JR_AD"  q "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
    if agendaCode = "FAKTURY"           q "ALVA.TemplateDesigner.Agendy.Faktury"
    q ""
}

}
