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
        d ..TridyAgend(.tridy)

        s className = ""
        for {
            s className = $o(tridy(className))
            q:className=""
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
            s entry.label = agendaLabel_$s(variantLabel'="":" ("_variantLabel_")", variant'="":" ("_variant_")", 1:"")
            s entry.agenda = agenda
            s entry.agendaLabel = agendaLabel
            s entry.variant = variant
            s entry.variantLabel = variantLabel
            s entry.oblastPrace = $parameter(className, "OblastPrace")
            s entry.cacheClass = className
            s entry.instanceParamName = $parameter(className, "InstanceParamName")
            s entry.elementKind = $parameter(className, "ElementKind")
            // 1. pád jednotného a množného čísla i zvlášť - čtou je starší návrháři, kteří tvary neznají.
            s tvary = $parameter(className, "InstanceTvary")
            s entry.instanceLabel = $p(tvary,",",1)
            s entry.instanceLabelPlural = $p(tvary,",",4)
            s entry.instanceTvary = tvary
            s entry.instanceRod = $parameter(className, "InstanceRod")
            s entry.vychoziSablonaMline = $parameter(className, "VychoziSablonaMline")
            // JSON boolean (ne 1/0) - System.Text.Json na .NET straně deserializuje do bool.
            d entry.%Set("released", $s($parameter(className, "Released")=1:1, 1:0), "boolean")
            d entry.%Set("customColumns", $s($parameter(className, "CustomColumns")=1:1, 1:0), "boolean")
            d entry.%Set("variantyTras", $s($parameter(className, "VariantyTras")=1:1, 1:0), "boolean")
            d entry.%Set("rozdilyVerzi", $s($parameter(className, "RozdilyVerzi")=1:1, 1:0), "boolean")
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
    d ..TridyAgend(.tridy)
    s className = ""
    for {
        s className = $o(tridy(className))
        q:className=""
        q:$parameter(className, "AgendaVariantCode")=agendaCode
    }
    q className
}

/// Třídy agend do `tridy(className)` - přímí potomci BaseAgenda, včetně těch bez kódu (volající je přeskočí).
/// Jediné místo, které agendy hledá; víc úrovní dědičnosti registr nezná (R8: sdílený kód patří do pomocných tříd).
ClassMethod TridyAgend(ByRef tridy)
{
    k tridy
    s baseClass = "ALVA.TemplateDesigner.Agendy.BaseAgenda"
    s sql = "SELECT ID FROM %Dictionary.ClassDefinition WHERE Super = ? AND ID <> ?"
    s rs = ##class(%SQL.Statement).%ExecDirect(, sql, baseClass, baseClass)
    if rs.%SQLCODE < 0 {
        $$$ThrowStatus($$$ERROR($$$GeneralError, "Seznam agend nejde načíst: "_rs.%Message))
    }
    while rs.%Next() {
        s tridy(rs.%Get("ID")) = ""
    }
}

/// Oblasti práce stromu šablon v pořadí, v jakém je strom ukazuje: $lb($lb(kód, název), ...).
/// Členění podle oblasti práce uživatele z rozboru převodu report generátoru (`F7`); agenda svou ohlásí parametrem
/// `OblastPrace`. Kódy se neukládají do šablon, název i pořadí jde kdykoli změnit. Agenda bez známé oblasti
/// je ve stromu pod Ostatní (`OblastOstatni`).
ClassMethod OblastiPrace() As %List
{
    s oblasti = $lb($lb("JIZDNI_RADY", "Jízdní řády"))
    s oblasti = oblasti_$lb($lb("PLAN_A_PROVOZ", "Plán a provoz"))
    s oblasti = oblasti_$lb($lb("OBJEDNAVKY", "Objednávky"))
    s oblasti = oblasti_$lb($lb("MZDY", "Mzdy"))
    s oblasti = oblasti_$lb($lb("POKLADNA_A_ODBAVENI", "Pokladna a odbavení"))
    s oblasti = oblasti_$lb($lb("UCETNICTVI", "Účetnictví"))
    s oblasti = oblasti_$lb($lb("MAJETEK_A_SKLAD", "Majetek a sklad"))
    s oblasti = oblasti_$lb($lb("VOZIDLA", "Vozidla"))
    s oblasti = oblasti_$lb($lb("OBECNE", "Obecné"))
    q oblasti
}

/// Oblast pro agendu bez známé oblasti práce - strom ji zařadí na konec, aby šablona nezmizela.
ClassMethod OblastOstatni() As %List
{
    q $lb("OSTATNI", "Ostatní")
}

}
