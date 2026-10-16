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
///   values: PUVODNI_DATUM_PLATNOSTI_OD/DO, PUVODNI_POPIS_PLATNOSTI, ZRUSENE_SPOJE,
///           POROVNANI_HLASENI (proč se neporovnávalo; prázdné = porovnáno nebo bez porovnání)
///   rows.SPOJE[]: VERZE ("puvodni" / "nova" / "" beze změny), ROZDILY: [0/1 zarovnané na CASY]
///                 - nový i zrušený spoj má ROZDILY samé 1 (celý sloupec v barvě verze, jako
///                   ve vzoru z CIS JŘ); zrušený stojí tam, kde byl v původní verzi (`B192a`)
///   rows.SKUPINY_DNU: navíc skupiny, které má jen zrušený spoj
///   rows.POZNAMKY[]: poznámky obou verzí (`B192b`), VERZE "puvodni" / "nova" / "" stejná v obou;
///                 původní podoba značky stojí těsně před novou
///   rows.SPOJE[] se stejnými časy a jinými značkami (`B192c`) - jeden sloupec bez VERZE, navíc:
///                 PLATNOST obou verzí (původní na konci), PLATNOST_VERZE ["" / "nova" / "puvodni"]
///                 zarovnané na PLATNOST, NZ_PUVODNI (negativní značka původní verze, liší-li se)
///                 a NZ_VERZE ("nova", když NZ má jen nová verze)
/// Barvu rozdílu určuje šablona (prvek Tabulka spojů), data říkají jen, která buňka se liší.
/// Do 16. 10. se zrušené spoje jen vypsaly poznámkou „Zrušené spoje: …" (druh 10) - ta už nevzniká,
/// ZRUSENE_SPOJE zůstává.
///
/// Zatím vědomě: porovnávají se jen časy spojů (ne platnost ani značky) a obě verze musí mít
/// stejné zastávky (rozhodnutí Jan 9. 10.). Když porovnání nejde, vytiskne se vybraná verze
/// a důvod nese POROVNANI_HLASENI - chyba by místo náhledu jen překážela.
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
        d ..AddField(fields, "POROVNANI_HLASENI", "Proč se neporovnávalo", "text")

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
        s pLin.description = "Číslo linky původní verze. Prázdné = táž linka jako vybraná. Bez skupiny i linky se tiskne jen vybraná verze. Patří k vybrané lince - při výběru jiné se vyprázdní."
        // Hodnota patří vybrané lince: návrhář ji nepamatuje a při výběru jiné linky vyprázdní -
        // jinak by z minula ukazovala na jinou trasu (nález Jan 9. 10.).
        s pLin.perInstance = 1
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

        // Porovnání, které nejde udělat (srovnávací skupina linku nemá, jiná trasa), sestavu
        // neshodí: vytiskne se vybraná verze a důvod jde do POROVNANI_HLASENI, které šablona ukáže
        // v hlavičce. Parametr typicky zůstal z jiné linky a chyba místo náhledu jen překážela
        // (nález Jan 9. 10.).
        s hlaseni = ""
        s skuNova = $g(vstup("SKUPINA_LINEK_ID")), linNova = $g(vstup("LINKA_ID"))
        i linNova = "" s linNova = $g(vstup("INSTANCE_ID"))
        i (skuPuvodni '= "") || (linPuvodni '= "") {
            i skuPuvodni = "" s skuPuvodni = skuNova
            i linPuvodni = "" s linPuvodni = linNova
            s linPuvodni = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).NormalizLinku(linPuvodni)
            i $d(^[dataDB]JRL(skuPuvodni, linPuvodni, "I")) {
                d ..NactiStranky(.sys, .vstup, skuPuvodni, linPuvodni, .puvodni)
            } else {
                s hlaseni = "Neporovnáno: linka "_(+linPuvodni)_" ve skupině "_skuPuvodni_" není."
            }
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
                    s hlaseni = "Neporovnáno: linka "_(+linPuvodni)_" ve skupině "_skuPuvodni_" má jiné zastávky než linka "_(+linNova)_" (skupina "_skuNova_")."
                }
            }
            d stranka.%Get("values").%Set("POROVNANI_HLASENI", hlaseni)
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
/// obě s příznakem verze a se seznamem odlišných buněk. Nový spoj má barvu nové verze celý sloupec,
/// zrušený celý sloupec barvu původní a stojí tam, kde byl v původní verzi. Spoj beze změny
/// zůstane, jak je.
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

    // Pořadí ve výsledku drží klíč po tisících, aby šel zrušený spoj vložit mezi dva sousední.
    s poradi = 0
    s it = radkyNove.%Get("SPOJE").%GetIterator()
    while it.%GetNext(.k, .spoj) {
        s klic = ..KlicSpoje(spoj)
        i '$d(indexPuvodnich(klic)) {
            d spoj.%Set("VERZE", "nova")
            d spoj.%Set("ROZDILY", ..CelySloupec(spoj))
            s poradi = poradi + 1000, serazene(poradi) = spoj
            continue
        }
        s spojPuvodni = spojePuvodni.%Get(indexPuvodnich(klic))
        s rozdily = ..Rozdily(spoj, spojPuvodni, .pocet)
        i 'pocet d ..ZnackyVerzi(spoj, spojPuvodni)
        i pocet {
            d spojPuvodni.%Set("VERZE", "puvodni")
            d spojPuvodni.%Set("ROZDILY", rozdily)
            d spoj.%Set("VERZE", "nova")
            d spoj.%Set("ROZDILY", ##class(%DynamicArray).%FromJSON(rozdily.%ToJSON()))
            s poradi = poradi + 1000, serazene(poradi) = spojPuvodni
        }
        s poradi = poradi + 1000, serazene(poradi) = spoj
        s poradiSparovanych(klic) = poradi
    }

    // Zrušené spoje - v původní verzi jsou, v nové ne. Stojí za předchozím spojem téže skupiny
    // dnů a směru z původní verze, jinak před prvním spojem té skupiny, jinak na konci. Prvek
    // kreslí skupinu dnů jako souvislý úsek, takže spoj mimo ni by dostal cizí nadpis.
    s it = spojePuvodni.%GetIterator()
    while it.%GetNext(.k, .spoj) {
        s klic = ..KlicSpoje(spoj), skupina = ..KlicSkupiny(spoj)
        i $d(poradiSparovanych(klic)) { s posledniVeSkupine(skupina) = poradiSparovanych(klic) continue }

        d spoj.%Set("VERZE", "puvodni")
        d spoj.%Set("ROZDILY", ..CelySloupec(spoj))
        i $d(posledniVeSkupine(skupina)) {
            s misto = posledniVeSkupine(skupina) + 1
        } else {
            s misto = ..PrvniVeSkupine(.serazene, skupina)
            s misto = $s(misto '= "": misto - 500, 1: $o(serazene(""), -1) + 1000)
        }
        s serazene(misto) = spoj, posledniVeSkupine(skupina) = misto
        s zrusene(+spoj.%Get("CISLO_SPOJE")) = ""
        s:spoj.%Get("SKUPINA")'="" skupinyZrusenych(spoj.%Get("SKUPINA")) = ""
    }

    s spoje = ##class(%DynamicArray).%New(), misto = ""
    f {
        s misto = $o(serazene(misto), 1, spoj)
        q:misto=""
        d spoje.%Push(spoj)
    }
    d radkyNove.%Set("SPOJE", spoje)
    d ..PrevezmiSkupinyDnu(radkyNove, radkyPuvodni, .skupinyZrusenych)
    d ..SlucPoznamky(radkyNove, radkyPuvodni)

    // Číslo jen jednou, i když spoj byl ve víc skupinách dnů.
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
    q nova
}

