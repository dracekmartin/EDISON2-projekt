Include GENERAL

/// Klient sdílení dat mezi firmami EDISONu v ISYCloudu (edwcore `apps/isycloud/docs/sdileni/api-edison.md`).
/// Obecný - o šablonách nic neví, aby ho mohl převzít i ISYJR (`B189`, R9). Návrhář ho volá z `API.SdileniSablon`.
///
/// Klíč firmy a adresa API leží mimo data firmy (API to vyžaduje: kopie databáze by jinak nesla klíč):
///   ^["MLINE"]ALVA.SdileniDat(IdFa, "klic") = klíč firmy z registrace (isys_…)
///   ^["MLINE"]ALVA.SdileniDat("adresa")     = adresa API jen na vývojovém serveru; jinde vždy ostrý ISYCloud
/// Globál není návrhářův: ISYCloud vydá firmě jediný klíč („první vyhrává"), takže ho musí najít každá agenda, která sdílí.
/// Chyby API vyhazuje jako obecnou chybu s textem pro uživatele.
Class ALVA.TemplateDesigner.Sdileni.Klient Extends %RegisteredObject
{

Parameter AdresaOstra = "https://isycloud.cz/api/sdileni";

/// Adresa API. Mimo vývojový server vždy ostrý ISYCloud - přepsaná adresa by jinak klíč firmy poslala jinam.
/// Vývojový server bez nastavené adresy ostrý ISYCloud nevolá: zaregistroval by tam firmy z DEV dat
/// a skutečný EDISON firmy by pak registraci nedostal („první vyhrává").
ClassMethod Adresa() As %String
{
    q:'##class(ALVA.GENERALAPI.Server).IsDev() ..#AdresaOstra
    s adresa = $g(^["MLINE"]ALVA.SdileniDat("adresa"))
    i adresa = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "Na vývojovém serveru není nastavená adresa ISYCloudu pro sdílení dat (Sdileni.Klient.SetAdresa)."))
    q adresa
}

