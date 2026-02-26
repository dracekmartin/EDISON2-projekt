Class ALVA.EDISONMAIN2.API.Tree Extends %RegisteredObject
{

ClassMethod GetMain(ByRef in As %String, ByRef out As %String)
{
	k data
	k dataTemp
	s ret=##class(ALVA.MTZ.Main).GetTree(.dataTemp) i +ret'=$$$OK ztrap ret
	m data("Sklady")=dataTemp k dataTemp
	
	m out("data")=data
	
	q $$$OK
}

ClassMethod GetTools(ByRef in As %String, ByRef out As %String)
{
	k data
	s data("Tools","EDISON")=$lb("Edison")
	s data("Tools","EDISON","form",1)=$lb(199005,"EDISON")
	s data("Tools","EDISON","form",2)=$lb(199010,"Organizační struktura")
	s data("Tools","EDISON","form",3)=$lb(199009,"Uživatelé")
	
	
	m out("data")=data
	
	q $$$OK
}

ClassMethod GetFavourite(ByRef in As %String, ByRef out As %String)
{
	k data
	s data("Favourite","Main")=$lb("Oblíbené")

	
	m out("data")=data
	
	q $$$OK
}

ClassMethod GetParnterSolutions(ByRef in As %String, ByRef out As %String)
{
	k data
	s data("ParnterSolutions","Main")=$lb("Partnerská řešení")

	
	m out("data")=data
	
	q $$$OK
}

}
