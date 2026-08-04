# Checklist na ruční proklik - celkové review 2026-08-04

> ⚠️ **Jednorázový dokument** - po projití a vytěžení (opravy / body do roadmapy) SMAZAT
> (git historie stačí). Vstup pro `B58`; body 3-5 souvisí s rozhodnutím `B52`.

Věci, které z kódu nejde posoudit - vizuální dojem, focus, srozumitelnost hlášek naživo.

1. **Ctrl+N jako nováček**: založ novou šablonu a bez znalostí zkus pokračovat - je z obrazovky
   (chip „Agenda: -", prázdné panely) poznat, že dalším krokem je Ctrl+Shift+A?
2. **Ctrl+P bez vybrané linky**: klikni „Tisk všech vybraných" → hláška teď navádí na štítek
   linky (opraveno 4. 8.) - dovede tě reálně k cíli?
3. **Server-side ověření (past z changelogu (7))**: zavolat `GetParameters` agendy ZastavkoveJrAd
   na dev serveru a ověřit, že reálně nevrací `TISK_KOLA`/`TISK_MISTENEK`/`SCHOVAT_AGREGAT_BEZBARIER`/
   `POZNAMKY_LINKY` (zdroj v repu je čistý, ale build na serveru nemusí být).
4. **Regrese pro reálného zákazníka (`B52`)**: pod firmou, kde měl uživatel `TISK_KOLA=true`
   (blob `^ALVA.EDControls.UserData(...,"TemplateDesigner.PrintParams",...)`), otevři vývěsku -
   zmizely symboly kol? A vrátil se souhrn bezbariér tomu, kdo ho měl skrytý?
5. **Nové přepínače v bloku poznámek**: zapni „Přeprava kol u odjezdů" - objeví se symbol u minuty
   I vysvětlivka v legendě současně? Zkus totéž v šabloně bez bloku poznámek - je poznat, že to
   bez něj nejde?
6. **Viditelnost řádku u zastávek**: helpText opraven 4. 8. na skutečná pole - ověř, že příklad
   `{NA_ZNAMENI} == 'false'` opravdu funguje.
7. **F1 jako nováček**: podle sekce „Data, agenda a instance" najdi parametry sestavy - text
   opraven na „tab Parametry vlevo"; sedí to s realitou? (Obsah nápovědy doplní `B58`.)
8. **Mock vs. reálná data**: bez vybrané instance - poznáš pohledem na náhled, že jde o smyšlená
   data (bez pageru je jediná stopa)? → podklad pro badge v `B58`.
9. **Výpadek Caché za běhu** (odpojit VPN): F5, výběr linky, Ctrl+S - kde se chyby objeví
   (status bar dole vs. popup) a všimneš si jich; je hláška srozumitelná?
10. **Dialog neuložených změn** (Ctrl+N nad rozdělanou šablonou): jaké popisky mají tlačítka -
    akční „Uložit/Zahodit/Storno" dle ux-conventions, nebo Ano/Ne?
11. **Synchronizace parametrů**: změň „Rozdělení sloupců" v tabu Parametry (má proběhnout re-fetch
    a náhled se změnit), pak otevři tiskový dialog, změň tam jiný parametr, zavři - ukazuje tab
    tutéž hodnotu? (Historicky nejdražší třída chyb v projektu.)
12. **Výběr linek - focus a klávesnice**: pořadí tabů filtr skupiny → seznam → Použít; Enter/Esc;
    „Použít" s 0 zaškrtnutými → náhled tiše spadne na mock - působí to jako chyba? (`B58`)
13. **Posun šipkami vs. nápověda**: help slibuje šipky 1 mm / Shift = krok mřížky / Ctrl = 0,5 mm -
    s výchozím krokem mřížky 1 mm se Shift chová stejně jako bez něj; působí to jako rozbité?
14. **Piktogramy očima** (dluh z 3. 8.): kolo, vlak, autobus na reálné vývěsce - stroj vidí znak,
    ne co je nakreslené.
15. **Nové labely negativních značek** („Pozadí značky", „Barva textu značky", u mřížky
    „Pozadí/Barva negativní značky") - čitelné v panelu i dialogu? Nepřetékají?
