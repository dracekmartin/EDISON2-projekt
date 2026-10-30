/// Slovník nabídky dat agend jízdních řádů (`B56` S9, `architektonicka-rozhodnuti-agendy.md` R8): popisek
/// a typ každého pole, které nabízejí aspoň dvě agendy, na jednom místě. Agenda v `GetSchema` jen
/// vyjmenuje, která pole nabízí a v jakém pořadí; pole jen jedné agendy si přidá sama přes `AddField`.
/// Popisek ze slovníku agenda přepíše jen u vědomého rozdílu (`AgendaSchemaDriftTests.ExemptLabel`).
/// Pole, které ve slovníku není, skončí chybou `<UNDEFINED>` - offline to dřív zachytí `AgendaSchemaDriftTests`.
Class ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat Extends %RegisteredObject
{

/// Přidá pole stránky (skalár) se jménem a typem ze slovníku.
ClassMethod AddPole(arr As %DynamicArray, klic As %String, popisek As %String = "")
{
    d ..DefinicePoli(.p)
    s def = p(klic)
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddField(arr, klic, $s(popisek'="":popisek, 1:$lg(def, 1)), $lg(def, 2))
}

/// Přidá kolekci s názvem ze slovníku a vrátí pole jejích polí, do kterého agenda pole doplní.
ClassMethod AddKolekce(collections As %DynamicArray, kolekce As %String) As %DynamicArray
{
    s def = ##class(%DynamicObject).%New()
    s def.name = $case(kolekce, "ZASTAVKY":"Zastávky", "SKUPINY_DNU":"Skupiny dnů", "ODJEZDY":"Odjezdy", "POZNAMKY":"Poznámky", "SPOJE":"Spoje")
    s def.collectionKey = kolekce
    s pole = ##class(%DynamicArray).%New()
    s def.fields = pole
    d collections.%Push(def)
    q pole
}

/// Přidá pole kolekce se jménem a typem ze slovníku. Kolekce je součástí klíče: totéž pole může
/// v kolekci znamenat něco jiného než na stránce (číslo zastávky CIS je jednou „aktuální", jednou ne).
ClassMethod AddPoleKolekce(arr As %DynamicArray, kolekce As %String, klic As %String)
{
    d ..DefinicePoliKolekci(.k)
    s def = k(kolekce, klic)
    d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddField(arr, klic, $lg(def, 1), $lg(def, 2))
}

/// Pole stránky: popisek a typ. Pořadí tu je jen pro čtení; v nabídce platí pořadí agendy.
ClassMethod DefinicePoli(ByRef p)
{
    // ── linka ──
    s p("CISLO_LINKY")            = $lb("Číslo linky (dle licence)", "number")
    s p("LOKALNI_OZNACENI_LINKY") = $lb("Veřejné označení linky", "text")
    s p("POPIS_LINKY")            = $lb("Název linky", "text")
    s p("TYP_JR")                 = $lb("Typ jízdního řádu", "text")
    s p("BARVA_LINKY")            = $lb("Barva linky", "color")
    s p("BARVA_LINKY_2")          = $lb("Barva linky 2", "color")
    // ── dopravce ──
    s p("POPIS_DOPRAVCE")         = $lb("Popis dopravce", "text")
    // Údaje po jednotlivých polích, aby si šablona složila jen to, co chce, a adresa nevyšla
    // dvakrát (#13158). Nesou prvního dopravce linky (`B194c` D4).
    s p("NAZEV_DOPRAVCE")         = $lb("Název dopravce", "text")
    s p("ADRESA_DOPRAVCE")        = $lb("Adresa dopravce", "text")
    s p("TELEFON_DOPRAVCE")       = $lb("Telefon dopravce", "text")
    s p("EMAIL_DOPRAVCE")         = $lb("E-mail dopravce", "text")
    s p("ICO_DOPRAVCE")           = $lb("IČO dopravce", "text")
    // ── platnost ──
    // Bez „Popisu platnosti" (Jan 23. 10.): větu si šablona složí z dat od-do.
    s p("DATUM_PLATNOSTI_OD")     = $lb("Datum platnosti od", "date")
    s p("DATUM_PLATNOSTI_DO")     = $lb("Datum platnosti do", "date")
    // ── zastávka a směr ──
    // „Název …", aby se v nabídce nepletl s příznakem řádku „Je aktuální zastávka" (Jan 27. 10.).
    s p("POPIS_ZASTAVKA")         = $lb("Název aktuální zastávky", "text")
    s p("CISLO_ZASTAVKA")         = $lb("Číslo aktuální zastávky", "text")
    s p("AKTUALNI_ZASTAVKA_CZST") = $lb("Číslo aktuální zastávky", "number")
    s p("SMER")                   = $lb("Směr (T/Z)", "text")
    s p("VYCHOZI_ZASTAVKA")       = $lb("Výchozí zastávka", "text")
    s p("KONECNA_ZASTAVKA")       = $lb("Konečná zastávka", "text")
    // ── poznámky a doplňky ──
    s p("POZNAMKA_NAD_LINKU")     = $lb("Poznámka nad linku", "text")
    s p("POZNAMKA_POD_DOPRAVCE")  = $lb("Poznámka pod dopravce", "text")
    s p("QR_KOD")                 = $lb("URL adresa pro QR kód", "text")
}

/// Pole kolekcí: popisek a typ podle kolekce. Spojovací klíče (ID, SKUPINA, ZASTAVKA_ID), interní kódy
/// (TYP, KATEGORIE) a prezentační nápovědy (FORE_COLOR, BACK_COLOR) data posílají, ale nabídka je neukazuje.
ClassMethod DefinicePoliKolekci(ByRef k)
{
    s k("ZASTAVKY", "NAZEV_ZASTAVKY")    = $lb("Název zastávky", "text")
    // Text, ne číslo: vedle minut nese „<" (spoj jede jinou trasou) a prázdno (mimo trasu).
    s k("ZASTAVKY", "CAS_JIZDY")         = $lb("Čas jízdy", "text")
    s k("ZASTAVKY", "PORADI")            = $lb("Pořadí", "number")
    s k("ZASTAVKY", "CZST")              = $lb("Číslo zastávky", "number")
    s k("ZASTAVKY", "AKTUALNI_ZASTAVKA") = $lb("Je aktuální zastávka", "boolean")
    s k("ZASTAVKY", "ZONY")              = $lb("Zóny", "text")
    s k("ZASTAVKY", "NASTUPISTE")        = $lb("Označník", "text")

    s k("SKUPINY_DNU", "NADPIS")         = $lb("Nadpis skupiny", "text")
    s k("SKUPINY_DNU", "NADPIS2")        = $lb("Nadpis skupiny 2", "text")
    s k("SKUPINY_DNU", "PORADI")         = $lb("Pořadí", "number")

    s k("ODJEZDY", "HODINA")             = $lb("Hodina", "number")
    s k("ODJEZDY", "MINUTA")             = $lb("Minuta", "number")

    s k("POZNAMKY", "DRUH")              = $lb("Druh poznámky", "number")
    s k("POZNAMKY", "ZNAK")              = $lb("Znak", "text")
    s k("POZNAMKY", "TEXT")              = $lb("Text poznámky", "text")

    s k("SPOJE", "CISLO_SPOJE")          = $lb("Číslo spoje", "number")
    s k("SPOJE", "NZ")                   = $lb("Negativní značka", "text")
}

}
