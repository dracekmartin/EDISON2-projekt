Include %occInclude

/// Skupina každého parametru sestavy agend JŘ je „Obecné" nebo název prvku, kterého se týká - i u
/// parametrů skládaných za běhu, které .NET kontrola (`AgendaParameterDriftTests`) ve zdrojáku nevidí.
/// Do 26. 10. tak prošla vlastní skupina „Typy provozu pro tisk" linkového MHD (`B194c` P4).
Class ALVA.TemplateDesigner.Tests.SkupinyParametruTest Extends ALVA.EDTesting.MLTestCase
{

/// Názvy prvků jako `ElementNames` v návrháři (prvky, ke kterým mají agendy JŘ parametry).
Parameter POVOLENE = ",Obecné,Tabulka spojů,Seznam zastávek,Minuty odjezdů,Poznámky,";

Method TestSkupinyJsouObecneNeboPrvek()
{
	set spatne = "", overeno = 0
	for firma = "KV_DP", "CB_CSADAD" {
		continue:'##class(%SYS.Namespace).Exists(firma)
		do ..SetupSys(.sys, firma)
		set sys("U","DATA") = firma
		for agenda = "ZastavkoveJrAd", "LinkovyJrAd", "ZastavkoveJrMhd", "LinkovyJrMhd" {
			kill ^||ML
			do $classmethod("ALVA.TemplateDesigner.Agendy."_agenda, "GetParameters", .sys)
			set it = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT",1), "[]")).%GetIterator()
			while it.%GetNext(.k, .p) {
				set overeno = overeno + 1
				set skupina = p.%Get("group")
				continue:..#POVOLENE[(","_skupina_",")
				set spatne = spatne_$select(spatne="":"", 1:"; ")_firma_" "_agenda_" "_p.%Get("name")_" („"_skupina_"“)"
			}
		}
	}
	if 'overeno {
		do $$$AssertSkipped("firmy KV_DP ani CB_CSADAD na serveru nejsou")
		quit
	}
	do $$$AssertEquals(spatne, "", "parametry mimo „Obecné“ a názvy prvků: "_spatne)
}

}
