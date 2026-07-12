Include (GENERAL, EDInputForm)

/// EDInputForm dispatch — generický formulář parametrů sestavy pro tiskový dialog.
///
/// Schéma parametrů NEdefinuje sama: nested-callem získá JSON z agenda metody `GetParameters`
/// (jediný zdroj definic parametrů per agenda) a převede ho na EDInputForm `Form`. Žádná
/// per-agenda EDInputForm třída — jedna generická obsluha pro všechny agendy.
///
/// IN["agendaClass"] = FQN agenda třídy (např. "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd").
///                     C# strana ho má v _classMap (CacheAgendaProvider).
/// IN["values"]      = (volitelné) JSON { "PARAM_NAME": "currentStringValue", ... } pro prefill
///                     aktuálními hodnotami (jinak se použije defaultValue z GetParameters).
///
/// `visibleWhen` (podmíněná viditelnost parametru) se zde NEvyhodnocuje — řeší ho C# host
/// (PrintDialog) nad EDInputForm poli: má k dispozici pravidla i hodnoty a umí přepnout
/// Field.Visibility živě. Validate roundtrip navíc nenese InitParams (agendaClass), takže
/// server-side evaluace by neměla kontext. Tato třída proto staví jen plochý formulář.
Class ALVA.TemplateDesigner.EDInputForm.AgendaParams Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s agendaClass = $g(^||ML("IN","agendaClass"))
        s valuesJson  = $g(^||ML("IN","values"))

        s paramsJson = ..FetchParamsJson(.sys, agendaClass)

        s vals = ""
        i valuesJson '= "" { s vals = ##class(%DynamicObject).%FromJSON(valuesJson) }

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        i paramsJson '= "" {
            s arr = ##class(%DynamicArray).%FromJSON(paramsJson)
            d ..AddParamGroups(form, arr, .vals)
        } else {
            d form.AddGroup("params", "Parametry sestavy")
        }

        // Universální export pole (členění výstupu + název souboru) — platí pro všechny agendy.
        // NEjsou součástí GetParameters → neukáží se v preview liště, jen v tiskovém dialogu.
        d ..AddExportFields(form, vals, agendaClass)

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Universální export pole pro tiskový dialog (společné všem agendám): členění výstupu při tisku
/// s daty (více instancí) + pattern názvu souboru s {POLE} tokeny. Klíče stabilní (parsuje C#),
/// popisky české. Viditelnost pole názvu (jen pro split) řeší C# host (PrintDialog.ApplyVisibility).
/// Volby členění dle capability agendy: PER_LINE (per instance) jen když agenda instance má,
/// PER_STOP (per stránka dat zastávka×směr) jen pro ElementKind=STOP — u Faktur/GENERAL by
/// nabízené volby byly nesmyslné.
ClassMethod AddExportFields(form As ALVA.EDControls.EDInputForm.Form, vals As %DynamicObject, agendaClass As %String = "")
{
    d form.AddGroup("export", "Export (tisk s daty)")

    // Capability metadata agendy (class parametry) — defenzivně, neznámá/prázdná třída → defaults.
    s elementKind = ""
    s instanceLabel = ""
    i agendaClass '= "" {
        try {
            s elementKind   = $parameter(agendaClass, "ElementKind")
            s instanceLabel = $parameter(agendaClass, "InstanceLabel")
        } catch ex { s elementKind = "", instanceLabel = "" }
    }

    s split = "SINGLE"
    i $isobject(vals) && vals.%IsDefined("__EXPORT_SPLIT") { s split = vals.%Get("__EXPORT_SPLIT") }
    s f = form.AddFieldToLastGroup("__EXPORT_SPLIT", "Členění výstupu", $$$FieldTypeList, 1, 1)
    d f.AddListItem("SINGLE", "Vše do jednoho PDF")
    s hasPerLine = (instanceLabel '= "")
    s hasPerStop = (elementKind = "STOP")
    i hasPerLine { d f.AddListItem("PER_LINE", "Jeden PDF na " _ ..Akuzativ(instanceLabel)) }
    i hasPerStop { d f.AddListItem("PER_STOP", "Jeden PDF na zastávku/směr") }
    // Uložená hodnota, kterou tato agenda nenabízí (přepnutí agendy) → fallback SINGLE.
    i (split="PER_LINE") && 'hasPerLine { s split = "SINGLE" }
    i (split="PER_STOP") && 'hasPerStop { s split = "SINGLE" }
    s:split'="" f.value = split

    s fname = ""
    i $isobject(vals) && vals.%IsDefined("__EXPORT_FILENAME") { s fname = vals.%Get("__EXPORT_FILENAME") }
    d form.AddText("__EXPORT_FILENAME", "Název souboru (tokeny {POLE})", fname)

    // Imposice (N-up): skládání stránek šablony na fyzické listy — stránky 1:1, blok centrovaný.
    // Geometrii/validaci mřížky řeší C# (SheetComposer — zná rozměr stránky šablony); tady jen volby.
    // Viditelnost detailních polí (jen při zvoleném formátu listu) řeší C# host (PrintDialog.ApplyVisibility).
    s sheet = "NONE"
    i $isobject(vals) && vals.%IsDefined("__EXPORT_SHEET") { s sheet = vals.%Get("__EXPORT_SHEET") }
    s f = form.AddFieldToLastGroup("__EXPORT_SHEET", "Uspořádání na list", $$$FieldTypeList, 1, 1)
    d f.AddListItem("NONE",         "Vypnuto (1 stránka = 1 list)")
    d f.AddListItem("A4_PORTRAIT",  "A4 na výšku")
    d f.AddListItem("A4_LANDSCAPE", "A4 na šířku")
    d f.AddListItem("A3_PORTRAIT",  "A3 na výšku")
    d f.AddListItem("A3_LANDSCAPE", "A3 na šířku")
    s:sheet'="" f.value = sheet

    s grid = "AUTO"
    i $isobject(vals) && vals.%IsDefined("__EXPORT_SHEET_GRID") { s grid = vals.%Get("__EXPORT_SHEET_GRID") }
    s f = form.AddFieldToLastGroup("__EXPORT_SHEET_GRID", "Mřížka listu (řádky × sloupce)", $$$FieldTypeList, 1, 1)
    d f.AddListItem("AUTO", "Auto (největší možná)")
    d f.AddListItem("1x2",  "1 × 2 (vedle sebe)")
    d f.AddListItem("2x1",  "2 × 1 (pod sebou)")
    d f.AddListItem("2x2",  "2 × 2")
    d f.AddListItem("2x3",  "2 × 3")
    d f.AddListItem("3x2",  "3 × 2")
    d f.AddListItem("3x3",  "3 × 3")
    s:grid'="" f.value = grid

    s repeat = 0
    i $isobject(vals) && vals.%IsDefined("__EXPORT_SHEET_REPEAT") { s v = vals.%Get("__EXPORT_SHEET_REPEAT")  s repeat = $s((v="true")||(v=1):1, 1:0) }
    d form.AddBool("__EXPORT_SHEET_REPEAT", "Každá stránka na celý list (pro řezání)", repeat)

    s marks = 0
    i $isobject(vals) && vals.%IsDefined("__EXPORT_SHEET_MARKS") { s v = vals.%Get("__EXPORT_SHEET_MARKS")  s marks = $s((v="true")||(v=1):1, 1:0) }
    d form.AddBool("__EXPORT_SHEET_MARKS", "Ořezové značky", marks)
}

