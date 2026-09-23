using Xunit;

// Testy sdílejí dev server (zápisy pod testovací identitou) a EDControls drží statické hooky
// (geometrie oken, telemetrie). Paralelní kolekce xUnitu by na nich závodily.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