/// Klíč pro spárování spoje mezi verzemi: směr ~ číslo spoje ~ skupina dnů.
ClassMethod KlicSpoje(spoj As %DynamicObject) As %String
{
    q spoj.%Get("SMER")_"~"_spoj.%Get("CISLO_SPOJE")_"~"_spoj.%Get("SKUPINA")
}

/// Skupina dnů spoje v jeho směru - souvislý úsek sloupců, do kterého se zrušený spoj vkládá.
ClassMethod KlicSkupiny(spoj As %DynamicObject) As %String
{
    q spoj.%Get("SMER")_"~"_spoj.%Get("SKUPINA")
}

/// Pořadí prvního spoje skupiny dnů ve sloučeném seznamu; prázdné, když tam skupina není.
ClassMethod PrvniVeSkupine(ByRef serazene, skupina As %String) As %String
{
    s misto = "", nalezeno = ""
    f {
        s misto = $o(serazene(misto), 1, spoj)
        q:misto=""
        i ..KlicSkupiny(spoj) = skupina s nalezeno = misto q
    }
    q nalezeno
}

/// Skupiny dnů, které má jen zrušený spoj, převezme z původní verze i s nadpisem - jinak by prvek
/// nad jeho sloupcem nenapsal, ve které dny jezdil.
ClassMethod PrevezmiSkupinyDnu(radkyNove As %DynamicObject, radkyPuvodni As %DynamicObject, ByRef skupinyZrusenych)
{
    q:'$d(skupinyZrusenych)
    s nove = radkyNove.%Get("SKUPINY_DNU"), puvodni = radkyPuvodni.%Get("SKUPINY_DNU")
    q:'$isobject(puvodni)
    i '$isobject(nove) s nove = ##class(%DynamicArray).%New() d radkyNove.%Set("SKUPINY_DNU", nove)
    s it = nove.%GetIterator()
    while it.%GetNext(.k, .skupina) { s maNova(skupina.%Get("ID")) = "" }
    s it = puvodni.%GetIterator()
    while it.%GetNext(.k, .skupina) {
        s id = skupina.%Get("ID")
        continue:(id = "")||$d(maNova(id))||('$d(skupinyZrusenych(id)))
        d nove.%Push(skupina)
    }
}

/// Spoj se stejnými časy, kterému se změnily značky (`B192c`): zůstane jeden sloupec, jen nese
/// i značky původní verze a u každé, ke které verzi patří - prvek je obarví přímo ve sloupci. Dvojice
/// sloupců jen kvůli značce by tabulku rozšířila, aniž by ukázala víc (vzor z CIS JŘ to tak má).
ClassMethod ZnackyVerzi(spoj As %DynamicObject, puvodni As %DynamicObject)
{
    s nove = spoj.%Get("PLATNOST"), stare = puvodni.%Get("PLATNOST")
    i '$isobject(nove) s nove = ##class(%DynamicArray).%New()
    i '$isobject(stare) s stare = ##class(%DynamicArray).%New()
    s it = nove.%GetIterator()
    while it.%GetNext(.k, .znak) { s:znak'="" veNove(znak) = "" }
    s it = stare.%GetIterator()
    while it.%GetNext(.k, .znak) { s:znak'="" veStare(znak) = "" }

    s znacky = ##class(%DynamicArray).%New(), verze = ##class(%DynamicArray).%New(), zmena = 0
    s it = nove.%GetIterator()
    while it.%GetNext(.k, .znak) {
        s jenNova = (znak '= "") && '$d(veStare(znak))
        d znacky.%Push(znak), verze.%Push($s(jenNova: "nova", 1: ""))
        s:jenNova zmena = 1
    }
    s it = stare.%GetIterator()
    while it.%GetNext(.k, .znak) {
        continue:(znak = "")||$d(veNove(znak))
        d znacky.%Push(znak), verze.%Push("puvodni")
        s zmena = 1, jenStara(znak) = ""
    }
    i zmena {
        d spoj.%Set("PLATNOST", znacky), spoj.%Set("PLATNOST_VERZE", verze)
        // Volný text zákazníka z původní verze se musí kreslit písmem i u nového spoje.
        s textove = puvodni.%Get("TEXTOVE_ZNACKY")
        i $isobject(textove) {
            s cil = spoj.%Get("TEXTOVE_ZNACKY")
            i '$isobject(cil) s cil = ##class(%DynamicArray).%New() d spoj.%Set("TEXTOVE_ZNACKY", cil)
            s it = textove.%GetIterator()
            while it.%GetNext(.k, .znak) { d:(znak '= "")&&$d(jenStara(znak)) cil.%Push(znak) }
        }
    }

    s nzNova = spoj.%Get("NZ"), nzStara = puvodni.%Get("NZ")
    q:nzNova=nzStara
    d:nzStara'="" spoj.%Set("NZ_PUVODNI", nzStara)
    d:nzNova'="" spoj.%Set("NZ_VERZE", "nova")
}

/// Poznámky obou verzí do legendy (`B192b`). Poznámka stejná v obou zůstane jednou bez verze.
/// Ostatní dostanou verzi: původní podoba značky stojí těsně před novou (jako dvojice spoje),
/// původní poznámka, jejíž značka v nové verzi není, za poslední poznámkou svého druhu.
/// Bez toho by legenda nevysvětlila značku, kterou nese jen původní nebo zrušený spoj.
ClassMethod SlucPoznamky(radkyNove As %DynamicObject, radkyPuvodni As %DynamicObject)
{
    s nove = radkyNove.%Get("POZNAMKY"), puvodni = radkyPuvodni.%Get("POZNAMKY")
    q:'$isobject(puvodni)
    i '$isobject(nove) s nove = ##class(%DynamicArray).%New()

    s it = nove.%GetIterator()
    while it.%GetNext(.k, .pozn) { s veNove(..KlicPoznamky(pozn)) = "" }
    s it = puvodni.%GetIterator(), zbyva = 0
    while it.%GetNext(.k, .pozn) {
        s klic = ..KlicPoznamky(pozn), vePuvodni(klic) = ""
        continue:$d(veNove(klic))
        d pozn.%Set("VERZE", "puvodni")
        s zbyva = zbyva + 1, jenPuvodni(zbyva) = pozn
    }

    s poradi = 0
    s it = nove.%GetIterator()
    while it.%GetNext(.k, .pozn) {
        i '$d(vePuvodni(..KlicPoznamky(pozn))) {
            d pozn.%Set("VERZE", "nova")
            s znacka = ..ZnackaPoznamky(pozn)
            f i=1:1:zbyva {
                continue:'$d(jenPuvodni(i))
                continue:(..ZnackaPoznamky(jenPuvodni(i)) '= znacka)
                s poradi = poradi + 1000, serazene(poradi) = jenPuvodni(i)
                k jenPuvodni(i)
            }
        }
        s poradi = poradi + 1000, serazene(poradi) = pozn, posledniDruhu(..DruhPoznamky(pozn)) = poradi
    }
    f i=1:1:zbyva {
        continue:'$d(jenPuvodni(i))
        s druh = ..DruhPoznamky(jenPuvodni(i))
        s misto = $s($d(posledniDruhu(druh)): posledniDruhu(druh) + 1, 1: $o(serazene(""), -1) + 1000)
        s serazene(misto) = jenPuvodni(i), posledniDruhu(druh) = misto
    }

    s vysledek = ##class(%DynamicArray).%New(), misto = ""
    f {
        s misto = $o(serazene(misto), 1, pozn)
        q:misto=""
        d vysledek.%Push(pozn)
    }
    d radkyNove.%Set("POZNAMKY", vysledek)
}

/// Celá poznámka pro porovnání verzí. Text jde jako kontrolní součet a délka - tarifní poznámky
/// bývají delší, než unese index lokálního pole.
ClassMethod KlicPoznamky(pozn As %DynamicObject) As %String
{
    s text = pozn.%Get("TEXT")
    q ..ZnackaPoznamky(pozn)_"~"_$zcrc($zcvt(text, "O", "UTF8"), 7)_"~"_$l(text)
}

/// Značka poznámky v jejím druhu - podle ní se páruje původní a nová podoba.
ClassMethod ZnackaPoznamky(pozn As %DynamicObject) As %String
{
    q ..DruhPoznamky(pozn)_"~"_pozn.%Get("TYP")_"~"_pozn.%Get("ZNAK")
}

ClassMethod DruhPoznamky(pozn As %DynamicObject) As %String
{
    q "~"_pozn.%Get("DRUH")
}

/// Celý sloupec v barvě verze (nový nebo zrušený spoj) - 1 u každé buňky, i mimo trasu.
ClassMethod CelySloupec(spoj As %DynamicObject) As %DynamicArray
{
    s rozdily = ##class(%DynamicArray).%New()
    f i=1:1:spoj.%Get("CASY").%Size() d rozdily.%Push(1, "number")
    q rozdily
}

/// Které buňky spoje se liší od jeho původní podoby (1 = liší se), zarovnané na CASY.
ClassMethod Rozdily(spoj As %DynamicObject, puvodni As %DynamicObject, ByRef pocet As %Integer) As %DynamicArray
{
    s pocet = 0
    s casy = spoj.%Get("CASY"), casyPuvodni = puvodni.%Get("CASY")
    s rozdily = ##class(%DynamicArray).%New()
    f i=0:1:casy.%Size()-1 {
        s jinak = (casy.%Get(i) '= casyPuvodni.%Get(i))
        d rozdily.%Push(jinak, "number")
        s pocet = pocet + jinak
    }
    // Původní podoba s víc řádky (nemělo by nastat - zastávky jsou stejné) se počítá jako rozdíl.
    i casyPuvodni.%Size() > casy.%Size() s pocet = pocet + 1
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
