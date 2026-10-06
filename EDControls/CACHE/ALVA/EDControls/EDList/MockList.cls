Include EDList

/// Testovací třída s hardcoded daty pro ověření EDList bez reálné databáze.
/// Pokrývá všechny aktuálně implementované datové typy,
/// aby šlo testovat rendering v EDListTestWindow.
Class ALVA.EDControls.EDList.MockList Extends %RegisteredObject
{

ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s list = ##class(ALVA.EDControls.EDList.List).%New()

    d list.AddColumn("rz",           "RZ",         $$$ListColumnTypeText,     110)
    d list.AddColumn("typ",         "Typ",        $$$ListColumnTypeText,     130)
    d list.AddColumn("aktivni",      "Aktivní",    $$$ListColumnTypeBool,      70)
    d list.AddColumn("odjezd",       "Odjezd",     $$$ListColumnTypeHHMM,      75, $$$ListAlignRight)
    d list.AddColumn("pocet",        "Počet km",   $$$ListColumnTypeNumber,    90, $$$ListAlignRight, 1, "N1")
    d list.AddColumn("datum",        "Datum",      $$$ListColumnTypeDate,     100)
    d list.AddColumn("posledniZmena","Naposledy",  $$$ListColumnTypeDateTime, 140) ; DATETIME test
    d list.AddColumn("barva",        "Barva",      $$$ListColumnTypeColor,     60) ; COLOR test (hex, poslední řádek legacy R;G;B)

    d list.WriteColumnsToOut()
    q 1
}

ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s list = ##class(ALVA.EDControls.EDList.List).%New()

    s row = list.AddRow("1")
    s row.rz             = "1B2 3456"
    s row.typ            = "Kloubový"
    s row.aktivni        = "1"
    s row.odjezd         = "830"
    s row.pocet          = "142.5"
    s row.datum          = "2024-03-15"
    s row.posledniZmena  = "2024-03-15T08:30:00"
    s row.barva          = "#FFFF8000"                            ; oranžová

    s row = list.AddRow("2")
    s row.rz             = "2C3 7890"
    s row.typ            = "Standardní"
    s row.aktivni        = "0"
    s row.odjezd         = "1245"
    s row.pocet          = "98"
    s row.datum          = "2024-03-16"
    s row.posledniZmena  = "2024-03-16T14:22:15"
    s row.barva          = "#FF0080FF"                            ; modrá

    s row = list.AddRow("3")
    s row.rz             = "3D4 1122"
    s row.typ            = "Kloubový"
    s row.aktivni        = "1"
    s row.odjezd         = "600"
    s row.pocet          = "210.75"
    s row.datum          = "2024-03-17"
    s row.posledniZmena  = "2024-03-17T19:05:48"
    s row.barva          = "#FF800080"                            ; fialová

    s row = list.AddRow("4")
    s row.rz             = "4E5 9988"
    s row.typ            = "Mikrobus"
    s row.aktivni        = "1"
    s row.odjezd         = "1530"
    s row.pocet          = "45.2"
    s row.datum          = "2024-03-18"
    s row.posledniZmena  = "2024-03-18T11:15:33"
    s row.barva          = "#FF00B400"                            ; zelená

    s row = list.AddRow("5")
    s row.rz             = "5F6 4433"
    s row.typ            = "Standardní"
    s row.aktivni        = "0"
    s row.odjezd         = "2100"
    s row.pocet          = "0"
    s row.datum          = "2024-03-19"
    s row.posledniZmena  = "2024-03-19T22:00:00"
    s row.barva          = "200;200;200"                          ; šedá, legacy R;G;B

    d list.WriteRowsToOut()
    q 1
}

}
