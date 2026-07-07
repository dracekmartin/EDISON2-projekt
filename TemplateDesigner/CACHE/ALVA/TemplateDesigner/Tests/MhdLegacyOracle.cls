Include %occInclude

/// C1: 1:1 porovnání MHD zastávkového JŘ proti LEGACY oracle producentovi
/// (ALVA.MHD.LinkyPrintFormatMHD.MakeData) — obchází padající formátovač ALVA.TISKMHD.Data.LoadData.
///
/// **Poziční** porovnání (ne line-global multiset): každý odjezd klíčován zastávkou × skupinou dnů
/// (nadpis) × HH:MM; poznámky zastávkou × typ/znak/text; zastávky názvem. Stejná normalizace na obou
/// stranách → rozdíl = reálná datová odchylka, ne odchylka formátu.
///
/// `CompareLine` = znovupoužitelné jádro (volá ho i pre-push `ZastavkoveJrMhdLegacyParityTest`).
/// `Compare` = MCP/MLRest diagnostický wrapper (souhrn do ^||ML("OUT")).
///
/// Náš engine odpovídá legacy s **TiskProjetychZast=1** (tiskne projeté zastávky) → parity test volá projete=1.
Class ALVA.TemplateDesigner.Tests.MhdLegacyOracle
{

/// Kanonický čas HH:MM (nula-padded), společný pro obě strany.
ClassMethod Cas(hod As %String, min As %String) As %String
{
	quit $tr($j(+hod,2)," ","0")_":"_$tr($j(+min,2)," ","0")
}

/// Trim + collapse whitespace pro robustní porovnání nadpisů/textů.
ClassMethod Norm(s As %String) As %String
{
	quit $zstrip(s, "<>W")
}

/// Porovná jednu linku. Naplní res(kat, "legN"/"ourN"/"match"/"diff") pro kat = "dep"/"zast"/"pozn".
/// Vrátí 1 když oracle proběhl, 0 = skip (legacy třída nebo namespace nedostupné na tomto serveru).
ClassMethod CompareLine(idfa As %String, db As %String, lin As %String, projete As %Integer, ByRef res As %String) As %Boolean
{
	kill res
	quit:'##class(%Dictionary.CompiledClass).%ExistsId("ALVA.MHD.LinkyPrintFormatMHD") 0
	quit:'##class(%SYS.Namespace).Exists(db) 0

	// ── LEGACY producent MakeData → outglb ─────────────────────────────
	kill ^||TdOracleGlb, arri
	set arri("par",1) = projete
	set ok = ##class(ALVA.MHD.LinkyPrintFormatMHD).MakeData(.idfa, .db, lin, "^||TdOracleGlb", .arri, .out)

	kill la, lz, lp
	set pgn = ""
	for {
		set pgn = $order(^||TdOracleGlb(pgn))
		quit:pgn=""
		set stop = ..Norm($get(^||TdOracleGlb(pgn,"key",1,"dta",8)))

		// zastávky (key=2, dta,4 = název)
		set zc = ""
		for {
			set zc = $order(^||TdOracleGlb(pgn,"key",2,zc))
			quit:zc=""
			set nz = ..Norm($get(^||TdOracleGlb(pgn,"key",2,zc,"dta",4)))
			continue:nz=""
			set lz(nz) = $get(lz(nz)) + 1
		}

		// odjezdy (key=3): sl = sloupec (skupina dnů), nadpis = dta,1|dta,2
		set sl = ""
		for {
			set sl = $order(^||TdOracleGlb(pgn,"key",3,"arr",sl))
			quit:sl=""
			set hdr = ..Norm($get(^||TdOracleGlb(pgn,"key",3,"arr",sl,"dta",1)))_"|"_..Norm($get(^||TdOracleGlb(pgn,"key",3,"arr",sl,"dta",2)))
			set hod = ""
			for {
				set hod = $order(^||TdOracleGlb(pgn,"key",3,"arr",sl,"arr",hod))
				quit:hod=""
				set cnt = ""
				for {
					set cnt = $order(^||TdOracleGlb(pgn,"key",3,"arr",sl,"arr",hod,cnt))
					quit:cnt=""
					set min = $get(^||TdOracleGlb(pgn,"key",3,"arr",sl,"arr",hod,cnt,"dta",1))
					continue:min=""
					set k = stop_"#"_hdr_"#"_..Cas(hod,min)
					set la(k) = $get(la(k)) + 1
				}
			}
		}

		// poznámky (key=4): typ 0/text 1/NZ 2/spec, dta,1=znak, dta,2=text
		set pc = ""
		for {
			set pc = $order(^||TdOracleGlb(pgn,"key",4,"arr",pc))
			quit:pc=""
			set ptyp = $get(^||TdOracleGlb(pgn,"key",4,"arr",pc,"typ",1))
			set ptyps = $case(ptyp, 0:"text", 1:"nz", 2:"specialni", :ptyp)
			// Legacy ukládá u speciálních znaků SUROVÝ ASCII kód (120/123) a konvertuje až při renderu;
			// náš engine konvertuje už v datech. Sjednotíme přes ConvertSpecialniZnak → porovnává se
			// finální glyf (což zároveň ověří, že náš převod = legacy převod).
			set pznakRaw = $get(^||TdOracleGlb(pgn,"key",4,"arr",pc,"dta",1))
			set pznak = $s(ptyp=2:##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(pznakRaw), 1:..Norm(pznakRaw))
			set ptext = ..Norm($get(^||TdOracleGlb(pgn,"key",4,"arr",pc,"dta",2)))
			set k = stop_"#"_ptyps_"#"_pznak_"#"_ptext
			set lp(k) = $get(lp(k)) + 1
		}
	}

	// ── NÁŠ engine GetData → ^||ML("OUT") ──────────────────────────────
	do ..SetupOur(.sys, idfa, db)
	kill ^||ML("OUT")
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)

	kill oa, oz, op
	set n = +$get(^||ML("OUT"))
	for p = 1:1:n {
		set page = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT",p)))
		set stop = ..Norm(page.values.%Get("POPIS_ZASTAVKA"))

		// mapa skupina-dnů ID → nadpis
		kill hdrMap
		set skup = page.rows.%Get("SKUPINY_DNU")
		if $isobject(skup) {
			set si = skup.%GetIterator()
			while si.%GetNext(.sk, .s) {
				set hdrMap(s.%Get("ID")) = ..Norm(s.%Get("NADPIS"))_"|"_..Norm(s.%Get("NADPIS2"))
			}
		}

		set zas = page.rows.%Get("ZASTAVKY")
		if $isobject(zas) {
			set iz = zas.%GetIterator()
			while iz.%GetNext(.zk, .z) {
				set nz = ..Norm(z.%Get("POPIS"))
				continue:nz=""
				set oz(nz) = $get(oz(nz)) + 1
			}
		}

		set odj = page.rows.%Get("ODJEZDY")
		if $isobject(odj) {
			set it = odj.%GetIterator()
			while it.%GetNext(.k1, .o) {
				set hdr = $get(hdrMap(o.%Get("SKUPINA_DNU_ID")))
				set k = stop_"#"_hdr_"#"_..Cas(o.%Get("HODINA"), o.%Get("MINUTA"))
				set oa(k) = $get(oa(k)) + 1
			}
		}

		set poz = page.rows.%Get("POZNAMKY")
		if $isobject(poz) {
			set ip = poz.%GetIterator()
			while ip.%GetNext(.pk, .pz) {
				set k = stop_"#"_pz.%Get("TYP")_"#"_..Norm(pz.%Get("ZNAK"))_"#"_..Norm(pz.%Get("TEXT"))
				set op(k) = $get(op(k)) + 1
			}
		}
	}

	do ..Summarize(.la, .oa, .res, "dep")
	do ..Summarize(.lz, .oz, .res, "zast")
	do ..Summarize(.lp, .op, .res, "pozn")
	quit 1
}

/// Porovná dva multisety (legacy la vs náš oa) → res(cat, ...). Match = shodný počet i md5.
ClassMethod Summarize(ByRef la As %String, ByRef oa As %String, ByRef res As %String, cat As %String)
{
	set canL = "", canO = "", legN = 0, ourN = 0, diff = "", diffN = 0
	set t = ""
	for { set t = $order(la(t)) quit:t=""  set canL = canL_t_"="_la(t)_";", legN = legN + la(t) }
	set t = ""
	for { set t = $order(oa(t)) quit:t=""  set canO = canO_t_"="_oa(t)_";", ourN = ourN + oa(t) }
	set t = ""
	for {
		set t = $order(la(t)) quit:t=""
		if $get(la(t)) '= $get(oa(t)) { if diffN < 12 set diff = diff_t_"(L"_$get(la(t))_"/O"_(+$get(oa(t)))_") "  set diffN = diffN + 1 }
	}
	set t = ""
	for {
		set t = $order(oa(t)) quit:t=""
		if '$data(la(t)) { if diffN < 12 set diff = diff_t_"(L0/O"_oa(t)_") "  set diffN = diffN + 1 }
	}
	set md5L = $system.Encryption.Base64Encode($system.Encryption.MD5Hash(canL))
	set md5O = $system.Encryption.Base64Encode($system.Encryption.MD5Hash(canO))
	set res(cat,"legN") = legN
	set res(cat,"ourN") = ourN
	set res(cat,"match") = ((md5L = md5O) && (legN = ourN))
	set res(cat,"diff") = diff
}

/// Naplní sys pro náš GetData (IdFa + data namespace).
ClassMethod SetupOur(ByRef sys, idfa As %String, db As %String)
{
	set sys("U","IdFa") = idfa
	set sys("U","delimiter") = "~"
	set sys("U","SystemDB") = $namespace
	set sys("U","DATA") = db
	set sys("U","MLP") = "1"
	set sys("U","loginName") = "test"
	set sys("U","fullName") = "test"
}

/// MCP/MLRest diagnostický wrapper. IN: LINKA_ID, DB, IDFA, PROJETE. OUT: souhrn per kategorie.
ClassMethod Compare(ByRef sys As %String) As %Status
{
	set idfa = $get(^||ML("IN","IDFA"))
	set db   = $get(^||ML("IN","DB"))
	set lin  = $get(^||ML("IN","LINKA_ID"))
	set projete = +$get(^||ML("IN","PROJETE"))

	set ok = ..CompareLine(idfa, db, lin, projete, .res)
	kill ^||ML("OUT"), ^||TdOracleGlb
	if 'ok {
		set ^||ML("OUT","SKIP") = "legacy oracle nedostupný (třída/namespace)"
		quit $$$OK
	}
	set ^||ML("OUT","DEP")  = "leg="_res("dep","legN")_" our="_res("dep","ourN")_" match="_$s(res("dep","match"):"ANO",1:"NE")_" diff="_res("dep","diff")
	set ^||ML("OUT","ZAST") = "leg="_res("zast","legN")_" our="_res("zast","ourN")_" match="_$s(res("zast","match"):"ANO",1:"NE")_" diff="_res("zast","diff")
	set ^||ML("OUT","POZN") = "leg="_res("pozn","legN")_" our="_res("pozn","ourN")_" match="_$s(res("pozn","match"):"ANO",1:"NE")_" diff="_res("pozn","diff")
	quit $$$OK
}

}
