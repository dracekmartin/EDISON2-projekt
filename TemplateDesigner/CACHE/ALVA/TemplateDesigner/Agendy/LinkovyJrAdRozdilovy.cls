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
///                 a NZ_VERZE ("nova", když NZ má jen nová verze); DOPRAVCI_PUVODNI (`B192f`) =
///                 dopravci původní verze, když se liší od DOPRAVCI
/// Barvu rozdílu určuje šablona (prvek Tabulka spojů), data říkají jen, která buňka se liší.
/// Do 16. 10. se zrušené spoje jen vypsaly poznámkou „Zrušené spoje: …" (druh 10) - ta už nevzniká,
/// ZRUSENE_SPOJE zůstává.
///
///   rows.ZASTAVKY[]: zastávky obou verzí jedním seznamem (`B192d`), VERZE "puvodni" / "nova"
///                 u zastávky jen v jedné verzi; PORADI přečíslované od 1, přípoje spojů
///                 přečíslované podle verze spoje; CASY a ZNACKY spojů zarovnané na tenhle seznam
///                 - TC_PUVODNI / ZONY_PUVODNI (`B192e`): Tč a pásmo původní verze u každé
///                   zastávky; zastávka jen v původní verzi má TC a ZONY prázdné (nová verze ji nemá)
///
/// Když porovnání nejde (srovnávací skupina linku nemá), vytiskne se vybraná verze a důvod nese
/// POROVNANI_HLASENI - chyba by místo náhledu jen překážela. Do 16. 10. tak skončila i jiná trasa.
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
            s klic = kolekce.%Get("collectionKey")
            d:klic="SPOJE" ..AddField(kolekce.%Get("fields"), "VERZE", "Verze spoje (původní / nová)", "text")
            continue:klic'="ZASTAVKY"
            d ..AddField(kolekce.%Get("fields"), "TC_PUVODNI", "Tarifní číslo (původní verze)", "number")
            d ..AddField(kolekce.%Get("fields"), "ZONY_PUVODNI", "Zóny (původní verze)", "text")
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
/// je to nastavení celé sestavy a ta skupina musí být první (AgendaParameterDriftTests).
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

        // Porovnání, které nejde udělat (srovnávací skupina linku nemá, sloučení selže), sestavu
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
                    s hlaseni = "Neporovnáno: linku "_(+linPuvodni)_" ze skupiny "_skuPuvodni_" se nepodařilo sloučit s linkou "_(+linNova)_" (skupina "_skuNova_"): "_exSlouceni.DisplayString()
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
    // Zastávky obou verzí jedním seznamem (`B192d`); časy všech spojů se na něj přerovnají, takže
    // dál se porovnává buňka po buňce jako u stejné trasy.
    d ..SlucZastavky(radkyNove, radkyPuvodni, nova.%Get("values").%Get("SMER"), .mapa)
    s it = radkyNove.%Get("SPOJE").%GetIterator()
    while it.%GetNext(.k, .spoj) { d ..Prerovnej(spoj, "n", .mapa) }
    s spojePuvodni = radkyPuvodni.%Get("SPOJE")
    s it = spojePuvodni.%GetIterator()
    while it.%GetNext(.k, .spoj) { d ..Prerovnej(spoj, "o", .mapa) }

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
        s rozdily = ..Rozdily(spoj, spojPuvodni, .pocet, .mapa)
        i 'pocet d ..ZnackyVerzi(spoj, spojPuvodni), ..DoplnPuvodni(spoj, spojPuvodni, .mapa), ..DopravciVerzi(spoj, spojPuvodni)
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

