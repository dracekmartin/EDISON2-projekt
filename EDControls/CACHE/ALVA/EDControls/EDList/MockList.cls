/// Testovací třída s hardcoded daty pro ověření EDList bez reálné databáze.
Class ALVA.EDControls.EDList.MockList Extends %RegisteredObject
{

ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s list = ##class(ALVA.EDControls.EDList.List).%New()
    d list.AddColumn("rz",      "RZ",          "TEXT",   110)
    d list.AddColumn("typ",     "Typ",          "TEXT",   130)
    d list.AddColumn("aktivni", "Aktivní",      "BOOL",    70)
    d list.AddColumn("odjezd",  "Odjezd",       "HHMM",    75, "RIGHT")
    s col = list.AddColumn("pocet", "Počet km", "NUMBER",  90, "RIGHT", 1, "N1")
    d list.AddColumn("datum",   "Datum",        "DATE",   100)
    d list.WriteColumnsToOut()
    q 1
}

ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s list = ##class(ALVA.EDControls.EDList.List).%New()

    s row = list.AddRow("1")
    s row.rz      = "1B2 3456"
    s row.typ     = "Kloubový"
    s row.aktivni = "1"
    s row.odjezd  = "830"
    s row.pocet   = "142.5"
    s row.datum   = "2024-03-15"

    s row = list.AddRow("2")
    s row.rz      = "2C3 7890"
    s row.typ     = "Standardní"
    s row.aktivni = "0"
    s row.odjezd  = "1245"
    s row.pocet   = "98"
    s row.datum   = "2024-03-16"

    s row = list.AddRow("3")
    s row.rz      = "3D4 1122"
    s row.typ     = "Kloubový"
    s row.aktivni = "1"
    s row.odjezd  = "600"
    s row.pocet   = "210.75"
    s row.datum   = "2024-03-17"

    d list.WriteRowsToOut()
    q 1
}

}
