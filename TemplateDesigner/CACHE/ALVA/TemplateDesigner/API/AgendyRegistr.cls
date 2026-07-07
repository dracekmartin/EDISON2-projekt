Include GENERAL

/// Registr dostupných agend TemplateDesigneru (Wave 2, 2026-05-20).
///
/// Auto-discovery z %Dictionary — najde všechny subclassy
/// ALVA.TemplateDesigner.Agendy.BaseAgenda a vrátí jejich AgendaCode + AgendaLabel
/// jako JSON pole. Vytvoření třídy = registrace agendy; není nutný separátní
/// config list (eliminuje drift mezi registrem a impl).
///
/// GENERAL agenda (šablona bez DB vazby) nemá Caché class — .NET strana ji
/// inject syntheticky v IAgendaRegistry implementaci.
Class ALVA.TemplateDesigner.API.AgendyRegistr Extends %RegisteredObject
{

/// Vrátí seznam dostupných agend (auto-discovery z BaseAgenda subclasses).
/// OUT["1"] = JSON pole:
///   [
///     { "code": "FAKTURY",           "label": "Faktury",             "cacheClass": "ALVA.TemplateDesigner.Agendy.Faktury" },
///     { "code": "ZASTAVKOVY_JR_MHD", "label": "Zastávkový JŘ (MHD)", "cacheClass": "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd" }
///   ]
///
/// Řazení: alfabeticky podle code (přirozený order). Pokud bude potřeba explicit
/// order, doplnit AgendaOrder parameter do BaseAgenda + ORDER BY (TODO).
///
/// Třídy bez AgendaCode (= prázdný string parameter) se přeskakují — nejsou
/// validně registrovány (abstraktní BaseAgenda samotná má AgendaCode = "").
ClassMethod ListAgendy(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s arr = ##class(%DynamicArray).%New()
        s baseClass = "ALVA.TemplateDesigner.Agendy.BaseAgenda"

        // SQL nad %Dictionary: najdi všechny přímé subclasses BaseAgenda.
        // (Pokud bude víc úrovní dědičnosti, použít %Dictionary.ClassDefinitionQuery
        // s rekurzí — pro Wave 2 stačí přímé subclasses.)
        s sql = "SELECT ID FROM %Dictionary.ClassDefinition WHERE Super = ? AND ID <> ? ORDER BY ID"
        s rs = ##class(%SQL.Statement).%ExecDirect(, sql, baseClass, baseClass)
        if rs.%SQLCODE < 0 {
            $$$ThrowStatus($$$ERROR($$$GeneralError, "ListAgendy SQL failed: "_rs.%Message))
        }

        while rs.%Next() {
            s className = rs.%Get("ID")
            s code = $parameter(className, "AgendaCode")
            s label = $parameter(className, "AgendaLabel")

            // Třída bez AgendaCode není validně registrovaná → skip.
            if code = "" continue

            s entry = ##class(%DynamicObject).%New()
            s entry.code = code
            s entry.label = label
            s entry.cacheClass = className
            d arr.%Push(entry)
        }

        s ^||ML("OUT","1") = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Vrátí FQN třídy BaseAgenda subclass s daným AgendaCode (auto-discovery z %Dictionary),
/// nebo "" když nenalezeno. Sdílený lookup — jediný zdroj mapování kód→třída (konzumuje
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
        if $parameter(className, "AgendaCode") = agendaCode {
            s result = className
            q
        }
    }
    q result
}

}
