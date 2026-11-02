/// Parametry sestavy sdílené agendami jízdních řádů (`B56` S2, `architektonicka-rozhodnuti-agendy.md` R8).
/// Každý parametr je tu definovaný jednou; agenda si v `GetParameters` vybere, které nabídne a v jakém
/// pořadí, a tím drží i pořadí v panelu. Jen parametry, které používají aspoň dvě agendy (pravidlo
/// druhého použití); parametr jedné agendy zůstává v ní.
/// ⚠️ Texty voleb enum parametrů jsou zamčené: persistuje se zobrazovaný text (`ux-conventions §8`).
/// Jiný zobrazovaný popisek se posílá vedle v `optionLabels` (vzor `AddVolbyRozdeleniSloupcu`).
Class ALVA.TemplateDesigner.Agendy.Jr.Parametry Extends %RegisteredObject
{

/// Parametr sestavy „Barvy spojů" agend AD jako volba: „Tisknout", „Netisknout", nebo prázdno =
/// podle linky. Návrhář posílá checkbox `TISKNOUT_BARVY_SPOJU`; starší návrhář u linkového JŘ místo
/// něj třístav `BARVY_SPOJU` (Automaticky (dle linky) / Tisknout / Netisknout), který se přebírá.
/// Checkbox nahradil třístav 26. 10. (rozhodnutí Jan): nastavení u linky nemá v datech zapnuté
/// žádná linka (sonda `BarvySpojuVyskyt`), takže volba „dle linky" nebyla k ničemu.
/// Do 30. 10. v `BaseAgenda` (`B56` S13); o tisku pak rozhodne `Jr.DataAd.TiskBarevSpoju`.
ClassMethod VolbaBarevSpoju() As %String
{
    s zaskrtnuto = $zcvt($g(^||ML("IN","TISKNOUT_BARVY_SPOJU")),"L")
    i zaskrtnuto '= "" q $s(zaskrtnuto = "true":"Tisknout", 1:"Netisknout")
    q $g(^||ML("IN","BARVY_SPOJU"))
}

/// Parametr „Tvar názvu zastávky" (KOVED #13089) - jedna definice pro vývěsku i linkový JŘ AD, aby se
/// nabízel stejně jako sdílené `Jr.DataAd.FormatNazvuZastavky`. Liší se jen výchozí tvar: vývěska
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

/// Volby parametru „Rozdělení sloupců podle platnosti", který má vývěska AD i linkový JŘ AD (`P2`).
/// `vzory` = `$lb($lb(klíč, uložený text, …), …)` z agendy. Uložený text je zamčený (ukládá se zobrazená
/// volba) a každá agenda ho má jinak; uživatel vidí popisek z `PopisekVzoruSloupcu` v `optionLabels`, takže
/// tatáž volba se v obou agendách jmenuje stejně a popisek jde přejmenovat bez ztráty uložených voleb.
/// Starší návrhář `optionLabels` nezná a ukáže uložené texty. Klíč vzoru (`optionKeys`) najde vzor v `PRESETY_SLOUPCU`.
ClassMethod AddVolbyRozdeleniSloupcu(param As %DynamicObject, vzory As %List, sAutomatickou As %Boolean)
{
    s volby = ##class(%DynamicArray).%New(), klice = ##class(%DynamicArray).%New(), popisky = ##class(%DynamicArray).%New()
    i sAutomatickou d volby.%Push("Automaticky (dle linky)"), klice.%Push(""), popisky.%Push("Automaticky (dle linky)")
    f i=1:1:$ll(vzory) {
        s klic = $li($li(vzory,i),1), text = $li($li(vzory,i),2)
        s popisek = ..PopisekVzoruSloupcu(klic)
        d volby.%Push(text), klice.%Push(klic), popisky.%Push($s(popisek'="":popisek, 1:text))
    }
    // „Vlastní rozložení" je ukazatel stavu šablony, ne volba: objeví se, když si uživatel sloupce upraví
    // u prvku. Bez něj by parametr neměl co hlásit a musel by lhát některým ze vzorů.
    d volby.%Push("Vlastní rozložení"), klice.%Push(""), popisky.%Push("Vlastní rozložení")
    d param.%Set("options", volby)
    d param.%Set("optionKeys", klice)
    d param.%Set("optionLabels", popisky)
}

/// Popisek vzoru rozdělení sloupců, stejný ve všech agendách (`P2`); prázdný = vzor bez společného popisku.
/// Popisky se neukládají, přejmenovat je jde.
ClassMethod PopisekVzoruSloupcu(klic As %String) As %String
{
    s popisek("BEZ_ROZLISENI")    = "Bez rozlišení"
    s popisek("PRAC_SO_NE")       = "Pracovní dny / Soboty / Neděle+svátky"
    s popisek("PRAC_VIKEND")      = "Pracovní dny / Soboty+Neděle+svátky"
    s popisek("PRAZDNINY_SO_NE")  = "Pracovní dny mimo prázdniny / o prázdninách / Soboty / Neděle+svátky"
    s popisek("PRAZDNINY_VIKEND") = "Pracovní dny mimo prázdniny / o prázdninách / Soboty+Neděle+svátky"
    q $g(popisek(klic))
}

/// Šipka směru a projeté zastávky v Seznamu zastávek, společné vývěskám AD i MHD. Obě volby
/// aplikuje návrhář, proto bez `serverSide`.
ClassMethod AddSipkyAProjeteZastavky(arr As %DynamicArray)
{
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true",  "Seznam zastávek", "V seznamu zastávek se od aktuální zastávky nakreslí šipka ve směru jízdy. Barvu a stranu šipky nastavíte u prvku Seznam zastávek.")
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false", "Seznam zastávek", "V seznamu zastávek se vytisknou i zastávky, které spoj před aktuální zastávkou už projel. Vypnuto = seznam začíná aktuální zastávkou.")
}

/// Volby variant tras (KOVED #13048 bod 5), společné vývěskám AD i MHD - počítá je `Jr.VariantyTras`.
/// Týkají se variantních sloupců seznamu zastávek, proto je klient bez toho sloupce schová (`JenSeSloupcemVariant`).
/// Písmena variant a rozdílů tras jsou dvě nezávislé řady; výchozí sady se v AD nekryjí (`Jr.VariantyTras.SadaVariantDefault`).
ClassMethod AddVariantyTras(arr As %DynamicArray)
{
    s pRazV = ##class(%DynamicObject).%New()
    s pRazV.name         = "RAZENI_VARIANT"
    s pRazV.label        = "Řazení variant tras"
    s pRazV.type         = "enum"
    s pRazV.group        = "Seznam zastávek"
    s pRazV.serverSide   = 1
    s pRazV.defaultValue = "Dle četnosti ze zastávky"
    s pRazV.description  = "Pořadí variantních časových sloupců v seznamu zastávek: nejčastější varianta z dané zastávky první, stabilní pořadí dle spojů (stejné napříč stránkami), od nejrychlejší varianty k nejpomalejší, nebo od nejpomalejší k nejrychlejší."
    s optsRV = ##class(%DynamicArray).%New()
    d optsRV.%Push("Dle četnosti ze zastávky")
    d optsRV.%Push("Stabilní (pořadí spojů)")
    // Popisky voleb jsou po vydání ZAMČENÉ (persistuje se zobrazovaný text), proto „nejkratší první"
    // rovnou v popisku - směr řazení už nepůjde upřesnit přejmenováním.
    d optsRV.%Push("Dle délky jízdy (nejkratší první)")
    d optsRV.%Push("Dle délky jízdy (nejdelší první)")
    s pRazV.options = optsRV
    d ..JenSeSloupcemVariant(pRazV)
    d arr.%Push(pRazV)

    // `B181`: hned pod řazením, protože ořezává podle něj.
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddNumberParam(arr, "MAX_SLOUPCU_VARIANT", "Nejvýš sloupců variant tras", "", "Seznam zastávek", "Kolik sloupců variant tras se nejvýš vytiskne. Prázdné = všechny. Vezmou se první podle řazení variant, ostatní se netisknou - jejich spoje pak ve sloupcích nemají svou trasu ani písmeno varianty. Zúží to seznam zastávek za cenu té informace.", 1)
    d ..JenSeSloupcemVariant(arr.%Get(arr.%Size() - 1))

    // `B142`: vědomá výměna „méně sloupců za méně informace", proto výchozí vypnuto. Popisek i
    // nápověda musí říct, CO to schová - jinak si to zapne někdo, kdo variantní sloupce nasadil
    // právě kvůli detailu (KOVED #13048).
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, "SLUCOVAT_PODMNOZINY", "Slučovat varianty, které jedou jen část jiné", "false", "Seznam zastávek", "Varianta, která jede jen souvislý úsek jiné trasy (od - do), dostane sloupec té delší. Ubere to sloupce na lince, kde část spojů jezdí jen kus trasy. Varianta, která uprostřed zajíždí jinam a část zastávek vynechá, si sloupec nechá - jinak by z vývěsky zmizelo, že tam spoj nejede.", 1)
    d ..JenSeSloupcemVariant(arr.%Get(arr.%Size() - 1))

    // ⚠️ Následující dvě volby musí zůstat vedle sebe a v tomhle pořadí - obě jsou o písmenech
    // variant a slučování mezi ně nepatří (nález Jan 14. 9.).
    // Zapínání písmen variant se 17. 8. zrušilo (bez nich nešlo u minuty poznat, který časový
    // sloupec pro ni platí) a 11. 9. se na žádost TRANSDEV vrátilo: vývěska, která varianty
    // vysvětluje jinde, je nepotřebuje. Hodnotu aplikuje KLIENT (vyprázdní záhlaví sloupců
    // a zhasne anotaci u minuty), takže do allowlistu server-side parametrů nepatří - server
    // písmena posílá dál a nic se kvůli té volbě znovu nenačítá.
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, "PISMENA_VARIANT", "Písmena variant tras", "true", "Seznam zastávek", "Označení variantních sloupců jízdních dob - v záhlaví sloupců i u minut odjezdů. Vypnuto = sloupce s časy zůstanou, jen bez písmen. Netýká se písmen rozdílů tras, ta se vypínají u linky.")
    d ..JenSeSloupcemVariant(arr.%Get(arr.%Size() - 1))

    s pPrvB = ##class(%DynamicObject).%New()
    s pPrvB.name         = "PRVNI_BEZ_PISMENE"
    s pPrvB.label        = "První varianta bez písmene"
    s pPrvB.type         = "bool"
    s pPrvB.defaultValue = "true"
    s pPrvB.group        = "Seznam zastávek"
    s pPrvB.serverSide   = 1
    s pPrvB.description  = "Nejčastější varianta zůstane bez písmene (hlavní trasa), odchylky se značí od A. Vypnuto = písmena už od první varianty."
    // Bez písmen variant tato volba nemá co ovlivnit - schovat ji je míň matoucí než ji nechat
    // svítit jako mrtvé zaškrtávátko.
    s pPrvB.visibleWhen  = "PISMENA_VARIANT=true"
    d ..JenSeSloupcemVariant(pPrvB)
    d arr.%Push(pPrvB)
}

/// Parametr variant tras má účinek jen se sloupcem Varianty tras v seznamu zastávek: bez něj se
/// varianty netisknou ani u minut. Klient takový parametr schová (rozhodnutí Jan 5. 10.); klíč
/// `visibleWhenColumn` je aditivní, starší klient ho ignoruje a parametr ukáže jako dřív.
ClassMethod JenSeSloupcemVariant(param As %DynamicObject)
{
    d param.%Set("visibleWhenColumn", "CAS_JIZDY_VARIANTY")
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
