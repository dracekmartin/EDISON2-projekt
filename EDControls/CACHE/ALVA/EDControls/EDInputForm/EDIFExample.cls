Include (GENERAL, EDInputForm)

/// Referenční příklad EDInputFormu — ukazuje všechny dostupné featury.
/// Při přidání nové featury do EDInputFormu přidej odpovídající příklad i sem.
Class ALVA.EDControls.EDInputForm.EDIFExample Extends %RegisteredObject
{

// =============================================================================

//  Init — definice formuláře

// =============================================================================

ClassMethod Init(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams

		s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

		// ── Skupina 1: Typy polí ─────────────────────────────────────────────
		// Každý typ pole jednou, bez dalších featur — čistá ukázka typu.
		// Helpery (AddText, AddBool, AddNumber, AddDecimal) pro simple fieldy;
		// AddFieldToLastGroup + property setters pro komplex (Date, List, Color, File, ComboBox).

		d form.AddGroup("types", "Typy polí")

		// Helpery — jeden řádek per field
		d form.AddText("text", "Text")
		d form.AddNumber("number", "Celé číslo", "", "0", "999", "ks")
		d form.AddDecimal("decimal", "Velikost fontu", "10", "4", "200", "pt", "N2")

		// Verbose — AddFieldToLastGroup + post-setting (Date nemá vlastní helper)
		s f = form.AddFieldToLastGroup("date", "Datum", $$$FieldTypeDate, 0, 0)
		s f.value = "2025-01-15"

		d form.AddBool("bool", "Checkbox")

		s f = form.AddFieldToLastGroup("list", "Dropdown", $$$FieldTypeList, 0, 0)
		d f.AddListItem("A", "Volba A")
		d f.AddListItem("B", "Volba B")
		d f.AddListItem("C", "Volba C")
		s f.value = "A"

		s f = form.AddFieldToLastGroup("button", "Tlačítko", $$$FieldTypeButton, 1, 0)
		s f.value = "Klikni"

		s f = form.AddFieldToLastGroup("file", "Soubor", $$$FieldTypeFile, 0, 0)
		s f.fileFilter = "PDF soubory (*.pdf)|*.pdf|Všechny soubory (*.*)|*.*"

		s f = form.AddFieldToLastGroup("folder", "Složka", $$$FieldTypeFile, 0, 0)
		s f.fileFilter = $$$FileFilterFolder

		s f = form.AddFieldToLastGroup("color", "Barva", $$$FieldTypeColor, 0, 0)
		s f.value = "#FF003366"

		; Image: value = Base64 — drží se jen na klientovi, na server se neposílá (limit MLRest)
		s f = form.AddFieldToLastGroup("image", "Obrázek", $$$FieldTypeImage, 0, 0)
		s f.helpText = "Náhled + Nahrát/Vymazat; picker dodává host přes PickImageImpl"

		; ComboBox: editable s autocomplete — user může napsat libovolný text, list je nápověda
		s f = form.AddFieldToLastGroup("combo", "Pole agendy (combo)", $$$FieldTypeComboBox, 0, 0)
		d f.AddListItem("CISLO_LINKY",  "Číslo linky")
		d f.AddListItem("NAZEV_LINKY",  "Název linky")
		d f.AddListItem("DATUM_OD",     "Platnost od")
		d f.AddListItem("DATUM_DO",     "Platnost do")
		d f.AddListItem("DOPRAVCE",     "Dopravce")

		// ── Skupina 2: Povinná pole ──────────────────────────────────────────
		// required=1 → červená hvězdička, BeforeSave validuje neprázdnost.

		d form.AddGroup("required", "Povinná pole")

		d form.AddText("reqText", "Povinný text", "", 1)

		s f = form.AddFieldToLastGroup("reqList", "Povinný dropdown", $$$FieldTypeList, 0, 1)
		d f.AddListItem("X", "Možnost X")
		d f.AddListItem("Y", "Možnost Y")

		// ── Skupina 3: Nápověda (helpText) ───────────────────────────────────
		// helpText na skupině i na poli; tlačítko přepíná helpTextMode.

		d form.AddGroup("helptext", "Nápověda")
		s form.GetGroup("helptext").helpText = "Tato skupina ukazuje helpText na úrovni skupiny"

		s f = form.AddFieldToLastGroup("htField", "Pole s nápovědou", $$$FieldTypeText, 0, 0)
		s f.helpText = "Toto je nápověda k poli — zobrazí se jako tooltip nebo ve sloupci"

		s f = form.AddFieldToLastGroup("htToggle", "Režim nápovědy", $$$FieldTypeButton, 1, 0)
		s f.value = "Přepnout na sloupec"

		// ── Skupina 4: Editovatelnost ────────────────────────────────────────
		// editable=0 → pole je read-only (šedé pozadí). Dynamicky měněno checkboxem.

		d form.AddGroup("editable", "Editovatelnost")

		s f = form.AddFieldToLastGroup("readonlyText", "Pouze ke čtení", $$$FieldTypeText, 0, 0)
		s f.editable = 0
		s f.value = "Tuto hodnotu nelze změnit"
		s f.helpText = "Editovatelnost přepneš checkboxem níže"

		d form.AddFieldToLastGroup("editToggle", "Povolit editaci", $$$FieldTypeBool, 1, 0)

		// ── Skupina 5: Viditelnost ───────────────────────────────────────────
		// Skupina začíná jako Collapsed; tlačítko přepíná Visible ↔ Collapsed.

		d form.AddGroup("visCollapsed", "Sbalitelná skupina", $$$ItemVisibilityCollapsed)
		d form.AddFieldToLastGroup("visField", "Skryté pole", $$$FieldTypeText, 0, 0)

		d form.AddGroup("visControls", "Ovládání viditelnosti")
		s f = form.AddFieldToLastGroup("visToggle", "Přepnout skupinu", $$$FieldTypeButton, 1, 0)
		s f.value = "Rozbalit skupinu"

		// ── Skupina 6: Validace ──────────────────────────────────────────────
		// validateOnChange=1 → validuje při každé změně (Change trigger).

		d form.AddGroup("validation", "Validace")

		s f = form.AddFieldToLastGroup("valNumber", "Kladné číslo", $$$FieldTypeNumber, 1, 0)
		s f.helpText = "Musí být > 0"

		s f = form.AddFieldToLastGroup("valRange", "Procenta (0–100)", $$$FieldTypeDecimal, 1, 0)
		s f.helpText = "Hodnota musí být v rozsahu 0 až 100"

		s f = form.AddFieldToLastGroup("valFormat", "Kód (5 znaků)", $$$FieldTypeText, 1, 0)
		s f.helpText = "Přesně 5 znaků, např. AB123"

		// ── Skupina 7: Kombinace featur ──────────────────────────────────────
		// Pole kombinující více vlastností najednou.

		d form.AddGroup("combos", "Kombinace featur")

		s f = form.AddFieldToLastGroup("comboReqHelp", "Povinné s nápovědou", $$$FieldTypeText, 0, 1)
		s f.helpText = "Toto pole je povinné a má nápovědu zároveň"

		s f = form.AddFieldToLastGroup("comboListVal", "Dropdown s validací", $$$FieldTypeList, 1, 0)
		d f.AddListItem("OK", "Platná volba")
		d f.AddListItem("WARN", "Volba s varováním")
		d f.AddListItem("ERR", "Neplatná volba")
		s f.helpText = "Volba ERR vyvolá validační chybu"

		s f = form.AddFieldToLastGroup("comboDynamic", "Povolit závislá pole", $$$FieldTypeBool, 1, 0)
		s f.helpText = "Zapne editaci a viditelnost závislých polí níže"

		s f = form.AddFieldToLastGroup("comboDepA", "Závislé datum", $$$FieldTypeDate, 0, 0)
		s f.editable = 0

		s f = form.AddFieldToLastGroup("comboDepB", "Závislý text", $$$FieldTypeText, 0, 0)
		s f.editable = 0

		// ── Skupina 8: Akční tlačítka ────────────────────────────────────────
		// Tlačítka která mění hodnoty jiných polí.

		d form.AddGroup("actions", "Akční tlačítka")

		s f = form.AddFieldToLastGroup("fillDefaults", "Vyplnit příklady", $$$FieldTypeButton, 1, 0)
		s f.value = "Vyplnit výchozí hodnoty"

		s f = form.AddFieldToLastGroup("clearAll", "Vyčistit formulář", $$$FieldTypeButton, 1, 0)
		s f.value = "Vyčistit vše"

		d form.WriteToOut()
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

// =============================================================================

//  Validate — validace a dynamické chování

// =============================================================================

ClassMethod Validate(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams

		s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

		// ── Formátové / business validace (skip při Init) ────────────────────

		i 'form.IsInitValidation() {
			// Kladné číslo
			s f = form.GetField("valNumber")
			i f.value'="" {
				i +f.value<=0 s f.validationError = "Musí být kladné číslo (> 0)"
			}

			// Rozsah 0–100
			s f = form.GetField("valRange")
			i f.value'="" {
				i (+f.value<0)||(+f.value>100) s f.validationError = "Hodnota musí být 0 až 100"
			}

			// Formát — přesně 5 znaků
			s f = form.GetField("valFormat")
			i f.value'="" {
				i $l(f.value)'=5 s f.validationError = "Kód musí mít přesně 5 znaků"
			}

			// Dropdown validace — volba ERR je neplatná
			s f = form.GetField("comboListVal")
			i f.value="ERR" s f.validationError = "Tato volba není povolena"
		}

		// ── Povinná pole (validace při BeforeSave) ───────────────────────────

		i form.IsBeforeSaveValidation() {
			s f = form.GetField("reqText")
			i f.value="" s f.validationError = "Povinný text musí být vyplněn"

			s f = form.GetField("comboReqHelp")
			i f.value="" s f.validationError = "Toto pole je povinné"
		}

		// ── Tlačítka ─────────────────────────────────────────────────────────

		i form.HasChangedField() {

			// Přepnutí helpTextMode (Tooltip ↔ Column)
			i form.changedKey = "htToggle" {
				i form.helpTextMode = $$$HelpTextModeTooltip {
					s form.helpTextMode = $$$HelpTextModeColumn
					s form.GetField("htToggle").value = "Přepnout na tooltip"
				} else {
					s form.helpTextMode = $$$HelpTextModeTooltip
					s form.GetField("htToggle").value = "Přepnout na sloupec"
				}
			}

			// Přepnutí viditelnosti skupiny (Visible ↔ Collapsed)
			i form.changedKey = "visToggle" {
				s g = form.GetGroup("visCollapsed")
				i g.visibility = $$$ItemVisibilityCollapsed {
					s g.visibility = $$$ItemVisibilityVisible
					s form.GetField("visToggle").value = "Sbalit skupinu"
				} else {
					s g.visibility = $$$ItemVisibilityCollapsed
					s form.GetField("visToggle").value = "Rozbalit skupinu"
				}
			}

			// Ukázkové tlačítko (button field) — jen echo zpět
			i form.changedKey = "button" {
				s form.GetField("text").value = "Tlačítko bylo stisknuto"
			}

			// Vyplnit výchozí hodnoty
			i form.changedKey = "fillDefaults" {
				s form.GetField("text").value = "Ukázkový text"
				s form.GetField("number").value = "42"
				s form.GetField("decimal").value = "3.14"
				s form.GetField("date").value = "2025-06-01"
				s form.GetField("bool").value = "1"
				s form.GetField("list").value = "B"
				s form.GetField("reqText").value = "Vyplněno"
				s form.GetField("reqList").value = "X"
				s form.GetField("valNumber").value = "7"
				s form.GetField("valRange").value = "50"
				s form.GetField("valFormat").value = "AB123"
				s form.GetField("comboReqHelp").value = "Ukázka"
				s form.GetField("comboListVal").value = "OK"
				s form.GetField("file").value = "C:\Documents\report.pdf"
				s form.GetField("folder").value = "C:\Documents\Output"
			}

			// Vyčistit vše
			i form.changedKey = "clearAll" {
				s cnt = form.items.Count()
				f idx=1:1:cnt {
					s item = form.items.GetAt(idx)
					i item.%IsA("ALVA.EDControls.EDInputForm.Field") {
						// Tlačítka a toggle checkboxy nečistíme
						i (item.type=$$$FieldTypeButton) continue
						i (item.key="editToggle")||(item.key="comboDynamic") continue
						s item.value = ""
					}
				}
			}
		}

		// ── Dynamická editovatelnost ─────────────────────────────────────────

		// editToggle checkbox → přepíná readonlyText.editable
		s toggle = form.GetField("editToggle")
		s target = form.GetField("readonlyText")
		i toggle.value = "1" {
			s target.editable = 1
		} else {
			s target.editable = 0
		}

		// comboDynamic checkbox → přepíná comboDepA + comboDepB
		s dynToggle = form.GetField("comboDynamic")
		s depA = form.GetField("comboDepA")
		s depB = form.GetField("comboDepB")
		i dynToggle.value = "1" {
			s depA.editable = 1
			s depB.editable = 1
		} else {
			s depA.editable = 0
			s depB.editable = 0
			s depA.value = ""
			s depB.value = ""
		}

		d form.WriteToOut()
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

// =============================================================================

//  Save — uložení dat

// =============================================================================

ClassMethod Save(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams

		s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

		k ^EDIFExample(userID)
		s cnt = form.items.Count()
		f idx=1:1:cnt {
			s item = form.items.GetAt(idx)
			i item.%IsA("ALVA.EDControls.EDInputForm.Field") {
				s ^EDIFExample(userID, item.key) = item.value
			}
		}

		d form.WriteToOut()
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

}
