/// Parametry sestavy sdílené agendami jízdních řádů (`B56` S2, `architektonicka-rozhodnuti-agendy.md` R8).
/// Každý parametr je tu definovaný jednou; agenda si v `GetParameters` vybere, které nabídne a v jakém
/// pořadí, a tím drží i pořadí v panelu. Jen parametry, které používají aspoň dvě agendy (pravidlo
/// druhého použití); parametr jedné agendy zůstává v ní.
/// ⚠️ Popisky voleb enum parametrů jsou zamčené: persistuje se zobrazovaný text (`ux-conventions §8`).
Class ALVA.TemplateDesigner.Agendy.Jr.Parametry Extends %RegisteredObject
{

/// Parametr „Tvar názvu zastávky" (KOVED #13089) - jedna definice pro vývěsku i linkový JŘ AD, aby se
/// nabízel stejně jako sdílené `BaseAgenda.FormatNazvuZastavky`. Liší se jen výchozí tvar: vývěska
/// „Zkrácený" (dosavadní chování, starý vývěskový modul dvojčárku srážel), linkový „Plný CIS" (starý
/// linkový tisk ji nechává; potvrzeno Janem 24. 8.).
ClassMethod ParametrTvaruNazvuZastavky(vychozi As %String) As %DynamicObject
{
    s p = ##class(%DynamicObject).%New()
    s p.name         = "TVAR_NAZVU_ZASTAVKY"
    s p.label        = "Tvar názvu zastávky"
    s p.type         = "enum"
    // „Obecné": platí pro název všude, kde se objeví (zastávky, nadpis, cíl, texty poznámek).
    s p.group        = "Obecné"
    s p.serverSide   = 1
    s p.defaultValue = vychozi
    s p.description  = "Zkrácený: Zlín,aut.nádr. · Typograficky upravený: Zlín, aut. nádr. (mezera za čárkou a tečkou) · Plný CIS zachová dvojčárku podle číselníku zastávek (tak tiskne starý linkový JŘ) včetně okresu a jeho zkratky - pro technické exporty (soutěže) k tomu vypněte i název pro MHD."
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("Zkrácený"), opts.%Push("Typograficky upravený"), opts.%Push("Plný CIS"), opts.%Push("Plný CIS s okresem")
    s p.options = opts
    q p
}

/// Počet stran (#8785): sazba zmenší nejdřív poznámky, pak tabulku, aby se sestava vešla. Řeší ho
/// návrhář při sazbě, ne server - proto bez `serverSide`; cíl čte z čísla na začátku volby a volba
/// bez čísla = bez omezení. „Automaticky" zatím nedělá nic: u linky pole zatím není (rozhodnutí Jan
/// 23. 9.). Jediná definice pro AD i MHD; MHD nastavení u linky nemá a jezdí v něm víc spojů, proto
/// „Bez omezení" místo „Automaticky" a až 3 strany (rozhodnutí Jan 22. 10.).
ClassMethod ParametrPoctuStran(dleLinky As %Boolean = 1, nejvic As %Integer = 2) As %DynamicObject
{
    s pStr = ##class(%DynamicObject).%New()
    s pStr.name         = "POCET_STRAN_SESTAVY"
    s pStr.label        = "Počet stran"
    s pStr.type         = "enum"
    s pStr.group        = "Obecné"
    s pStr.defaultValue = $s(dleLinky:"Automaticky (dle linky)", 1:"Bez omezení")
    s pStr.description  = "Na kolik stran se má vejít každý směr linky. Když se nevejde, zmenší se nejdřív poznámky, pak tabulka."_$s(dleLinky:" Automaticky = podle nastavení u linky (zatím bez omezení).", 1:"")
    s optsStr = ##class(%DynamicArray).%New()
    d optsStr.%Push(pStr.defaultValue)
    f n=1:1:nejvic d optsStr.%Push(n_" "_$s(n=1:"strana", n<5:"strany", 1:"stran"))
    s pStr.options = optsStr
    q pStr
}

/// Zastávky, na kterých žádný spoj nestaví: u AD nastavení u linky (`PATCH,tisknoutJenTcKdeSpojeZastavuji`,
/// zapnuto na 362 linkách) ubírá řádky, takže je z celého inventáře nejvíc vidět; týž třístav jako
/// u sloupce tarifních čísel. Jediná definice pro AD i MHD; `dleLinky` = linka nastavení má a nabízí
/// se „Automaticky (dle linky)" (MHD ho nemá, výchozí je pak Tisknout jako starý tisk MHD).
ClassMethod ParametrZastavkyBezZastaveni(dleLinky As %Boolean = 1) As %DynamicObject
{
    s pBez = ##class(%DynamicObject).%New()
    s pBez.name         = "ZASTAVKY_BEZ_ZASTAVENI"
    s pBez.label        = "Zastávky, kde žádný spoj nestaví"
    s pBez.type         = "enum"
    s pBez.group        = "Tabulka spojů"
    s pBez.serverSide   = 1
    s pBez.defaultValue = $s(dleLinky:"Automaticky (dle linky)", 1:"Tisknout")
    s pBez.description  = "Zda se v tabulce objeví i zastávky, na kterých žádný spoj linky nestaví (jen je projíždí nebo jedou jinudy)."_$s(dleLinky:" Automaticky = podle nastavení u linky v jízdních řádech.", 1:"")
    s optsBez = ##class(%DynamicArray).%New()
    d:dleLinky optsBez.%Push("Automaticky (dle linky)")
    d optsBez.%Push("Tisknout")
    d optsBez.%Push("Netisknout")
    s pBez.options = optsBez
    q pBez
}

/// Zvýraznění zastávky, ze které se uživatel dívá (legacy `arr,5` + `arr,10`). Data řádek jen
/// **označí**, vzhled si řeší šablona pravidlem zvýraznění nad polem „Je aktuální zastávka" - stejný
/// vzor jako aktuální zastávka u vývěsky. Jediná definice pro AD i MHD variantu; `sTarifnimCislem`
/// = agenda nabízí i Zvýraznit tarifní číslo (MHD sloupec Tč nemá) a popis na něj odkáže.
ClassMethod ParametrZvyrazneniZastavky(sTarifnimCislem As %Boolean = 1) As %DynamicObject
{
    s pZvZ = ##class(%DynamicObject).%New()
    s pZvZ.name         = "ZVYRAZNIT_ZASTAVKU"
    s pZvZ.label        = "Zvýraznit zastávku"
    s pZvZ.type         = "string"
    s pZvZ.group        = "Tabulka spojů"
    s pZvZ.serverSide   = 1
    // Věta o městě dává smysl jen u meziměstské linky (AD); MHD jezdí v jednom městě (`B194c` P7).
    s pZvZ.description  = "Kus názvu zastávky, jejíž řádek se má označit jako aktuální a zvýraznit (nerozlišují se velká a malá písmena). Prázdné = nezvýrazňovat nic. "_$s(sTarifnimCislem:"Projíždí-li linka město víckrát, označí se všechny jeho zastávky; jednu z nich vybere Zvýraznit tarifní číslo.", 1:"Odpovídá-li zadaný text víc zastávkám, označí se všechny.")
    q pZvZ
}

/// Směr pro tisk (legacy MHD `arr,11` „SmerID#", Jan 12. 10.): které směry linky se vytisknou.
/// Jediná definice pro AD i MHD variantu - musí se v obou jmenovat a chovat stejně.
ClassMethod ParametrSmeruTisku() As %DynamicObject
{
    s pSmer = ##class(%DynamicObject).%New()
    s pSmer.name         = "SMER_TISKU"
    s pSmer.label        = "Směr pro tisk"
    s pSmer.type         = "enum"
    // „Obecné": rozhoduje o složení celé sestavy (které stránky vyjdou), ne o jednom prvku.
    s pSmer.group        = "Obecné"
    s pSmer.serverSide   = 1
    s pSmer.defaultValue = "Oba"
    // Bez věty o obousměrném tisku: při jednom směru se ta volba sama schová (její visibleWhen).
    s pSmer.description  = "Které směry linky se vytisknou."
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("Oba"), opts.%Push("Tam"), opts.%Push("Zpět")
    s pSmer.options = opts
    q pSmer
}

/// Hodnota parametru `SMER_TISKU` → směry k tisku ("T,Z" / "T" / "Z"). Neznámá hodnota (i starší
/// klient, který parametr nezná) = oba směry, tedy chování před zavedením volby.
ClassMethod SmeryKTisku(volba As %String) As %String
{
    q $case(volba, "Tam":"T", "Zpět":"Z", :"T,Z")
}

/// Pořadí vývěsek v sestavě, společné vývěskám AD i MHD. `B161`: výchozí pořadí je parita se starým
/// tiskem (`JRapiLinPrtMhd1.mac:397-398`, zastávka vnější smyčka, směr vnitřní) - obě vývěsky téže
/// zastávky jdou za sebou, takže se vytisknou a pověsí jedním výjezdem. TRANSDEV chce projít trasu tam
/// a pak zpět; místo přepsání logiky je to volba, protože tvar pořadí se za měsíc měnil potřetí
/// (rozhodnutí Jan 18. 9.).
ClassMethod ParametrPoradiStranek() As %DynamicObject
{
    s pPorStr = ##class(%DynamicObject).%New()
    s pPorStr.name         = "PORADI_STRANEK"
    s pPorStr.label        = "Pořadí stránek"
    s pPorStr.type         = "enum"
    s pPorStr.group        = "Obecné"
    s pPorStr.serverSide   = 1
    s pPorStr.defaultValue = "Po zastávkách (oba směry za sebou)"
    s pPorStr.description  = "V jakém pořadí jdou vývěsky v exportu. Po zastávkách = linka se projde jednou a u každé zastávky jsou obě její vývěsky za sebou (tak to dělá i starý tisk). Po směrech = nejdřív celý směr Tam od první zastávky k poslední, pak celý směr Zpět od poslední k první, takže se trasa projde tam a zase zpátky."
    s optsPS = ##class(%DynamicArray).%New()
    d optsPS.%Push("Po zastávkách (oba směry za sebou)")
    d optsPS.%Push("Po směrech (tam vzestupně, zpět sestupně)")
    s pPorStr.options = optsPS
    q pPorStr
}

/// Šipka směru a projeté zastávky v Seznamu zastávek, společné vývěskám AD i MHD. Obě volby
/// aplikuje návrhář, proto bez `serverSide`.
ClassMethod AddSipkyAProjeteZastavky(arr As %DynamicArray)
{
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true",  "Seznam zastávek", "V seznamu zastávek se od aktuální zastávky nakreslí šipka ve směru jízdy. Barvu a stranu šipky nastavíte u prvku Seznam zastávek.")
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false", "Seznam zastávek", "V seznamu zastávek se vytisknou i zastávky, které spoj před aktuální zastávkou už projel. Vypnuto = seznam začíná aktuální zastávkou.")
}

/// Časová osa Minut odjezdů a pevný rozsah hodin, společné vývěskám AD i MHD. Popisné volby
/// (Plný den vědomě vynechán, lze docílit Vlastním rozsahem 0-23); Hodiny od a do jsou vidět jen
/// u Vlastního rozsahu a zvládnou rozsah přes půlnoc (22-5).
ClassMethod AddCasovaOsaOdjezdu(arr As %DynamicArray)
{
    s param = ##class(%DynamicObject).%New()
    s param.name         = "CASOVA_OSA"
    s param.label        = "Časová osa odjezdů"
    s param.type         = "enum"
    s param.group        = "Minuty odjezdů"
    s param.defaultValue = "Jen hodiny s odjezdy"
    s param.description  = "Které hodiny se vytisknou: jen hodiny s odjezdy, všechny hodiny od první do poslední hodiny s odjezdem (i prázdné), nebo pevný rozsah Hodiny od - Hodiny do."
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("Jen hodiny s odjezdy")
    d opts.%Push("Souvislá od první do poslední")
    d opts.%Push("Vlastní rozsah")
    s param.options = opts
    d arr.%Push(param)

    s pOd = ##class(%DynamicObject).%New()
    s pOd.name         = "HODINY_OD"
    s pOd.label        = "Hodiny od"
    s pOd.type         = "number"
    s pOd.group        = "Minuty odjezdů"
    s pOd.defaultValue = "4"
    s pOd.description  = "První vytištěná hodina (0-23)."
    s pOd.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pOd)

    s pDo = ##class(%DynamicObject).%New()
    s pDo.name         = "HODINY_DO"
    s pDo.label        = "Hodiny do"
    s pDo.type         = "number"
    s pDo.group        = "Minuty odjezdů"
    s pDo.defaultValue = "23"
    s pDo.description  = "Poslední vytištěná hodina (0-23). Menší než Hodiny od = rozsah přes půlnoc."
    s pDo.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pDo)
}

/// Roztažení Minut odjezdů na výšku prvku, společné vývěskám AD i MHD: řádky hodin a písmo se zvětší,
/// když je hodin méně, než prvek pojme; záhlaví skupin dnů a sloupec hodin zůstávají.
ClassMethod AddRoztazeniNaVysku(arr As %DynamicArray)
{
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, "ROZTAHNOUT_NA_VYSKU", "Roztáhnout na výšku", "false", "Minuty odjezdů", "Když je hodin s odjezdy méně, než se do prvku vejde, řádky hodin i písmo se zvětší, aby prvek vyplnily. Záhlaví zůstává.")
}

}
