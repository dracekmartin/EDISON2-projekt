Include EDInputForm

Class ALVA.EDControls.EDInputForm.Form Extends %RegisteredObject
{

// ─── Validation trigger constants (mirrors C# EValidationTrigger) ─────────────

Parameter TriggerInit = "Init";

Parameter TriggerChange = "Change";

Parameter TriggerBeforeSave = "BeforeSave";

/// Trigger received with this form; set automatically by ReadFromIn.
Property trigger As %String;

/// Key of the field that triggered validation. Set by ReadFromIn.
/// Non-empty only when trigger is "Change" and the validation was auto-triggered.
Property changedKey As %String;

/// Ordered list of Group and Field items that make up the form.
Property items As list Of ALVA.EDControls.EDInputForm.Item;

/// Controls how help text is displayed on the .NET side.
/// $$$HelpTextModeTooltip (default) or $$$HelpTextModeColumn.
Property helpTextMode As %String [ InitialExpression = {$$$HelpTextModeTooltip} ];

// ─── Builder ─────────────────────────────────────────────────────────────────

/// Appends a group separator. Pass visibility to control display on the .NET side
/// ($$$ItemVisibilityVisible, $$$ItemVisibilityCollapsed, etc.).
/// Groups don't get a visible number — number is 0. Fields are numbered sequentially
/// across all groups (1, 2, 3, ...) — Renumber sets this after insert.
Method AddGroup(key As %String, groupName As %String, visibility As %String = {$$$ItemVisibilityVisible})
{
	s g = ##class(ALVA.EDControls.EDInputForm.Group).%New()
	s g.key = key
	s g.groupName = groupName
	s g.visibility = visibility
	d ..items.Insert(g)
	d ..Renumber()
}

/// Appends a field and returns it so the caller can set value / validationError.
/// Field number is assigned sequentially across all groups (1-based) — Renumber sets
/// this after insert. Groups don't increment the field counter.
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

/// Inserts a field into the group identified by groupKey (after its last field).
/// If the group is not found, appends to the end.
/// Returns the new field so the caller can set value / validationError.
Method AddFieldToSpecificGroup(groupKey As %String, key As %String, label As %String, type As %String = {$$$FieldTypeText}, validateOnChange As %Boolean = 0, required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ##class(ALVA.EDControls.EDInputForm.Field).%New()
	s f.key = key
	s f.label = label
	s f.type = type
	s f.validateOnChange = validateOnChange
	s f.required = required

	// Find group, then scan to end of its fields
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

/// Renumbers items: groups don't get a visible number (set to 0); fields are
/// numbered sequentially across all groups (1, 2, 3, ...).
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

// ─── Access ───────────────────────────────────────────────────────────────────

/// Returns the first Field whose key matches, or "" if not found.
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

/// Returns the first Group whose key matches, or "" if not found.
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

// ─── Trigger helpers ──────────────────────────────────────────────────────────

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

/// True for Init and BeforeSave — full-form round-trips, not a single-field change.
Method IsWholeFormValidation() As %Boolean
{
	q (..trigger=..#TriggerInit)||(..trigger=..#TriggerBeforeSave)
}

// ─── Changed field helpers ────────────────────────────────────────────────

/// True when a specific field triggered this Change validation.
Method HasChangedField() As %Boolean
{
	q ..changedKey'=""
}

/// Returns the Field that triggered validation, or "" if not applicable
/// (Init, BeforeSave, or manual Change with no specific field).
Method GetChangedField() As ALVA.EDControls.EDInputForm.Field
{
	i ..changedKey="" q ""
	q ..GetField(..changedKey)
}

// ─── Serialization ────────────────────────────────────────────────────────────

/// Writes all items to ^||ML("OUT",...) as CList ($lb) values.
/// Each item is written at subscript 1, 2, ... (CVar["1"], CVar["2"] on .NET).
/// Format per item (number is always position 2):
///   GROUP → $lb("GROUP", number, key, groupName, visibility, helpText)
///   FIELD → $lb("FIELD", number, key, label, type, value, validateOnChange, required,
///                validationError, editable, visibility, helpText, listItems, fileFilter,
///                min, max, format, unit)
/// Also writes helpTextMode at string subscript "helpTextMode".
Method WriteToOut()
{
	s i = 0
	s cnt = ..items.Count()
	f idx=1:1:cnt {
		s ^||ML("OUT",$i(i)) = ..items.GetAt(idx).Serialize()
	}
	s ^||ML("OUT","helpTextMode") = ..helpTextMode
}

/// Reads ^||ML("IN",...) and returns a populated Form.
/// Trigger is at string subscript "trigger".
/// Items are at subscripts 1, 2, ... as CList ($lb) values:
///   $lb("GROUP", number, key, groupName, visibility, helpText)   ; number round-tripped, re-assigned on insert
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
			// Read helpText for the group just added
			i $ll(listVal)>=6 s form.items.GetAt(form.items.Count()).helpText = $li(listVal,6)
		}
		i type="FIELD" {
			s f = form.AddFieldToLastGroup($li(listVal,3),$li(listVal,4),$li(listVal,5),$li(listVal,7),$li(listVal,8))
			s f.value = $li(listVal,6)
			i $ll(listVal)>=10 s f.editable = $li(listVal,10)
			i $ll(listVal)>=11 s f.visibility = $li(listVal,11)
			i $ll(listVal)>=12 s f.helpText = $li(listVal,12)
			// Position 13: list items for List field type
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
			// Position 14: file filter for File field type
			i $ll(listVal)>=14 s f.fileFilter = $li(listVal,14)
			// Positions 15-18: Decimal/Number extras (min, max, format, unit)
			i $ll(listVal)>=15 s f.min    = $li(listVal,15)
			i $ll(listVal)>=16 s f.max    = $li(listVal,16)
			i $ll(listVal)>=17 s f.format = $li(listVal,17)
			i $ll(listVal)>=18 s f.unit   = $li(listVal,18)
		}
	}
	q form
}

}
