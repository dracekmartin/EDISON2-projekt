Include GENERAL

/// Storage tiskových šablon TemplateDesigneru v Caché (CRUD).
///
/// Šablona = uložený layout tiskové sestavy (např. zastávkový JŘ MHD pro linku 42,
/// faktura pro fakturační systém, ...) jako JSON blob + sada metadat pro identifikaci.
///
/// Per-IDFa (každá firma má vlastní úložiště), sdílené napříč uživateli - autor
/// je jen metadata, ne access control. Šablony bez vazby na agendu mají AgendaCode = "GENERAL".
///
/// Operace: ListTemplates (metadata), LoadTemplate, SaveTemplate (insert nebo update),
/// DeleteTemplate, RenameTemplate.
///
/// Globální struktura:
///   ^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Metadata", <pole>) = <hodnota>
///   ^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Data")             = malý blob: celý JSON / velký: počet kusů
///   ^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Data", n)          = n-tý kus JSON blobu (jen velký blob)
///   ^ALVA.TemplateDesigner.Templates(IdFa, "IDX", "ByAgenda", AgendaCode, TemplateID) = ""
///   ^ALVA.TemplateDesigner.Templates(IdFa, "DEFAULT", AgendaCode) = TemplateID - výchozí pro tisk (`API.SablonyProTisk`)
///
/// Blob: MALÝ (vejde se do 1 kusu) se ukládá jednoklíčově - formát čitelný i staršími klienty
/// bez podpory kusů (#13013). VELKÝ po kusech, nikdy jako jeden řetězec - překročil by limit
/// délky řádku na wire (příjem i odpověď). Formáty se při načtení rozliší existencí "Data",1.
///
/// Metadata pole: Name, AgendaCode, Author, CreatedAt, ModifiedAt, ModifiedBy, Version
///
/// TemplateID je GUID generovaný na klientovi (Template.Id v C# modelu) - round-trip s file JSON
/// je beze ztráty identity.
///
/// Seznam pro UI: ALVA.TemplateDesigner.EDList.Sablony (read-only čtení metadat pro picker).
Class ALVA.TemplateDesigner.API.Sablony Extends %RegisteredObject
{

/// Vrátí seznam šablon (jen metadata, ne data blob).
/// IN["agendaCode"] = filtr (volitelný; prázdné = všechny agendy)
/// OUT["1"] = JSON pole metadat:
///   [
///     { "templateId":"...","name":"...","agendaCode":"MHD","author":"JM",
///       "createdAt":"2026-05-14T10:00:00Z","modifiedAt":"...","modifiedBy":"JM","version":"1.0" },
///     ...
///   ]
ClassMethod ListTemplates(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s agendaCode = $g(^||ML("IN","agendaCode"))
        s arr = ##class(%DynamicArray).%New()

        if agendaCode '= "" {
            // Filtrované přes sekundární index
            s templateId = ""
            for {
                s templateId = $o(^ALVA.TemplateDesigner.Templates(IdFa,"IDX","ByAgenda",agendaCode,templateId))
                q:templateId=""
                d ..AppendMetadata(arr, IdFa, templateId)
            }
        } else {
            // Všechny šablony per IdFa
            s templateId = ""
            for {
                s templateId = $o(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId))
                q:templateId=""
                d ..AppendMetadata(arr, IdFa, templateId)
            }
        }

        s ^||ML("OUT","1") = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Načte jednu šablonu (metadata + data blob).
