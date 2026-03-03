Class ALVA.EDControls.EDList.List Extends %RegisteredObject
{

Property columns As list Of ALVA.EDControls.EDList.Column;

Property rows As %DynamicArray;

// ─── Inicializace ─────────────────────────────────────────────────────────────

Method %OnNew() As %Status
{
    s ..rows = ##class(%DynamicArray).%New()
    q 1
}

// ─── Builder — sloupce ────────────────────────────────────────────────────────

/// Přidá sloupec a vrátí ho pro případné další nastavení (isLink, format apod.).
Method AddColumn(key As %String, label As %String, type As %String = "TEXT", width As %Integer = 0, align As %String = "", sortable As %Boolean = 0) As ALVA.EDControls.EDList.Column
{
    s col = ##class(ALVA.EDControls.EDList.Column).%New()
    s col.key      = key
    s col.label    = label
    s col.type     = type
    s col.width    = width
    s col.align    = align
    s col.sortable = sortable
    d ..columns.Insert(col)
    q col
}

// ─── Builder — řádky ──────────────────────────────────────────────────────────

/// Vytvoří nový řádek s daným id, přidá ho do interní %DynamicArray a vrátí ho.
/// Volající nastaví pole přímo:  s row.nazev = "Praha"
Method AddRow(id As %String) As %DynamicObject
{
    s row    = ##class(%DynamicObject).%New()
    s row.id = id
    d ..rows.%Push(row)
    q row
}

// ─── Výstup ───────────────────────────────────────────────────────────────────

/// Zapíše OUT[n] = JSON pole definic sloupců (jedno volání = jeden OUT záznam).
Method WriteColumnsToOut()
{
    s arr = ##class(%DynamicArray).%New()
    f i=1:1:..columns.Count() {
        s col = ..columns.GetAt(i)
        s obj = ##class(%DynamicObject).%New()
        s obj.key      = col.key
        s obj.label    = col.label
        s obj.type     = col.type
        s obj.format   = col.format
        s obj.width    = col.width
        s obj.align    = col.align
        s obj.sortable = col.sortable
        s obj.visible  = col.visible
        s obj.isLink   = col.isLink
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
