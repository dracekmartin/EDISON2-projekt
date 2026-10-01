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
            s stranka = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            d ..PridejOmezeni(dataDB, skupina, lin, stranka)
            d ..PridejPolohy(dataDB, stranka)
            d stranky.%Push(stranka)
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

/// Ke každému spoji stránky přidá `OMEZENI` - datumová omezení jako data, aby aplikace sama
/// poznala, jestli spoj jede dnes. Tisk je má jen jako text (`SestavTextOmezeni`), který se skládá
/// podle nastavení firmy a může být i bez roku, takže se z něj spolehlivě číst nedá.
///   OMEZENI: [{"druh":"jen|jede|take|nejede","useky":[{"od":"2022-12-24","do":"2023-01-01"}]}]
///   TYDNY:   "sude" | "liche" | ""  (dlouhodobá platnost spoje)
ClassMethod PridejOmezeni(dataDB As %String, sku As %String, lin As %String, stranka As %DynamicObject)
{
    s spoje = stranka.rows.%Get("SPOJE")
    q:'$isobject(spoje)
    s it = spoje.%GetIterator()
    while it.%GetNext(.i, .spoj) {
        s smer = spoj.%Get("SMER"), sp = spoj.%Get("CISLO_SPOJE")
        continue:(smer = "")||(sp = "")
        s dlouhodoba = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",8)
        d spoj.%Set("TYDNY", $s(dlouhodoba = 4:"sude", dlouhodoba = 5:"liche", 1:""))
        s omezeni = ##class(%DynamicArray).%New()
        // Druhy omezení podle klíče pod spojem (význam viz `LinkovyJrAd.SestavTextOmezeni`).
        f omzId=11,12,13,22 {
            s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
            continue:omz=""
            s data = $s(omz["@":$p(omz,"@",2), 1:omz)
            s useky = ##class(%DynamicArray).%New()
            f j = 1:1:$l(data,"#") {
                s polozka = $p(data,"#",j)
                continue:polozka=""
                s od = ..IsoDatum($p(polozka,":",1)), do = ..IsoDatum($p(polozka,":",2))
                continue:od=""
                d useky.%Push({"od":(od),"do":($s(do'="":do, 1:od))})
            }
            continue:useky.%Size()=0
            d omezeni.%Push({"druh":($case(omzId, 11:"jen", 12:"take", 13:"jede", :"nejede")),"useky":(useky)})
        }
        d spoj.%Set("OMEZENI", omezeni)
    }
}

/// Souřadnice pro mapu: ke každému řádku zastávky `LAT`/`LON` z číselníku zastávek (`^JRZ` pole
/// 12 a 13) a k zastávce výlepu `SLOUPKY` - sloupky s GPS (`^JRZ(czst,"Sloupek",id)` =
/// `číslo~lat~lon~popis směru`). Sloupky má jen část zastávek, bez nich je známá jen zastávka.
ClassMethod PridejPolohy(dataDB As %String, stranka As %DynamicObject)
{
    s zastavky = stranka.rows.%Get("ZASTAVKY")
    q:'$isobject(zastavky)
    s it = zastavky.%GetIterator()
    while it.%GetNext(.i, .radek) {
        s czst = radek.%Get("CZST")
        continue:czst=""
        s rec = $g(^[dataDB]JRZ(czst))
        s lat = $p(rec,"$",12), lon = $p(rec,"$",13)
        i $isvalidnum(lat) && $isvalidnum(lon) && (+lat '= 0) {
            d radek.%Set("LAT", +lat), radek.%Set("LON", +lon)
        }
        continue:radek.%Get("AKTUALNI_ZASTAVKA")'="true"
        s sloupky = ##class(%DynamicArray).%New(), id = ""
        f {
            s id = $o(^[dataDB]JRZ(czst,"Sloupek",id))
            q:id=""
            s sloupek = $g(^[dataDB]JRZ(czst,"Sloupek",id))
            s sLat = $p(sloupek,"~",2), sLon = $p(sloupek,"~",3)
            continue:'$isvalidnum(sLat)||'$isvalidnum(sLon)||(+sLat=0)
            d sloupky.%Push({"cislo":($p(sloupek,"~",1)),"lat":(+sLat),"lon":(+sLon),"popis":($p(sloupek,"~",4))})
        }
        d radek.%Set("SLOUPKY", sloupky)
    }
}

/// `d.m.yyyy` → `yyyy-mm-dd`; neúplné datum → "".
ClassMethod IsoDatum(datum As %String) As %String
{
    s d = +$p(datum,".",1), m = +$p(datum,".",2), r = $p(datum,".",3)
    i (d = 0) || (m = 0) || ($l(r) '= 4) q ""
    q r_"-"_$tr($j(m,2)," ","0")_"-"_$tr($j(d,2)," ","0")
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
