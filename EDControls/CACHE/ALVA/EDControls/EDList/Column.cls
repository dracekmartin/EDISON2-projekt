Class ALVA.EDControls.EDList.Column Extends %RegisteredObject
{

/// Identifikátor sloupce - používá se jako klíč v datech řádku.
Property key As %String;

Property label As %String;

/// Datový typ: $$$ListColumnType* z EDList.inc (TEXT, NUMBER, DATE, ...); List neznámý odmítne (AddColumn i zápis).
/// IMAGE a CODEBOOK jsou rezervované: projdou, ale klient je zatím kreslí jako text (raw hodnota).
/// COLOR hodnota v řádku: hex #AARRGGBB / #RRGGBB jako EDInputForm; starší R;G;B klient čte dál.
Property type As %String [ InitialExpression = "TEXT" ];

/// NUMBER: "N0", "N2" apod.  DATE/DATETIME: "dd.MM.yyyy" apod.  Ostatní: prázdný.
Property format As %String;

/// Šířka sloupce v pixelech. 0 = automaticky podle obsahu.
Property width As %Integer [ InitialExpression = 0 ];

/// Zarovnání buněk: prázdný = auto (dle typu), jinak $$$ListAlign* z EDList.inc.
Property align As %String;

Property sortable As %Boolean [ InitialExpression = 0 ];

Property visible As %Boolean [ InitialExpression = 1 ];

/// Pokud true, buňka se zobrazí jako odkaz a vyvolá event CellLinkClicked.
Property isLink As %Boolean [ InitialExpression = 0 ];

}
