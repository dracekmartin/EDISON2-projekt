Include GENERAL

/// Data linkového JŘ pro webovou aplikaci pro cestující (demo `edwcore/apps/tisky-qr`).
///
/// Tenká JSON obálka nad `Agendy.LinkovyJrAd.GetData`, volaná přes MLRest `QueryJSON`
/// (signatura `(sys, in, out)`). Výstup agendy se vrací **beze změny** - co jede dnes a kam,
/// počítá aplikace. Tisk se touhle třídou nemění.
///
/// IN:  {"idfa":"CB_CSADAD","linka":"330510","tc":"14"}
/// OUT: {"success":true,"data":{"skupina":1,"linka":"0330510","stranky":[<stránka GetData>,...]}}
///      chyba {"success":false,"error":{"code":"VSTUP|NENALEZENO|SYSTEM","message":"..."}}
Class ALVA.TemplateDesigner.API.Cestujici Extends %RegisteredObject
{

/// Technická identita volajícího v kontextu ERP - `$$$FillParams` na prázdném jménu spadne.
Parameter VOLAJICI = "TISKY-QR";

/// Stránky linkového JŘ (oba směry) se zvýrazněnou zastávkou výlepu.
///
/// Skupina linek se do QR nedává (mění se s novým jízdním řádem), dohledá se tady:
/// bere se **nejvyšší** skupina, ve které linka je. Pro demo to stačí; správně by rozhodla
/// platnost vůči dnešku.
ClassMethod Linka(ByRef sys, ByRef in, ByRef out) As %Status
{
    s err = "", code = ""
    try {
        // QueryJSON předává tělo už rozparsované; text nebo stream jen pro jiné volající.
        i $isobject(in) && in.%IsA("%DynamicAbstractObject") { s req = in }
        else { s req = ##class(%DynamicObject).%FromJSON(in) }
        s idfa = req.%Get("idfa"), linka = req.%Get("linka"), tc = req.%Get("tc")
        i (idfa = "") || (linka = "") s code = "VSTUP", err = "Chybí idfa nebo linka" ztrap
        d ..BuildSys(idfa, .sys)
        s dataDB = sys("U","DATA")
        s lin = $tr($j(linka, 7), " ", "0")

        s skupina = "", sku = ""
        f {
            s sku = $o(^[dataDB]JRL(sku), -1)
            q:sku=""
            i $isvalidnum(sku) && $d(^[dataDB]JRL(sku, lin, "I")) s skupina = sku q
        }
        i skupina = "" s code = "NENALEZENO", err = "Linka "_linka_" neexistuje" ztrap

        k ^||ML("IN"), ^||ML("OUT"), ^||ML("ERR")
        s ^||ML("IN","SKUPINA_LINEK_ID") = skupina
        s ^||ML("IN","LINKA_ID") = lin
        s ^||ML("IN","ZVYRAZNIT_TC") = tc
        d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)
        i $g(^||ML("ERR")) '= "" s code = "SYSTEM", err = ^||ML("ERR") ztrap

        s stranky = ##class(%DynamicArray).%New(), key = ""
        f {
            s key = $o(^||ML("OUT", key))
            q:key=""
            continue:key="ERR"
            d stranky.%Push(##class(%DynamicObject).%FromJSON(^||ML("OUT", key)))
        }
        k ^||ML("IN"), ^||ML("OUT")
        s out = {"success":true,"data":{"skupina":(skupina),"linka":(lin),"stranky":(stranky)}}
    }
    catch ex {
        i err = "" s err = ex.DisplayString()
        i code = "" s code = "SYSTEM"
        i $isobject($g(%response)) s %response.Status = $s(code = "SYSTEM":500, 1:400)
        s out = {"success":false,"error":{"code":(code),"message":(err)}}
    }
    q $$$OK
}

/// Kontext ERP pro `$$$FillParams` agendy (opis `ALWA.EDISONKA.CACHE.Sys.Base.BuildSys`).
ClassMethod BuildSys(idfa As %String, ByRef sys As %String)
{
    k sys("U")
    s sys("U","IdFa") = idfa
    s sys("U","delimiter") = ##class(ALVA.EDISON.Main).GetDelimiter()
    s sys("U","SystemDB") = ##class(ALVA.EDISON.Main).GetSystemDB()
    s sys("U","DATA") = ##class(ALVA.GENERALAPI.DataBase).dataDB(idfa)
    s sys("U","MLP") = 0
    s sys("U","loginName") = ..#VOLAJICI
    s sys("U","fullName") = ..#VOLAJICI
}

}
