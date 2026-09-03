Include GENERAL

/// Seznam instancí pro aktuální agendu - konkrétní záznamy, nad kterými se bude tisknout
/// šablona (uživatel jednu/víc vybere v Instance Pickeru, výběr se předá do GetData).
///
/// Co je "instance" závisí na agendě:
/// - **MHD** (zastávkový JŘ)  → linky MHD (např. "42 - Praha–Brno", "15 - Linka přes centrum")
/// - **FAKTURY**             → jednotlivé faktury (např. "2024001 - ACME s.r.o.", "2024002 - Beta a.s.")
/// - **Meziměstský JŘ**       → linky meziměstské dopravy (až bude implementováno)
/// - **Vozový JŘ**            → oběhy vozidel (až bude implementováno)
///
/// Sloupce seznamu: cislo, nazev, subtitle (vše viditelné). value = opaque ID předaný do GetData
/// (= Id řádku, ne sloupec). Status chip skládá „číslo - název" na .NET z cislo+nazev (BuildSelectedInstances).
///
/// Data: routuje přes $classmethod na agenda-specifickou GetAvailableInstances
///       v ALVA.TemplateDesigner.Agendy.<konkrétní> (každá agenda iteruje vlastní doménovou strukturu -
///       MHD: ^ALVA.MHD.LSO(IdFa,1,"IDX",...); faktury: TBD).
/// Wire formát: CVar named IN + JSON multi-slot OUT (Styl 3 viz EDControls/DOCS/01-wire-formats.md).
/// Konzument: InstancePickerDialog v TD (.NET)
Class ALVA.TemplateDesigner.EDList.Instance Extends %RegisteredObject
{

/// Strop počtu instancí, které picker natáhne. **Není to stránkování** - seznam se má načíst celý,
/// aby hledací pole v dialogu prohledalo všechny linky, ne jen načtenou stránku.
///
/// ⚠️ Do 3. 9. tady bylo **200** a hledání pak nenašlo linku, která byla dál: napsané číslo
/// nevrátilo nic a vypadalo to, že linka v datech není (nález Jan 1. 9., `380761` v CB_CSADAD).
/// Skupina linek má reálně stovky záznamů - naměřeno 3. 9. na devu: nejvíc 240 (CB_CSADAD skupina 1),
/// pak 228, jinde desítky. Limit tedy řezal o čtyřicet.
///
/// Strop zůstává jako **pojistka proti chybě v datech** (poškozený globál by jinak stáhl celý `^JRL`),
/// ne jako omezení pro uživatele - proti naměřeným stovkám je o dva řády výš. Nula by nešla:
/// agendy čtou `i 'limit s limit = 50`, takže „bez limitu" by znamenalo padesát řádků.
Parameter MAXINSTANCI = 20000;

/// Vrátí definici sloupců pro EDList. Společné pro obě varianty.
ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s list = ##class(ALVA.EDControls.EDList.List).%New()
        d list.AddColumn("cislo",    "Číslo",  "TEXT",  70, "",  1)
        d list.AddColumn("nazev",    "Název",  "TEXT", 260, "",  1)
        d list.AddColumn("subtitle", "Detail", "TEXT", 220, "",  0)
        d list.WriteColumnsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Filter (sub-subscript pod "filter"):
///   IN["filter","agendaCode"]      = kód agendy (např. "MHD")
///   IN["filter","SKUPINA_LINEK_ID"] = zvolená skupina linek (volitelné; prázdné = default první)
/// OUT[1..N] = JSON objekt per řádek
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s agendaCode = $g(^||ML("IN","filter","agendaCode"))
        s skupina    = $g(^||ML("IN","filter","SKUPINA_LINEK_ID"))
        s instances = ..FetchInstances(.sys, agendaCode, skupina)

        s list = ##class(ALVA.EDControls.EDList.List).%New()
        s count = instances.%Size()
        for i=0:1:count-1 {
            s inst = instances.%Get(i)
            if '$isobject(inst) continue
            s value    = inst.%Get("value")
            s cislo    = inst.%Get("cislo")
            s nazev    = inst.%Get("nazev")
            s subtitle = inst.%Get("subtitle")
            s row = list.AddRow(value)              // value = Id řádku (→ .NET AgendaInstance.Value)
            // "string" type - %Get vrací kanonicky-číselné cislo → bez toho by row.%Set znovu emitoval JSON number
            d row.%Set("cislo",    cislo, "string")
            d row.%Set("nazev",    nazev, "string")
            d row.%Set("subtitle", subtitle, "string")
        }
        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Sdílený fetch - routuje na agenda-specifickou GetAvailableInstances přes $classmethod.
/// Vrací %DynamicArray objektů {value, display, subtitle}.
ClassMethod FetchInstances(ByRef sys As %String, agendaCode As %String, skupina As %String = "") As %DynamicArray
{
    s arr = ##class(%DynamicArray).%New()
    s cacheClass = ..GetClassForAgenda(agendaCode)
    q:cacheClass="" arr

    ; Volání agenda metody - sdílí stejné ^||ML globály.
    ; Save and clear OUT, set IN params, call, parse, restore.
    s json = ""
    ; search zůstává prázdný - filtruje se v dialogu na klientovi nad CELÝM seznamem, proto strop
    ; místo stránky (viz MAXINSTANCI).
    s ^||ML("IN","search") = ""
    s ^||ML("IN","limit") = ..#MAXINSTANCI
    s ^||ML("IN","SKUPINA_LINEK_ID") = skupina

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
/// Deleguje na AgendyRegistr.GetClassForCode (auto-discovery z %Dictionary) - žádný
/// hardcoded mirror, přidání nové agendy = jen nová BaseAgenda subclass.
ClassMethod GetClassForAgenda(agendaCode As %String) As %String
{
    q ##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(agendaCode)
}

}
