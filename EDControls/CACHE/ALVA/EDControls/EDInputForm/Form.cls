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

// ─── Builder ─────────────────────────────────────────────────────────────────

/// Appends a group separator. Pass isVisible=0 to hide the group on the .NET side.
/// number is assigned automatically (1-based, sequential across groups and fields).
Method AddGroup(key As %String, groupName As %String, isVisible As %Boolean = 1)
{
	s g = ##class(ALVA.EDControls.EDInputForm.Group).%New()
	s g.key = key
	s g.groupName = groupName
	s g.isVisible = isVisible
	d ..items.Insert(g)
	s g.number = ..items.Count()
}

/// Appends a field and returns it so the caller can set value / validationError.
/// number is assigned automatically (1-based, sequential across groups and fields).
Method AddFieldToLastGroup(key As %String, label As %String, type As %String = {$$$FieldTypeText}, validateOnChange As %Boolean = 0, required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ##class(ALVA.EDControls.EDInputForm.Field).%New()
	s f.key = key
	s f.label = label
	s f.type = type
	s f.validateOnChange = validateOnChange
	s f.required = required
	d ..items.Insert(f)
	s f.number = ..items.Count()
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

/// Re-assigns sequential numbers (1-based) to all items.
Method Renumber() [ Private ]
{
	s cnt = ..items.Count()
	f idx=1:1:cnt {
		s ..items.GetAt(idx).number = idx
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
///   GROUP → $lb("GROUP", number, key, groupName, isVisible)
///   FIELD → $lb("FIELD", number, key, label, type, value, validateOnChange, required [, validationError])
Method WriteToOut()
{
	s i = 0
	s cnt = ..items.Count()
	f idx=1:1:cnt {
		s ^||ML("OUT",$i(i)) = ..items.GetAt(idx).Serialize()
	}
}

/// Reads ^||ML("IN",...) and returns a populated Form.
/// Trigger is at string subscript "trigger".
/// Items are at subscripts 1, 2, ... as CList ($lb) values:
///   $lb("GROUP", number, key, groupName, isVisible)   ; number round-tripped, re-assigned on insert
///   $lb("FIELD", number, key, label, type, value, validateOnChange, required)
ClassMethod ReadFromIn() As ALVA.EDControls.EDInputForm.Form
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	s form.trigger = $g(^||ML("IN","trigger"))
	s form.changedKey = $g(^||ML("IN","changedKey"))
	s idx = ""
	f {
		s idx = $o(^||ML("IN",idx))
		q:idx=""
		i idx="trigger" continue
		i idx="changedKey" continue
		s listVal = ^||ML("IN",idx)
		s type = $li(listVal,1)
		i type="GROUP" {
			s isVisible = $s($ll(listVal)>=5:$li(listVal,5)'="0",1:1)
			d form.AddGroup($li(listVal,3),$li(listVal,4),isVisible)
		}
		i type="FIELD" {
			s f = form.AddFieldToLastGroup($li(listVal,3),$li(listVal,4),$li(listVal,5),$li(listVal,7),$li(listVal,8))
			s f.value = $li(listVal,6)
			i $ll(listVal)>=10 s f.editable = $li(listVal,10)
		}
	}
	q form
}

}