/// IN["templateId"] = GUID šablony
/// OUT["metadata"]  = JSON metadat
/// OUT["dataChunkCount"] = počet kusů blobu; OUT["data1"]..["dataN"] = kusy JSON blobu (klient složí)
/// OUT["data"]      = celý JSON blob (jen u legacy šablony uložené bez kusů)
/// Neexistující šablona → $$$ERROR
ClassMethod LoadTemplate(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s templateId = $g(^||ML("IN","templateId"))
        // Zdrojová větev: default session firma; "MLINE" pro otevření systémové šablony.
        s sourceIdFa = $g(^||ML("IN","sourceIdFa"))
        i sourceIdFa = "" s sourceIdFa = IdFa
        if templateId = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "templateId is required"))
        if '$d(^ALVA.TemplateDesigner.Templates(sourceIdFa,"DTA",templateId)) {
            $$$ThrowStatus($$$ERROR($$$GeneralError, "Template not found: "_templateId))
        }

        s meta = ..BuildMetadataObject(sourceIdFa, templateId)
        s ^||ML("OUT","metadata") = meta.%ToJSON()

        // Blob: nový chunkovaný formát (Data = počet, Data(n) = kus) rozpozná existence Data(1);
        // legacy jednoklíčový uzel (Data = celý JSON) vrať jako dřív.
        s dataBase = $name(^ALVA.TemplateDesigner.Templates(sourceIdFa,"DTA",templateId,"Data"))
        i $d(@dataBase@(1)) {
            s chunkCount = +$g(@dataBase)
            f n=1:1:chunkCount s ^||ML("OUT","data"_n) = $g(@dataBase@(n))
            s ^||ML("OUT","dataChunkCount") = chunkCount
        } else {
            s ^||ML("OUT","data") = $g(@dataBase)
        }
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Uloží šablonu (insert nebo update podle existence TemplateID).
/// IN["templateId"]  = GUID (povinný; klient generuje)
/// IN["name"]        = jméno šablony (povinné)
/// IN["agendaCode"]  = kód agendy ("MHD", "GENERAL", ...; povinný)
/// IN["version"]     = verze formátu (např. "1.0"; volitelné, default "1.0")
/// IN["dataChunkCount"] + IN["data1"]..["dataN"] = JSON blob šablony po kusech (povinný)
///                     Legacy IN["data"] = celý JSON blob v jednom klíči (fallback, stále přijímáno)
/// OUT["templateId"] = uložený TemplateID (echo)
ClassMethod SaveTemplate(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s templateId = $g(^||ML("IN","templateId"))
        s name       = $g(^||ML("IN","name"))
        s agendaCode = $g(^||ML("IN","agendaCode"))
        s version    = $g(^||ML("IN","version"))
        // Blob chodí po kusech (data1..dataN + počet v dataChunkCount), aby jeden Caché řetězec
        // nepřekročil limit délky. Legacy klient posílá jednoklíčové "data" (fallback níže).
        s chunkCount = $g(^||ML("IN","dataChunkCount"))

        if templateId = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "templateId is required"))
        if name       = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "name is required"))
        if agendaCode = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "agendaCode is required"))
        if version    = "" s version = "1.0"

        // Přítomnost dat: buď chunkovaný vstup (počet > 0), nebo legacy neprázdné "data"
        i chunkCount = "" {
            if $g(^||ML("IN","data")) = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "data is required"))
        } elseif (+chunkCount <= 0) {
            $$$ThrowStatus($$$ERROR($$$GeneralError, "data is required"))
        }

        // Cílová větev: default session firma; "MLINE" pro seed systémových šablon (distribuce).
        s targetIdFa = $g(^||ML("IN","targetIdFa"))
        i targetIdFa = "" s targetIdFa = IdFa

        // Zámek MLINE: systémová (M-line) větev je read-only, zápis jen distribuční cestou
        // (seed nastaví "distribution"=1). Normální UI systémové needituje - jen kopíruje
        // („Uložit jako" do firmy). Vynuceno server-side, ne jen klientská iluze (governance bod 2).
        i (targetIdFa = "MLINE") && ($g(^||ML("IN","distribution")) '= "1") {
            $$$ThrowStatus($$$ERROR($$$GeneralError, "MLINE je systémová (read-only) větev - zápis jen přes distribuci."))
        }

        // Vyřazenou šablonu M-line (`RetireTemplate`) už nic nezapíše: starší návrhář na jiném počítači ji při startu
        // nalévá dál a vrátil by ji zákazníkům. Bez chyby - jeho seed by jinak hlásil chybu při každém startu.
        i $d(^ALVA.TemplateDesigner.Templates(targetIdFa,"RETIRED",templateId)) {
            s ^||ML("OUT","templateId") = templateId
            s ^||ML("OUT","retired") = "1"
            ret $$$OK
        }

        s nowIso = $zdt($h, 3)
        s existed = $d(^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId))

        // Při update: udržet původní CreatedAt + Author; při novém: nastavit aktuální
        if existed {
            s oldAgenda = $g(^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","AgendaCode"))
            if (oldAgenda '= "") && (oldAgenda '= agendaCode) {
                k ^ALVA.TemplateDesigner.Templates(targetIdFa,"IDX","ByAgenda",oldAgenda,templateId)
            }
        } else {
            s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","CreatedAt") = nowIso
            s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","Author")    = userID
        }

        s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","Name")       = name
        s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","AgendaCode") = agendaCode
        s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","Version")    = version
        s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","ModifiedAt") = nowIso
        s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Metadata","ModifiedBy") = userID

        // Blob: malý (1 kus / legacy vstup) ulož JEDNOKLÍČOVĚ - přečte ho i starší klient bez
        // podpory kusů (#13013: mix nové úložiště × produkční .exe → „šablona porušená").
        // Po kusech (Data = počet, Data(n) = kus) jen víc-kusý blob - jeden řetězec by na wire
        // překročil limit délky řádku; takový starší klient stejně nikdy neuložil.
        // Nejdřív smaž starý obsah (oba formáty z předchozího uložení).
        k ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Data")
        i (chunkCount '= "") && (+chunkCount > 1) {
            f n=1:1:+chunkCount {
                s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Data",n) = $g(^||ML("IN","data"_n))
            }
            s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Data") = +chunkCount
        } elseif chunkCount '= "" {
            s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Data") = $g(^||ML("IN","data1"))
        } else {
            s ^ALVA.TemplateDesigner.Templates(targetIdFa,"DTA",templateId,"Data") = $g(^||ML("IN","data"))
        }

        s ^ALVA.TemplateDesigner.Templates(targetIdFa,"IDX","ByAgenda",agendaCode,templateId) = ""

        s ^||ML("OUT","templateId") = templateId
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Smaže šablonu.
/// IN["templateId"] = GUID
/// OUT["deleted"]   = "1" pokud existovala, "0" pokud ne
ClassMethod DeleteTemplate(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s templateId = $g(^||ML("IN","templateId"))
        if templateId = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "templateId is required"))

        if $d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId)) {
            s agendaCode = $g(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","AgendaCode"))
            if agendaCode '= "" {
                k ^ALVA.TemplateDesigner.Templates(IdFa,"IDX","ByAgenda",agendaCode,templateId)
                // Výchozí šablona firmy pro tisk (`API.SablonyProTisk`) by ukazovala do prázdna.
                k:$g(^ALVA.TemplateDesigner.Templates(IdFa,"DEFAULT",agendaCode))=templateId ^ALVA.TemplateDesigner.Templates(IdFa,"DEFAULT",agendaCode)
            }
            k ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId)
            s ^||ML("OUT","deleted") = "1"
        } else {
            s ^||ML("OUT","deleted") = "0"
        }
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Vyřadí šablonu M-line (seznam vyřazených nese návrhář, `SystemTemplateSeeder.Retired`): smaže ji z větve M-line,
/// zapamatuje si, že je vyřazená (`SaveTemplate` ji pak už nezapíše), a zruší výchozí šablony pro tisk firem, které
/// na ni ukazují. Jen distribuční cestou, stejně jako zápis do větve M-line.
/// IN["templateId"]   = GUID
/// IN["targetIdFa"]   = větev, default MLINE (testy předají vlastní)
/// IN["distribution"] = "1"
/// OUT["deleted"]     = "1" pokud šablona ve větvi byla, jinak "0"
ClassMethod RetireTemplate(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s templateId = $g(^||ML("IN","templateId"))
        if templateId = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "templateId is required"))
        if $g(^||ML("IN","distribution")) '= "1" {
            $$$ThrowStatus($$$ERROR($$$GeneralError, "Šablonu M-line vyřadí jen distribuce."))
        }
        s vetev = $g(^||ML("IN","targetIdFa"))
        s:vetev="" vetev = "MLINE"

        s ^||ML("OUT","deleted") = ..RetireFromBranch(vetev, templateId)
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Vyřazení šablony ve větvi; vrací 1, když tam šablona byla. Návrhář ho volá při každém startu, už vyřazená
/// šablona proto skončí hned.
ClassMethod RetireFromBranch(vetev As %String, templateId As %String) As %Boolean
{
    q:$d(^ALVA.TemplateDesigner.Templates(vetev,"RETIRED",templateId))&&'$d(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId)) 0
    s ^ALVA.TemplateDesigner.Templates(vetev,"RETIRED",templateId) = $zdt($h, 3)

    // Tisk by výchozí šablonu, která není, přeskočil, ale v datech firmy by zůstal odkaz do prázdna.
    s firma = ""
    f {
        s firma = $o(^ALVA.TemplateDesigner.Templates(firma))
        q:firma=""
        s agenda = ""
        f {
            s agenda = $o(^ALVA.TemplateDesigner.Templates(firma,"DEFAULT",agenda))
            q:agenda=""
            k:^ALVA.TemplateDesigner.Templates(firma,"DEFAULT",agenda)=templateId ^ALVA.TemplateDesigner.Templates(firma,"DEFAULT",agenda)
        }
    }

    q:'$d(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId)) 0
    s agendaCode = $g(^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId,"Metadata","AgendaCode"))
    k:agendaCode'="" ^ALVA.TemplateDesigner.Templates(vetev,"IDX","ByAgenda",agendaCode,templateId)
    k ^ALVA.TemplateDesigner.Templates(vetev,"DTA",templateId)
    q 1
}

