/// Agenda FAKTURY — fakturační sestavy.
///
/// Data:
/// - **Skalární pole faktury**: číslo, datumy (vystavení/splatnost), variabilní/konstantní/specifický symbol,
///   dodavatel (název, IČ, DIČ, adresa, kontakty), odběratel (název, IČ, DIČ, adresa),
///   platební údaje (číslo účtu, IBAN, SWIFT), částky (základ daně, DPH, celkem), poznámky.
/// - **Tabulková kolekce POLOZKY**: jednotlivé položky faktury (popis, kód, množství,
///   měrná jednotka, jednotková cena, celkem, sazba DPH).
///
/// Fixed šablony tabulek: shrnutí faktury (základ + DPH + celkem), platební údaje.
///
/// Aktuálně **referenční implementace s mock daty** — reálné DB volání (GetData)
/// vyžaduje napojení na konkrétní strukturu fakturačního systému (TODO).
Class ALVA.TemplateDesigner.Agendy.Faktury Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaCode = "FAKTURY";

Parameter AgendaLabel = "Faktury";

Parameter InstanceLabel = "faktura";

Parameter InstanceLabelPlural = "faktury";

/// Vrátí schéma polí FAKTURY agendy: skalární pole + položky faktury.
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    s def = ##class(%DynamicObject).%New()

    // ── Skalární pole ─────────────────────────────────────────────────
    s fields = ##class(%DynamicArray).%New()

    // Čísla a datumy
    d ..AddField(fields, "CISLO_FAKTURY",      "Číslo faktury",           "text",   "2024001")
    d ..AddField(fields, "DATUM_VYSTAVENI",    "Datum vystavení",         "date",   "15.03.2024")
    d ..AddField(fields, "DATUM_SPLATNOSTI",   "Datum splatnosti",        "date",   "29.03.2024")
    d ..AddField(fields, "VS",                 "Variabilní symbol",       "text",   "2024001")
    d ..AddField(fields, "KS",                 "Konstantní symbol",       "text",   "0308")
    d ..AddField(fields, "SS",                 "Specifický symbol",       "text",   "")

    // Dodavatel
    d ..AddField(fields, "NAZEV_DODAVATELE",   "Název dodavatele",        "text",   "ACME s.r.o.")
    d ..AddField(fields, "IC_DODAVATELE",      "IČ dodavatele",           "text",   "12345678")
    d ..AddField(fields, "DIC_DODAVATELE",     "DIČ dodavatele",          "text",   "CZ12345678")
    d ..AddField(fields, "ADRESA_DODAVATELE",  "Adresa dodavatele",       "text",   "Pražská 123, 110 00 Praha 1")
    d ..AddField(fields, "TELEFON_DODAVATELE", "Telefon dodavatele",      "text",   "+420 777 123 456")
    d ..AddField(fields, "EMAIL_DODAVATELE",   "E-mail dodavatele",       "text",   "info@acme.cz")

    // Odběratel
    d ..AddField(fields, "NAZEV_ODBERATELE",   "Název odběratele",        "text",   "Beta a.s.")
    d ..AddField(fields, "IC_ODBERATELE",      "IČ odběratele",           "text",   "87654321")
    d ..AddField(fields, "DIC_ODBERATELE",     "DIČ odběratele",          "text",   "CZ87654321")
    d ..AddField(fields, "ADRESA_ODBERATELE",  "Adresa odběratele",       "text",   "Brněnská 456, 602 00 Brno")

    // Platba
    d ..AddField(fields, "CISLO_UCTU",         "Číslo účtu",              "text",   "123456789/0100")
    d ..AddField(fields, "IBAN",               "IBAN",                    "text",   "CZ6501000000001234567899")
    d ..AddField(fields, "SWIFT",              "SWIFT/BIC",               "text",   "KOMBCZPP")

    // Částky
    d ..AddField(fields, "ZAKLAD_DANE",        "Základ daně",             "number", "10000.00")
    d ..AddField(fields, "DPH",                "DPH 21%",                 "number", "2100.00")
    d ..AddField(fields, "CELKOVA_CASTKA",     "Celková částka",          "number", "12100.00")
    d ..AddField(fields, "MENA",               "Měna",                    "text",   "CZK")

    // Poznámky
    d ..AddField(fields, "POZNAMKA",           "Poznámka",                "text",   "Děkujeme za Vaši objednávku")
    d ..AddField(fields, "ZPUSOB_PLATBY",      "Způsob platby",           "text",   "Převodem")

    s def.fields = fields

    // ── Tabulkové kolekce (fieldCollections) ─────────────────────────
    s collections = ##class(%DynamicArray).%New()

    // Položky faktury
    s gPolozky = ##class(%DynamicObject).%New()
    s gPolozky.name          = "Položky faktury"
    s gPolozky.collectionKey = "POLOZKY"
    s gPolozkyFields = ##class(%DynamicArray).%New()
    d ..AddField(gPolozkyFields, "POPIS",      "Popis položky",    "text")
    d ..AddField(gPolozkyFields, "KOD",        "Kód položky",      "text")
    d ..AddField(gPolozkyFields, "MNOZSTVI",   "Množství",         "number")
    d ..AddField(gPolozkyFields, "MJ",         "Měrná jednotka",   "text")
    d ..AddField(gPolozkyFields, "CENA",       "Jednotková cena",  "number")
    d ..AddField(gPolozkyFields, "CELKEM",     "Celkem",           "number")
    d ..AddField(gPolozkyFields, "DPH_SAZBA",  "Sazba DPH (%)",    "number")
    s gPolozky.fields = gPolozkyFields
    d collections.%Push(gPolozky)

    s def.fieldCollections = collections

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    q 1
}

