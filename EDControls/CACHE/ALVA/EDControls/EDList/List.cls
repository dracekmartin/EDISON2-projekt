Include (%occInclude, EDList)

Class ALVA.EDControls.EDList.List Extends %RegisteredObject
{

Property columns As list Of ALVA.EDControls.EDList.Column;

Property rows As %DynamicArray;

Method %OnNew() As %Status
{
    s ..rows = ##class(%DynamicArray).%New()
    q 1
}

/// Přidá sloupec a vrátí ho pro případné další nastavení (visible apod.).
/// type a align z EDList.inc; neznámou hodnotu odmítne, klient by ji tiše zobrazil jako TEXT / zarovnání dle typu.
/// format: NUMBER → "N0"/"N2", DATE/DATETIME → "dd.MM.yyyy", ostatní prázdný.
Method AddColumn(key As %String, label As %String, type As %String = "TEXT", width As %Integer = 0, align As %String = "", sortable As %Boolean = 0, format As %String = "") As ALVA.EDControls.EDList.Column
{
    d ..ValidateColumn(key, type, align)
    s col = ##class(ALVA.EDControls.EDList.Column).%New()
    s col.key      = key
    s col.label    = label
    s col.type     = type
    s col.width    = width
    s col.align    = align
    s col.sortable = sortable
    s col.format   = format
    d ..columns.Insert(col)
    q col
}

/// Volá ji AddColumn i WriteColumnsToOut - type/align jdou nastavit i přímo na vráceném sloupci.
ClassMethod ValidateColumn(key As %String, type As %String, align As %String) [ Private ]
{
    if '..IsKnownType(type) $$$ThrowStatus($$$ERROR($$$GeneralError, "EDList: neznámý typ sloupce '"_type_"' (sloupec '"_key_"')"))
    if '..IsKnownAlign(align) $$$ThrowStatus($$$ERROR($$$GeneralError, "EDList: neznámé zarovnání '"_align_"' (sloupec '"_key_"')"))
}

/// Klient parsuje typ bez ohledu na velikost písmen, proto i tady; prázdný = TEXT.
ClassMethod IsKnownType(type As %String) As %Boolean
{
    q (type = "") || ($lf($$$ListColumnTypes, $zcvt(type, "U")) > 0)
}

/// Prázdné zarovnání = podle typu sloupce.
ClassMethod IsKnownAlign(align As %String) As %Boolean
{
    q (align = "") || ($lf($$$ListAligns, $zcvt(align, "U")) > 0)
}

/// Vytvoří nový řádek s daným id, přidá ho do interní %DynamicArray a vrátí ho.
/// Volající nastaví pole přímo:  s row.nazev = "Praha"
Method AddRow(id As %String) As %DynamicObject
{
    s row    = ##class(%DynamicObject).%New()
    s row.id = id
    d ..rows.%Push(row)
    q row
}

/// Zapíše OUT[n] = JSON pole definic sloupců (jedno volání = jeden OUT záznam).
Method WriteColumnsToOut()
{
    s arr = ##class(%DynamicArray).%New()
    f i=1:1:..columns.Count() {
        s col = ..columns.GetAt(i)
        d ..ValidateColumn(col.key, col.type, col.align)
        s obj = ##class(%DynamicObject).%New()
        s obj.key      = col.key
        s obj.label    = col.label
        s obj.type     = col.type
        s obj.format   = col.format
        s obj.width    = col.width
        s obj.align    = col.align
        s obj.sortable = col.sortable
        s obj.visible  = col.visible
        s:col.iconKey'="" obj.iconKey = col.iconKey
        s:col.trailingIconKey'="" obj.trailingIconKey = col.trailingIconKey
        d arr.%Push(obj)
    }
    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
}

/// Zapíše OUT[n], OUT[n+1], ... = jeden JSON objekt na řádek.
Method WriteRowsToOut()
{
    f i=0:1:..rows.%Size()-1 {
        s ^||ML("OUT", $i(^||ML("OUT"))) = ..rows.%Get(i).%ToJSON()
    }
}

}
