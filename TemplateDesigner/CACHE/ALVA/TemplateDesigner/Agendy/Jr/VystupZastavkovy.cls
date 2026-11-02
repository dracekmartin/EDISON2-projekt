/// Stavba výstupu zastávkového JŘ (vývěsky), společná vývěsce AD i MHD (`B56` S3,
/// `architektonicka-rozhodnuti-agendy.md` R8): řádky zastávek, skupiny dnů, odjezdy a poznámky ve tvaru,
/// který čtou prvky (`KontraktDat.cs`). Data dostává parametry, do pracovního globálu agendy nesahá
/// (`cache-api.md` pravidlo 6). Do 30. 10. měla každá vývěska vlastní kopii.
/// Značky u odjezdů (`AddAnotace`) tu nejsou: vývěska AD posílá navíc kategorii, spoj a rozsah
/// „nepoznámkovat" a MHD je posílat nesmí (`legacy-nahrada.md §4`).
Class ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy Extends %RegisteredObject
{

/// Řádek zastávky s prázdnými poli zón, atributů a poznámek, které agenda doplní.
/// `casJizdy` má tři stavy jako `TRASA_MIN`: číslo, „<" (základní trasa projíždí bez stání) nebo
/// prázdno (mimo základní trasu).
ClassMethod BuildZastavka(id As %String, popis As %String, tc As %String = "", casJizdy As %String = 0, poradi As %Integer = 1, jeAktualni As %Boolean = 0, naZnameni As %Boolean = 0, odsazeni As %Integer = 0) As %DynamicObject
{
    s zastavka = ##class(%DynamicObject).%New()
    // Čitelná field ID (SLOVO_SLOVO) - sdílený kontrakt sloupců StopList↔DynamicTable.
    // TC vědomě nevystaveno; poziční potřebu pokrývá PORADI.
    d zastavka.%Set("ID", id)                          // interní join klíč (ODJEZDY.ZASTAVKA_ID), ne ve schématu
    d zastavka.%Set("NAZEV_ZASTAVKY", popis)
    d zastavka.%Set("CAS_JIZDY", casJizdy)
    d zastavka.%Set("PORADI", poradi)
    d zastavka.%Set("AKTUALNI_ZASTAVKA", $s(jeAktualni:"true", 1:"false"))
    d zastavka.%Set("NA_ZNAMENI", $s(naZnameni:"true", 1:"false"))
    d zastavka.%Set("ODSAZENI", odsazeni)
    d zastavka.%Set("ZONY", ##class(%DynamicArray).%New())
    d zastavka.%Set("ATRIBUTY", ##class(%DynamicArray).%New())
    d zastavka.%Set("POZNAMKY_ZASTAVKY", ##class(%DynamicArray).%New())
    q zastavka
}

ClassMethod AddZona(zastavka As %DynamicObject, zona As %String)
{
    d zastavka.%Get("ZONY").%Push(zona)
}

ClassMethod AddAtribut(zastavka As %DynamicObject, znak As %String, popis As %String = "")
{
    s atribut = ##class(%DynamicObject).%New()
    d atribut.%Set("ZNAK", znak)
    d atribut.%Set("POPIS", popis)
    d zastavka.%Get("ATRIBUTY").%Push(atribut)
}

ClassMethod AddPoznamkaZastavky(zastavka As %DynamicObject, znak As %String, typ As %String = "text", text As %String = "")
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("TEXT", text)
    d zastavka.%Get("POZNAMKY_ZASTAVKY").%Push(poznamka)
}

ClassMethod BuildSkupinaDnu(id As %String, nadpis As %String, nadpis2 As %String = "", poradi As %Integer = 1) As %DynamicObject
{
    s skupina = ##class(%DynamicObject).%New()
    d skupina.%Set("ID", id)
    d skupina.%Set("NADPIS", nadpis)
    d skupina.%Set("NADPIS2", nadpis2)
    d skupina.%Set("PORADI", poradi)
    q skupina
}

ClassMethod BuildOdjezd(zastavkaId As %String, skupinaDnuId As %String, hodina As %Integer, minuta As %Integer) As %DynamicObject
{
    s odjezd = ##class(%DynamicObject).%New()
    d odjezd.%Set("ZASTAVKA_ID", zastavkaId)
    d odjezd.%Set("SKUPINA_DNU_ID", skupinaDnuId)
    d odjezd.%Set("HODINA", hodina)
    d odjezd.%Set("MINUTA", minuta)
    d odjezd.%Set("FORE_COLOR", "")
    d odjezd.%Set("BACK_COLOR", "")
    d odjezd.%Set("ANOTACE", ##class(%DynamicArray).%New())
    q odjezd
}

/// `kategorie` (atribut-zastavky / bezbarier / rozdil-trasy / rucni-komentar / omezeni / smerove)
/// umožní TD filtru prořezat legendu jen v relevantní kategorii (např. „x - na znamení" se nesmí
/// smazat jen proto, že nemá anotaci u minut). Prázdná = filtr ji neschová (#7161).
ClassMethod BuildPoznamka(typ As %String, znak As %String, text As %String, foreColor As %String = "", backColor As %String = "", kategorie As %String = "") As %DynamicObject
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TEXT", text)
    d poznamka.%Set("FORE_COLOR", foreColor)
    d poznamka.%Set("BACK_COLOR", backColor)
    d poznamka.%Set("KATEGORIE", kategorie)
    q poznamka
}

/// Konvertuje 8-bit integer (0-255) na dvouznakovou velkou hex reprezentaci.
ClassMethod IntToHex2(n As %Integer) As %String [ Private ]
{
    s digits = "0123456789ABCDEF"
    i n < 0 s n = 0
    i n > 255 s n = 255
    q $e(digits, (n\16) + 1) _ $e(digits, (n#16) + 1)
}

/// Konvertuje RGB string "R;G;B" (např. "255;191;128") na SkiaSharp hex "#FFRRGGBB"; prázdný vstup →
/// prázdno. „0;0;0" je černá - barva linky a poznámek MHD. Barva spoje AD, kde „0;0;0" znamená
/// „bez barvy", jde přes `Jr.DataAd.BarvaSpoje` (dva zdroje, ne kopie - `legacy-nahrada.md §4`).
ClassMethod RgbToHex(rgb As %String) As %String
{
    i rgb = "" q ""
    s r = +$p(rgb,";",1)
    s g = +$p(rgb,";",2)
    s b = +$p(rgb,";",3)
    q "#FF"_..IntToHex2(r)_..IntToHex2(g)_..IntToHex2(b)
}

/// Konvertuje číselný kód speciálního znaku na Unicode string: 1-7 = dny v týdnu, nad 31 = znak
/// fontu (`$char`), jinak beze změny.
ClassMethod ConvertSpecialniZnak(cislo As %String) As %String
{
    i cislo = 1 q "Po"
    i cislo = 2 q "Út"
    i cislo = 3 q "St"
    i cislo = 4 q "Čt"
    i cislo = 5 q "Pá"
    i cislo = 6 q "So"
    i cislo = 7 q "Ne"
    i +cislo > 31 q $c(cislo)
    q cislo
}

}
