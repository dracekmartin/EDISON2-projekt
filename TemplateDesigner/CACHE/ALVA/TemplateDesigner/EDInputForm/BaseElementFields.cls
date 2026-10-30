Include (GENERAL, EDInputForm)

/// Sdílený „balíček" base polí elementu (rámeček + rohy · pozice a velikost · podmínky) - jediný
/// zdroj té sady, kterou dřív každý *Edit.cls opisoval ručně. Deset kopií znamenalo drift:
/// tloušťka rámečku 0-10 mm u textu × 0-5 mm u zbytku, dynamická tabulka bez polí rámečku,
/// rámeček u tří elementů namixovaný v cizí skupině, rozdílný popisek výšky.
/// Dvojče StyleFields, který totéž dělá pro sadu polí stylu textu.
///
/// Klíče polí = property path na base C# TemplateElement - čte/zapisuje je ElementFormBinder
/// přes reflexi. Kanonické hodnoty a seskupení drží C# GetPositionDescriptors; že se ty dvě
/// strany nerozejdou, hlídá EDInputFormSchemaDriftTests.
Class ALVA.TemplateDesigner.EDInputForm.BaseElementFields
{

/// Rámeček elementu + rohy, vlastní skupina. <var>withCorners</var>=0 pro elementy bez plochy,
/// kde rohy nemají smysl (čára). <var>defColor</var>/<var>defWidth</var> = výchozí hodnoty toho
/// konkrétního prvku; musí souhlasit s jeho C# defaulty (hlídá EDInputFormSchemaDriftTests).
ClassMethod AddBorder(form As ALVA.EDControls.EDInputForm.Form, withCorners As %Boolean = 1, defColor As %String = "#00FFFFFF", defWidth As %String = 0)
{
    d form.AddGroup("border", "Rámeček prvku")
    s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
    s f.value = defColor
    s f.helpText = "Lze zadat i {BARVA_LINKY} nebo {BARVA_LINKY_2} - barvu vezme z jízdního řádu, kde linka může mít víc barev (nenastavená = bílá)"
    d form.AddDecimal("BorderWidth", "Tloušťka rámečku", defWidth, 0, 5, "mm")
    i withCorners {
        s f = form.AddFieldToLastGroup("CornerStyle", "Rohy", $$$FieldTypeList, 0, 0)
        d f.AddListItem("None", "Ostré"), f.AddListItem("Rounded", "Zaoblené"), f.AddListItem("Beveled", "Zkosené")
        s f.value = "None"
        d form.AddDecimal("CornerSize", "Velikost rohu", 0, 0, 50, "mm")
    }
}

/// Mřížka tabulkového prvku = jen VNITŘNÍ čáry, vlastní skupina. Vnější obrys je rámeček elementu
/// (AddBorder) - do 4. 8. 2026 vedly k obrysu dvě cesty a to byl důvod sjednocení.
/// <var>withLineToggles</var>=0 u prvku, který mřížkou kreslí jen čáru pod záhlavím (legenda
/// poznámek) - přepínače řádků/sloupců by tam lhaly. <var>defRowLines</var>/<var>defColumnLines</var>/
/// <var>defColor</var> = výchozí hodnoty toho konkrétního prvku (každý si mřížku inicializuje
/// jinak); musí souhlasit s jeho C# defaulty (hlídá EDInputFormSchemaDriftTests).
ClassMethod AddGrid(form As ALVA.EDControls.EDInputForm.Form, withLineToggles As %Boolean = 1, defRowLines As %Boolean = 1, defColumnLines As %Boolean = 0, defColor As %String = "#FFD0D0D0")
{
    d form.AddGroup("grid", "Mřížka")
    i withLineToggles {
        d form.AddBool("Grid.ShowRowLines", "Čáry mezi řádky", defRowLines)
        d form.AddBool("Grid.ShowColumnLines", "Čáry mezi sloupci", defColumnLines)
    }
    s f = form.AddFieldToLastGroup("Grid.Color", "Barva mřížky", $$$FieldTypeColor, 0, 0)
    s f.value = defColor
    s f = form.AddDecimal("Grid.Width", "Tloušťka mřížky", "", 0, 5, "mm")
    s f.helpText = "Prázdné nebo 0 = podle tloušťky rámečku prvku"
    s f = form.AddFieldToLastGroup("Grid.Style", "Styl mřížky", $$$FieldTypeList, 0, 1)
    d f.AddListItem("Solid", "Plná"), f.AddListItem("Dashed", "Čárkovaná"), f.AddListItem("Dotted", "Tečkovaná")
    s f.value = "Solid"
}

/// Pozice, velikost, rotace, vrstva, uzamčení.
/// <var>defHeight</var>="" = dynamická výška (dopočítá ji engine), jinak pevná výchozí hodnota;
/// <var>heightRequired</var>=1 u elementů, které bez výšky nemají smysl (obrázek, kód, obdélník);
/// <var>widthMin</var>/<var>heightMin</var>=0 pro čáru (svislá má nulovou šířku, vodorovná nulovou výšku).
/// Nejmenší rozměr musí souhlasit s C# `TemplateElement.NejmensiRozmerMm` (hlídá PanelProtiFormulariTests); do 30. 10.
/// dovolovala výška podle obsahu 0 u všech prvků, ačkoli „podle obsahu" znamená prázdné pole.
ClassMethod AddPosition(form As ALVA.EDControls.EDInputForm.Form, defWidth As %String, defHeight As %String = "", heightRequired As %Boolean = 0, widthMin As %String = 1, heightMin As %String = 1)
{
    d form.AddGroup("position", "Pozice a velikost")
    d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
    d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
    d form.AddDecimal("Width", "Šířka", defWidth, widthMin, "", "mm", "", 1)
    i defHeight = "" {
        s f = form.AddDecimal("Height", "Výška", "", heightMin, "", "mm")
        s f.helpText = "Prázdné = výška se počítá podle obsahu"
    } else {
        d form.AddDecimal("Height", "Výška", defHeight, heightMin, "", "mm", "", heightRequired)
    }
    d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
    s f = form.AddNumber("ZIndex", "Vrstva", 0, 0, 100)
    s f.helpText = "Vyšší vrstva leží nad nižší"
    s f = form.AddBool("IsLocked", "Uzamčeno", 0)
    s f.helpText = "Uzamčený prvek nelze přesunout ani změnit jeho velikost"
}

/// Podmínka viditelnosti elementu. Řádkové podmínky (viditelnost řádku, pravidla zvýraznění)
/// přidává element sám za tímto blokem - jejich nápověda vyjmenovává pole konkrétního řádku,
/// takže sdílet nejde.
/// <var>subjekt</var> = čeho se podmínka týká (element, řádek) - mění jen nápovědu, aby řádek
/// tabulky nemluvil o elementu. Pole i popisek zůstávají tytéž, ať se výraz zadává všude stejně.
ClassMethod AddConditions(form As ALVA.EDControls.EDInputForm.Form, subjekt As %String = "prvek")
{
    d form.AddGroup("advanced", "Podmínky")
    s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
    s f.placeholders = 1
    s f.helpText = "Např. {POCET} > 0 - " _ subjekt _ " se zobrazí jen když podmínka platí"
}

}
