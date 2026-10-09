Include GENERAL

/// Agenda: **Linkový JŘ AD - rozdílový** (`B192`) - dvě verze téže linky v jedné tabulce, aby bylo
/// na papíře vidět, co se změnilo (objížďková proti kmenové, nová proti platné). Předloha je výtisk
/// zákazníka: změněný spoj stojí dvakrát vedle sebe, původní a nový, a odlišné časy jsou podbarvené;
/// spoj beze změny je jednou.
///
/// **Nová verze = vybraná instance** (skupina a linka jako u linkového JŘ), **původní** se zadá
/// parametry „Porovnat se skupinou" / „Porovnat s linkou" (prázdné = táž skupina / táž linka).
/// Bez obou parametrů se tiskne jen nová verze, jako běžný linkový JŘ.
///
/// Data staví beze změny `LinkovyJrAd` (volá se dvakrát) a tahle třída jen sloučí spoje. Je to
/// samostatná přímá podtřída `BaseAgenda`, protože registr agend hledá jen přímé potomky
/// (`AgendyRegistr.ListAgendy`).
///
/// Wire navíc proti linkovému JŘ (jen přidané klíče, R3):
///   values: PUVODNI_DATUM_PLATNOSTI_OD/DO, PUVODNI_POPIS_PLATNOSTI, ZRUSENE_SPOJE
///   rows.SPOJE[]: VERZE ("puvodni" / "nova" / "" beze změny), ROZDILY: [0/1 zarovnané na CASY]
///   rows.POZNAMKY: poznámka „Zrušené spoje: …" (druh 10), když nějaké jsou
/// Barvu rozdílu určuje šablona (prvek Tabulka spojů), data říkají jen, která buňka se liší.
///
/// Zatím vědomě: porovnávají se jen časy spojů (ne platnost ani značky) a obě verze musí mít
/// stejné zastávky - jinak sestava skončí srozumitelnou chybou (rozhodnutí Jan 9. 10.).
Class ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

/// ⚠️ Stabilní persistovaný klíč (šablony, preference, launch kontrakt) - nikdy neměnit.
Parameter AgendaVariantCode As %String = "LINKOVY_JR_AD_ROZDILOVY";

Parameter Agenda As %String = "LINKOVY_JR";

Parameter AgendaLabel As %String = "Linkový JŘ";

Parameter Variant As %String = "AD_ROZDILOVY";

Parameter VariantLabel As %String = "AD - rozdílový (porovnání dvou verzí)";

/// Zatím jen na ukázku a pro vývoj - uživatelům se nenabízí. Spuštění ze shellu s kódem agendy
/// ji otevře i tak (launch kontrakt filtr zveřejnění nemá).
Parameter Released = 0;

Parameter InstanceParamName As %String = "LINKA_ID";

Parameter ElementKind As %String = "LINE";

Parameter InstanceLabel As %String = "linka";

Parameter InstanceLabelPlural As %String = "linky";

/// Schéma linkového JŘ + platnost původní verze, zrušené spoje a verze spoje.
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        k ^||ML("OUT")
        d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetSchema(.sys)
        s def = ##class(%DynamicObject).%FromJSON($g(^||ML("OUT",1)))
        k ^||ML("OUT")

        s fields = def.%Get("fields")
        d ..AddField(fields, "PUVODNI_DATUM_PLATNOSTI_OD", "Platnost od (původní verze)", "date")
        d ..AddField(fields, "PUVODNI_DATUM_PLATNOSTI_DO", "Platnost do (původní verze)", "date")
        d ..AddField(fields, "PUVODNI_POPIS_PLATNOSTI", "Popis platnosti (původní verze)", "text")
        d ..AddField(fields, "ZRUSENE_SPOJE", "Zrušené spoje", "text")

        s it = def.%Get("fieldCollections").%GetIterator()
        while it.%GetNext(.k, .kolekce) {
            continue:kolekce.%Get("collectionKey")'="SPOJE"
            d ..AddField(kolekce.%Get("fields"), "VERZE", "Verze spoje (původní / nová)", "text")
        }

        s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetTableTemplates(.sys)
}

ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetTableTemplate(.sys)
}

ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetAvailableSkupiny(.sys)
}

ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetAvailableInstances(.sys)
}

ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetMockData(.sys)
}

