Include (%occInclude, GENERAL)

/// Read-only diagnostika úložiště šablon (#13013): projde všechny větve
/// ^ALVA.TemplateDesigner.Templates a ohlásí stav blobu každé šablony -
/// chunkovaný (počet kusů) / legacy (délka) / CHYBÍ (utržená šablona:
/// metadata bez dat, typicky pozůstatek pádu ukládání před fixem 2026-07-23).
/// Nic nemění ani nemaže.
Class ALVA.TemplateDesigner.Tests.SablonyDiag
{

/// Bezeztrátový přepínač formátu blobu jedné šablony (dev nástroj pro test kompatibility
/// klientů, #13013): jednoklíčový ↔ po kusech (1 kus). Obsah se nemění, jen tvar uložení.
/// Vícekusý blob nepřepíná (odmítne) - slepení by mohlo překročit limit délky řetězce.
/// IN: IDFA, TEMPLATE_ID. OUT: STAV = výsledný formát.
ClassMethod PrepnoutFormatBlobu(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set idfaVetev = $get(^||ML("IN","IDFA"))
		set tid = $get(^||ML("IN","TEMPLATE_ID"))
		kill ^||ML("OUT")
		if (idfaVetev = "") || (tid = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "IDFA a TEMPLATE_ID jsou povinné"))
		set dataBase = $name(^ALVA.TemplateDesigner.Templates(idfaVetev,"DTA",tid,"Data"))
		if '$data(@dataBase) $$$ThrowStatus($$$ERROR($$$GeneralError, "Šablona nenalezena: "_tid))

		if $data(@dataBase@(1)) {
			if +$get(@dataBase) > 1 $$$ThrowStatus($$$ERROR($$$GeneralError, "Vícekusý blob nepřepínám (kusů: "_+$get(@dataBase)_")"))
			set blob = $get(@dataBase@(1))
			kill @dataBase
			set @dataBase = blob
			set ^||ML("OUT","STAV") = "jednoklicovy (delka "_$length(blob)_")"
		}
		else {
			set blob = $get(@dataBase)
			set @dataBase = 1
			set @dataBase@(1) = blob
			set ^||ML("OUT","STAV") = "chunked (1 kus, delka "_$length(blob)_")"
		}
	} catch (ex) { $$$CatchErrRest }
	quit $$$OK
}

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

/// Read-only: kolika uloženým šablonám by zapojení voleb mřížky změnilo tisk (`L15`).
///
/// Volby „Čáry mezi řádky" a „Čáry mezi sloupci" se do šablon ukládaly, i když je prvek odjezdů
/// nečetl. Než se zapojí, musí se vědět, komu je má odškrtnuté - tomu by mřížka z hotového tisku
/// zmizela. Sonda **nic nemění**, jen čte.
///
/// Vypisuje jen šablony, kde je aspoň jedna volba vypnutá; zbytek se počítá do souhrnu.
/// OUT("SOUHRN") = "sablon=N prvku=M dotcenych=K", OUT(n) = "idfa | název | prvek#i | radky=0/1 sloupce=0/1"
ClassMethod MrizkaOdjezduProbe(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML("OUT")
		set n = 0, sablon = 0, prvku = 0, dotcenych = 0
		set idfaIter = ""
		for {
			set idfaIter = $order(^ALVA.TemplateDesigner.Templates(idfaIter))
			quit:idfaIter=""
			set tid = ""
			for {
				set tid = $order(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid))
				quit:tid=""
				set nazev = $get(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid,"Metadata","Name"))
				set dataBase = $name(^ALVA.TemplateDesigner.Templates(idfaIter,"DTA",tid,"Data"))

				// Blob je buď v jednom uzlu (legacy), nebo po kusech
				set json = ""
				if $data(@dataBase@(1)) {
					set k = ""
					for { set k = $order(@dataBase@(k)) quit:k=""  set json = json_$get(@dataBase@(k)) }
				} else { set json = $get(@dataBase) }
				continue:json=""

				set sablon = sablon + 1
				set obj = ##class(%DynamicObject).%FromJSON(json)
				continue:'$isobject(obj)
				set sekce = obj.sections
				continue:'$isobject(sekce)

				set si = sekce.%GetIterator()
				while si.%GetNext(.sk, .sekceObj) {
					continue:'$isobject(sekceObj)
					set prvky = sekceObj.elements
					continue:'$isobject(prvky)
					set pi = prvky.%GetIterator()
					while pi.%GetNext(.pk, .prvek) {
						continue:'$isobject(prvek)
						continue:prvek.type'="DepartureGridElement"
						set prvku = prvku + 1
						set grid = prvek.grid
						// Chybí-li `grid` v JSON, platí výchozí hodnoty prvku = obojí zapnuto.
						set radky = 1, sloupce = 1
						if $isobject(grid) {
							if grid.showRowLines '= "" set radky = ''grid.showRowLines
							if grid.showColumnLines '= "" set sloupce = ''grid.showColumnLines
						}
						continue:radky&&sloupce
						set dotcenych = dotcenych + 1, n = n + 1
						set ^||ML("OUT",$translate($justify(n,4)," ","0")) = idfaIter_" | "_nazev_" | prvek#"_prvku_" | radky="_radky_" sloupce="_sloupce
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = "sablon="_sablon_" prvku="_prvku_" dotcenych="_dotcenych
	} catch (ex) { $$$CatchErrRest }
	quit $$$OK
}

}
