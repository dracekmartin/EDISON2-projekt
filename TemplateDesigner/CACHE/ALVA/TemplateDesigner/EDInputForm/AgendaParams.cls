Include (GENERAL, EDInputForm)

/// EDInputForm dispatch - generický formulář parametrů sestavy pro tiskový dialog.
///
/// Schéma parametrů NEdefinuje sama: nested-callem získá JSON z agenda metody `GetParameters`
/// (jediný zdroj definic parametrů per agenda) a převede ho na EDInputForm `Form`. Žádná
/// per-agenda EDInputForm třída - jedna generická obsluha pro všechny agendy.
///
/// IN["agendaClass"] = FQN agenda třídy (např. "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd").
///                     C# strana ho má v _classMap (CacheAgendaProvider).
/// IN["values"]      = (volitelné) JSON { "PARAM_NAME": "currentStringValue", ... } pro prefill
///                     aktuálními hodnotami (jinak se použije defaultValue z GetParameters).
///
/// `visibleWhen` (podmíněná viditelnost parametru) se zde NEvyhodnocuje - řeší ho C# host
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
        // includeExport=0 → jen parametry sestavy (levý tab v hlavním okně); default 1 = i export
        // skupina (tiskový dialog). Export pole jsou tiskově specifická, do tabu nepatří.
        s includeExport = $g(^||ML("IN","includeExport"), 1)

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

        // Universální export pole (členění výstupu + název souboru) - platí pro všechny agendy.
        // NEjsou součástí GetParameters → jen v tiskovém dialogu, ne v tabu parametrů.
        i includeExport '= 0 { d ..AddExportFields(form, vals, agendaClass) }

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Universální export pole pro tiskový dialog (společné všem agendám): členění výstupu při tisku
/// s daty (více instancí) + pattern názvu souboru s {POLE} tokeny. Klíče stabilní (parsuje C#),
/// popisky české. Viditelnost pole názvu (jen pro split) řeší C# host (PrintDialog.ApplyVisibility).
/// Volby členění dle capability agendy: PER_LINE (per instance) jen když agenda instance má,
/// PER_STOP (per stránka dat zastávka×směr) jen pro ElementKind=STOP - u Faktur/GENERAL by
/// nabízené volby byly nesmyslné.
ClassMethod AddExportFields(form As ALVA.EDControls.EDInputForm.Form, vals As %DynamicObject, agendaClass As %String = "")
{
    d form.AddGroup("export", "Export (tisk s daty)")

    // Capability metadata agendy (class parametry) - defenzivně, neznámá/prázdná třída → defaults.
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
    s f = form.AddText("__EXPORT_FILENAME", "Název souboru (tokeny {POLE})", fname)
    s f.placeholders = 1

    // Měřítko tisku: hotová sazba se před tiskem celá zvětší/zmenší (parita s MeritkoVyves
    // starého postskriptu - vývěsky 1,3x, kniha 1x). Stránkování se tím nemění, jen rozměr
    // na papíře. Kolik z toho vyjde milimetrů řekne stavový řádek dialogu - server nezná
    // rozměr stránky šablony.
    s meritko = 1
    i $isobject(vals) && vals.%IsDefined("__EXPORT_SCALE") { s meritko = vals.%Get("__EXPORT_SCALE") }
    s f = form.AddDecimal("__EXPORT_SCALE", "Měřítko tisku", meritko, 0.1, 10, "x")
    s f.helpText = "1 = skutečná velikost. Zvětší se celá stránka včetně písma; počet stránek se nemění."

    // Uspořádání na list (formát listu, mřížka, oboustranný tisk, ořezové značky) už tu není -
    // tiskové okno ho má v panelu pod formulářem (papír, co se skládá, nákres). Hodnoty jdou
    // do týchž klíčů __EXPORT_SHEET*, takže dřív uložené volby platí dál.
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

/// Nested call na agenda `GetParameters` - vyčistí OUT okolo (mirror SaveTemplate.FetchAgendyJson).
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
        s f = form.AddBool(name, label, $s((val="true")||(val=1):1, 1:0))
    } elseif type = "number" {
        s f = form.AddNumber(name, label, val)
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
        s f = form.AddText(name, label, $s(val="":"", 1:val))
    }
    // Aditivní `description` z GetParameters → nápovědná bublina pole (starší agendy ho nemají)
    s desc = p.description
    s:desc'="" f.helpText = desc
}

}
