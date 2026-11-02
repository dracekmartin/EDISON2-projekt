Include GENERAL

/// Šablona pro tisk bez návrháře (`B190`, rozhodnutí R4 2. 11.): výchozí šablona firmy pro agendu
/// a pravidlo, kterou šablonu tisk vezme. Jedno pravidlo pro formulář před tiskem v okně JŘ,
/// spuštění návrháře do PDF i tisk bez uživatele. Platí první krok, který šablonu dá:
///   1. šablona, kterou poslal volající (vybraná ve formuláři okna JŘ, nebo určená nastavením modulu),
///   2. šablona přiřazená instanci (lince) - zatím se nedělá, šablona se bere výš (Jan 2. 11.),
///   3. výchozí šablona firmy pro agendu,
///   4. výchozí šablona M-line pro agendu (parametr `VychoziSablonaMline` třídy agendy),
///   5. jinak důvod „není nastavená šablona pro tisk".
/// Kroky 1 a 5 nekončí chybou programu, ale důvodem pro uživatele (`duvod`, prázdné `templateId`);
/// chyba je jen neznámá agenda.
///
/// Výchozí šablona firmy leží mimo JSON šablony, aby ji „Uložit jako" nezkopírovalo:
///   ^ALVA.TemplateDesigner.Templates(IdFa, "DEFAULT", AgendaCode) = TemplateID
/// Smí ukazovat i na šablonu M-line - firma si tím vybere jinou hotovou šablonu, než je výchozí M-line.
/// Šablony samotné drží `API.Sablony`.
Class ALVA.TemplateDesigner.API.SablonyProTisk Extends %RegisteredObject
{

/// Větev systémových šablon (`API.Sablony`); testy pravidla si předají vlastní.
Parameter MlineIdFa = "MLINE";

/// Šablona pro tisk podle pravidla.
/// IN["agendaCode"] = kód agendy (povinný)
/// IN["templateId"] = šablona vybraná volajícím (volitelná, krok 1)
/// OUT["templateId"], OUT["sourceIdFa"] (firma nebo MLINE, pro `API.Sablony.LoadTemplate`), OUT["name"],
/// OUT["zdroj"] = volajici | firma | mline (který krok šablonu dal)
/// Bez šablony: OUT["templateId"] = "" a OUT["duvod"] = text pro uživatele.
ClassMethod GetTemplateForPrint(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s sablona = ..SablonaProTisk(IdFa, $g(^||ML("IN","agendaCode")), $g(^||ML("IN","templateId")))
        s ^||ML("OUT","templateId") = sablona.templateId
        s ^||ML("OUT","sourceIdFa") = sablona.sourceIdFa
        s ^||ML("OUT","name")       = sablona.name
        s ^||ML("OUT","zdroj")      = sablona.zdroj
        s ^||ML("OUT","duvod")      = sablona.duvod
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Nastaví výchozí šablonu firmy pro agendu (krok 3 pravidla).
/// IN["agendaCode"] = kód agendy (povinný)
/// IN["templateId"] = šablona firmy nebo M-line téže agendy; prázdné = zrušit výchozí šablonu firmy
ClassMethod SetDefaultTemplate(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        d ..WriteVychoziFirmy(IdFa, $g(^||ML("IN","agendaCode")), $g(^||ML("IN","templateId")))
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Šablona pro tisk podle pravidla jako {templateId, sourceIdFa, name, zdroj, duvod}. Bez šablony je
/// `templateId` prázdné a `duvod` říká uživateli proč. Volá ji i formulář před tiskem v okně JŘ (repo EDISON),
/// proto bez `sys`.
ClassMethod SablonaProTisk(IdFa As %String, agendaCode As %String, templateId As %String = "", mlineIdFa As %String = {..#MlineIdFa}) As %DynamicObject
{
    s agendaClass = ..AgendaClass(agendaCode)

    // Krok 1: výslovná volba volajícího. Chybějící šablona nevede k tichému přechodu na výchozí -
    // uživatel by dostal jiný výtisk, než si vybral.
    i templateId '= "" {
        s sablona = ..NajdiSablonu(IdFa, agendaCode, templateId, mlineIdFa)
        i '$isobject(sablona) q ..BezSablony("Vybraná šablona pro tisk v databázi není nebo patří jiné agendě. Vyberte šablonu znovu.")
        s sablona.zdroj = "volajici"
        q sablona
    }

    // Krok 2 (šablona u linky) se zatím nedělá.

    // Krok 3: výchozí šablona firmy. Smazaná nebo přeřazená do jiné agendy se přeskočí.
    s vychozi = $g(^ALVA.TemplateDesigner.Templates(IdFa,"DEFAULT",agendaCode))
    i vychozi '= "" {
        s sablona = ..NajdiSablonu(IdFa, agendaCode, vychozi, mlineIdFa)
        i $isobject(sablona) { s sablona.zdroj = "firma"  q sablona }
    }

    // Krok 4: výchozí šablona M-line. Do MLINE ji nalévá návrhář při startu, takže na serveru,
    // kde návrhář s touto šablonou ještě neběžel, chybí.
    s vychoziMline = $parameter(agendaClass, "VychoziSablonaMline")
    i vychoziMline '= "" {
        s sablona = ..NajdiSablonu(mlineIdFa, agendaCode, vychoziMline, mlineIdFa)
        i $isobject(sablona) { s sablona.zdroj = "mline"  q sablona }
    }

    q ..BezSablony("Pro agendu „"_..AgendaLabel(agendaClass)_""" není nastavená šablona pro tisk. Nastavte výchozí šablonu v návrháři sestav.")
}

/// Výsledek pravidla bez šablony: prázdné templateId a důvod pro uživatele.
ClassMethod BezSablony(duvod As %String) As %DynamicObject
{
    s vysledek = ##class(%DynamicObject).%New()
    s vysledek.templateId = ""
    s vysledek.sourceIdFa = ""
    s vysledek.name = ""
    s vysledek.zdroj = ""
    s vysledek.duvod = duvod
    q vysledek
}

/// Šablony agendy pro výběr ve formuláři před tiskem: vlastní šablony firmy a pak šablony M-line, obojí
/// podle jména. Položka {templateId, sourceIdFa, name, zdroj ("vlastní" / "M-line"), vychozi} - `vychozi`
/// má ta, kterou by pravidlo vzalo bez volby volajícího.
ClassMethod SeznamProTisk(IdFa As %String, agendaCode As %String, mlineIdFa As %String = {..#MlineIdFa}) As %DynamicArray
{
    // Bez výchozí šablony seznam pořád nabídne, co je - formulář pak žádnou nepředvybere.
    s vychoziId = ..SablonaProTisk(IdFa, agendaCode, "", mlineIdFa).templateId

    // Firma, která sama je MLINE (autorování systémových šablon), by šablony vypsala dvakrát.
    s vetve = $lb(IdFa)
    s:IdFa'=mlineIdFa vetve = vetve_$lb(mlineIdFa)

    s seznam = ##class(%DynamicArray).%New()
    f iVetev = 1:1:$ll(vetve) {
        s vetev = $lg(vetve, iVetev)
        k poradi
        s id = ""
        f {
            s id = $o(^ALVA.TemplateDesigner.Templates(vetev,"IDX","ByAgenda",agendaCode,id))  q:id=""
            continue:'$d(^ALVA.TemplateDesigner.Templates(vetev,"DTA",id))
            s jmeno = $g(^ALVA.TemplateDesigner.Templates(vetev,"DTA",id,"Metadata","Name"))
            s poradi($zcvt(jmeno,"L")_" ", id) = jmeno
        }
        s klic = ""
        f {
            s klic = $o(poradi(klic))  q:klic=""
            s id = ""
            f {
                s id = $o(poradi(klic,id),1,jmeno)  q:id=""
                s polozka = ##class(%DynamicObject).%New()
                s polozka.templateId = id
                s polozka.sourceIdFa = vetev
                s polozka.name = jmeno
                s polozka.zdroj = $s(vetev=mlineIdFa:"M-line", 1:"vlastní")
                d polozka.%Set("vychozi", (id = vychoziId), "boolean")
                d seznam.%Push(polozka)
            }
        }
    }
    q seznam
}

/// Zapíše (prázdné templateId = smaže) výchozí šablonu firmy pro agendu.
ClassMethod WriteVychoziFirmy(IdFa As %String, agendaCode As %String, templateId As %String, mlineIdFa As %String = {..#MlineIdFa})
{
    d ..AgendaClass(agendaCode)
    i templateId = "" {
        k ^ALVA.TemplateDesigner.Templates(IdFa,"DEFAULT",agendaCode)
        q
    }
    i '$isobject(..NajdiSablonu(IdFa, agendaCode, templateId, mlineIdFa)) {
        $$$ThrowStatus($$$ERROR($$$GeneralError, "Šablona v databázi není nebo patří jiné agendě, výchozí být nemůže."))
    }
    s ^ALVA.TemplateDesigner.Templates(IdFa,"DEFAULT",agendaCode) = templateId
}

/// Šablona dané agendy ve větvi firmy, jinak ve větvi M-line, jako {templateId, sourceIdFa, name};
/// "" když v žádné není nebo patří jiné agendě.
ClassMethod NajdiSablonu(IdFa As %String, agendaCode As %String, templateId As %String, mlineIdFa As %String) As %DynamicObject
{
    q:templateId="" ""
    f vetev = IdFa, mlineIdFa {
        continue:'$d(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId))
        continue:$g(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId,"Metadata","AgendaCode"))'=agendaCode
        s sablona = ##class(%DynamicObject).%New()
        s sablona.templateId = templateId
        s sablona.sourceIdFa = vetev
        s sablona.name = $g(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId,"Metadata","Name"))
        q
    }
    q $g(sablona)
}

/// Třída agendy podle kódu; neznámý kód je chyba volajícího.
ClassMethod AgendaClass(agendaCode As %String) As %String
{
    i agendaCode = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "agendaCode is required"))
    s agendaClass = ##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(agendaCode)
    i agendaClass = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "Neznámá agenda: "_agendaCode))
    q agendaClass
}

/// Jméno agendy pro hlášky ve tvaru registru („Linkový JŘ (AD)").
ClassMethod AgendaLabel(agendaClass As %String) As %String
{
    s varianta = $parameter(agendaClass, "VariantLabel")
    s:varianta="" varianta = $parameter(agendaClass, "Variant")
    q $parameter(agendaClass, "AgendaLabel")_$s(varianta'="":" ("_varianta_")", 1:"")
}

}