/// Přejmenuje šablonu (jen pole Name v metadatech).
/// IN["templateId"] = GUID
/// IN["newName"]    = nové jméno
/// OUT["renamed"]   = "1"
ClassMethod RenameTemplate(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s templateId = $g(^||ML("IN","templateId"))
        s newName    = $g(^||ML("IN","newName"))
        if templateId = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "templateId is required"))
        if newName    = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "newName is required"))
        if '$d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId)) {
            $$$ThrowStatus($$$ERROR($$$GeneralError, "Template not found: "_templateId))
        }

        s ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","Name")       = newName
        s ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","ModifiedAt") = $zdt($h, 3)
        s ^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata","ModifiedBy") = userID

        s ^||ML("OUT","renamed") = "1"
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

// ── Helper metody ─────────────────────────────────────────────────────────────

/// Sestaví %DynamicObject metadat pro existující šablonu.
ClassMethod BuildMetadataObject(IdFa As %String, templateId As %String) As %DynamicObject
{
    s base = $name(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId,"Metadata"))
    s obj = ##class(%DynamicObject).%New()
    d obj.%Set("templateId",  templateId)
    d obj.%Set("name",        $g(@base@("Name")))
    d obj.%Set("agendaCode",  $g(@base@("AgendaCode")))
    d obj.%Set("author",      $g(@base@("Author")))
    d obj.%Set("createdAt",   $g(@base@("CreatedAt")))
    d obj.%Set("modifiedAt",  $g(@base@("ModifiedAt")))
    d obj.%Set("modifiedBy",  $g(@base@("ModifiedBy")))
    d obj.%Set("version",     $g(@base@("Version")))
    q obj
}

