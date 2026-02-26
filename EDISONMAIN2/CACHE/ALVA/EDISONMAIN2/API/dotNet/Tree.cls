Class ALVA.EDISONMAIN2.API.dotNet.Tree Extends %RegisteredObject
{

ClassMethod Get(ByRef sys As %String, ByRef in As %String, ByRef out As %String) As %Status
{
	s ret=$$$OK 
	s D=$c(0)
	try{ 
		s type=+$g(^||ML("IN",1))
	
		k in1,out1
		i type=1{
			s ret=##class(ALVA.EDISONMAIN2.API.Tree).GetMain(.in1,.out1)
		}
		elseif type=2{
			s ret=##class(ALVA.EDISONMAIN2.API.Tree).GetFavourite(.in1,.out1)
		}
		elseif type=3{
			s ret=##class(ALVA.EDISONMAIN2.API.Tree).GetTools(.in1,.out1)
		}
		elseif type=4{
			s ret=##class(ALVA.EDISONMAIN2.API.Tree).GetParnterSolutions(.in1,.out1)
		}
		
		s i=0
		s agenda=""
		f{
			s agenda=$o(out1("data",agenda)) q:agenda=""
			s agendaId=""
			f{
				s agendaId=$o(out1("data",agenda,agendaId)) q:agendaId=""
				s ^||ML("OUT",$i(i))="AGENDA"_D_$lg(out1("data",agenda,agendaId),1) //AGENDA~"Nazev agendy"
				
				//Hlavni formulare agendy
				s formId=""
				f{
					s formId=$o(out1("data",agenda,agendaId,"form",formId)) q:formId=""
					s ^||ML("OUT",$i(i))="FORM"_D_$lg(out1("data",agenda,agendaId,"form",formId),1)_D_$lg(out1("data",agenda,agendaId,"form",formId),2) //FORM~"Id formulare"~"Nazev formulare"
				}
				
				//Slozky agendy
				s subId=""
				f{
					s subId=$o(out1("data",agenda,agendaId,"sub",subId)) q:subId=""
					s ^||ML("OUT",$i(i))="SUB"_D_$lg(out1("data",agenda,agendaId,"sub",subId),1) //FORM~"Nazev slozky"
					
					//Formulare slozky agendy
					s formId=""
					f{
						s formId=$o(out1("data",agenda,agendaId,"sub",subId,"form",formId)) q:formId=""
						s ^||ML("OUT",$i(i))="FORMSUB"_D_$lg(out1("data",agenda,agendaId,"sub",subId,"form",formId),1)_D_$lg(out1("data",agenda,agendaId,"sub",subId,"form",formId),2) //FORM~"Id formulare"~"Nazev formulare"
					}
					
					s ^||ML("OUT",$i(i))="SUB_END"
				}
			}
			s ^||ML("OUT",$i(i))="AGENDA_END"
		}
		
	} catch ex{
		s err=$g(err)
		s ^||ML("ERR")=$case(err="",1:ex.DisplayString(),0:err_"-"_ex.DisplayString())
		s ret=$$$ERROR($$$GeneralError,err) 
	}
	q ret
}

}
