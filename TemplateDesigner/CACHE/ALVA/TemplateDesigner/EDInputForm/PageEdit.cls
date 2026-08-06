Include (GENERAL, EDInputForm)

/// EDInputForm dispatch třída pro editaci vlastností stránky (Template.PageSize + CultureCode).
///
/// Field key = property path na C# Template:
///   PageSize.Width / .Height / .MarginTop/Bottom/Left/Right, CultureCode.
/// „Format" a „Orientation" jsou VIRTUÁLNÍ driver pole - nemají property na Template
/// (ElementPropertyAccessor je bezpečně ignoruje při apply). Slouží jen k řízení výpočtu
/// rozměrů ve Validate: změna formátu/orientace → server dopočítá Width/Height a echem je vrátí.
///
/// Formáty stránky se berou z ALVA.TemplateDesigner.API.Settings (per-IdFa override + vestavěné
/// defaulty), ne hardcoded zde.
Class ALVA.TemplateDesigner.EDInputForm.PageEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

        // ── Skupina "Stránka" ─────────────────────────────────────────────
        d form.AddGroup("page", "Stránka")

        s f = form.AddFieldToLastGroup("Format", "Formát", $$$FieldTypeList, 1, 1)
        s formats = ##class(ALVA.TemplateDesigner.API.Settings).PrintFormats($g(IdFa))
        f i=1:1:$ll(formats) {
            s rec = $li(formats,i)
            d f.AddListItem($lg(rec,1), $lg(rec,4))
        }
        d f.AddListItem("Custom", "Vlastní")
        s f.value = "A4"

        s f = form.AddFieldToLastGroup("Orientation", "Orientace", $$$FieldTypeList, 1, 1)
        d f.AddListItem("portrait", "Na výšku"), f.AddListItem("landscape", "Na šířku")
        s f.value = "portrait"

        s f = form.AddDecimal("PageSize.Width", "Šířka", 210, 1, 2000, "mm")
        s f.validateOnChange = 1
        s f = form.AddDecimal("PageSize.Height", "Výška", 297, 1, 2000, "mm")
        s f.validateOnChange = 1

        // ── Skupina "Okraje" ──────────────────────────────────────────────
        d form.AddGroup("margins", "Okraje")
        d form.AddDecimal("PageSize.MarginTop", "Nahoře", 0, 0, 200, "mm")
        d form.AddDecimal("PageSize.MarginBottom", "Dole", 0, 0, 200, "mm")
        d form.AddDecimal("PageSize.MarginLeft", "Vlevo", 0, 0, 200, "mm")
        d form.AddDecimal("PageSize.MarginRight", "Vpravo", 0, 0, 200, "mm")

        // ── Skupina "Jazyk" ───────────────────────────────────────────────
        d form.AddGroup("locale", "Jazyk")
        s f = form.AddFieldToLastGroup("CultureCode", "Kultura (formátování)", $$$FieldTypeList, 0, 0)
        d f.AddListItem("", "(výchozí dle systému)"), f.AddListItem("cs-CZ", "Čeština")
        d f.AddListItem("sk-SK", "Slovenština"), f.AddListItem("en-US", "Angličtina")
        d f.AddListItem("de-DE", "Němčina")

        // ── Skupina "Výchozí styl" ────────────────────────────────────────
        // Výchozí font celé šablony - kořen kaskády (dědí ho elementy bez vlastního fontu).
        // Prázdné = zapečený default (Arial). Key = property path → Template.DefaultTextStyle.FontFamily
        // (accessor lazy alokuje DefaultTextStyle). Ostatní style property zatím vědomě nevystaveny.
        //
        // ComboBox (editovatelný + autocomplete) s PRÁZDNÝM seznamem - Caché fonty klienta nezná.
        // Seznam nainstalovaných fontů naplní C# klient po načtení schématu (ElementEditDialog
        // .OnFormInitialized z SKFontManager). Editovatelný = uživatel může i napsat vlastní font.
        d form.AddGroup("style", "Výchozí styl")
        d form.AddFieldToLastGroup("DefaultTextStyle.FontFamily", "Výchozí font", $$$FieldTypeComboBox, 0, 0)

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Validate(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

        s fFmt = form.GetField("Format")
        s fOri = form.GetField("Orientation")
        s fW = form.GetField("PageSize.Width")
        s fH = form.GetField("PageSize.Height")

        // Guard - kdyby některé pole chybělo (GetField vrací ""), jen echo bez výpočtu.
        i (fFmt'="")&&(fOri'="")&&(fW'="")&&(fH'="") {
            s ck = form.changedKey
            i form.HasChangedField()&&((ck="Format")||(ck="Orientation")) {
                // Preset → dopočítat rozměry (mimo "Vlastní")
                s dims = ##class(ALVA.TemplateDesigner.API.Settings).FormatDims($g(IdFa), fFmt.value)
                i (fFmt.value'="Custom")&&(dims'="") {
                    s bw = $p(dims,"~",1)
                    s bh = $p(dims,"~",2)
                    i fOri.value="landscape" {
                        s fW.value = bh
                        s fH.value = bw
                    } else {
                        s fW.value = bw
                        s fH.value = bh
                    }
                }
            } else {
                // Ruční rozměry / initial → odvodit formát + orientaci z rozměrů
                s w = +fW.value
                s h = +fH.value
                s mn = $s(w<h:w,1:h)
                s mx = $s(w<h:h,1:w)
                s detected = "Custom"
                s formats = ##class(ALVA.TemplateDesigner.API.Settings).PrintFormats($g(IdFa))
                f i=1:1:$ll(formats) {
                    s rec = $li(formats,i)
                    s bw = $lg(rec,2)
                    s bh = $lg(rec,3)
                    i ((mn-bw)<1)&&((bw-mn)<1)&&((mx-bh)<1)&&((bh-mx)<1) s detected=$lg(rec,1) q
                }
                s fFmt.value = detected
                s fOri.value = $s(w>h:"landscape",1:"portrait")
            }
        }

        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

}
