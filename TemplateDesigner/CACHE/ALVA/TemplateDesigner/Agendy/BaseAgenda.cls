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
/// Sem patří jen kontrakt a obecné věci pro všechny agendy, i budoucí z report generátoru (R8).
/// Čtení dat AD a MHD a věci jen pro jízdní řády jsou v pomocných třídách `Agendy/Jr/` (do 30. 10.
/// tu jich bylo 14, `B56` S13); že základ data nečte, hlídá `DataPodleZdrojeTests`.
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

/// Oblast práce, pod kterou agenda leží ve stromu šablon (kód z `API.AgendyRegistr.OblastiPrace`, např. "JIZDNI_RADY").
/// Strom se neukládá, jde ho kdykoli přeskupit. Agenda s neznámým nebo prázdným kódem je ve stromu pod Ostatní.
Parameter OblastPrace As %String = "";

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

/// Id systémové šablony M-line (`Template.Id` v `SystemTemplates/` exe), kterou agenda tiskne, když
/// firma nemá vlastní výchozí šablonu (krok 4 pravidla `API.SablonyProTisk`, `B190`). Určuje ji
/// M-line vydáním třídy; prázdné = agenda výchozí šablonu M-line nemá.
Parameter VychoziSablonaMline As %String = "";

/// Předpony názvů parametrů sestavy, jejichž hodnota platí jen ve firmě (číslo z číselníku firmy), čárkou.
/// Šablona poslaná jiné firmě je nenese (`API.SdileniSablon`, `B189`): u příjemce by znamenaly jiný záznam.
/// Předpona, ne výčet z nabídky - uložená hodnota může patřit typu, který firma mezitím zablokovala.
Parameter ParametryJenVeFirme As %String = "";

/// Druh JŘ elementů, které agenda podporuje - řídí kontextové menu/paletu okna.
/// "STOP" = zastávkové (StopList/DepartureGrid/NotesLegend), "LINE" = knižní/linkové
/// (LineTable), "" = bez JŘ kontextu (okno ukáže všechny). C# vlastní mapování druh→typy.
Parameter ElementKind As %String = "";

/// Jak se jmenuje instance agendy - to, co se tiskne (linka, oběh, zaměstnanec). Návrhář z tvarů skládá
/// texty oken a hlášek („Žádná vybraná linka nemá data", „Tisk se zastavil u linky 12"), aby žádný obecný
/// text nepočítal s linkami. Tvary čárkou: 1., 2. a 4. pád jednotného čísla, 1. a 2. pád množného
/// a volitelně 4. pád množného, jen když se liší od 1. (mužský životný: zaměstnanci / zaměstnance).
/// Výchozí „instance" pro agendu bez doménového pojmu.
Parameter InstanceTvary As %String = "instance,instance,instanci,instance,instancí";

/// Rod instance pro shodu v textech („Žádná vybraná linka" / „Žádný vybraný oběh"):
/// F ženský, M mužský neživotný, MZ mužský životný, N střední.
Parameter InstanceRod As %String = "F";

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

/// Datum z horologu na český tvar "dd.mm.yyyy" pro pole, která uživatel vidí v šabloně.
/// Prázdný vstup → prázdný výstup. Obecný základ (R8), ne data MHD: bude ho potřebovat i agenda
/// report generátoru. Dnes ho volá `Jr.DataMhd`.
///
/// Tvar je zatím **napevno český** (rozhodnutí Jan 2026-08-18). Správně by šel z lokalizace,
/// ale všichni zákazníci tisknou česky, takže by to dnes byla vrstva bez konzumenta; až
/// vznikne první jinojazyčná sestava, nahradí se tělo téhle metody a volající se nemění.
///
/// ⚠️ **Nepoužívat `$zd(h, 4)`** - dává "dd/mm/yyyy" s lomítky. Do 18. 8. tak MHD vývěska
/// tiskla datum platnosti, zatímco AD vedle ní tečky; skládá se to z ISO tvaru záměrně,
/// aby výstup nezáležel na tom, jaký oddělovač který $ZDATE formát zvolí.
ClassMethod FormatDatum(horolog As %String) As %String
{
    i $g(horolog) = "" q ""
    s iso = $zd(horolog, 3)          // yyyy-mm-dd
    q $p(iso,"-",3)_"."_$p(iso,"-",2)_"."_$p(iso,"-",1)
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

/// Název linky v náhledu bez vybrané linky, když první linka firmy nedala data (žádná není, nebo
/// nemá trasu). Text vidí uživatel na plátně: bez globálů a kódu firmy, jen co udělat.
ClassMethod TextUkazkyBezDat() As %String
{
    q "Ukázka bez dat - vyberte linku (Ctrl+Shift+I)"
}

}