/// Rozloží parametry do EDInputForm skupin dle `group` klíče (pořadí = první výskyt skupiny,
/// stejná sémantika jako grouping lišty parametrů v hlavním okně). Parametry bez `group`
/// padají do defaultní skupiny „Parametry sestavy".
ClassMethod AddParamGroups(form As ALVA.EDControls.EDInputForm.Form, arr As %DynamicArray, ByRef vals)
{
    s sz = arr.%Size()

    // 1. průchod: skupiny v pořadí prvního výskytu + index parametrů per skupina
    s pocetSkupin = 0
    for i=0:1:(sz-1) {
        s p = arr.%Get(i)
        continue:'$isobject(p)
        s g = p.group
        s:g="" g = "Parametry sestavy"
        i '$d(skupinaIndex(g)) {
            s pocetSkupin = pocetSkupin + 1
            s skupinaIndex(g) = pocetSkupin
            s skupinaNazev(pocetSkupin) = g
        }
        s skupinaParam(skupinaIndex(g), i) = ""
    }

    i pocetSkupin = 0 { d form.AddGroup("params", "Parametry sestavy")  ret }

    // 2. průchod: per skupina AddGroup + pole v původním pořadí
    for gi=1:1:pocetSkupin {
        d form.AddGroup("params" _ gi, skupinaNazev(gi))
        s idx = ""
        f {
            s idx = $o(skupinaParam(gi, idx))
            q:idx=""
            d ..AddParamField(form, arr.%Get(idx), vals)
        }
    }
}

