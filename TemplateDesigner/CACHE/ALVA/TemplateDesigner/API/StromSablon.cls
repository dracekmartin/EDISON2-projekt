Include GENERAL

/// Strom šablon pro okno šablon v návrháři (`B190`, rozhodnutí Jan 2. 11.): oblast práce → agenda → varianta.
/// Skládá se z registru agend (`AgendyRegistr.OblastiPrace`, parametr agendy `OblastPrace`) a nic se v něm neukládá;
/// složky, které by si zakládal uživatel (oblasti starého report generátoru), vědomě nejsou.
/// U varianty je počet šablon, které firma vidí (`API.Sablony.VetveFirmy`), a šablona, kterou tisk vezme bez volby
/// volajícího (`API.SablonyProTisk`). Jen zveřejněné agendy; šablony ostatních zůstávají pod Všechny šablony.
/// Oblast bez agendy strom vynechá.
Class ALVA.TemplateDesigner.API.StromSablon Extends %RegisteredObject
{

/// Větev systémových šablon; testy si předají vlastní.
Parameter MlineIdFa = "MLINE";

/// OUT["1"] = JSON
///   { "tiskZModulu", "sdileni", "oblasti": [ { "code", "label", "agendy": [ { "code", "label",
///       "varianty": [ { "code" (AgendaVariantCode), "label", "pocet", "tiskne", "vychozi": { "templateId", "name", "zdroj" } } ]
///   } ] } ] }
/// Varianta s prázdným `label` = agenda bez variant (Obecná, agendy report generátoru); `tiskne` = agenda se tiskne
/// z modulu a smí mít výchozí šablonu (Obecná ne); `vychozi.zdroj` = firma | mline, bez šablony pro tisk prázdný objekt.
/// `tiskZModulu` = tisk z okna jízdních řádů je zapnutý (týž přepínač jako položka v okně JŘ, repo EDISON
/// `ISYJR2/TEMPLATES/Tisk.cls`): bez něj okno šablon výchozí šablonu neukazuje, nastavení by nemělo účinek.
/// `sdileni` = okno nabízí posílání šablon jiným firmám (`API.SdileniSablon.JeZapnute`, `B189`).
ClassMethod GetStrom(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s strom = ..Strom(IdFa)
        d strom.%Set("tiskZModulu", ##class(ALVA.TemplateDesigner.API.SablonyProTisk).JeTiskZModulu(dataDB), "boolean")
        d strom.%Set("sdileni", ##class(ALVA.TemplateDesigner.API.SdileniSablon).JeZapnute(), "boolean")
        s ^||ML("OUT","1") = strom.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Strom šablon firmy jako %DynamicObject (tvar viz `GetStrom`).
ClassMethod Strom(IdFa As %String, mlineIdFa As %String = {..#MlineIdFa}) As %DynamicObject
{
    s oblasti = ##class(ALVA.TemplateDesigner.API.AgendyRegistr).OblastiPrace()
    s ostatni = ##class(ALVA.TemplateDesigner.API.AgendyRegistr).OblastOstatni()
    f i = 1:1:$ll(oblasti) s znama($lg($lg(oblasti, i), 1)) = ""
    s oblasti = oblasti_$lb(ostatni)

    // agendy(oblast, agenda) = název; poradi(oblast, název, agenda) a varianty(agenda, název varianty, kód) řadí podle názvu
    d ##class(ALVA.TemplateDesigner.API.AgendyRegistr).TridyAgend(.tridy)
    s trida = ""
    f {
        s trida = $o(tridy(trida))  q:trida=""
        s code = $parameter(trida, "AgendaVariantCode")
        continue:code=""
        continue:$parameter(trida, "Released")'=1
        s oblast = $parameter(trida, "OblastPrace")
        s:(oblast="")||'$d(znama(oblast)) oblast = $lg(ostatni, 1)
        s agenda = $parameter(trida, "Agenda")
        s:agenda="" agenda = code
        s nazev = $parameter(trida, "AgendaLabel")
        s varianta = $parameter(trida, "VariantLabel")
        s:varianta="" varianta = $parameter(trida, "Variant")
        s agendy(oblast, agenda) = nazev
        s poradi(oblast, $zcvt(nazev, "L")_" ", agenda) = ""
        s varianty(agenda, $zcvt(varianta, "L")_" ", code) = varianta
    }
    // Obecná agenda nemá třídu (návrhář ji vkládá sám); její šablony jsou statické, bez dat.
    s agendy("OBECNE", "GENERAL") = "Obecná (bez dat)"
    s poradi("OBECNE", "obecná (bez dat) ", "GENERAL") = ""
    s varianty("GENERAL", " ", "GENERAL") = ""

    s strom = ##class(%DynamicObject).%New()
    s strom.oblasti = ##class(%DynamicArray).%New()
    f i = 1:1:$ll(oblasti) {
        s oblast = $lg($lg(oblasti, i), 1)
        continue:'$d(agendy(oblast))
        s uzelOblasti = ##class(%DynamicObject).%New()
        s uzelOblasti.code = oblast
        s uzelOblasti.label = $lg($lg(oblasti, i), 2)
        s uzelOblasti.agendy = ##class(%DynamicArray).%New()
        s klic = ""
        f {
            s klic = $o(poradi(oblast, klic))  q:klic=""
            s agenda = ""
            f {
                s agenda = $o(poradi(oblast, klic, agenda))  q:agenda=""
                d uzelOblasti.agendy.%Push(..UzelAgendy(IdFa, agenda, agendy(oblast, agenda), .varianty, mlineIdFa))
            }
        }
        d strom.oblasti.%Push(uzelOblasti)
    }
    q strom
}

/// Uzel agendy s variantami seřazenými podle názvu.
ClassMethod UzelAgendy(IdFa As %String, agenda As %String, nazev As %String, ByRef varianty, mlineIdFa As %String) As %DynamicObject
{
    s uzel = ##class(%DynamicObject).%New()
    s uzel.code = agenda
    s uzel.label = nazev
    s uzel.varianty = ##class(%DynamicArray).%New()
    s klic = ""
    f {
        s klic = $o(varianty(agenda, klic))  q:klic=""
        s code = ""
        f {
            s code = $o(varianty(agenda, klic, code), 1, nazevVarianty)  q:code=""
            s uzelVarianty = ##class(%DynamicObject).%New()
            s uzelVarianty.code = code
            s uzelVarianty.label = nazevVarianty
            d uzelVarianty.%Set("pocet", ..PocetSablon(IdFa, code, mlineIdFa), "number")
            d uzelVarianty.%Set("tiskne", ..JeTisknutelna(code), "boolean")
            s uzelVarianty.vychozi = ..Vychozi(IdFa, code, mlineIdFa)
            d uzel.varianty.%Push(uzelVarianty)
        }
    }
    q uzel
}

/// Počet šablon agendy, které firma vidí (vlastní i M-line).
ClassMethod PocetSablon(IdFa As %String, agendaCode As %String, mlineIdFa As %String) As %Integer
{
    s pocet = 0
    s vetve = ##class(ALVA.TemplateDesigner.API.Sablony).VetveFirmy(IdFa, mlineIdFa)
    f iVetev = 1:1:$ll(vetve) {
        k ids
        d ##class(ALVA.TemplateDesigner.API.Sablony).IdSablon($lg(vetve, iVetev), agendaCode, .ids)
        s id = ""
        f {
            s id = $o(ids(id))  q:id=""
            s pocet = pocet + 1
        }
    }
    q pocet
}

/// Agenda se tiskne z modulu, a smí proto mít výchozí šablonu pro tisk: má třídu agendy. Obecná (bez dat) ji nemá.
ClassMethod JeTisknutelna(agendaCode As %String) As %Boolean
{
    q ##class(ALVA.TemplateDesigner.API.AgendyRegistr).GetClassForCode(agendaCode)'=""
}

/// Šablona, kterou tisk agendy vezme bez volby volajícího, jako {templateId, name, zdroj}; bez ní prázdný objekt.
/// Agenda, která se netiskne z modulu (Obecná), výchozí šablonu nemá.
ClassMethod Vychozi(IdFa As %String, agendaCode As %String, mlineIdFa As %String) As %DynamicObject
{
    s vychozi = ##class(%DynamicObject).%New()
    q:'..JeTisknutelna(agendaCode) vychozi
    s sablona = ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaProTisk(IdFa, agendaCode, "", mlineIdFa)
    q:sablona.templateId="" vychozi
    s vychozi.templateId = sablona.templateId
    s vychozi.name = sablona.name
    s vychozi.zdroj = sablona.zdroj
    q vychozi
}

}
