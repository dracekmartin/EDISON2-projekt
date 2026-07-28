---
description: Přidej nový typ elementu do TemplateDesigneru (třída + JSON + toolbar)
argument-hint: [NázevElementu]
allowed-tools: Read, Edit, Write, Bash(grep *), Bash(ls *)
---

Přidej nový element typu "$ARGUMENTS" do TemplateDesigneru.
Postupuj přesně podle těchto 5 kroků - v tomto pořadí.

## Kontext - přečti před zahájením

Vzorový element (TextElement): !`cat dotnet/Models/Elements/TextElement.cs`
Existující JsonDerivedType registrace: !`grep -n "JsonDerivedType" dotnet/Models/TemplateElement.cs`
Toolbar handler: !`grep -n -A 3 "BtnAddElement_Click" dotnet/MainWindow.xaml.cs`
Existující elementy: !`ls dotnet/Models/Elements/`

## 5 kroků implementace

### Krok 1 - Vytvořit třídu elementu

Soubor: `dotnet/Models/Elements/$ARGUMENTS.cs`

Šablona:
```csharp
using SkiaSharp;
using EDControls;

namespace TemplateDesigner.Models.Elements;

public class $ARGUMENTS : TemplateElement
{
    // --- Properties ---
    // (přidej relevantní properties)

    // --- Rendering ---
    public override void Render(RenderContext ctx)
    {
        // TODO: implementace renderování
    }

    // --- Clone ---
    public override TemplateElement Clone() => new $ARGUMENTS
    {
        Id        = Guid.NewGuid(),
        X         = X,
        Y         = Y,
        Width     = Width,
        Height    = Height,
        ZIndex    = ZIndex,
        IsLocked  = IsLocked,
        VisibilityExpression = VisibilityExpression,
        // TODO: zkopírovat všechny vlastní properties
    };

    // --- Property descriptors ---
    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        // TODO: přidat deskriptory
        .. GetPositionDescriptors()  // X, Y, Width, Height, ZIndex, Lock, Visibility
    ];
}
```

### Krok 2 - Registrovat JsonDerivedType

V `dotnet/Models/TemplateElement.cs` přidej na konec bloku atributů `[JsonDerivedType]`:
```csharp
[JsonDerivedType(typeof($ARGUMENTS), "$ARGUMENTS")]
```

### Krok 3 - Přidat do toolbar menu

V `dotnet/MainWindow.xaml.cs` v metodě `BtnAddElement_Click` přidej novou položku menu:
```csharp
var mi$ARGUMENTS = new MenuItem { Header = "$ARGUMENTS" };  // uprav Header na česky
mi$ARGUMENTS.Click += (_, _) => AddElement(new $ARGUMENTS { Width = 40, Height = 20 });
menu.Items.Add(mi$ARGUMENTS);
```

### Krok 4 - Přidat using pokud chybí

Zkontroluj že `dotnet/MainWindow.xaml.cs` a `dotnet/Designer/DesignerCanvas.cs` mají:
```csharp
using TemplateDesigner.Models.Elements;
```

### Krok 5 - Ověřit build

Spusť build a oprav případné chyby:
```
dotnet build dotnet/TemplateDesigner.csproj
```

## Po dokončení

Připomeň uživateli:
- Doplnit implementaci `Render()` - renderování na SkiaSharp canvas
- Doplnit `GetPropertyDescriptors()` - properties viditelné v panelu vlastností
- Spustit aplikaci a otestovat přidání elementu na canvas