/// Nastaví adresu API na vývojovém serveru (lokální nebo testovací ISYCloud).
/// IN["adresa"] = adresa včetně /api/sdileni; OUT["adresa"] = adresa, která platí
ClassMethod SetAdresa(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        i '##class(ALVA.GENERALAPI.Server).IsDev() $$$ThrowStatus($$$ERROR($$$GeneralError, "Adresu ISYCloudu jde změnit jen na vývojovém serveru."))
        s ^["MLINE"]ALVA.SdileniDat("adresa") = $g(^||ML("IN","adresa"))
        s ^||ML("OUT","adresa") = ..Adresa()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Požadavek na API s klíčem firmy; vrátí tělo odpovědi jako JSON ("" bez těla). Firma bez klíče se nejdřív
/// zaregistruje, odmítnutý klíč (zrušený podporou) se jednou zaregistruje znovu a požadavek se zopakuje.
ClassMethod Request(IdFa As %String, metoda As %String, cesta As %String, telo As %DynamicAbstractObject = "") As %DynamicAbstractObject
{
    s klic = ..Klic(IdFa)
    s:klic="" klic = ..Register(IdFa)
    s odpoved = ..Send(metoda, cesta, klic, .stav, telo)
    i (stav = 401) && (..KodChyby(odpoved) = "neplatny_klic") {
        s klic = ..Register(IdFa)
        s odpoved = ..Send(metoda, cesta, klic, .stav, telo)
    }
    d ..CheckOdpoved(stav, odpoved)
    q ..ParseJson(odpoved)
}

/// Nahraje soubor do rozpracované zásilky. Text jde v UTF-8 s kontrolním součtem, server ho podle něj ověří.
/// Název souboru jen ASCII bez lomítek (jde v cestě URL).
ClassMethod UploadSoubor(IdFa As %String, zasilkaId As %String, nazev As %String, obsah As %String) As %DynamicObject
{
    s bajty = $zcvt(obsah, "O", "UTF8")
    s cesta = "/zasilky/"_zasilkaId_"/soubory/"_nazev
    s odpoved = ..Send("PUT", cesta, ..Klic(IdFa), .stav, "", bajty)
    d ..CheckOdpoved(stav, odpoved)
    q ..ParseJson(odpoved)
}

/// Stáhne soubor zásilky a vrátí ho jako text (UTF-8). Délka a kontrolní součet se ověří - přerušené spojení
/// by jinak vrátilo useknutý soubor bez chyby.
ClassMethod DownloadSoubor(IdFa As %String, zasilkaId As %String, souborId As %String) As %String
{
    s req = ..NewRequest(..Klic(IdFa), .zaklad)
    s req.ReadRawMode = 1
    $$$ThrowOnError(..CheckSpojeni(req.Get(zaklad_"/zasilky/"_zasilkaId_"/soubory/"_souborId)))
    s stav = req.HttpResponse.StatusCode
    s bajty = ..ReadData(req.HttpResponse)
    d ..CheckOdpoved(stav, bajty)
    s delka = req.HttpResponse.GetHeader("CONTENT-LENGTH")
    s soucet = $zcvt(req.HttpResponse.GetHeader("X-OBSAH-SHA256"), "L")
    i ((delka '= "") && (+delka '= $l(bajty))) || ((soucet '= "") && (soucet '= ..Sha256Hex(bajty))) {
        $$$ThrowStatus($$$ERROR($$$GeneralError, "Soubor ze sdílení dat se nestáhl celý. Zkuste to znovu."))
    }
    q $zcvt(bajty, "I", "UTF8")
}

/// Stav sdílení firmy bez vyhození chyby: {aktivni, nazev, duvod}. Pro okno, které se rozhoduje, jestli akci nabídnout.
ClassMethod Stav(IdFa As %String) As %DynamicObject
{
    s stav = {"aktivni": false, "nazev": "", "duvod": ""}
    try {
        s ja = ..Request(IdFa, "GET", "/ja")
        d stav.%Set("aktivni", 1, "boolean")
        s stav.nazev = ja.nazev
    } catch ex {
        s text = ex.DisplayString()
        s:text?1"ERROR #"1.N1": ".E text = $p(text, ": ", 2, $l(text, ": "))
        s stav.duvod = text
    }
    q stav
}

/// Zaregistruje EDISON firmy a uloží klíč. ISYCloud vydá firmě jediný klíč; když ho mezitím získal jiný
/// proces téhož serveru (souběžné první použití), vezme se jeho.
ClassMethod Register(IdFa As %String) As %String
{
    s telo = {"idfa": (IdFa), "server": (..ServerName()), "verzeEdisonu": "", "licence": (..Licence(IdFa))}
    s odpoved = ..Send("POST", "/registrace", "", .stav, telo)
    i stav = 409 {
        s klic = ..Klic(IdFa)
        q:klic'="" klic
    }
    d ..CheckOdpoved(stav, odpoved)
    s klic = ..ParseJson(odpoved).klic
    i klic = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "ISYCloud při registraci nevrátil klíč firmy."))
    s ^["MLINE"]ALVA.SdileniDat(IdFa, "klic") = klic
    q klic
}

ClassMethod Klic(IdFa As %String) As %String
{
    q $g(^["MLINE"]ALVA.SdileniDat(IdFa, "klic"))
}

/// Odešle požadavek a vrátí tělo odpovědi; `telo` jde jako JSON, `bajty` jako binární obsah souboru.
ClassMethod Send(metoda As %String, cesta As %String, klic As %String, ByRef stav As %Integer, telo As %DynamicAbstractObject = "", bajty As %String = "") As %String
{
    s req = ..NewRequest(klic, .zaklad)
    i $isobject(telo) {
        s req.ContentType = "application/json; charset=utf-8"
        d req.EntityBody.Write($zcvt(telo.%ToJSON(), "O", "UTF8"))
    } elseif bajty '= "" {
        s req.ContentType = "application/octet-stream"
        d req.SetHeader("X-Obsah-Sha256", ..Sha256Hex(bajty))
        d req.EntityBody.Write(bajty)
    }
    $$$ThrowOnError(..CheckSpojeni(req.Send(metoda, zaklad_cesta)))
    s stav = req.HttpResponse.StatusCode
    q ..ReadData(req.HttpResponse)
}