/// Vrátí seznam dostupných Fixed šablon tabulek pro FAKTURY agendu.
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    s t1 = ##class(%DynamicObject).%New()
    s t1.key   = "FAKTURA_SHRNUTI"
    s t1.label = "Shrnutí faktury (základ, DPH, celkem)"
    d arr.%Push(t1)

    s t2 = ##class(%DynamicObject).%New()
    s t2.key   = "FAKTURA_PLATBA"
    s t2.label = "Platební údaje"
    d arr.%Push(t2)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q 1
}

/// Vrátí definici konkrétní Fixed šablony (sloupce + řádky).
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s key = $g(^||ML("IN", "key"))

    if key = "FAKTURA_SHRNUTI" {
        d ..WriteTemplate2Col([
            ["Základ daně",      "{ZAKLAD_DANE}",      "N2"],
            ["DPH 21%",          "{DPH}",              "N2"],
            ["Celková částka",   "{CELKOVA_CASTKA}",   "N2"]
        ])
        q 1
    }

    if key = "FAKTURA_PLATBA" {
        d ..WriteTemplate2Col([
            ["Číslo účtu",       "{CISLO_UCTU}",       ""],
            ["Variabilní symbol","{VS}",               ""],
            ["Částka k úhradě",  "{CELKOVA_CASTKA}",   "N2"],
            ["Datum splatnosti", "{DATUM_SPLATNOSTI}", ""]
        ])
        q 1
    }

    q 1
}

/// Vrátí parametry pro interaktivní preview (checkboxy, výběry apod.).
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    // Formát částek
    s pFormat = ##class(%DynamicObject).%New()
    s pFormat.name  = "FORMAT_CASTKY"
    s pFormat.label = "Formát částek"
    s pFormat.type  = "enum"
    d pFormat.%Set("default", "N2")
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("N2")
    d opts.%Push("N0")
    d opts.%Push("#,##0.00")
    s pFormat.options = opts
    d arr.%Push(pFormat)

    // Tisk poznámky
    d ..AddBoolParam(arr, "TISK_POZNAMKY", "Tisknout poznámku", 1)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q 1
}

