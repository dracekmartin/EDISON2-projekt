# EDActionInProgress — poznámky pro vývojáře

Overlay „Akce probíhá" s pixel-art kapybarou. Vlož jako **posledního sourozence**
obsahu v kořenovém `Gridu` okna (musí pokrýt celé okno). Na `Loaded` se sám naváže na
`EDViewModel.IsActionInProgress` — v XAML se `IsActive` binding **nepíše**. Aktivace
z VM: `using var _ = ActionInProgress();` (viz EDViewModel doc).

## Neodvoditelné

- **Storyboard startuje/zastavuje code-behind podle `IsActive`**, ne `Loaded` trigger —
  `RepeatBehavior=Forever` animace by jinak tikala i pro `Collapsed` overlay a držela
  dispatcher vzhůru. `Begin(..., isControllable: true)` při aktivaci zároveň resetuje
  kapybaru na start.
- **Závoj nekreslí sám** — drží `WindowDimmer.Dim(window)`; refcount zaručí jediný tint
  i při překryvu s dialogovým dimem ze shellu. Tint je jeden zmrazený brush
  (`WindowDimmer.TintBrush`) — míra ztmavení se mění na jednom místě.
  U nezobrazeného okna dimmer přeskočí vizuál a nechá jen blokaci vstupu.
- Stín karty je na samostatné vrstvě pod kartou — `DropShadowEffect` se tak
  nepřerendrovává každý frame animace.
- `RenderOptions.EdgeMode="Aliased"` + `SnapsToDevicePixels` na pixel-artu brání
  sub-pixelovým mezerám. Geometrii animace čti přímo z XAML.
