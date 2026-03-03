Class ALVA.TemplateDesigner.API.TemplateDesignerMHD Extends %RegisteredObject
{

ClassMethod GetDatabazovePolozky(ByRef sys As %String) As %Status
{
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~CISLO_LINKY|Číslo linky"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~LOKALNI_OZNACENI_LINKY|Označení linky"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~TITULEK|Titulek"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POPIS_LINKY|Popisek linky"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POPIS_DOPRAVCE|Popis Dopravce"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POPIS_PLATNOSTI|Popis platnosti"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~CISLO_ZASTAVKA|Číslo zastávky"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POPIS_ZASTAVKA|Popis zastávky"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POPIS_ZASTAVKA_CIS|Popis zastávky CIS"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POPIS_SMER|Popis směr"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POPIS_SMER_CIS|Popis směr CIS"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~DATUM_PLATNOSTI_OD|Datum platnosti od"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~DATUM_PLATNOSTI_DO|Datum platnosti do"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~TYP_JR|Typ jízdního řádu"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~POZNAMKA_POD_DOPRAVCE|Poznámka pod dopravce"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~INFORMACE|Informace"
    s ^||ML("OUT",$i(^||ML("OUT"))) = "DBFieldId~NEJEDOU_SPOJE|Nejedou spoje"

    ret 1
}

}
