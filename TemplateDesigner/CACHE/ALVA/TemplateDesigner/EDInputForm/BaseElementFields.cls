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
/// kde rohy nemají smysl (čára).
ClassMethod AddBorder(form As ALVA.EDControls.EDInputForm.Form, withCorners As %Boolean = 1)
{
    d form.AddGroup("border", "Rámeček elementu")
    s f = form.AddFieldToLastGroup("BorderColor", "Barva rámečku", $$$FieldTypeColor, 0, 0)
    s f.value = "#00FFFFFF"
    s f.helpText = "Lze zadat i {BARVA_LINKY} nebo {BARVA_LINKY_2} - barvu vezme z jízdního řádu, kde linka může mít víc barev (nenastavená = bílá)"
    d form.AddDecimal("BorderWidth", "Tloušťka rámečku", 0, 0, 5, "mm")
    i withCorners {
        s f = form.AddFieldToLastGroup("CornerStyle", "Rohy", $$$FieldTypeList, 0, 0)
        d f.AddListItem("None", "Ostré"), f.AddListItem("Rounded", "Zaoblené"), f.AddListItem("Beveled", "Zkosené")
        s f.value = "None"
        d form.AddDecimal("CornerSize", "Velikost rohu", 0, 0, 50, "mm")
    }
}

/// Pozice, velikost, rotace, vrstva, uzamčení.
/// <var>defHeight</var>="" = dynamická výška (dopočítá ji engine), jinak pevná výchozí hodnota;
/// <var>heightRequired</var>=1 u elementů, které bez výšky nemají smysl (obrázek, kód, obdélník);
/// <var>widthMin</var>=0 pro svislou čáru (nulová šířka je u ní legitimní).
ClassMethod AddPosition(form As ALVA.EDControls.EDInputForm.Form, defWidth As %String, defHeight As %String = "", heightRequired As %Boolean = 0, widthMin As %String = 1)
{
    d form.AddGroup("position", "Pozice a velikost")
    d form.AddDecimal("X", "X", 0, 0, "", "mm", "", 1)
    d form.AddDecimal("Y", "Y", 0, 0, "", "mm", "", 1)
    d form.AddDecimal("Width", "Šířka", defWidth, widthMin, "", "mm", "", 1)
    i defHeight = "" {
        s f = form.AddDecimal("Height", "Výška", "", 0, "", "mm")
        s f.helpText = "Prázdné = dynamická výška (počítá engine)"
    } else {
        d form.AddDecimal("Height", "Výška", defHeight, 1, "", "mm", "", heightRequired)
    }
    d form.AddDecimal("Rotation", "Rotace", 0, -360, 360, "°")
    s f = form.AddNumber("ZIndex", "ZIndex", 0, 0, 100)
    s f.helpText = "Pořadí vrstev (vyšší = nad nižším)"
    s f = form.AddBool("IsLocked", "Uzamčeno", 0)
    s f.helpText = "Uzamčený element nelze přesunout ani změnit jeho velikost"
}

/// Podmínka viditelnosti elementu. Řádkové podmínky (viditelnost řádku, pravidla zvýraznění)
/// přidává element sám za tímto blokem - jejich nápověda vyjmenovává pole konkrétního řádku,
/// takže sdílet nejde.
ClassMethod AddConditions(form As ALVA.EDControls.EDInputForm.Form)
{
    d form.AddGroup("advanced", "Podmínky")
    s f = form.AddText("VisibilityExpression", "Podmínka viditelnosti")
    s f.helpText = "Např. {POCET} > 0 - element se zobrazí jen když podmínka platí"
}

}
