Class ALVA.EDControls.EDInputForm.Form Extends %RegisteredObject
{

// ─── Validation trigger constants (mirrors C# EValidationTrigger) ─────────────

Parameter TriggerInit = "Init";

Parameter TriggerChange = "Change";

Parameter TriggerBeforeSave = "BeforeSave";

/// Trigger received with this form; set automatically by ReadFromIn.
Property trigger As %String;

/// Ordered list of Group and Field items that make up the form.
Property items As list Of ALVA.EDControls.EDInputForm.Item;

// ─── Builder ─────────────────────────────────────────────────────────────────

/// Appends a group separator.
Method AddGroup(groupName As %String)
{
	s g = ##class(ALVA.EDControls.EDInputForm.Group).%New()
	s g.groupName = groupName
	d ..items.Insert(g)
}

/// Appends a field and returns it so the caller can set value / validationError.
Method AddField(key As %String, label As %String, type As %String = "TEXT", validateOnChange As %Boolean = 0, required As %Boolean = 0) As ALVA.EDControls.EDInputForm.Field
{
	s f = ##class(ALVA.EDControls.EDInputForm.Field).%New()
	s f.key = key
	s f.label = label
	s f.type = type
	s f.validateOnChange = validateOnChange
	s f.required = required
	d ..items.Insert(f)
	q f
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

// ─── Serialization ────────────────────────────────────────────────────────────

/// Writes all items to ^||ML("OUT",...) using $i(i) indexing.
Method WriteToOut(del As %String)
{
	s i = 0
	s cnt = ..items.Count()
	f idx=1:1:cnt {
		s ^||ML("OUT",$i(i)) = ..items.GetAt(idx).Serialize(del)
	}
}

/// Reads ^||ML("IN",...) and returns a populated Form.
/// IN[1] is the validation trigger; IN[2..] are GROUP/FIELD entries.
ClassMethod ReadFromIn(del As %String) As ALVA.EDControls.EDInputForm.Form
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	s form.trigger = $g(^||ML("IN",1))
	s idx = ""
	f {
		s idx = $o(^||ML("IN",idx))
		q:idx=""
		s line = ^||ML("IN",idx)
		s type = $p(line,del,1)
		i type="GROUP" {
			d form.AddGroup($p(line,del,2))
		}
		i type="FIELD" {
			s f = form.AddField($p(line,del,2),$p(line,del,3),$p(line,del,4),$p(line,del,6),$p(line,del,7))
			s f.value = $p(line,del,5)
		}
	}
	q form
}

}
