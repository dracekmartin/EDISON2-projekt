Include %occInclude

/// C1: 1:1 porovnání MHD zastávkového JŘ proti LEGACY oracle producentovi
/// (ALVA.MHD.LinkyPrintFormatMHD.MakeData) - obchází padající formátovač ALVA.TISKMHD.Data.LoadData.
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

/// Porovná jednu linku. Naplní res(kat, "legN"/"ourN"/"match"/"diff") pro kat = "dep"/"zast"/"pozn"/"cas"
/// (cas = čas jízdy v řádku zastávky, klíčem vývěska × zastávka × minuty).
/// Vrátí 1 když oracle proběhl, 0 = skip (legacy třída nebo namespace nedostupné na tomto serveru).
ClassMethod CompareLine(idfa As %String, db As %String, lin As %String, projete As %Integer, ByRef res As %String) As %Boolean
{
	kill res
	quit:'##class(%Dictionary.CompiledClass).%ExistsId("ALVA.MHD.LinkyPrintFormatMHD") 0
	quit:'##class(%SYS.Namespace).Exists(db) 0

	// Přestup posíláme písmenem (A/L), legacy kódem fontu (119/120) - viz ZastavkoveJrMhd.ZnakAtributu.
	// Vlak vychází v obou tiscích jako lokomotiva; autobus je u legacy vagon, u nás autobus (vědomě, B194a C3).
	kill prestupKod
	set atr = ""
	for {
		set atr = $order(^[db]ALVA.MHD.ENUMS(idfa,"Atribut","DTA",atr))
		quit:atr=""
		set kod = $get(^[db]ALVA.MHD.ENUMS(idfa,"Atribut","DTA",atr,"SpecZnak"))
		set zn = $get(^[db]ALVA.MHD.ENUMS(idfa,"Atribut","DTA",atr,"Znak"))
		if (kod '= "") && ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).JePrestup(zn) set prestupKod(kod) = zn
	}

	// ── LEGACY producent MakeData → outglb ─────────────────────────────
	kill ^||TdOracleGlb, arri
	set arri("par",1) = projete
	set ok = ##class(ALVA.MHD.LinkyPrintFormatMHD).MakeData(.idfa, .db, lin, "^||TdOracleGlb", .arri, .out)

	kill la, lz, lp, lc
	set pgn = ""
	for {
		set pgn = $order(^||TdOracleGlb(pgn))
		quit:pgn=""
		set stop = ..Norm($get(^||TdOracleGlb(pgn,"key",1,"dta",8)))

		// zastávky (key=2, dta,4 = název, dta,3 = čas jízdy od vývěskové zastávky, u projetých prázdný)
		set zc = ""
		for {
			set zc = $order(^||TdOracleGlb(pgn,"key",2,zc))
			quit:zc=""
			set nz = ..Norm($get(^||TdOracleGlb(pgn,"key",2,zc,"dta",4)))
			continue:nz=""
			set lz(nz) = $get(lz(nz)) + 1
			set k = stop_"#"_nz_"#"_$get(^||TdOracleGlb(pgn,"key",2,zc,"dta",3))
			set lc(k) = $get(lc(k)) + 1
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
			set pznak = $s(ptyp'=2:..Norm(pznakRaw), (pznakRaw'="")&&$data(prestupKod(pznakRaw)):prestupKod(pznakRaw), 1:##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).ConvertSpecialniZnak(pznakRaw))
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

	kill oa, oz, op, oc
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
			set iz = zas.%GetIterator(), zaVyveskou = 0
			while iz.%GetNext(.zk, .z) {
				set:z.%Get("AKTUALNI_ZASTAVKA")="true" zaVyveskou = 1
				set nz = ..Norm(..NazevSeZnackami(z))
				continue:nz=""
				set oz(nz) = $get(oz(nz)) + 1
				// Projetá zastávka nese u nás 0, starý tisk prázdno - klient ji stejně neukazuje.
				set k = stop_"#"_nz_"#"_$select(zaVyveskou:+z.%Get("CAS_JIZDY"), 1:"")
				set oc(k) = $get(oc(k)) + 1
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
	do ..Summarize(.lc, .oc, .res, "cas")
	quit 1
}

/// Název zastávky ve tvaru starého tisku. Ten značky poznámek píše přímo do názvu („Areál VŠTE, A"),
/// náš seznam zastávek je kreslí za název z vlastního pole - pro porovnání se proto složí stejně.
ClassMethod NazevSeZnackami(z As %DynamicObject) As %String
{
	set znacky = "", pz = z.%Get("POZNAMKY_ZASTAVKY")
	if $isobject(pz) {
		set ip = pz.%GetIterator()
		while ip.%GetNext(.pk, .pv) { set znacky = znacky_pv.%Get("ZNAK") }
	}
	quit z.%Get("NAZEV_ZASTAVKY")_$s(znacky="":"", 1:", "_znacky)
}

/// Porovná dva multisety (legacy la vs náš oa) → res(cat, ...). Match = shodný počet i md5.
/// res(cat,"covered") = 1 když KAŽDÝ legacy klíč máme aspoň v legacy počtu (naše strana smí
/// mít navíc) - assert pro vědomé nadmnožiny (auto-značky sloučených skupin apod.).
ClassMethod Summarize(ByRef la As %String, ByRef oa As %String, ByRef res As %String, cat As %String)
{
	set canL = "", canO = "", legN = 0, ourN = 0, diff = "", diffN = 0, covered = 1
	set t = ""
	for {
		set t = $order(la(t)) quit:t=""
		set canL = canL_t_"="_la(t)_";", legN = legN + la(t)
		if +$get(oa(t)) < la(t) set covered = 0
	}
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
	set res(cat,"covered") = covered
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
		ret $$$OK
	}
	set ^||ML("OUT","DEP")  = "leg="_res("dep","legN")_" our="_res("dep","ourN")_" match="_$s(res("dep","match"):"ANO",1:"NE")_" diff="_res("dep","diff")
	set ^||ML("OUT","ZAST") = "leg="_res("zast","legN")_" our="_res("zast","ourN")_" match="_$s(res("zast","match"):"ANO",1:"NE")_" diff="_res("zast","diff")
	set ^||ML("OUT","POZN") = "leg="_res("pozn","legN")_" our="_res("pozn","ourN")_" match="_$s(res("pozn","match"):"ANO",1:"NE")_" diff="_res("pozn","diff")
	set ^||ML("OUT","CAS")  = "leg="_res("cas","legN")_" our="_res("cas","ourN")_" match="_$s(res("cas","match"):"ANO",1:"NE")_" diff="_res("cas","diff")
	quit $$$OK
}

/// Diagnostika zastávek: seznam zastávek JEDNÉ stránky (zastávka×směr) legacy vs TD vedle sebe.
/// IN: LINKA_ID, DB, IDFA, PROJETE, STOP (přesný název aktuální zastávky stránky), SMER (T/Z).
/// OUT: LEG(n) / OUR(n) = řádky stop-listu v pořadí tisku. Pro nalezení kořene zastávky 2×.
ClassMethod StopProbe(ByRef sys As %String) As %Status
{
	set idfa = $get(^||ML("IN","IDFA"))
	set db   = $get(^||ML("IN","DB"))
	set lin  = $get(^||ML("IN","LINKA_ID"))
	set projete = +$get(^||ML("IN","PROJETE"))
	set hledanyStop = ..Norm($get(^||ML("IN","STOP")))
	set hledanySmer = $get(^||ML("IN","SMER"),"T")

	// Legacy stránka
	kill ^||TdOracleGlb, arri, leg
	set arri("par",1) = projete
	set ok = ##class(ALVA.MHD.LinkyPrintFormatMHD).MakeData(.idfa, .db, lin, "^||TdOracleGlb", .arri, .out)
	set legN = 0, pgn = ""
	for {
		set pgn = $order(^||TdOracleGlb(pgn))
		quit:pgn=""
		continue:..Norm($get(^||TdOracleGlb(pgn,"key",1,"dta",8)))'=hledanyStop
		continue:$get(^||TdOracleGlb(pgn,"key",1,"dta",12))'=hledanySmer
		set zc = ""
		for {
			set zc = $order(^||TdOracleGlb(pgn,"key",2,zc))
			quit:zc=""
			set legN = legN + 1
			set leg(legN) = "tc="_$get(^||TdOracleGlb(pgn,"key",2,zc,"dta",2))_" podtrz="_(+$get(^||TdOracleGlb(pgn,"key",2,zc,"dta",1)))_" "_..Norm($get(^||TdOracleGlb(pgn,"key",2,zc,"dta",4)))
		}
		quit
	}
	kill ^||TdOracleGlb

	// Naše stránka
	do ..SetupOur(.sys, idfa, db)
	kill ^||ML("OUT"), our
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
	set ourN = 0, n = +$get(^||ML("OUT"))
	for p = 1:1:n {
		set page = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT",p)))
		continue:..Norm(page.values.%Get("POPIS_ZASTAVKA"))'=hledanyStop
		continue:page.values.%Get("SMER")'=hledanySmer
		set zas = page.rows.%Get("ZASTAVKY")
		if $isobject(zas) {
			set iz = zas.%GetIterator()
			while iz.%GetNext(.zk, .z) {
				set ourN = ourN + 1
				set our(ourN) = "poradi="_z.%Get("PORADI")_" akt="_z.%Get("AKTUALNI_ZASTAVKA")_" "_..Norm(..NazevSeZnackami(z))
			}
		}
		quit
	}

	kill ^||ML("OUT")
	set ^||ML("OUT","LEGN") = legN
	set ^||ML("OUT","OURN") = ourN
	for i = 1:1:legN { set ^||ML("OUT","LEG",$tr($j(i,3)," ","0")) = leg(i) }
	for i = 1:1:ourN { set ^||ML("OUT","OUR",$tr($j(i,3)," ","0")) = our(i) }
	quit $$$OK
}

/// Diagnostika legendy: poznámky JEDNÉ stránky (zastávka×směr) legacy vs TD vedle sebe.
/// IN: LINKA_ID, DB, IDFA, PROJETE, STOP, SMER (T/Z). OUT: LEG(n)/OUR(n) = typ|znak|text.
ClassMethod NotesProbe(ByRef sys As %String) As %Status
{
	set idfa = $get(^||ML("IN","IDFA"))
	set db   = $get(^||ML("IN","DB"))
	set lin  = $get(^||ML("IN","LINKA_ID"))
	set projete = +$get(^||ML("IN","PROJETE"))
	set hledanyStop = ..Norm($get(^||ML("IN","STOP")))
	set hledanySmer = $get(^||ML("IN","SMER"),"T")

	kill ^||TdOracleGlb, arri, leg
	set arri("par",1) = projete
	set ok = ##class(ALVA.MHD.LinkyPrintFormatMHD).MakeData(.idfa, .db, lin, "^||TdOracleGlb", .arri, .out)
	set legN = 0, pgn = ""
	for {
		set pgn = $order(^||TdOracleGlb(pgn))
		quit:pgn=""
		continue:..Norm($get(^||TdOracleGlb(pgn,"key",1,"dta",8)))'=hledanyStop
		continue:$get(^||TdOracleGlb(pgn,"key",1,"dta",12))'=hledanySmer
		set pc = ""
		for {
			set pc = $order(^||TdOracleGlb(pgn,"key",4,"arr",pc))
			quit:pc=""
			set legN = legN + 1
			set leg(legN) = $get(^||TdOracleGlb(pgn,"key",4,"arr",pc,"typ",1))_"|"_$get(^||TdOracleGlb(pgn,"key",4,"arr",pc,"dta",1))_"|"_..Norm($get(^||TdOracleGlb(pgn,"key",4,"arr",pc,"dta",2)))
		}
		quit
	}
	kill ^||TdOracleGlb

	do ..SetupOur(.sys, idfa, db)
	kill ^||ML("OUT"), our
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
	set ourN = 0, n = +$get(^||ML("OUT"))
	for p = 1:1:n {
		set page = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT",p)))
		continue:..Norm(page.values.%Get("POPIS_ZASTAVKA"))'=hledanyStop
		continue:page.values.%Get("SMER")'=hledanySmer
		set poz = page.rows.%Get("POZNAMKY")
		if $isobject(poz) {
			set ip = poz.%GetIterator()
			while ip.%GetNext(.pk, .pz) {
				set ourN = ourN + 1
				set our(ourN) = pz.%Get("TYP")_"|"_pz.%Get("ZNAK")_"|"_..Norm(pz.%Get("TEXT"))
			}
		}
		quit
	}

	kill ^||ML("OUT")
	set ^||ML("OUT","LEGN") = legN
	set ^||ML("OUT","OURN") = ourN
	for i = 1:1:legN { set ^||ML("OUT","LEG",$tr($j(i,3)," ","0")) = leg(i) }
	for i = 1:1:ourN { set ^||ML("OUT","OUR",$tr($j(i,3)," ","0")) = our(i) }
	quit $$$OK
}

/// Diagnostika SMO: počet stránek TD vs legacy + rozpad typů provozu linky na SMO sloupce.
/// Rozliší, zda je duplikace odjezdů z variantních stránek (pages leg≠our) nebo ze SMO dělení.
/// IN: LINKA_ID, DB, IDFA, PROJETE. OUT: PAGES + TP(tpid) = smoid/col/počet spojů/nadpis.
ClassMethod SmoProbe(ByRef sys As %String) As %Status
{
	set idfa = $get(^||ML("IN","IDFA"))
	set db   = $get(^||ML("IN","DB"))
	set lin  = $get(^||ML("IN","LINKA_ID"))
	set projete = +$get(^||ML("IN","PROJETE"))

	// Legacy počet stránek
	kill ^||TdOracleGlb, arri
	set arri("par",1) = projete
	set ok = ##class(ALVA.MHD.LinkyPrintFormatMHD).MakeData(.idfa, .db, lin, "^||TdOracleGlb", .arri, .out)
	set pagesLeg = 0, pgn = ""
	for { set pgn = $order(^||TdOracleGlb(pgn)) quit:pgn=""  set pagesLeg = pagesLeg + 1 }
	kill ^||TdOracleGlb

	// Náš počet stránek
	do ..SetupOur(.sys, idfa, db)
	kill ^||ML("OUT")
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
	set pagesOur = +$get(^||ML("OUT"))

	// Typy provozu použité na lince (počet spojů každého)
	kill tp
	set SpojID = ""
	for {
		set SpojID = $order(^[db]ALVA.MHD.LSO(idfa,2,"IDX",1,1,1,lin,SpojID)) quit:SpojID=""
		set tpid = $get(^[db]ALVA.MHD.LSO(idfa,2,"DTA",SpojID,"DTA",1,"TypProvozuID"))
		continue:tpid=""
		set tp(tpid) = $get(tp(tpid)) + 1
	}

	kill ^||ML("OUT")
	set ^||ML("OUT","PAGES") = "leg="_pagesLeg_" our="_pagesOur
	set tpid = ""
	for {
		set tpid = $order(tp(tpid)) quit:tpid=""
		set smoid = $get(^[db]ALVA.MHD.ENUMS(idfa,"TypyProvozu","DTA",tpid,"SMOID"))
		set col = $case(smoid, "SMO1":1, "SMO2":2, "SMO3":3, "SMO4":4, :"")
		set n1 = $get(^[db]ALVA.MHD.ENUMS(idfa,"TypyProvozu","DTA",tpid,"NadpisSMOPrvni"))
		set n2 = $get(^[db]ALVA.MHD.ENUMS(idfa,"TypyProvozu","DTA",tpid,"NadpisSMODruhy"))
		set ^||ML("OUT","TP",tpid) = "smoid="_smoid_" col="_col_" spoju="_tp(tpid)_" nadpis="_n1_"|"_n2
	}
	quit $$$OK
}

}