ClassMethod NewRequest(klic As %String, ByRef zaklad As %String) As %Net.HttpRequest
{
    d ##class(%Net.URLParser).Parse(..Adresa(), .casti)
    s https = ($zcvt($g(casti("scheme")), "L") = "https")
    s req = ##class(%Net.HttpRequest).%New()
    s req.Server = $g(casti("host"))
    s req.Https = https
    s:https req.SSLConfiguration = "MLREST"
    s req.Port = $s($g(casti("port"))'="":casti("port"), https:443, 1:80)
    s req.Timeout = 30
    d:klic'="" req.SetHeader("X-API-Token", klic)
    s zaklad = $g(casti("path"))
    q req
}

/// Spojení se nenavázalo (síť, adresa): chyba s textem pro uživatele místo technické hlášky.
ClassMethod CheckSpojeni(sc As %Status) As %Status
{
    q:$$$ISOK(sc) sc
    q $$$ERROR($$$GeneralError, "ISYCloud pro sdílení dat není dostupný ("_..Adresa()_"). Zkuste to později.")
}

/// Chyba API → obecná chyba s textem pro uživatele. Stavy, které řeší podpora, vysvětlí sama.
ClassMethod CheckOdpoved(stav As %Integer, odpoved As %String)
{
    q:(stav >= 200)&&(stav < 300)
    s kod = ..KodChyby(odpoved), text = ""
    i (kod = "firma_neaktivni") || (kod = "registrace_odmitnuta") {
        s text = "Firma nemá povolené sdílení dat. Povolí ho podpora M-line."
    } elseif kod = "firma_registrovana" {
        s text = "Sdílení dat má firma zaregistrované z jiného EDISONu. Novou registraci povolí podpora M-line."
    } else {
        try { s text = ##class(%DynamicObject).%FromJSON(odpoved).error } catch { }
    }
    s:text="" text = "ISYCloud odmítl požadavek sdílení dat (HTTP "_stav_")."
    $$$ThrowStatus($$$ERROR($$$GeneralError, text))
}

ClassMethod KodChyby(odpoved As %String) As %String
{
    s kod = ""
    try { s kod = ##class(%DynamicObject).%FromJSON(odpoved).kod } catch { }
    q kod
}

ClassMethod ParseJson(odpoved As %String) As %DynamicAbstractObject
{
    q:odpoved="" ""
    q ##class(%DynamicAbstractObject).%FromJSON(odpoved)
}

/// Tělo odpovědi jako řetězec (JSON přeložený podle znakové sady odpovědi, soubor v syrových bajtech).
ClassMethod ReadData(odpoved As %Net.HttpResponse) As %String
{
    s data = odpoved.Data
    q:'$isobject(data) data
    s text = ""
    d data.Rewind()
    while 'data.AtEnd { s text = text_data.Read(32000) }
    q text
}

ClassMethod Sha256Hex(bajty As %String) As %String
{
    s otisk = $system.Encryption.SHAHash(256, bajty), hex = ""
    f i = 1:1:$l(otisk) s hex = hex_$tr($j($zhex($a(otisk, i)), 2), " ", "0")
    q $zcvt(hex, "L")
}

/// Kód aktivní licence firmy (posílá se při registraci; ISYCloud zatím ukládá jen jeho otisk).
ClassMethod Licence(IdFa As %String) As %String
{
    q $g(^["MLINE"]ALVA.LICENCE(IdFa, $g(^["MLINE"]ALVA.LICENCE(IdFa), 0)))
}

/// Název Caché serveru STROJ:INSTANCE, jako klíč instalace AI (`ALVA.EDISONAI.API.Config.ServerName`).
ClassMethod ServerName() As %String
{
    s name = ""
    try {
        s host = $zcvt($SYSTEM.INetInfo.LocalHostName(), "U")
        s inst = $zcvt(##class(%SYS.System).GetInstanceName(), "U")
        s name = host_$s(inst'="":":"_inst, 1:"")
    } catch { s name = "" }
    q name
}

}
