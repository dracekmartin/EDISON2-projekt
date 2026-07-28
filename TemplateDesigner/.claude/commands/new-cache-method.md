---
description: Vytvoř Caché metodu + odpovídající C# volání s QueryOrThrow
argument-hint: [NázevMetody]
allowed-tools: Read, Edit, Write, Bash(grep *)
---

Vytvoř novou Caché metodu "$ARGUMENTS" a odpovídající C# volání.
Vždy používej CVar formát - nikdy object[].

## Kontext - přečti před zahájením

Existující Caché třídy: !`ls CACHE/ALVA/TemplateDesigner/API/`
Vzorová Caché třída: !`cat CACHE/ALVA/TemplateDesigner/API/AgendaAPI.cls`
Referenční implementace: !`cat CACHE/ALVA/TemplateDesigner/API/ZastavkoveJrMhdAPI.cls`
CacheAgendaProvider (C# strana): !`cat dotnet/Serialization/CacheAgendaProvider.cs`

## Co vytvořit

### 1. Caché metoda (přidej do příslušné třídy v CACHE/)

Šablona s CVar výstupem (pojmenované klíče):
```objectscript
/// Popis co metoda dělá
ClassMethod $ARGUMENTS(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        // Čtení vstupních parametrů
        s paramA = $g(^||ML("IN","paramA"))
        s paramB = $g(^||ML("IN","paramB"))

        // Business logika
        // ...

        // Výstup - pojmenované klíče (čte se jako output["klic"] v C#)
        s ^||ML("OUT","result") = "ok"
        s ^||ML("OUT","data") = $lb("TypeA","value1","value2")

        // Výstup - indexované řádky (iteruje se foreach v C#)
        // s i = 0
        // s ^||ML("OUT",$i(i)) = $lb("ROW","a","b")
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}
```

### 2. C# volání (přidej do příslušné třídy v dotnet/)

Šablona pro pojmenované klíče:
```csharp
public ReturnType $ARGUMENTS(string paramA, string paramB)
{
    var input = new CVar();
    input["paramA"] = new CVar(paramA);
    input["paramB"] = new CVar(paramB);

    CVar output = _mlRest.QueryOrThrow("ALVA.TemplateDesigner.API.NázevTřídy", "$ARGUMENTS", input);

    // Čtení pojmenovaných hodnot
    string result = output["result"].Value?.ToString() ?? "";
    CList data = output["data"];  // implicit CVar→CList

    return /* zpracovaný výsledek */;
}
```

Šablona pro iteraci řádků:
```csharp
public List<SomeType> $ARGUMENTS(string paramA)
{
    var input = new CVar();
    input["paramA"] = new CVar(paramA);

    CVar output = _mlRest.QueryOrThrow("ALVA.TemplateDesigner.API.NázevTřídy", "$ARGUMENTS", input);

    var results = new List<SomeType>();
    foreach (KeyValuePair<string, CVar> kvp in output)
    {
        if (kvp.Key == "ERR") continue;
        CList row = kvp.Value;
        string type = row.GetString(1);  // 1-based indexing
        if (type != "OCEKAVANY_TYP") continue;

        results.Add(new SomeType
        {
            Field1 = row.GetString(2),
            Field2 = row.GetString(3),
        });
    }
    return results;
}
```

## Pravidla - vždy dodržovat

- **CVar** pro input i output - nikdy `object[]` v novém kódu
- `$g(^||ML("IN","key"))` pro čtení vstupu (bezpečné i když klíč chybí)
- `s err = ""` + `$$$FillParams` + `$$$CatchErrRest` + `ret $$$OK` - povinná kostra. **Bez `$$$Init`** (TD konvence, cache-api rule 14); `s err = ""` na prvním řádku je povinné, protože `$$$CatchErrRest` čte `err` a `$$$FillParams` ho nenastavuje
- **ObjectScript**: nikdy `_` v dot-notation - `_` je operátor konkatenace!
  - ❌ `obj.KEY_NAME = v`  → způsobí chybu (concatenates KEY, NAME)
  - ✅ `obj.%Set("KEY_NAME", v)` → správně
- `QueryOrThrow` automaticky vyhodí výjimku při chybě - neřeš `RestStatus` zvlášť
- Pokud potřebuješ graceful degradation (pokračovat i při chybě), použij `Query` + `RestStatus.IsError`

## Po dokončení

Připomeň uživateli:
- Nahrát Caché třídu do systému (přes Studio nebo `do $system.OBJ.Load(...)`)
- Ověřit že metoda je dostupná pod správným namespace