/// Parametry linkového JŘ, před nimi dva, kterými se zadá původní verze. Patří do „Obecné" -
/// je to nastavení celé sestavy a ta skupina musí být první (AgendaParamGroupsDriftTests).
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        k ^||ML("OUT")
        d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetParameters(.sys)
        s linkove = ##class(%DynamicArray).%FromJSON($g(^||ML("OUT",1)))
        k ^||ML("OUT")

        s arr = ##class(%DynamicArray).%New()
        s pSku = ##class(%DynamicObject).%New()
        s pSku.name = "POROVNAT_SKUPINU"
        s pSku.label = "Porovnat se skupinou JŘ"
        s pSku.type = "text"
        s pSku.group = "Obecné"
        s pSku.serverSide = 1
        s pSku.defaultValue = ""
        s pSku.description = "Číslo skupiny jízdních řádů, ve které je původní verze linky. Prázdné = táž skupina jako vybraná linka."
        d arr.%Push(pSku)

        s pLin = ##class(%DynamicObject).%New()
        s pLin.name = "POROVNAT_LINKU"
        s pLin.label = "Porovnat s linkou"
        s pLin.type = "text"
        s pLin.group = "Obecné"
        s pLin.serverSide = 1
        s pLin.defaultValue = ""
        s pLin.description = "Číslo linky původní verze. Prázdné = táž linka jako vybraná. Bez skupiny i linky se tiskne jen vybraná verze."
        d arr.%Push(pLin)

        // Převzaté parametry mají skupiny u sebe (kontroluje je drift test nad LinkovyJrAd), proto
        // se přidávají přes %Set - %Push by test četl jako parametr této třídy bez skupiny.
        s it = linkove.%GetIterator()
        while it.%GetNext(.k, .prevzaty) { d arr.%Set(arr.%Size(), prevzaty) }

        s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Stránky nové verze (vybraná instance) sloučené se stránkami původní verze.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    k vstup m vstup = ^||ML("IN")
    try {
        $$$FillParams

        s skuPuvodni = $zstrip($g(^||ML("IN","POROVNAT_SKUPINU")), "<>W")
        s linPuvodni = $zstrip($g(^||ML("IN","POROVNAT_LINKU")), "<>W")

        d ..NactiStranky(.sys, .vstup, "", "", .nove)

        s skuNova = $g(vstup("SKUPINA_LINEK_ID")), linNova = $g(vstup("LINKA_ID"))
        i linNova = "" s linNova = $g(vstup("INSTANCE_ID"))
        i (skuPuvodni '= "") || (linPuvodni '= "") {
            i skuPuvodni = "" s skuPuvodni = skuNova
            i linPuvodni = "" s linPuvodni = linNova
            // Linka ve skupině chybět může (parametr zůstal z jiné linky) - říct to rovnou, ne
            // až chybou stavby jízdního řádu.
            s linPuvodni = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).NormalizLinku(linPuvodni)
            i '$d(^[dataDB]JRL(skuPuvodni, linPuvodni, "I")) {
                $$$ThrowStatus($$$ERROR($$$GeneralError, "Původní verze: linka "_(+linPuvodni)_" ve skupině "_skuPuvodni_" není. Zkontrolujte parametry Porovnat se skupinou JŘ a Porovnat s linkou."))
            }
            d ..NactiStranky(.sys, .vstup, skuPuvodni, linPuvodni, .puvodni)
        }

        k ^||ML("IN") m ^||ML("IN") = vstup
        k ^||ML("OUT")
        f i=1:1:$g(nove) {
            s stranka = nove(i)
            s protejsek = ..ProtejsiStranka(stranka, i, .puvodni)
            i protejsek '= "" {
                try {
                    s stranka = ..SlucStranky(stranka, protejsek)
                } catch exSlouceni {
                    // Hláška musí říct, co s čím se porovnávalo - jinak parametr, který zůstal
                    // z jiné linky, vypadá jako chyba porovnání (nález Jan 9. 10.).
                    $$$ThrowStatus($$$ERROR($$$GeneralError, "Linka "_(+linNova)_" (skupina "_skuNova_") a původní verze - linka "_(+linPuvodni)_" (skupina "_skuPuvodni_") - mají jiné zastávky. Porovnání zatím umí jen dvě verze se stejnou trasou."))
                }
            }
            s ^||ML("OUT", $i(^||ML("OUT"))) = stranka.%ToJSON()
        }
    } catch (ex) {
        k ^||ML("IN") m ^||ML("IN") = vstup
        k ^||ML("OUT")
        $$$CatchErrRest
    }
    ret $$$OK
}

