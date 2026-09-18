Include %occInclude

/// `B161` - volba **Pořadí stránek** u zastávkového JŘ městských linek.
///
/// Obě agendy musí nabízet totéž a chovat se stejně, jinak by se tisk v každé choval jinak - stejná
/// podmínka jako u `B149`, kdy se pořadí sjednocovalo poprvé. Vlastní pravidla drží
/// <class>ALVA.TemplateDesigner.Tests.ZastavkoveJrAdPoradiStranekTest</class>; tady se kontroluje,
/// že je městská agenda splňuje taky.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrMhdPoradiStranekTest Extends ALVA.EDTesting.MLTestCase
{

/// Firma + namespace gold MHD dat (MHD je multi-tenant → obojí CB_DP), stejně jako gold test.
Parameter GOLDDB = "CB_DP";

Parameter Linka = "1001";

Parameter PoSmerech = "Po směrech (tam vzestupně, zpět sestupně)";

/// Načte testovací linku s daným pořadím stránek; 0 = namespace na tomhle serveru není.
ClassMethod Nacti(ByRef sys, poradiStranek As %String = "") As %Boolean
{
	set db = ..#GOLDDB
	quit:'##class(%SYS.Namespace).Exists(db) 0
	do ..SetupSys(.sys, db)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = ..#Linka
	set:poradiStranek'="" ^||ML("IN","PORADI_STRANEK") = poradiStranek
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
	quit 1
}

/// Volba „Po směrech": nejdřív celý jeden směr, pak celý druhý - a nic se neztratí.
Method TestPoSmerechNemichaSmery()
{
	quit:'..Nacti(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdPoradiStranekTest).PosloupnostStranek(.vychozi)

	quit:'..Nacti(.sys, ..#PoSmerech)
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdPoradiStranekTest).PosloupnostStranek(.poSmerech)

	do $$$AssertEquals($get(poSmerech), $get(vychozi), "obě volby musí vydat stejný počet stránek")

	// Po první změně směru se ten původní už nesmí vrátit.
	set prvni = $piece($get(poSmerech(1)), "~", 1), zmen = 0
	for i = 2 : 1 : $get(poSmerech) {
		continue:$piece(poSmerech(i),"~",1)=$piece(poSmerech(i-1),"~",1)
		set zmen = zmen + 1
	}
	do $$$AssertEquals(zmen, 1, "směry se mají vystřídat právě jednou, ne prokládat (první je "_prvni_")")
}

/// Výchozí pořadí drží obě vývěsky téže zastávky u sebe - to je parita se starým tiskem.
Method TestVychoziPoradiStridaSmery()
{
	quit:'..Nacti(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdPoradiStranekTest).PosloupnostStranek(.poradi)
	do $$$AssertNotEquals($get(poradi), "", "linka nevydala žádnou stránku")

	set dvojic = 0
	for i = 1 : 1 : ($get(poradi) - 1) {
		continue:$piece(poradi(i),"~",1)=$piece(poradi(i+1),"~",1)
		continue:$piece(poradi(i),"~",2)'=$piece(poradi(i+1),"~",2)
		set dvojic = dvojic + 1
	}
	do $$$AssertTrue(dvojic > 0, "ve výchozím pořadí nejde za sebou ani jedna dvojice vývěsek téže zastávky")
}

}
