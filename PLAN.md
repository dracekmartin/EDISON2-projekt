# Plán
1. Používat Claude!!!
2. Standardizace - MH
   1. Hlášky
   3. Názvosloví akcí (lokalizace)
   4. Ikony
3. Architektura projektu - JM
   1. Složky v .NET a v Cache
      1. V Caché ve Windows složka pro celé Windows, v ní soubory InputForm.cls, DataGrid.cls, Window.cls
   2. Suffix Window namísto prefix form
4. Vytvořit README.md a popsat body 1 a 2
5. EDWindow - MH
   1. EDErrorHandler
   2. Akce probíhá
   3. MlRest
   4. FormMode
6. EDInputForm - MH
7. EDDataGrid - JM
8. Šablony formulářů
   1. Prázné EDWindow - MH
   2. EDInputForm - MH
   3. EDDataGrid - JM

## 3. Struktura projektu

### Verze 1 (Legacy)

/MyProject
├── /dotnet
│   ├── /Windows
│   ├── /Controls
│   ├── /ViewModels
│   ├── /Models
│   └── ...
│
├── /cache
│   ├── .project
│   └── /ALVA
│       └── /NAZEV_CACHE_PROJEKTU
│           ├── /Windows
│           │   ├── /Window          # Základní třída okna
│           │   ├── /InputForm       # Všechny InputForm okna (= BIG)
│           │   └── /DataGrid        # Všechna DataGrid okna (= Seznam)
│           └── /API
│
├── CLAUDE.md (Programátorský + AI manuál)
└── README.md (Uživatelský manuál)


### Verze 2

/MyProject
├── /dotnet
│   ├── /Windows
         - EDISONMAIN/
           - DesignTimeData/ (Optional)
           - EDISONMAINWindow.xaml
           - EDISONMAINWindow.xaml.cs
           - EDISONMAINViewModel.cs
│   ├── /Controls/ (Optional)
│   ├── /Models/ (Optional)
│   └── ...
│
├── /cache
│   ├── .project
│   └── /ALVA
│       └── /NAZEV_CACHE_PROJEKTU/
│           ├── /Windows/
│           │    - EDISONMAIN/
                   - EDISONMAINInputForm1.cls
                   - EDISONMAINDataGrid1.cls
                   - ...
│           └── /API/
│
├── CLAUDE.md (Programátorský + AI manuál)
└── README.md (Uživatelský manuál)