/// Postaví stránky linkového JŘ pro zadanou skupinu a linku (prázdné = jak přišly ve vstupu).
/// Chybu linkového JŘ (např. linka ve skupině není) vyhodí znovu s označením původní verze.
ClassMethod NactiStranky(ByRef sys As %String, ByRef vstup, sku As %String, lin As %String, ByRef stranky)
{
    k stranky, ^||ML("IN"), ^||ML("OUT"), ^||ML("ERR")
    m ^||ML("IN") = vstup
    i sku '= "" s ^||ML("IN","SKUPINA_LINEK_ID") = sku
    i lin '= "" s ^||ML("IN","LINKA_ID") = lin, ^||ML("IN","INSTANCE_ID") = lin

    d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)

    s chyba = $g(^||ML("ERR"))
    k ^||ML("ERR")
    i chyba '= "" {
        // Tvar „1~<err>CRLF<popis výjimky>" - <err> bývá prázdné, text je až za CRLF.
        s text = $zstrip($replace($e(chyba, $f(chyba, "~"), $l(chyba)), $c(13,10), " "), "<>W")
        i $e(text, 1, 13) = "ERROR #5001: " s text = $e(text, 14, $l(text))
        i (sku '= "") || (lin '= "") s text = "Původní verze (skupina "_sku_", linka "_lin_"): "_text
        $$$ThrowStatus($$$ERROR($$$GeneralError, text))
    }

    s idx = ""
    f {
        s idx = $o(^||ML("OUT", idx), 1, json)
        q:idx=""
        s stranky($i(stranky)) = ##class(%DynamicObject).%FromJSON(json)
    }
    k ^||ML("OUT")
}

/// Stránka původní verze ke stránce nové: přednostně táž pozice, pokud má týž směr; jinak první
/// stránka téhož směru. Žádná = nic se nesrovnává.
ClassMethod ProtejsiStranka(stranka As %DynamicObject, poradi As %Integer, ByRef puvodni) As %DynamicObject
{
    q:'$g(puvodni) ""
    s smer = stranka.%Get("values").%Get("SMER")
    i $d(puvodni(poradi)) && (puvodni(poradi).%Get("values").%Get("SMER") = smer) q puvodni(poradi)
    s nalezena = ""
    f i=1:1:puvodni {
        i puvodni(i).%Get("values").%Get("SMER") = smer s nalezena = puvodni(i) q
    }
    q nalezena
}