/// Akuzativ českého labelu instance pro větu „Jeden PDF na …" (linka→linku, faktura→fakturu,
/// instance→instanci). Jednoduchá heuristika koncovky; neznámá koncovka → beze změny.
ClassMethod Akuzativ(slovo As %String) As %String
{
    q:slovo="" slovo
    q:$e(slovo,*)="a" $e(slovo, 1, *-1) _ "u"
    q:$e(slovo,*)="e" $e(slovo, 1, *-1) _ "i"
    q slovo
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

/// Nested call na agenda `GetParameters` — vyčistí OUT okolo (mirror SaveTemplate.FetchAgendyJson).
/// Vrací JSON pole parametrů ("[]" pokud agenda není zadaná nebo žádné nemá).
ClassMethod FetchParamsJson(ByRef sys As %String, agendaClass As %String) As %String
{
    q:agendaClass="" "[]"

    // Vyčistit OUT vč. kořenového counteru, aby GetParameters psalo od OUT[1].
    k ^||ML("OUT")

    d $classmethod(agendaClass, "GetParameters", .sys)
    s json = $g(^||ML("OUT","1"))

    // Zachovat ERR z nested GetParameters (jinak by se chyba tiše ztratila), pak reset OUT vč. counteru.
    s savedErr = $g(^||ML("OUT","ERR"))
    k ^||ML("OUT")
    s:savedErr'="" ^||ML("OUT","ERR") = savedErr

    q $s(json="":"[]", 1:json)
}

/// Převede jeden parametr (z GetParameters JSON) na EDInputForm pole + prefill hodnotou.
/// Typy: bool / number / enum / date / (ostatní → text). visibleWhen se ignoruje (řeší C#).
ClassMethod AddParamField(form As ALVA.EDControls.EDInputForm.Form, p As %DynamicObject, vals As %DynamicObject)
{
    s name  = p.name
    s label = p.label
    s type  = p.type
    s def   = p.defaultValue

    // Aktuální hodnota z IN["values"] přebije defaultValue
    s val = def
    i $isobject(vals) && vals.%IsDefined(name) { s val = vals.%Get(name) }

    i type = "bool" {
        d form.AddBool(name, label, $s((val="true")||(val=1):1, 1:0))
    } elseif type = "number" {
        d form.AddNumber(name, label, val)
    } elseif type = "enum" {
        // required=1 → enum musí mít vybranou hodnotu (List dropdown bez úvodního prázdného řádku)
        s f = form.AddFieldToLastGroup(name, label, $$$FieldTypeList, 0, 1)
        s opts = p.options
        i $isobject(opts) {
            s osz = opts.%Size()
            for j=0:1:(osz-1) { s o = opts.%Get(j)  d:o'="" f.AddListItem(o, o) }
        }
        s:val'="" f.value = val
    } elseif type = "date" {
        s f = form.AddFieldToLastGroup(name, label, $$$FieldTypeDate, 0, 0)
        s:val'="" f.value = val
    } else {
        d form.AddText(name, label, $s(val="":"", 1:val))
    }
}

}