/// Přidá metadata existující šablony do %DynamicArray (pro ListTemplates).
ClassMethod AppendMetadata(arr As %DynamicArray, IdFa As %String, templateId As %String)
{
    if '$d(^ALVA.TemplateDesigner.Templates(IdFa,"DTA",templateId)) q
    s obj = ..BuildMetadataObject(IdFa, templateId)
    d arr.%Push(obj)
}

// ── Šablony, které firma vidí ────────────────────────────────────────────────

/// Větve, ze kterých firma bere šablony: vlastní a sdílená M-line, v tomto pořadí. Firma MLINE (autorování
/// systémových šablon) má jen jednu, jinak by se šablony vypsaly dvakrát. Čtou z toho okno šablon návrháře
/// (seznam i strom) a formulář před tiskem v okně JŘ; další větev (šablony od jiné firmy) přibude jen tady.
ClassMethod VetveFirmy(IdFa As %String, mlineIdFa As %String = "MLINE") As %List
{
    q $s(IdFa=mlineIdFa:$lb(IdFa), 1:$lb(IdFa, mlineIdFa))
}

/// Šablony jedné větve do `ids(templateId)`: jen agendy `agendaCode` (index ByAgenda), prázdné = všechny agendy.
/// Položka indexu bez šablony se přeskočí.
ClassMethod IdSablon(vetev As %String, agendaCode As %String, ByRef ids)
{
    s id = ""
    if agendaCode = "" {
        for {
            s id = $o(^ALVA.TemplateDesigner.Templates(vetev,"DTA",id))
            q:id=""
            s ids(id) = ""
        }
        q
    }
    for {
        s id = $o(^ALVA.TemplateDesigner.Templates(vetev,"IDX","ByAgenda",agendaCode,id))
        q:id=""
        s:$d(^ALVA.TemplateDesigner.Templates(vetev,"DTA",id)) ids(id) = ""
    }
}

}