/// Vrátí data pro tisk (runtime — implementuje reálné DB volání).
/// Tato metoda je zodpovědností konkrétní agendy a závisí na databázové struktuře.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    // TODO: implementovat reálné načtení dat z databáze
    q 1
}

/// Vrátí ukázková data pro live preview v designeru.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s out = ##class(%DynamicObject).%New()

    // ── Skalární hodnoty ──────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_FAKTURY",      "2024001")
    d vals.%Set("DATUM_VYSTAVENI",    "15.03.2024")
    d vals.%Set("DATUM_SPLATNOSTI",   "29.03.2024")
    d vals.%Set("VS",                 "2024001")
    d vals.%Set("KS",                 "0308")
    d vals.%Set("SS",                 "")
    d vals.%Set("NAZEV_DODAVATELE",   "ACME s.r.o.")
    d vals.%Set("IC_DODAVATELE",      "12345678")
    d vals.%Set("DIC_DODAVATELE",     "CZ12345678")
    d vals.%Set("ADRESA_DODAVATELE",  "Pražská 123, 110 00 Praha 1")
    d vals.%Set("TELEFON_DODAVATELE", "+420 777 123 456")
    d vals.%Set("EMAIL_DODAVATELE",   "info@acme.cz")
    d vals.%Set("NAZEV_ODBERATELE",   "Beta a.s.")
    d vals.%Set("IC_ODBERATELE",      "87654321")
    d vals.%Set("DIC_ODBERATELE",     "CZ87654321")
    d vals.%Set("ADRESA_ODBERATELE",  "Brněnská 456, 602 00 Brno")
    d vals.%Set("CISLO_UCTU",         "123456789/0100")
    d vals.%Set("IBAN",               "CZ6501000000001234567899")
    d vals.%Set("SWIFT",              "KOMBCZPP")
    d vals.%Set("ZAKLAD_DANE",        "10000.00")
    d vals.%Set("DPH",                "2100.00")
    d vals.%Set("CELKOVA_CASTKA",     "12100.00")
    d vals.%Set("MENA",               "CZK")
    d vals.%Set("POZNAMKA",           "Děkujeme za Vaši objednávku")
    d vals.%Set("ZPUSOB_PLATBY",      "Převodem")
    s out.values = vals

    // ── Tabulkové kolekce ─────────────────────────────────────────────
    s rows = ##class(%DynamicObject).%New()

    // Položky faktury
    s dphSazba = 21  ; DPH 21% - standardní sazba
    s polozky = ##class(%DynamicArray).%New()
    d polozky.%Push(..MakePolozka("Vývoj software", "SW001", 40,  1500, dphSazba))
    d polozky.%Push(..MakePolozka("Konzultace",     "KONZ1", 8,   800,  dphSazba))
    d polozky.%Push(..MakePolozka("Hosting",        "HOST1", 1,   500,  dphSazba))
    d polozky.%Push(..MakePolozka("Doména",         "DOM1",  1,   200,  dphSazba))
    s rows.POLOZKY = polozky

    s out.rows = rows

    s ^||ML("OUT", $i(^||ML("OUT"))) = out.%ToJSON()
    q 1
}

ClassMethod MakePolozka(popis As %String, kod As %String, mnozstvi As %Numeric, cena As %Numeric, dphSazba As %Numeric, mj As %String = "hod") As %DynamicObject
{
    s p = ##class(%DynamicObject).%New()
    d p.%Set("POPIS",      popis)
    d p.%Set("KOD",        kod)
    d p.%Set("MNOZSTVI",   mnozstvi)
    d p.%Set("MJ",         mj)
    d p.%Set("CENA",       cena)
    d p.%Set("CELKEM",     mnozstvi * cena)
    d p.%Set("DPH_SAZBA",  dphSazba)
    q p
}

}
