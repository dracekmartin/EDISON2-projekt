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
        d form.AddGroup("params", "Parametry sestavy")

        i paramsJson '= "" {
            s arr = ##class(%DynamicArray).%FromJSON(paramsJson)
            s sz = arr.%Size()
            for i=0:1:(sz-1) {
                s p = arr.%Get(i)
                i $isobject(p) { d ..AddParamField(form, p, vals) }
            }
        }

        // Universální export pole (členění výstupu + název souboru) — platí pro všechny agendy.
        // NEjsou součástí GetParameters → neukáží se v preview liště, jen v tiskovém dialogu.
        d ..AddExportFields(form, vals)

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Universální export pole pro tiskový dialog (společné všem agendám): členění výstupu při tisku
/// s daty (více instancí) + pattern názvu souboru s {POLE} tokeny. Klíče stabilní (parsuje C#),
/// popisky české. Viditelnost pole názvu (jen pro split) řeší C# host (PrintDialog.ApplyVisibility).
ClassMethod AddExportFields(form As ALVA.EDControls.EDInputForm.Form, vals As %DynamicObject)
{
    d form.AddGroup("export", "Export (tisk s daty)")

    s split = "SINGLE"
    i $isobject(vals) && vals.%IsDefined("__EXPORT_SPLIT") { s split = vals.%Get("__EXPORT_SPLIT") }
    s f = form.AddFieldToLastGroup("__EXPORT_SPLIT", "Členění výstupu", $$$FieldTypeList, 1, 1)
    d f.AddListItem("SINGLE",   "Vše do jednoho PDF")
    d f.AddListItem("PER_LINE", "Jeden PDF na linku")
    d f.AddListItem("PER_STOP", "Jeden PDF na zastávku/směr")
    s:split'="" f.value = split

    s fname = ""
    i $isobject(vals) && vals.%IsDefined("__EXPORT_FILENAME") { s fname = vals.%Get("__EXPORT_FILENAME") }
    d form.AddText("__EXPORT_FILENAME", "Název souboru (tokeny {POLE})", fname)
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
