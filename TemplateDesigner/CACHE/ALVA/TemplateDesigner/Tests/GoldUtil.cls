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

}
