Include %occInclude

/// Sdílený digest výstupu GetData pro gold regresní testy zastávkového JŘ (AD i MHD).
/// Jedno místo výpočtu → AD a MHD gold nemohou divergovat (viz roadmap C8 anti-drift).
///
/// Digest = hrubé počty řádků (čitelné, rychlá diagnóza) + MD5 celého spojeného JSON
/// (chytí i jemný posun hodnot). Deterministický, protože GetData je byte-identický.
/// Formát wire je společný oběma agendám: OUT[1..N] = { values, rows: { ZASTAVKY, ODJEZDY, POZNAMKY, ... } }.
Class ALVA.TemplateDesigner.Tests.GoldUtil
{

/// Přečte aktuální ^||ML("OUT") a vrátí kompaktní digest string.
ClassMethod Digest() As %String
{
	set n = +$get(^||ML("OUT"))
	set all = "", tz = 0, to = 0, tp = 0
	for p = 1:1:n {
		set json = $get(^||ML("OUT",p))
		set all = all_json
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set tz = tz + obj.rows.%Get("ZASTAVKY").%Size()
		set to = to + obj.rows.%Get("ODJEZDY").%Size()
		set tp = tp + obj.rows.%Get("POZNAMKY").%Size()
	}
	set md5 = $system.Encryption.Base64Encode($system.Encryption.MD5Hash(all))
	quit "pages="_n_" zast="_tz_" odj="_to_" pozn="_tp_" md5="_md5
}

/// Digest **linkového** JŘ. Vlastní metoda, protože linkový wire má jiné kolekce
/// (`SPOJE`/`SKUPINY_DNU` místo `ODJEZDY`) - společná by musela hádat tvar a mlčky vracet nuly.
/// Počítá se i suma časových buněk: bez ní by se ztráta celého sloupce spojů projevila jen v md5.
ClassMethod DigestLinkovy() As %String
{
	set n = +$get(^||ML("OUT"))
	set all = "", tz = 0, ts = 0, tp = 0, tc = 0
	for p = 1:1:n {
		set json = $get(^||ML("OUT",p))
		set all = all_json
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set tz = tz + obj.rows.%Get("ZASTAVKY").%Size()
		set spoje = obj.rows.%Get("SPOJE")
		set ts = ts + spoje.%Size()
		set tp = tp + obj.rows.%Get("POZNAMKY").%Size()
		for si = 0:1:spoje.%Size()-1 {
			set casy = spoje.%Get(si).%Get("CASY")
			continue:'$isobject(casy)
			for ci = 0:1:casy.%Size()-1 {
				continue:casy.%Get(ci)=""
				set tc = tc + 1
			}
		}
	}
	set md5 = $system.Encryption.Base64Encode($system.Encryption.MD5Hash(all))
	quit "pages="_n_" zast="_tz_" spoju="_ts_" casu="_tc_" pozn="_tp_" md5="_md5
}

}