/// Spoj v jednom sloupci, kterému se změnil dopravce (`B192f`): nese i dopravce původní verze,
/// aby hlavička porovnala dopravce obou verzí. Na skutečných datech dopravce v jedné verzi
/// obvykle chybí úplně (CB_CSADAD 0320167 a 0132400, skupina 141 proti 127).
ClassMethod DopravciVerzi(spoj As %DynamicObject, puvodni As %DynamicObject)
{
    s nove = spoj.%Get("DOPRAVCI"), stare = puvodni.%Get("DOPRAVCI")
    i '$isobject(stare) s stare = ##class(%DynamicArray).%New()
    s textNove = $s($isobject(nove): nove.%ToJSON(), 1: "[]")
    q:textNove=stare.%ToJSON()
    d spoj.%Set("DOPRAVCI_PUVODNI", stare)
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

/// Které buňky spoje se liší od jeho původní podoby (1 = liší se), zarovnané na sloučené CASY.
/// Porovnávají se jen zastávky, které mají obě verze - zastávka jen v jedné verzi dvojici nedělá,
/// má barvu celý řádek.
ClassMethod Rozdily(spoj As %DynamicObject, puvodni As %DynamicObject, ByRef pocet As %Integer, ByRef mapa) As %DynamicArray
{
    s pocet = 0
    s casy = spoj.%Get("CASY"), casyPuvodni = puvodni.%Get("CASY")
    s rozdily = ##class(%DynamicArray).%New()
    f k=0:1:casy.%Size()-1 {
        s spolecny = ($g(mapa("o", k)) '= "") && ($g(mapa("n", k)) '= "")
        s jinak = spolecny && (casy.%Get(k) '= casyPuvodni.%Get(k))
        d rozdily.%Push(jinak, "number")
        s pocet = pocet + jinak
    }
    q rozdily
}

/// Spoj v jednom sloupci (na společných zastávkách beze změny) nese u zastávky, kterou má jen
/// původní verze, svůj původní čas - jako vzor FTL (spoj 17 u zrušené Olomouc,Nedvězí).
ClassMethod DoplnPuvodni(spoj As %DynamicObject, puvodni As %DynamicObject, ByRef mapa)
{
    s casy = spoj.%Get("CASY"), casyPuvodni = puvodni.%Get("CASY")
    s znacky = spoj.%Get("ZNACKY"), znackyPuvodni = puvodni.%Get("ZNACKY")
    f k=0:1:$g(mapa("K"))-1 {
        continue:$g(mapa("n", k))'=""
        d casy.%Set(k, casyPuvodni.%Get(k))
        d:$isobject(znacky)&&$isobject(znackyPuvodni) znacky.%Set(k, znackyPuvodni.%Get(k))
    }
}

/// Sloučí zastávky obou verzí do jednoho seznamu (`B192d`): nejdelší společná posloupnost řádků
/// zůstane společná, ostatní dostanou VERZE "puvodni" / "nova" a stojí mezi společnými sousedy.
/// V úseku, kde se verze liší, jde ve směru tam původní před novou a zpět obráceně - vzor FTL čte
/// trasu v opačném směru odzadu, takže fyzicky totéž místo stojí v obou směrech stejně.
/// Pořadí zastávek se přečísluje od 1; příjezdový a odjezdový řádek téže zastávky ho sdílejí.
/// mapa("K") = počet řádků, mapa("o"/"n", k) = index řádku v původní / nové verzi ("" = tam není),
/// mapa("po"/"pn", pořadí) = sloučené pořadí zastávky dané verze.
ClassMethod SlucZastavky(radkyNove As %DynamicObject, radkyPuvodni As %DynamicObject, smer As %String, ByRef mapa)
{
    k mapa
    s mapa("K") = 0, mapa("P") = 0
    s nove = radkyNove.%Get("ZASTAVKY"), stare = radkyPuvodni.%Get("ZASTAVKY")
    i '$isobject(nove) s nove = ##class(%DynamicArray).%New()
    i '$isobject(stare) s stare = ##class(%DynamicArray).%New()
    s n = stare.%Size(), m = nove.%Size()
    f i=1:1:n s a(i) = ..KlicZastavky(stare.%Get(i-1))
    f j=1:1:m s b(j) = ..KlicZastavky(nove.%Get(j-1))

    // Délka nejdelší společné posloupnosti od (i, j) do konce.
    f i=n:-1:1 {
        f j=m:-1:1 {
            i a(i) = b(j) { s lcs(i, j) = $g(lcs(i+1, j+1)) + 1 continue }
            s x = $g(lcs(i+1, j)), y = $g(lcs(i, j+1))
            s lcs(i, j) = $s(x >= y: x, 1: y)
        }
    }

    s vysledek = ##class(%DynamicArray).%New()
    s i = 1, j = 1
    while (i <= n) || (j <= m) {
        i (i <= n) && (j <= m) && ($g(a(i)) = $g(b(j))) {
            d ..VylozUsek(vysledek, stare, nove, .jenStare, .jenNove, smer, .mapa)
            d ..PridejZastavku(vysledek, nove.%Get(j-1), "", i-1, stare.%Get(i-1).%Get("PORADI"), j-1, nove.%Get(j-1).%Get("PORADI"), .mapa)
            s i = i + 1, j = j + 1
            continue
        }
        i (j > m) || ((i <= n) && ($g(lcs(i+1, j)) >= $g(lcs(i, j+1)))) {
            s jenStare($i(jenStare)) = i, i = i + 1
        } else {
            s jenNove($i(jenNove)) = j, j = j + 1
        }
    }
    d ..VylozUsek(vysledek, stare, nove, .jenStare, .jenNove, smer, .mapa)
    d ..PuvodniHodnoty(vysledek, stare, "TC", .mapa)
    d ..PuvodniHodnoty(vysledek, stare, "ZONY", .mapa)
    d radkyNove.%Set("ZASTAVKY", vysledek)
}

/// Doplní sloučeným zastávkám původní Tč nebo pásmo (`pole` → pole_PUVODNI, `B192e`). Přidaná
/// zastávka posune Tč všech dalších (vzor ARRIVA). Posílá se vždy, i beze změny - vzor FTL má
/// původní sloupce i u náhrady zastávky bez posunu (rozhodnutí Jan 17. 10.). Zastávka jen
/// v původní verzi má hodnotu jen v původním sloupci, protože nová verze ji nemá.
ClassMethod PuvodniHodnoty(vysledek As %DynamicArray, stare As %DynamicArray, pole As %String, ByRef mapa)
{
    f k=0:1:mapa("K")-1 {
        s radek = vysledek.%Get(k), i = mapa("o", k)
        d radek.%Set(pole_"_PUVODNI", $s(i '= "": stare.%Get(i).%Get(pole), 1: ""))
        d:(mapa("n", k) = "") radek.%Set(pole, "")
    }
}

/// Vypíše zastávky úseku, ve kterém se verze liší, a úsek vyprázdní.
ClassMethod VylozUsek(vysledek As %DynamicArray, stare As %DynamicArray, nove As %DynamicArray, ByRef jenStare, ByRef jenNove, smer As %String, ByRef mapa)
{
    f krok=1,2 {
        s staraNapred = (smer '= "Z")
        i (krok = 1) = staraNapred {
            f x=1:1:$g(jenStare) {
                s i = jenStare(x)
                d ..PridejZastavku(vysledek, stare.%Get(i-1), "puvodni", i-1, stare.%Get(i-1).%Get("PORADI"), "", "", .mapa)
            }
        } else {
            f x=1:1:$g(jenNove) {
                s j = jenNove(x)
                d ..PridejZastavku(vysledek, nove.%Get(j-1), "nova", "", "", j-1, nove.%Get(j-1).%Get("PORADI"), .mapa)
            }
        }
    }
    k jenStare, jenNove
}

/// Přidá řádek zastávky do sloučeného seznamu a zapíše, odkud je.
ClassMethod PridejZastavku(vysledek As %DynamicArray, radek As %DynamicObject, verze As %String, indexStary As %String, poradiStare As %String, indexNovy As %String, poradiNove As %String, ByRef mapa)
{
    s k = mapa("K"), mapa("o", k) = indexStary, mapa("n", k) = indexNovy, mapa("K") = k + 1
    s cislo = ""
    s:poradiNove'="" cislo = $g(mapa("pn", poradiNove))
    s:(cislo="")&&(poradiStare'="") cislo = $g(mapa("po", poradiStare))
    i cislo = "" s (cislo, mapa("P")) = mapa("P") + 1
    s:poradiNove'="" mapa("pn", poradiNove) = cislo
    s:poradiStare'="" mapa("po", poradiStare) = cislo
    d radek.%Set("PORADI", cislo, "number")
    d:verze'="" radek.%Set("VERZE", verze)
    d vysledek.%Push(radek)
}

/// Klíč řádku zastávky pro sloučení verzí: zastávka a druh řádku (odjezdový / příjezdový).
ClassMethod KlicZastavky(radek As %DynamicObject) As %String
{
    q radek.%Get("CZST")_"~"_radek.%Get("TYP")
}

/// Přerovná časy a značky spoje na sloučený seznam zastávek a přečísluje jeho přípoje.
/// Zastávka, kterou verze spoje nemá, dostane „jede jinudy", když spoj tím úsekem projíždí (má
/// vlastní údaj před ní i za ní), jinak nic - jako vzor z CIS JŘ.
ClassMethod Prerovnej(spoj As %DynamicObject, verze As %String, ByRef mapa)
{
    s casy = spoj.%Get("CASY"), znacky = spoj.%Get("ZNACKY")
    i '$isobject(casy) s casy = ##class(%DynamicArray).%New()
    s prvni = "", posledni = ""
    f k=0:1:mapa("K")-1 {
        s i = $g(mapa(verze, k))
        continue:i=""
        continue:casy.%Get(i)=""
        s:prvni="" prvni = k
        s posledni = k
    }
    s noveCasy = ##class(%DynamicArray).%New(), noveZnacky = ##class(%DynamicArray).%New()
    f k=0:1:mapa("K")-1 {
        s i = $g(mapa(verze, k))
        i i '= "" {
            d noveCasy.%Push(casy.%Get(i))
            d noveZnacky.%Push($s($isobject(znacky): znacky.%Get(i), 1: ""))
            continue
        }
        s jinudy = (prvni '= "") && (k > prvni) && (k < posledni)
        d noveCasy.%Push($s(jinudy: "<", 1: "")), noveZnacky.%Push("")
    }
    d spoj.%Set("CASY", noveCasy)
    d:$isobject(znacky) spoj.%Set("ZNACKY", noveZnacky)

    s pripoje = spoj.%Get("PRIPOJE")
    q:'$isobject(pripoje)
    s it = pripoje.%GetIterator()
    while it.%GetNext(.k, .pr) {
        s poradi = pr.%Get("PORADI")
        continue:poradi=""
        d:$d(mapa("p"_verze, poradi)) pr.%Set("PORADI", mapa("p"_verze, poradi), "number")
    }
}

}
