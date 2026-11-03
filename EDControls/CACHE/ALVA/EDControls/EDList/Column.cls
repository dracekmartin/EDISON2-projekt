Class ALVA.EDControls.EDList.Column Extends %RegisteredObject
{

/// Identifikátor sloupce - používá se jako klíč v datech řádku.
Property key As %String;

Property label As %String;

/// Datový typ: $$$ListColumnType* z EDList.inc (TEXT, NUMBER, DATE, ...); List neznámý odmítne (AddColumn i zápis).
/// IMAGE hodnota v řádku: klíč sdílené ikony klienta (EDIcons.MlineLogo), prázdná = bez ikony; starší klient ji ukáže
/// jako text. CODEBOOK je rezervovaný: projde, ale klient ho zatím kreslí jako text (raw kód).
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

/// TEXT sloupec: klíč pole řádku s ikonou (hodnota jako u IMAGE, EDIcons.MlineLogo), kterou klient ukáže před textem;
/// prázdný = bez ikony. Posílá se jen vyplněný, starší klient ho ignoruje a ukáže jen text.
Property iconKey As %String;

/// TEXT sloupec: klíč pole řádku s ikonou, kterou klient ukáže hned za textem (značka stavu řádku, výchozí šablona
/// pro tisk); prázdný = bez ikony. Posílá se jen vyplněný, starší klient ho ignoruje.
Property trailingIconKey As %String;

}
