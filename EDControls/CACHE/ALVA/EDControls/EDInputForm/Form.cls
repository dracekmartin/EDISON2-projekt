Include EDInputForm

Class ALVA.EDControls.EDInputForm.Form Extends %RegisteredObject
{

// ─── Konstanty validačního triggeru (zrcadlí C# EValidationTrigger) ───────────

Parameter TriggerInit = "Init";

Parameter TriggerChange = "Change";

Parameter TriggerBeforeSave = "BeforeSave";

/// Trigger přijatý s formulářem; nastavuje automaticky ReadFromIn.
Property trigger As %String;

/// Klíč pole, které spustilo validaci (nastavuje ReadFromIn); neprázdné jen u
/// triggeru "Change" auto-spuštěného konkrétním polem.
Property changedKey As %String;

/// Uspořádaný seznam položek Group/Field tvořících formulář.
Property items As list Of ALVA.EDControls.EDInputForm.Item;

/// Řídí zobrazení nápovědy na .NET straně: $$$HelpTextModeTooltip (default) nebo $$$HelpTextModeColumn.
Property helpTextMode As %String [ InitialExpression = {$$$HelpTextModeTooltip} ];

// ─── Builder ─────────────────────────────────────────────────────────────────

/// Přidá oddělovač skupiny; visibility řídí zobrazení na .NET straně
/// ($$$ItemVisibilityVisible, $$$ItemVisibilityCollapsed, ...). Skupina nemá viditelné
/// číslo (0); pole se číslují průběžně napříč skupinami — po insertu nastaví Renumber.
Method AddGroup(key As %String, groupName As %String, visibility As %String = {$$$ItemVisibilityVisible})
{
	s g = ##class(ALVA.EDControls.EDInputForm.Group).%New()
	s g.key = key
	s g.groupName = groupName
	s g.visibility = visibility
	d ..items.Insert(g)
	d ..Renumber()
}

/// Přidá pole do poslední skupiny a vrátí ho (caller doplní value / validationError).
/// Číslo pole je průběžné napříč skupinami (1-based) — po insertu nastaví Renumber;
/// skupiny čítač polí neinkrementují.
Method AddFieldToLastGroup(key As %String, label As %String, type As %String = {$$$FieldTypeText}, validateOnChange As %Boolean = 0, required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ##class(ALVA.EDControls.EDInputForm.Field).%New()
	s f.key = key
	s f.label = label
	s f.type = type
	s f.validateOnChange = validateOnChange
	s f.required = required
	d ..items.Insert(f)
	d ..Renumber()
	q f
}

/// ─── Convenience helpers pro nejčastější field typy ─────────────────────────
/// Vrací nový Field — caller může nastavit další properties (helpText, format,
/// validationError, ...). Pro complex typy (List/ComboBox/Color/File) použij
/// AddFieldToLastGroup + post-setting properties (viz EDIFExample.cls).
///
/// Pro skupinu — nejprve volej AddGroup(...), helpery přidají field do poslední.

/// Text field. value=initial obsah, required=povinnost.
Method AddText(key As %String, label As %String, value As %String = "", required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ..AddFieldToLastGroup(key, label, $$$FieldTypeText, 0, required)
	s f.value = value
	q f
}

/// Bool (checkbox) field. value=0/1.
Method AddBool(key As %String, label As %String, value As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ..AddFieldToLastGroup(key, label, $$$FieldTypeBool, 0, 0)
	s f.value = value
	q f
}

/// Number (integer) field s volitelným rozsahem + jednotkou.
/// min/max/unit prázdné = bez validace/displeje.
Method AddNumber(key As %String, label As %String, value As %String = "", min As %String = "", max As %String = "", unit As %String = "", required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ..AddFieldToLastGroup(key, label, $$$FieldTypeNumber, 0, required)
	s f.value = value
	s:min'="" f.min = min
	s:max'="" f.max = max
	s:unit'="" f.unit = unit
	q f
}

/// Decimal (float) field s volitelným rozsahem + jednotkou + formátem.
/// format např. "0.0#", "N2", "0.00".
Method AddDecimal(key As %String, label As %String, value As %String = "", min As %String = "", max As %String = "", unit As %String = "", format As %String = "", required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ..AddFieldToLastGroup(key, label, $$$FieldTypeDecimal, 0, required)
	s f.value = value
	s:min'="" f.min = min
	s:max'="" f.max = max
	s:unit'="" f.unit = unit
	s:format'="" f.format = format
	q f
}

/// Vloží pole do skupiny dle groupKey (za její poslední pole); nenalezená skupina =
/// přidá na konec. Vrací nové pole (caller doplní value / validationError).
Method AddFieldToSpecificGroup(groupKey As %String, key As %String, label As %String, type As %String = {$$$FieldTypeText}, validateOnChange As %Boolean = 0, required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ##class(ALVA.EDControls.EDInputForm.Field).%New()
	s f.key = key
	s f.label = label
	s f.type = type
	s f.validateOnChange = validateOnChange
	s f.required = required

	// Najdi skupinu, pak doskenuj na konec jejích polí
	s insertAt = 0, cnt = ..items.Count()
	f idx=1:1:cnt {
		s item = ..items.GetAt(idx)
		i item.%IsA("ALVA.EDControls.EDInputForm.Group") && (item.key = groupKey) {
			s insertAt = idx
			f j=(idx+1):1:cnt {
				s next = ..items.GetAt(j)
				i next.%IsA("ALVA.EDControls.EDInputForm.Group") q
				s insertAt = j
			}
			q
		}
	}

	i insertAt=0 {
		d ..items.Insert(f)
	} else {
		d ..items.InsertAt(f, insertAt + 1)
	}
	d ..Renumber()
	q f
}

/// Přečísluje položky: skupiny nemají viditelné číslo (0), pole se číslují průběžně
/// napříč skupinami (1, 2, 3, ...).
Method Renumber() [ Private ]
{
	s cnt = ..items.Count()
	s fieldIdx = 0
	f idx=1:1:cnt {
		s item = ..items.GetAt(idx)
		i item.%IsA("ALVA.EDControls.EDInputForm.Group") {
			s item.number = 0
		} else {
			s fieldIdx = fieldIdx + 1
			s item.number = fieldIdx
		}
	}
}

// ─── Přístup ────────────────────────────────────────────────────────────────

/// Vrátí první Field se shodným key, nebo "" když není nalezen.
Method GetField(key As %String) As ALVA.EDControls.EDInputForm.Field
{
	s result = "", cnt = ..items.Count()
	f idx=1:1:cnt {
		s item = ..items.GetAt(idx)
		i item.%IsA("ALVA.EDControls.EDInputForm.Field") && (item.key=key) s result = item
		q:result'=""
	}
	q result
}

/// Vrátí první Group se shodným key, nebo "" když není nalezena.
Method GetGroup(key As %String) As ALVA.EDControls.EDInputForm.Group
{
	s result = "", cnt = ..items.Count()
	f idx=1:1:cnt {
		s item = ..items.GetAt(idx)
		i item.%IsA("ALVA.EDControls.EDInputForm.Group") && (item.key=key) s result = item
		q:result'=""
	}
	q result
}

// ─── Trigger helpery ──────────────────────────────────────────────────────────

Method IsInitValidation() As %Boolean
{
	q ..trigger=..#TriggerInit
}

Method IsChangeValidation() As %Boolean
{
	q ..trigger=..#TriggerChange
}

Method IsBeforeSaveValidation() As %Boolean
{
	q ..trigger=..#TriggerBeforeSave
}

/// True pro Init a BeforeSave — round-trip celého formuláře, ne změna jednoho pole.
Method IsWholeFormValidation() As %Boolean
{
	q (..trigger=..#TriggerInit)||(..trigger=..#TriggerBeforeSave)
}

// ─── Helpery změněného pole ────────────────────────────────────────────────

/// True, když tuto Change validaci spustilo konkrétní pole.
Method HasChangedField() As %Boolean
{
	q ..changedKey'=""
}

/// Vrátí Field, které spustilo validaci, nebo "" když neaplikovatelné
/// (Init, BeforeSave, nebo ruční Change bez konkrétního pole).
Method GetChangedField() As ALVA.EDControls.EDInputForm.Field
{
	i ..changedKey="" q ""
	q ..GetField(..changedKey)
}

// ─── Serializace ──────────────────────────────────────────────────────────────

/// Zapíše všechny položky do ^||ML("OUT",...) jako CList ($lb) hodnoty na subscriptech
/// 1, 2, ... (na .NET CVar["1"], CVar["2"]). Formát položky (number je vždy pozice 2):
///   GROUP → $lb("GROUP", number, key, groupName, visibility, helpText)
///   FIELD → $lb("FIELD", number, key, label, type, value, validateOnChange, required,
///                validationError, editable, visibility, helpText, listItems, fileFilter,
///                min, max, format, unit)
/// helpTextMode se zapíše na string subscript "helpTextMode".
Method WriteToOut()
{
	s i = 0
	s cnt = ..items.Count()
	f idx=1:1:cnt {
		s ^||ML("OUT",$i(i)) = ..items.GetAt(idx).Serialize()
	}
	s ^||ML("OUT","helpTextMode") = ..helpTextMode
}

/// Přečte ^||ML("IN",...) a vrátí naplněný Form. Trigger je na string subscriptu
/// "trigger"; položky na subscriptech 1, 2, ... jako CList ($lb):
///   $lb("GROUP", number, key, groupName, visibility, helpText)   ; number round-tripovaný, po insertu přečíslován
///   $lb("FIELD", number, key, label, type, value, validateOnChange, required, validationError, editable, visibility, helpText, listItems, fileFilter)
ClassMethod ReadFromIn() As ALVA.EDControls.EDInputForm.Form
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	s form.trigger = $g(^||ML("IN","trigger"))
	s form.changedKey = $g(^||ML("IN","changedKey"))
	s form.helpTextMode = $s($g(^||ML("IN","helpTextMode"))'="":^||ML("IN","helpTextMode"),1:$$$HelpTextModeTooltip)
	s idx = ""
	f {
		s idx = $o(^||ML("IN",idx))
		q:idx=""
		i idx="trigger" continue
		i idx="changedKey" continue
		i idx="helpTextMode" continue
		s listVal = ^||ML("IN",idx)
		s type = $li(listVal,1)
		i type="GROUP" {
			s visibility = $s($ll(listVal)>=5:$li(listVal,5),1:$$$ItemVisibilityVisible)
			d form.AddGroup($li(listVal,3),$li(listVal,4),visibility)
			// helpText pro právě přidanou skupinu
			i $ll(listVal)>=6 s form.items.GetAt(form.items.Count()).helpText = $li(listVal,6)
		}
		i type="FIELD" {
			s f = form.AddFieldToLastGroup($li(listVal,3),$li(listVal,4),$li(listVal,5),$li(listVal,7),$li(listVal,8))
			s f.value = $li(listVal,6)
			i $ll(listVal)>=10 s f.editable = $li(listVal,10)
			i $ll(listVal)>=11 s f.visibility = $li(listVal,11)
			i $ll(listVal)>=12 s f.helpText = $li(listVal,12)
			// Pozice 13: položky listu pro typ List
			i $ll(listVal)>=13 {
				s optStr = $li(listVal,13)
				i optStr'="" {
					f k=1:1:$l(optStr,$$$ListItemSep) {
						s entry = $p(optStr,$$$ListItemSep,k)
						s key = $p(entry,$$$ListKeyValSep,1)
						s label = $p(entry,$$$ListKeyValSep,2)
						s:label="" label = key
						d f.AddListItem(key, label)
					}
				}
			}
			// Pozice 14: filtr souboru pro typ File
			i $ll(listVal)>=14 s f.fileFilter = $li(listVal,14)
			// Pozice 15-18: extra pro Decimal/Number (min, max, format, unit)
			i $ll(listVal)>=15 s f.min    = $li(listVal,15)
			i $ll(listVal)>=16 s f.max    = $li(listVal,16)
			i $ll(listVal)>=17 s f.format = $li(listVal,17)
			i $ll(listVal)>=18 s f.unit   = $li(listVal,18)
		}
	}
	q form
}

}
