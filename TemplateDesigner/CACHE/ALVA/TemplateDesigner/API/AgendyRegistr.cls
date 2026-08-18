Include GENERAL

/// Registr dostupných agend TemplateDesigneru (2026-05-20; dvouosý model 2026-07-11).
///
/// Auto-discovery z %Dictionary - najde všechny subclassy
/// ALVA.TemplateDesigner.Agendy.BaseAgenda a vrátí jejich metadata (kombinovaný klíč
/// AgendaVariantCode + osy Agenda/Variant + labely) jako JSON pole. Vytvoření třídy =
/// registrace; není nutný separátní config list (eliminuje drift mezi registrem a impl).
///
/// GENERAL agenda (šablona bez DB vazby) nemá Caché class - .NET strana ji
/// inject syntheticky v IAgendaRegistry implementaci.
Class ALVA.TemplateDesigner.API.AgendyRegistr Extends %RegisteredObject
{

/// Vrátí seznam dostupných konfigurací (agenda × varianta) - auto-discovery z BaseAgenda subclasses.
/// OUT["1"] = JSON pole, např.:
///   [
///     { "code": "ZASTAVKOVY_JR_AD", "label": "Zastávkový JŘ (AD)", "cacheClass": "...",
///       "agenda": "ZASTAVKOVY_JR", "agendaLabel": "Zastávkový JŘ",
///       "variant": "AD", "variantLabel": "AD (linková doprava)", ... }
///   ]
///
/// Wire kompatibilita: klíče `code` (kombinovaný) a `label` (kombinovaný label,
/// AgendaLabel + " (Variant)") zůstávají pro starší klienty; nové osy jsou aditivní.
///
/// Řazení: alfabeticky podle code (přirozený order). Pokud bude potřeba explicit
/// order, doplnit AgendaOrder parameter do BaseAgenda + ORDER BY (TODO).
///
/// Třídy bez AgendaVariantCode (= prázdný string parameter) se přeskakují - nejsou
/// validně registrovány (abstraktní BaseAgenda samotná ho má prázdný).
ClassMethod ListAgendy(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s arr = ##class(%DynamicArray).%New()
        s baseClass = "ALVA.TemplateDesigner.Agendy.BaseAgenda"

        // SQL nad %Dictionary: najdi všechny přímé subclasses BaseAgenda.
        // (Pokud bude víc úrovní dědičnosti, použít %Dictionary.ClassDefinitionQuery
        // s rekurzí - zatím stačí přímé subclasses.)
        s sql = "SELECT ID FROM %Dictionary.ClassDefinition WHERE Super = ? AND ID <> ? ORDER BY ID"
        s rs = ##class(%SQL.Statement).%ExecDirect(, sql, baseClass, baseClass)
        if rs.%SQLCODE < 0 {
            $$$ThrowStatus($$$ERROR($$$GeneralError, "ListAgendy SQL failed: "_rs.%Message))
        }

        while rs.%Next() {
            s className = rs.%Get("ID")
            s code = $parameter(className, "AgendaVariantCode")
            s agenda = $parameter(className, "Agenda")
            s agendaLabel = $parameter(className, "AgendaLabel")
            s variant = $parameter(className, "Variant")
            s variantLabel = $parameter(className, "VariantLabel")

            // Třída bez kombinovaného klíče není validně registrovaná → skip.
            if code = "" continue

            s entry = ##class(%DynamicObject).%New()
            s entry.code = code
            // Kombinovaný label pro starší klienty - historický formát "Zastávkový JŘ (AD)".
            s entry.label = agendaLabel_$s(variant'="":" ("_variant_")", 1:"")
            s entry.agenda = agenda
            s entry.agendaLabel = agendaLabel
            s entry.variant = variant
            s entry.variantLabel = variantLabel
            s entry.cacheClass = className
            s entry.instanceParamName = $parameter(className, "InstanceParamName")
            s entry.elementKind = $parameter(className, "ElementKind")
            s entry.instanceLabel = $parameter(className, "InstanceLabel")
            s entry.instanceLabelPlural = $parameter(className, "InstanceLabelPlural")
            // JSON boolean (ne 1/0) - System.Text.Json na .NET straně deserializuje do bool.
            d entry.%Set("released", $s($parameter(className, "Released")=1:1, 1:0), "boolean")
            d arr.%Push(entry)
        }

        s ^||ML("OUT","1") = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vrátí FQN třídy BaseAgenda subclass s daným AgendaVariantCode (auto-discovery z %Dictionary),
/// nebo "" když nenalezeno. Sdílený lookup - jediný zdroj mapování kód→třída (konzumuje
/// EDList.Instance; žádný hardcoded mirror). Stejná discovery cesta jako ListAgendy.
ClassMethod GetClassForCode(agendaCode As %String) As %String
{
    q:agendaCode="" ""
    s result = ""
    s baseClass = "ALVA.TemplateDesigner.Agendy.BaseAgenda"
    s sql = "SELECT ID FROM %Dictionary.ClassDefinition WHERE Super = ? AND ID <> ?"
    s rs = ##class(%SQL.Statement).%ExecDirect(, sql, baseClass, baseClass)
    q:rs.%SQLCODE<0 ""
    while rs.%Next() {
        s className = rs.%Get("ID")
        if $parameter(className, "AgendaVariantCode") = agendaCode {
            s result = className
            q
        }
    }
    q result
}

}
