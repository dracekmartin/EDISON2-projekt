Include (%occInclude, GENERAL)

/// Read-only diagnostika úložiště šablon (#13013): projde všechny větve
/// ^ALVA.TemplateDesigner.Templates a ohlásí stav blobu každé šablony —
/// chunkovaný (počet kusů) / legacy (délka) / CHYBÍ (utržená šablona:
/// metadata bez dat, typicky pozůstatek pádu ukládání před fixem 2026-07-23).
/// Nic nemění ani nemaže.
Class ALVA.TemplateDesigner.Tests.SablonyDiag
{

/// OUT(n) = "idfa | templateId | název | agenda | modifiedAt | stav blobu"
ClassMethod IntegrityProbe(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML("OUT")
		set n = 0
		set idfaIter = ""
		for {
			set idfaIter = $order(^ALVA.TemplateDesigner.Templates(idfaIter))
			quit:idfaIter=""
			set tid = ""
			for {
				set tid = $order(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid))
				quit:tid=""
				set nazev = $get(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid,"Metadata","Name"))
				set agenda = $get(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid,"Metadata","AgendaCode"))
				set modif = $get(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid,"Metadata","ModifiedAt"))
				set dataBase = $name(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid,"Data"))
				if $data(@dataBase@(1)) {
					set stav = "chunked("_(+$get(@dataBase))_" kusu)"
					// Konzistence: počet uzlů vs count node
					set realKusu = 0, k = ""
					for { set k = $order(@dataBase@(k)) quit:k=""  set realKusu = realKusu + 1 }
					if realKusu '= +$get(@dataBase) set stav = stav_" NEKONZISTENTNI(realne "_realKusu_")"
				}
				elseif $get(@dataBase) '= "" {
					set stav = "legacy(delka "_$length($get(@dataBase))_")"
				}
				else {
					set stav = "CHYBI-DATA (utrzena sablona)"
				}
				set n = n + 1
				set ^||ML("OUT",$translate($justify(n,4)," ","0")) = idfaIter_" | "_tid_" | "_nazev_" | "_agenda_" | "_modif_" | "_stav
			}
		}
		set ^||ML("OUT","CELKEM") = n
	} catch (ex) { $$$CatchErrRest }
	quit $$$OK
}

}