/// Sloučí spoje nové stránky s původní: změněný spoj dostane vlevo vedle sebe svou původní podobu,
/// obě s příznakem verze a se seznamem odlišných buněk; nový spoj je celý odlišný; spoj, který
/// v nové verzi chybí, se jen vypíše. Spoj beze změny zůstane, jak je.
/// Spoj se páruje podle směru, čísla a skupiny dnů - týž spoj ve dvou skupinách dnů je dvakrát.
ClassMethod SlucStranky(nova As %DynamicObject, puvodni As %DynamicObject) As %DynamicObject
{
    s radkyNove = nova.%Get("rows"), radkyPuvodni = puvodni.%Get("rows")
    i '..StejneZastavky(radkyNove.%Get("ZASTAVKY"), radkyPuvodni.%Get("ZASTAVKY")) {
        $$$ThrowStatus($$$ERROR($$$GeneralError, "Verze linky se liší v zastávkách - porovnání zatím umí jen dvě verze se stejnou trasou."))
    }

    s spojePuvodni = radkyPuvodni.%Get("SPOJE")
    s it = spojePuvodni.%GetIterator()
    while it.%GetNext(.k, .spoj) { s indexPuvodnich(..KlicSpoje(spoj)) = k }

    s spoje = ##class(%DynamicArray).%New()
    s it = radkyNove.%Get("SPOJE").%GetIterator()
    while it.%GetNext(.k, .spoj) {
        s klic = ..KlicSpoje(spoj)
        i '$d(indexPuvodnich(klic)) {
            d spoj.%Set("VERZE", "nova")
            d spoj.%Set("ROZDILY", ..Rozdily(spoj, "", .pocet))
            d spoje.%Push(spoj)
            continue
        }
        s sparovane(klic) = ""
        s spojPuvodni = spojePuvodni.%Get(indexPuvodnich(klic))
        s rozdily = ..Rozdily(spoj, spojPuvodni, .pocet)
        i 'pocet { d spoje.%Push(spoj) continue }

        d spojPuvodni.%Set("VERZE", "puvodni")
        d spojPuvodni.%Set("ROZDILY", rozdily)
        d spoj.%Set("VERZE", "nova")
        d spoj.%Set("ROZDILY", ##class(%DynamicArray).%FromJSON(rozdily.%ToJSON()))
        d spoje.%Push(spojPuvodni), spoje.%Push(spoj)
    }
    d radkyNove.%Set("SPOJE", spoje)

    // Zrušené spoje - v původní verzi jsou, v nové ne. Číslo jen jednou, i když spoj byl ve víc
    // skupinách dnů.
    s klic = ""
    f {
        s klic = $o(indexPuvodnich(klic))
        q:klic=""
        continue:$d(sparovane(klic))
        s zrusene(+$p(klic, "~", 2)) = ""
    }
    s seznam = "", cislo = ""
    f {
        s cislo = $o(zrusene(cislo))
        q:cislo=""
        s seznam = seznam_$s(seznam = "": "", 1: ", ")_cislo
    }

    s hodnoty = nova.%Get("values"), puvodniHodnoty = puvodni.%Get("values")
    d hodnoty.%Set("PUVODNI_DATUM_PLATNOSTI_OD", puvodniHodnoty.%Get("DATUM_PLATNOSTI_OD"))
    d hodnoty.%Set("PUVODNI_DATUM_PLATNOSTI_DO", puvodniHodnoty.%Get("DATUM_PLATNOSTI_DO"))
    d hodnoty.%Set("PUVODNI_POPIS_PLATNOSTI", puvodniHodnoty.%Get("POPIS_PLATNOSTI"))
    d hodnoty.%Set("ZRUSENE_SPOJE", seznam)

    i seznam '= "" {
        s poznamky = radkyNove.%Get("POZNAMKY")
        i '$isobject(poznamky) s poznamky = ##class(%DynamicArray).%New() d radkyNove.%Set("POZNAMKY", poznamky)
        s pozn = ##class(%DynamicObject).%New()
        d pozn.%Set("DRUH", 10)
        d pozn.%Set("TYP", "text")
        d pozn.%Set("ZNAK", "")
        d pozn.%Set("TEXT", "Zrušené spoje: "_seznam)
        d poznamky.%Push(pozn)
    }
    q nova
}

/// Klíč pro spárování spoje mezi verzemi: směr ~ číslo spoje ~ skupina dnů.
ClassMethod KlicSpoje(spoj As %DynamicObject) As %String
{
    q spoj.%Get("SMER")_"~"_spoj.%Get("CISLO_SPOJE")_"~"_spoj.%Get("SKUPINA")
}

/// Které buňky spoje se liší od jeho původní podoby (1 = liší se), zarovnané na CASY. U nového
/// spoje (bez původní podoby) se liší každá buňka, ve které něco je.
ClassMethod Rozdily(spoj As %DynamicObject, puvodni, ByRef pocet As %Integer) As %DynamicArray
{
    s pocet = 0
    s casy = spoj.%Get("CASY")
    s casyPuvodni = $s($isobject(puvodni): puvodni.%Get("CASY"), 1: "")
    s rozdily = ##class(%DynamicArray).%New()
    f i=0:1:casy.%Size()-1 {
        s cas = casy.%Get(i)
        i $isobject(casyPuvodni) { s jinak = (cas '= casyPuvodni.%Get(i)) }
        else { s jinak = (cas '= "") }
        d rozdily.%Push(jinak, "number")
        s pocet = pocet + jinak
    }
    // Původní podoba s víc řádky (nemělo by nastat - zastávky jsou stejné) se počítá jako rozdíl.
    i $isobject(casyPuvodni) && (casyPuvodni.%Size() > casy.%Size()) s pocet = pocet + 1
    q rozdily
}

/// Mají obě verze tytéž řádky zastávek (stejné zastávky v témž pořadí a stejného druhu)?
ClassMethod StejneZastavky(nove As %DynamicArray, puvodni As %DynamicArray) As %Boolean
{
    q:('$isobject(nove))||('$isobject(puvodni)) 0
    q:nove.%Size()'=puvodni.%Size() 0
    s stejne = 1
    f i=0:1:nove.%Size()-1 {
        s a = nove.%Get(i), b = puvodni.%Get(i)
        i (a.%Get("CZST") '= b.%Get("CZST")) || (a.%Get("TYP") '= b.%Get("TYP")) s stejne = 0 q
    }
    q stejne
}

}
