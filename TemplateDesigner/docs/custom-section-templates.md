# Custom šablony sekcí — uživatelský návod

Tento dokument popisuje, jak ukládat a načítat vlastní šablony sekcí.

---

## Co jsou custom šablony sekcí?

Custom šablona sekce je **uložená konfigurace sekce včetně všech elementů**, kterou si uživatel vytvoří a pojmenuje pro opakované použití.

**Příklad:**
- Vytvoříte sekci "Záhlaví DPO" s logem, názvem firmy a čárou
- Uložíte jako šablonu
- Příště: jedno kliknutí → instant kopie celé sekce

---

## Jak uložit sekci jako šablonu

### Krok 1: Vytvořte sekci s obsahem
```
1. [+ Přidat sekci] → Hlavička stránky
2. Přidejte do ní elementy:
   - Logo (ImageElement)
   - Název firmy (TextElement)
   - Čára (LineElement)
```

### Krok 2: Uložte jako šablonu
```
3. Pravý klik na sekci v levém panelu
4. Vyberte "💾 Uložit jako šablonu..."
5. Zadejte název (např. "Záhlaví DPO")
6. Klikněte [💾 Uložit]
```

→ Šablona se uloží do `section-templates/záhlaví-dpo.json`

---

## Jak načíst uloženou šablonu

### Varianta A — Menu:
```
1. Klikněte [+ Přidat sekci ▾]
2. V menu se objeví položka "💾 záhlaví-dpo"
3. Klikněte na ni
```

→ Vytvoří se kopie sekce včetně všech elementů

### Varianta B — Duplikace:
```
1. Pravý klik na existující sekci vytvořenou ze šablony
2. "Duplikovat"
```

→ Také vytvoří kopii (ale musí sekce už existovat v šabloně)

---

## Kde se šablony ukládají

**Cesta:**
```
<složka-aplikace>/section-templates/
```

**Příklad (Debug):**
```
C:\Users\miesb\source\EDISONMAIN2\TemplateDesigner\bin\Debug\net9.0-windows\section-templates\
```

**Příklad (Release/produkce):**
```
C:\Program Files\TemplateDesigner\section-templates\
```

→ Vedle .exe souboru aplikace

---

## Sdílení šablon s kolegy

### Možnost 1 — Kopie složky:
```
1. Zkopíruj složku `section-templates/` z tvého PC
2. Vlož do stejného umístění na PC kolegy
3. Restart aplikace (nebo nové načtení šablony)
```

### Možnost 2 — Sdílený disk (doporučeno):
```
1. Uložte šablony na sdílený síťový disk
2. Všichni uživatelé mají přístup ke stejným šablonám
3. Změny vidí všichni
```

**Poznámka:** V budoucnu budou šablony v centrální databázi (Caché).

---

## Formát JSON

Custom šablona je JSON soubor se strukturou sekce:

```json
{
  "name": "Záhlaví DPO",
  "printBehavior": "OnEveryPage",
  "designerHeight": 25,
  "overflowBehavior": "KeepTogether",
  "elements": [
    {
      "type": "ImageElement",
      "x": 5,
      "y": 5,
      "width": 40,
      "height": 15,
      "imagePath": "logo-dpo.png"
    },
    {
      "type": "TextElement",
      "x": 50,
      "y": 10,
      "text": "Dopravní podnik Ostrava",
      "fontSize": 14
    },
    {
      "type": "LineElement",
      "x": 0,
      "y": 24,
      "width": 210,
      "height": 1
    }
  ]
}
```

→ **Lze editovat ručně** v textovém editoru (pokud potřebujete rychlé úpravy)

---

## Běžné případy použití

| Scénář | Šablona |
|---|---|
| Logo + název firmy | Záhlaví DPO |
| Číslo stránky + datum tisku | Patička standard |
| Tabulka jízdních řádů (4 sloupce) | Tabulka JR |
| QR kód s popisem | QR kód dokument |
| Podpis + razítko | Patička s podpisem |

---

## Tipy a triky

### 🔹 Pojmenování šablon:
- Používejte **popisné názvy**: "Záhlaví DPO oficiální" místo "Template1"
- Přidejte **verzi** pokud se šablony vyvíjejí: "Tabulka JR v2"

### 🔹 Organizace:
- Vytvořte **základní sadu** šablon pro celou firmu
- Sdílejte je přes síťový disk nebo DB (budoucnost)

### 🔹 Údržba:
- Pravidelně **revidujte** zastaralé šablony
- **Smazat šablonu:** Smažte soubor `*.json` ze složky `section-templates/`

### 🔹 Backup:
- **Zálohujte** složku `section-templates/` při záloze systému
- Důležité firemní šablony archivujte

---

## FAQ

**Q: Můžu editovat šablonu po uložení?**
A: ANO. Načti šablonu, uprav sekci, ulož znovu pod stejným názvem (přepíše se).

**Q: Co když smažu soubor .json?**
A: Šablona zmizí z menu. Nelze obnovit (pokud nemáš backup).

**Q: Můžu použít šablonu v jiném projektu?**
A: ANO. Zkopíruj .json soubor do `section-templates/` druhého projektu.

**Q: Funguje to offline?**
A: ANO. Šablony jsou uložené lokálně, nepotřebuješ síť ani databázi.

**Q: Kdy budou šablony v databázi?**
A: Plánováno v V2 (upgrade po implementaci Caché backendu). Lokální soubory zůstanou jako fallback.

---

**Datum:** 2026-03-02
**Verze:** V1 (file-based)
