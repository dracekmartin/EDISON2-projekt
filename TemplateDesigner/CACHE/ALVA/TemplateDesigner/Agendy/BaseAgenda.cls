Include GENERAL

/// Abstraktní základ pro implementaci agendy v TemplateDesigneru: kontrakt, výchozí metody a sdílené
/// helpery. Agenda = zdroj dat a doménová logika jednoho typu sestavy (zastávkový a linkový JŘ AD a MHD).
///
/// Povinné (Abstract, bez nich třída neprojde překladem):
/// - GetSchema           - nabídka dat: skalární pole + tabulkové kolekce
/// - GetData             - data pro tisk, stránka po stránce
/// - GetMockData         - ukázka pro náhled bez vybrané instance
///
/// Volitelné (výchozí = prázdný výsledek):
/// - GetParameters       - parametry sestavy (panel parametrů, tiskový dialog)
/// - GetAvailableSkupiny, GetAvailableInstances - výběr instancí (skupiny linek, linky)
///
/// Předpřipravené tabulky (`GetTableTemplates` / `GetTableTemplate`) jsou od 30. 10. zrušené: návrhář
/// je nikde nenabízel (`B56` S10). Hotové bloky do šablony má pokrýt `B66` (vlastní bloky prvků).
///
/// Každá metoda s `ByRef sys` dodržuje vzor z `cache-api.md` (pravidla 14-15: `s err = ""`, `try`/`catch`
/// s `$$$CatchErrRest`, `ret $$$OK`); hlídá ho `OsetreniChybAgendTests`. Vzorové agendy jsou JŘ.
///
/// Registrace agendy (2026-05-20; dvouosý model 2026-07-11):
///   Agenda se registruje **deklarativně** přes class parametry - kombinovaný klíč
///   AgendaVariantCode + osy Agenda/Variant (+ labely). ALVA.TemplateDesigner.API.AgendyRegistr
///   prochází %Dictionary, najde všechny subclassy BaseAgenda a vrátí jejich metadata
///   jako JSON list. Vytvoření třídy = registrace konfigurace (agenda × varianta).
///
///   GENERAL agenda (šablona bez DB vazby) nemá Caché class - .NET strana ji inject
///   syntheticky v IAgendaRegistry impl.
Class ALVA.TemplateDesigner.Agendy.BaseAgenda Extends %RegisteredObject [ Abstract ]
{

/// Kombinovaný kód konfigurace agenda+varianta (např. "ZASTAVKOVY_JR_AD", "LINKOVY_JR_MHD").
/// Override v každé subclass. **Stabilní persistovaný klíč** - žije v uložených šablonách
/// (Template JSON `agendaCode`), DB indexu ByAgenda, per-uživatel preferencích a EDISON
/// launch kontraktu (`Agenda=<kód>`); hodnoty se NIKDY nemění (jen aditivně přibývají).
/// Konvence: <AGENDA>[_<VARIANTA>], velkými písmeny, podtržítka.
Parameter AgendaVariantCode As %String = "";

/// Kód agendy = nejvyšší úroveň členění ("ZASTAVKOVY_JR", "LINKOVY_JR", "GENERAL").
/// Dvouosý model (2026-07-11): agenda (co za sestavu dělám) × varianta (datový zdroj).
/// Zrcadlí legacy: TiskMHD/TiskJR = typ jako aplikace + skrytý přepínač VolanoZ (JRMAIN×MHD);
/// budoucí RG agendy (Fakturace, MHD provozní, Mzdy…) přibudou jako další agendy bez variant.
Parameter Agenda As %String = "";

/// User-facing label AGENDY (např. "Zastávkový JŘ", "Linkový JŘ") - bez varianty.
/// Override v každé subclass. (Do 2026-07-11 nesl kombinovaný label vč. "(AD)" -
/// kombinovaný label pro starší klienty počítá AgendyRegistr do wire klíče `label`.)
Parameter AgendaLabel As %String = "";

/// Kód varianty agendy = datový zdroj ("AD", "MHD"); "" = agenda bez variant (budoucí agendy report generátoru).
/// Legacy ekvivalent: přepínač VolanoZ (JRMAIN×MHD) v TiskMHD/TiskJR.
Parameter Variant As %String = "";

/// User-facing label varianty (např. "AD (linková doprava)", "MHD"); "" = bez varianty.
Parameter VariantLabel As %String = "";

/// Zveřejněná pro uživatele (1) vs interní/pilotní (0, default). Nezveřejněná agenda se
/// nenabízí v UI výběru (Změnit agendu, filtr šablon), ale zůstává plně funkční - existující
/// šablony s jejím kódem se otevřou, EDISON launch funguje. Zveřejnění = vědomý krok (přepnout
/// na 1), nová agenda „neuteče" k uživatelům omylem.
Parameter Released As %Boolean = 0;

/// Jméno IN parametru GetData, kterým se předává vybraná instance (linka/faktura).
/// Zastávkové a knižní JŘ = "LINKA_ID", ostatní default "INSTANCE_ID". Okno ho čte
/// z registru (AgendaRegistryEntry) → žádné hardcoded větvení dle kódu agendy.
Parameter InstanceParamName As %String = "INSTANCE_ID";

/// Agenda čte vlastní definici sloupců ze šablony (`DEFINICE_SLOUPCU`, `ParseDefiniceSloupcu`).
/// Návrhář podle toho ukáže záložku „Vlastní definice sloupců" v okně Minut odjezdů; u agendy,
/// která ji nečte, by nic nedělala (vývěska MHD, rozhodnutí Jan 24. 10.: koncept AD).
Parameter CustomColumns As %Boolean = 0;

/// Agenda posílá u zastávek časy variant tras (`TRASA_MIN`). Návrhář podle toho nabídne v Seznamu
/// zastávek sloupec „Varianty tras"; bez nich by zůstal prázdný (vývěska MHD, `B194d` E5). Až MHD
/// varianty dostane (backlog), stačí přepnout tady.
Parameter VariantyTras As %Boolean = 0;

/// Agenda posílá v jedné tabulce dvě verze linky a značí, co je jen v původní a co jen v nové
/// (rozdílový linkový JŘ). Návrhář podle toho nabídne v Tabulce spojů a Poznámkách barvy rozdílů;
/// jinde by nic neobarvily. Do 30. 10. to návrhář poznal podle kódu agendy (`B194g` R4).
Parameter RozdilyVerzi As %Boolean = 0;

/// Druh JŘ elementů, které agenda podporuje - řídí kontextové menu/paletu okna.
/// "STOP" = zastávkové (StopList/DepartureGrid/NotesLegend), "LINE" = knižní/linkové
/// (LineTable), "" = bez JŘ kontextu (okno ukáže všechny). C# vlastní mapování druh→typy.
Parameter ElementKind As %String = "";

/// User-facing název „instance" agendy (jedn./mn. č., 1. pád) - okno ho zobrazí místo
/// obecného „instance" (JŘ = „linka"/„linky", faktury = „faktura"/„faktury"). Override
/// v subclass. Default „instance" pro agendu bez doménového pojmu.
Parameter InstanceLabel As %String = "instance";

Parameter InstanceLabelPlural As %String = "instance";

/// Vrátí DataSchema pro agendu: skalární pole (fields) + tabulkové kolekce (fieldCollections).
/// OUT[1] = JSON objekt:
///   { "fields": [...], "fieldCollections": [...] }
/// fields       = skalární pole; každá hodnota existuje jednou na celou sestavu
/// fieldCollections = kolekce sad polí; každá kolekce tvoří zdroj řádků pro Dynamic tabulku
ClassMethod GetSchema(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí data pro tisk: skalární hodnoty + kolekce řádků.
/// IN:  parametry závislé na agendě (ID záznamu, datum, ...)
/// OUT[1..N] = stejný JSON formát jako GetMockData, ale s reálnými daty.
///             Každý OUT node = jedna samostatná stránka:
///               { "values": { "FIELD": "value", ... }, "rows": { "COLL": [...] } }
///             Agendy s jediným výstupem zapisují pouze OUT[1].
///             Multi-page agendy (např. zastávkový JŘ MHD: 1 stránka per zastávka × směr)
///             iterují a zapisují do OUT[$i(^||ML("OUT"))].
ClassMethod GetData(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí parametry sestavy dostupné pro tuto agendu.
/// Výchozí implementace vrátí prázdný seznam - přepsat v konkrétní agendě.
/// OUT[1] = JSON pole:
///   [
///     { "name": "TISK_SIPKY",  "label": "Tisknout šipky",  "type": "bool",   "defaultValue": "true" },
///     { "name": "FORMAT_CASU", "label": "Formát času",      "type": "enum",   "defaultValue": "H:mm",
///       "options": ["H:mm", "HH:mm"] }
///   ]
/// Typy: "bool", "enum", "string", "number", "date"
/// defaultValue je vždy string - C# strana konvertuje dle type.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        s ^||ML("OUT", $i(^||ML("OUT"))) = "[]"
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vrátí seznam dostupných instancí agendy (např. linky u agend JŘ).
/// Slouží pro Instance Picker v TD - uživatel vybere 1-N instancí, nad kterými pracuje.
/// 
/// Výchozí implementace vrací prázdný seznam - agenda bez konceptu instancí ho nemusí přepisovat.
/// 
/// IN["search"] = volitelný filtr (substring, case-insensitive)
/// IN["limit"]  = max počet řádků (default 50)
/// OUT["1"] = JSON pole:
///   [
///     { "value": "42", "display": "42 - Praha–Brno", "subtitle": "Platnost od 1.1.2024" },
///     ...
///   ]
/// 
/// value    = opaque ID předaný do GetData (IN["LINKA_ID"] u MHD)
/// display  = hlavní zobrazovaný text (číslo + název)
/// subtitle = volitelný menší text pod displayem (platnost, dopravce, atd.)
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        s ^||ML("OUT","1") = "[]"
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vrátí seznam skupin linek pro filtr v Instance Pickeru (vybere se právě jedna → načtou se její
/// instance přes GetAvailableInstances(SKUPINA_LINEK_ID)). OUT[1] = JSON [{ "value": id, "label": název }].
/// Default = prázdné pole → agenda nemá koncept skupin (picker filtr skryje). JR agendy override.
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        s ^||ML("OUT","1") = "[]"
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vrátí ukázková (mock) data pro live preview v designeru.
/// OUT[1] = JSON objekt:
///   {
///     "values": { "FIELD": "value", ... },
///     "rows": {
///       "COLLECTION_KEY": [ { "FIELD": "value", ... }, ... ],
///       ...
///     }
///   }
ClassMethod GetMockData(ByRef sys As %String) As %Status [ Abstract ]
{
}

// ── Helper metody pro agendy ─────────────────────────────────────────────────

/// Přidá jedno pole do JSON array polí.
/// arr     = %DynamicArray kam se přidává
/// name    = název pole (např. "CISLO_FAKTURY")
/// label   = popisek (např. "Číslo faktury")
/// type    = typ pole: "text", "number", "date", "time", "dateTime", "boolean"
/// example = (volitelné) příklad hodnoty
ClassMethod AddField(arr As %DynamicArray, name As %String, label As %String, type As %String, example As %String = "")
{
    s field = ##class(%DynamicObject).%New()
    s field.name  = name
    s field.label = label
    s field.type  = type
    if example '= "" {
        s field.example = example
    }
    d arr.%Push(field)
}

/// Barva spoje AD z legacy tvaru `r;g;b` na `#AARRGGBB` pro SkiaSharp; prázdno, když barva není zadaná.
/// **`0;0;0` je u spojů AD „nenastaveno", ne černá** (2 185 160 z 2 189 806 spojů s barvou, 30. 10.) - kdyby
/// se posílalo, dostal by každý spoj výslovnou černou a nešlo by poznat obarvený spoj od neobarveného.
///
/// ⚠️ `RgbToHex` ve vývěskách NENÍ kopie téhož: barva linky a barvy poznámek MHD berou `0;0;0` jako
/// černou, protože tam je to skutečná volba (rozhodnutí Jan 30. 10., `legacy-nahrada.md §4`).
ClassMethod RgbNaHex(rgb As %String) As %String
{
    q:rgb="" ""
    q:rgb="0;0;0" ""
    s hex = "0123456789ABCDEF"
    s out = "#FF"
    f i=1:1:3 {
        s n = +$p(rgb,";",i)
        i n < 0 s n = 0
        i n > 255 s n = 255
        s out = out_$e(hex,(n\16)+1)_$e(hex,(n#16)+1)
    }
    q out
}

/// Nadpisy skupin dnů AD z nastavení firmy, jako starý zastávkový tisk (`JRapiLinPrtMhd1.mac:541-571`):
/// věta `^JRSET("Definice","WinMHD",rec,"DSC")` (kusy `~`) podle `rozlozeni` - 0 = pracovní / soboty /
/// neděle a svátky (kusy 2-4), 1 = prázdninová trojice (5-7), 2 = pracovní / víkend (8-9),
/// 3 = prázdninová čtveřice (10-13). Linka může mít vlastní sadu (`PATCH,ISY_LinkyTiskMHDNadpisy` →
/// `^JRSET("LinkyTiskMHDNadpisy",sada,"R<rozlozeni+1>_SMO<i>")`), ta má přednost. Prázdný kus → i-tý kus
/// `vychozi`. Výstup `t(i)` = nadpis, `t(i,2)` = druhý řádek (text za `<br>`, další `<br>` jako mezera).
/// Zastávkový JŘ AD měl do 26. 10. nadpisy napevno česky (`B194` T11).
ClassMethod NadpisySkupinDnuAd(dataDB As %String, sku As %String, lin As %String, rec As %String, rozlozeni As %Integer, vychozi As %String, ByRef t)
{
    k t
    s dsc = "", sada = ""
    i dataDB '= "" {
        s dsc = $g(^[dataDB]JRSET("Definice","WinMHD",rec,"DSC"))
        i (sku '= "") && (lin '= "") s sada = $g(^[dataDB]JRL(sku,lin,"PATCH","ISY_LinkyTiskMHDNadpisy"))
    }
    s prvniKus = $case(rozlozeni, 1:5, 2:8, 3:10, :2)
    f i=1:1:$l(vychozi,"~") {
        s txt = $p(dsc,"~",prvniKus+i-1)
        i sada '= "" {
            s vlastni = $g(^[dataDB]JRSET("LinkyTiskMHDNadpisy",sada,"R"_(rozlozeni+1)_"_SMO"_i))
            i vlastni '= "" s txt = vlastni
        }
        i $tr(txt," ","") = "" s txt = $p(vychozi,"~",i)
        s t(i) = $zstrip($p(txt,"<br>",1),"<=>W")
        s t(i,2) = $zstrip($replace($p(txt,"<br>",2,99),"<br>"," "),"<=>W")
    }
}

/// Parametr sestavy „Barvy spojů" agend AD jako volba: „Tisknout", „Netisknout", nebo prázdno =
/// podle linky. Návrhář posílá checkbox `TISKNOUT_BARVY_SPOJU`; starší návrhář u linkového JŘ místo
/// něj třístav `BARVY_SPOJU` (Automaticky (dle linky) / Tisknout / Netisknout), který se přebírá.
/// Checkbox nahradil třístav 26. 10. (rozhodnutí Jan): nastavení u linky nemá v datech zapnuté
/// žádná linka (sonda `BarvySpojuVyskyt`), takže volba „dle linky" nebyla k ničemu.
ClassMethod VolbaBarevSpoju() As %String
{
    s zaskrtnuto = $zcvt($g(^||ML("IN","TISKNOUT_BARVY_SPOJU")),"L")
    i zaskrtnuto '= "" q $s(zaskrtnuto = "true":"Tisknout", 1:"Netisknout")
    q $g(^||ML("IN","BARVY_SPOJU"))
}

/// Tisknou se barvy spojů? `volba` z `VolbaBarevSpoju`; bez ní rozhodne linka
/// (`PATCH,barevnyTiskSpoju` = 1 znamená tisknout černě, `JRapiLinPrtWin.mac:2085-2102`).
ClassMethod TiskBarevSpoju(volba As %String, dataDB As %String, sku As %String, lin As %String) As %Boolean
{
    q $case(volba, "Tisknout":1, "Netisknout":0, :'+$g(^[dataDB]JRL(sku,lin,"PATCH","barevnyTiskSpoju")))
}

/// Přidá bool parametr do pole parametrů.
/// arr        = %DynamicArray kam se přidává
/// name       = název parametru (např. "TISK_POZNAMKY")
/// label      = popisek (např. "Tisknout poznámku")
/// defaultVal = výchozí hodnota (0/1 nebo true/false)
/// group      = volitelná skupina pro vizuální seskupení v UI (aditivní klíč kontraktu)
/// desc       = volitelná nápověda (aditivní klíč `description` → helpText bublina ve formuláři)
/// serverSide = hodnotu parametru zpracovává až `GetData`, takže její změna musí klientovi
/// spustit načtení dat, ne jen překreslení náhledu. Dřív si ten seznam držel klient natvrdo a dalo
/// se na něj zapomenout; příznak patří k definici parametru, kde se stejně píše.
ClassMethod AddBoolParam(arr As %DynamicArray, name As %String, label As %String, defaultVal As %Boolean, group As %String = "", desc As %String = "", serverSide As %Boolean = 0)
{
    s param = ##class(%DynamicObject).%New()
    s param.name  = name
    s param.label = label
    s param.type  = "bool"
    // Call-sites předávají i string "true" - numericky je 0, holé $s(defaultVal:...) by ho vyhodnotilo
    // jako false (historicky tichý bug: TISK_SIPKY default na wire "false" proti záměru).
    s param.defaultValue = $s((defaultVal=1)||(defaultVal="true"):"true", 1:"false")
    s:group'="" param.group = group
    s:desc'="" param.description = desc
    s:serverSide param.serverSide = 1
    d arr.%Push(param)
}

/// Přidá number parametr do pole parametrů.
ClassMethod AddNumberParam(arr As %DynamicArray, name As %String, label As %String, defaultVal As %String, group As %String = "", desc As %String = "", serverSide As %Boolean = 0)
{
    s param = ##class(%DynamicObject).%New()
    s param.name  = name
    s param.label = label
    s param.type  = "number"
    s param.defaultValue = defaultVal
    s:group'="" param.group = group
    s:desc'="" param.description = desc
    s:serverSide param.serverSide = 1
    d arr.%Push(param)
}

/// Přidá mock hodnotu do objektu skalárních hodnot.
/// vals = %DynamicObject kam se přidává
/// name = název pole (např. "CISLO_FAKTURY")
/// val  = hodnota (string nebo number)
ClassMethod AddMockValue(vals As %DynamicObject, name As %String, val As %String)
{
    d vals.%Set(name, val)
}

/// Tvar názvu zastávky pro tisk. Vstup je název z registru zastávek, kde jsou obec, část obce
/// a lokalita spojené **čárkami** - prázdná část obce se tak projeví **dvojčárkou**
/// („Zlín,,aut.nádr."). <var>okres</var> = pole 3 registru.
///
/// Tvary: „Zkrácený" = dvojčárka na jednu · „Plný CIS" = dvojčárka zůstane (technické exporty,
/// soutěže) · „Plný CIS s okresem" = a navíc okres na konci · „Typograficky upravený" = zkrácený
/// + mezera za čárkou a tečkou.
///
/// **Slovenský režim skládá název jinak** (legacy `gTskNaZst^JRapiZst`): nikoli čárkami, ale
/// spojením neprázdných částí přes „, ". Prochází to přes týž vstup, takže se prázdné části
/// prostě vynechají. ⚠️ **Neověřené porovnáním** - v porovnávací sadě není slovenská firma.
///
/// ⚠️ **Sdílené oběma rodinami agend schválně**: kdyby si zastávková vývěska a linkový JŘ
/// formátovaly název každá po svém, rozešly by se a táž zastávka by se v jedné sestavě jmenovala
/// jinak než v druhé. Právě to byl stav do 24. 8.
ClassMethod FormatNazvuZastavky(nazev As %String, okres As %String = "", tvar As %String = "", jazyk As %String = "CZ") As %String
{
    i nazev = "" ret ""
    i jazyk = "*SK" {
        s vysledek = ""
        f di=1:1:$l(nazev, ",") {
            s cast = $zstrip($p(nazev, ",", di), "<>W")
            i cast '= "" s vysledek = vysledek_$s(vysledek="":"", 1:", ")_cast
        }
        ret vysledek
    }
    i tvar = "Plný CIS" ret nazev
    i tvar = "Plný CIS s okresem" ret nazev_$s(okres'="":","_okres, 1:"")
    s zkraceny = $replace(nazev, ",,", ",")
    i tvar = "Typograficky upravený" ret ..MezeryPoInterpunkci(zkraceny)
    ret zkraceny
}

/// Doplní mezeru za čárku a tečku - „Zlín,aut.nádr." → „Zlín, aut. nádr.". Před číslicí se mezera
/// nedoplňuje, aby zůstalo „č.1" a desetinná čísla. Záměrně jen pro **názvy zastávek**: na volný
/// text se pustit nesmí, rozbila by webovou adresu („www.idzk.cz" → „www. idzk. cz").
ClassMethod MezeryPoInterpunkci(txt As %String) As %String
{
    s vysledek = ""
    f i=1:1:$l(txt) {
        s znak = $e(txt, i)
        s vysledek = vysledek_znak
        i (znak '= ",") && (znak '= ".") continue
        s dalsi = $e(txt, i + 1)
        i dalsi = "" continue            // konec textu
        i dalsi = " " continue           // mezera už tam je
        i dalsi ? 1N continue            // číslice
        s vysledek = vysledek_" "
    }
    ret vysledek
}

/// ── Vlastní definice sloupců (sdílená oběma rodinami agend) ──────────────────────────
/// Definici skládá uživatel v šabloně a posílá se do `GetData` jako `DEFINICE_SLOUPCU`.
/// **Význam definice musí být pro všechny agendy tentýž** - jinak by táž definice udělala
/// ve vývěsce jiné sloupce než v linkovém JŘ. Liší se až to, co s výsledkem prvek udělá:
/// vývěsce je skupina **jeden sloupec**, linkovému JŘ **pás nad několika sloupci spojů**
/// (a spoj patřící do dvou skupin se vypíše dvakrát).
/// Vlastní definice sloupců vývěsky (parametr DEFINICE_SLOUPCU, JSON ze šablony) → lokální pole.
/// Vrací počet sloupců; 0 = bez definice / nevalidní JSON → režim „automaticky dle linky".
/// defArr(i,"id"|"nadpis"|"nadpis2"|"dny"|"jen"|"bez") + defArr("nevyp",id) pro potlačení značek;
/// seznamy značek normalizované do tvaru ",1,2," (containment test přes [).
ClassMethod ParseDefiniceSloupcu(ByRef defArr) As %Integer
{
    k defArr
    s json = $g(^||ML("IN","DEFINICE_SLOUPCU"))
    i json = "" q 0
    s def = ""
    try { s def = ##class(%DynamicObject).%FromJSON(json) } catch (ex) { ret 0 }
    i ('$isobject(def)) || ('def.%IsA("%Library.DynamicObject")) q 0
    s sloupce = def.%Get("sloupce")
    i ('$isobject(sloupce)) || ('sloupce.%IsA("%Library.DynamicArray")) q 0
    s n = 0
    s iter = sloupce.%GetIterator()
    while iter.%GetNext(.key, .sl) {
        i '$isobject(sl) continue
        s n = n + 1
        s id = sl.%Get("id")
        i id = "" s id = n
        s defArr(n,"id")      = id
        s defArr(n,"nadpis")  = sl.%Get("nadpis")
        s defArr(n,"nadpis2") = sl.%Get("nadpis2")
        s defArr(n,"dny")     = sl.%Get("dny")
        s defArr(n,"jen")     = ..NormalizujSeznamZnacek(sl.%Get("znackyJen"))
        s defArr(n,"bez")     = ..NormalizujSeznamZnacek(sl.%Get("znackyBez"))
        // období se drží v původním textovém tvaru - rok se doplňuje až proti platnosti JŘ
        s defArr(n,"obdobi")  = sl.%Get("obdobi")
        s defArr("nevyp",id)  = ..NormalizujSeznamZnacek(sl.%Get("nevypisovatZnacky"))
    }
    s defArr = n
    q n
}

/// CSV seznam čísel značek → normalizovaný tvar ",1,2," (ořez mezer, +normalizace čísel).
/// Prázdný/bezobsažný vstup → "".
ClassMethod NormalizujSeznamZnacek(seznam As %String) As %String
{
    i $g(seznam) = "" q ""
    s out = ","
    f i=1:1:$l(seznam,",") {
        s cislo = $zstrip($p(seznam,",",i),"<>W")
        i cislo = "" continue
        s out = out_(+cislo)_","
    }
    q $s(out=",":"", 1:out)
}

/// Sedí spoj do sloupce podle dnů? Sloupec bez dnů ("") neomezuje - sedí každý spoj
/// (kontrakt „nevyplněná podmínka neomezuje"). Spoj bez omezení ("") sedí vždy; "-" nikdy;
/// jinak stačí průnik alespoň jednoho dne.
ClassMethod SedneDoSloupceDny(dnySpoje As %String, sloupecDny As %String) As %Boolean
{
    i sloupecDny = "" q 1
    i dnySpoje = "" q 1
    i dnySpoje = "-" q 0
    // „X" (pracovní dny) se pro **zařazení** chová jako 1-5: spoj jedoucí jen v pondělí do sloupce
    // pracovních dnů patří (jen ho nepokrývá celý, takže dostane značku). Od kalendářního „12345"
    // se „X" liší až na ose Období, kde svátek do pracovních dnů nepadne - `DenPatriDoSloupce`.
    i sloupecDny [ "X" s sloupecDny = sloupecDny_"12345"
    // „+" znamená „neděle a svátky" i u sloupce, ne jen svátky - jinak by sloupec „Neděle a svátky"
    // musel vyjmenovat „7+" a spoj jedoucí jen v neděli by z pouhého „+" vypadl.
    i (sloupecDny [ "+") && (sloupecDny '[ "7") s sloupecDny = sloupecDny_"7"
    f i=1:1:$l(dnySpoje) {
        i sloupecDny [ $e(dnySpoje,i) ret 1
    }
    q 0
}

/// Dny, kdy spoj podle týdenní platnosti jede, jako znaky 1-7,+. "" = jede každý den (prázdná
/// nebo "." platnost); "-" = žádný rozpoznaný den (nerozpoznané Plk → spoj se nikam nezařadí,
/// jako legacy KamPrt=0). "X" = legislativní zkratka pracovních dnů → 1-5.
///
/// ⚠️ **"+" znamená podle vyhlášky 122/2014 „jede v neděli **a** ve státem uznané svátky"**, takže
/// z něj plyne i neděle - proto se doplní "7". Bez toho spoj s "+" nespadl do vlastního sloupce
/// s dny "7" a zákazníkovi vyšel sloupec „Neděle" prázdný, přestože nedělní spoje na lince byly
/// (`B81`, hlásil zákazník). Že jsou to dvě různé značky, potvrzuje i příloha vyhlášky zákazem
/// kombinace "+" se "7" - kdyby "+" neděli nezahrnovalo, dávala by ta dvojice smysl.
ClassMethod DnySpoje(plk As %String) As %String
{
    i (plk = "") || (plk = ".") q ""
    s dny = ""
    f i=1:1:$l(plk) {
        s ch = $e(plk,i)
        i ch = "X" { f d=1:1:5 { i dny'[d s dny = dny_d } }
        elseif ("1234567+"[ch) { i dny'[ch s dny = dny_ch }
    }
    i (dny["+") && (dny'["7") s dny = dny_"7"
    q $s(dny="":"-", 1:dny)
}

/// ── Důvod prázdného výstupu (`DUVOD_PRAZDNE`, `cache-api.md`) ──
/// Agenda během GetData zapíše, proč stránka nevznikla (co v datech chybí a co s tím), a na konci
/// `VydatDuvodPrazdne` důvody pošle - jen když nevznikla žádná stránka. Návrhář z nich udělá hlášku
/// „nemá data, nevytiskne se". Sdílí všechny agendy JŘ, aby linka bez dat hlásila totéž všude (`B193g`).
ClassMethod ZacitDuvodyPrazdne()
{
    k ^||TdDuvodPrazdne
}

/// Zapíše důvod (stejný jen jednou) a vrátí "" - pro `q ..PrazdnaStranka("…")` místo prázdné stránky.
ClassMethod PrazdnaStranka(duvod As %String) As %String
{
    i (duvod '= "") && '$d(^||TdDuvodPrazdne("text", duvod)) {
        s ^||TdDuvodPrazdne("text", duvod) = ""
        s ^||TdDuvodPrazdne($i(^||TdDuvodPrazdne)) = duvod
    }
    q ""
}

/// Konec GetData: bez jediné stránky v `^||ML("OUT")` pošle zapsané důvody, a když žádný není,
/// `vychozi`. Chybou to vědomě není - při tisku víc linek by zastavila i ty s daty.
ClassMethod VydatDuvodPrazdne(vychozi As %String = "")
{
    s k = "", stranek = 0
    f {
        s k = $o(^||ML("OUT", k))
        q:k=""
        i $isvalidnum(k) s stranek = 1 q
    }
    i 'stranek {
        s out = ""
        f i=1:1:+$g(^||TdDuvodPrazdne) s out = out_$s(out="":"", 1:" ")_^||TdDuvodPrazdne(i)
        s:out="" out = vychozi
        s:out'="" ^||ML("OUT","DUVOD_PRAZDNE") = out
    }
    k ^||TdDuvodPrazdne
}

/// ── Chyba v datech (`B194a` C11) ──
/// Linku nejde vytisknout kvůli datům, která uživatel opraví (linka v JŘ není, spoj bez kalendáře…).
/// Text řekne co a co s tím; návrhář ho ukáže jako informaci s číslem linky a tisk zastaví. Pozná ji
/// podle tvaru obecné chyby (5001) - chyba v kódu ho nemá a zůstává chybou programu s hlášením.
ClassMethod ChybaDat(text As %String)
{
    throw ##class(%Exception.StatusException).CreateFromStatus($system.Status.Error(5001, text))
}

/// Linka AD, která ve skupině není - přes výběr linek to nejde, jen spuštěním z EDISONu s linkou
/// z jiné skupiny, nebo když ji někdo v JŘ smazal či přesunul, zatímco je v návrháři vybraná.
ClassMethod TextLinkaNeniVJr(sku As %String, lin As %String) As %String
{
    q "Linka "_(+lin)_" ve skupině "_sku_" v jízdních řádech není. Mohla být smazána nebo přesunuta do jiné skupiny - vyberte linky znovu."
}

/// Název linky v náhledu bez vybrané linky, když první linka firmy nedala data (žádná není, nebo
/// nemá trasu). Text vidí uživatel na plátně: bez globálů a kódu firmy, jen co udělat.
ClassMethod TextUkazkyBezDat() As %String
{
    q "Ukázka bez dat - vyberte linku (Ctrl+Shift+I)"
}

/// Upřesnění ke spoji AD „na objednání" (`klic` = RBU) nebo „podmínečně" (RBP), které se lepí za
/// větu legendy: text u spoje, jinak u linky, jinak v nastavení firmy. Prázdné = bez upřesnění.
/// 1:1 legacy `JRapiNZUtl3.mac:41-48, 118-130` (spoj `#10`/`#11` podle `JRapiSp.mac:192-193`,
/// linka `PATCH,SP_TX_JOBJ/JPOD` podle `JRapiLin.mac:402-405`).
///
/// Starý vývěskový tisk bral jen text linky (`JRapiLinPrtMhd1.mac:234`); obě agendy AD berou
/// celé pořadí, aby vývěska i linkový JŘ psaly totéž (rozhodnutí Jan 26. 10.).
ClassMethod UpresneniAtributuSpoje(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String, klic As %String) As %String
{
    s naObjednani = (klic = "RBU")
    s txt = $g(^[dataDB]JRSET("Definice", $s(naObjednani:"PS_RADIOBUS_NZ_RBU", 1:"PS_PODMINECNE_NZ_RBP")))
    s textLinky = $g(^[dataDB]JRL(sku,lin,"PATCH",$s(naObjednani:"SP_TX_JOBJ", 1:"SP_TX_JPOD")))
    i textLinky '= "" s txt = textLinky
    s textSpoje = $p($g(^[dataDB]JRL(sku,lin,smer,sp)), "#", $s(naObjednani:10, 1:11))
    i textSpoje '= "" s txt = textSpoje
    q $zstrip(txt, "<>W")
}

/// Záznam zastávky z registru (`$`-věta jako `^JRZ(czst)`) **platný k datu** `hrl` (horolog,
/// prázdné = dnešek). Zastávka může mít časové varianty `^JRZ(czst,"CasoveVariantyUdaju",od)`:
/// platí ta s nejpozdějším začátkem ≤ hrl, jejíž konec (`$`-pole 42) je prázdný nebo ≥ hrl;
/// skončená se přeskočí a zkouší se předchozí. Žádná neplatí → hlavní záznam; je-li prázdný
/// a datum leží před první variantou, první varianta.
/// 1:1 legacy `gZst^JRapiZst` (`JRapiZst.mac:123-147`), kterým starý tisk AD čte každou zastávku
/// k „platí od" linky (`JRapiLinPrtMhd.mac:78`, `JRapiLinPrtPS1.mac:26`). `variantaOd` = začátek
/// vybrané varianty, prázdné u hlavního záznamu (podle něj se hledají sloupky zastávky).
ClassMethod ZaznamZastavkyKDatu(dataDB As %String, czst As %String, hrl As %String = "", ByRef variantaOd As %String = "") As %String
{
    s variantaOd = ""
    i czst = "" ret ""
    s zaznam = $g(^[dataDB]JRZ(czst))
    i hrl = "" s hrl = +$h
    s od = hrl + 0.1
    f {
        s od = $o(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",od),-1,varianta)
        q:od=""
        s konec = $p(varianta,"$",42)
        i (konec = "") || (konec >= hrl) {
            s zaznam = varianta, variantaOd = od
            q
        }
    }
    i zaznam = "" {
        s prvni = $o(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",""))
        i (prvni '= "") && (hrl < prvni) {
            s zaznam = $g(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",prvni)), variantaOd = prvni
        }
    }
    ret zaznam
}

}
