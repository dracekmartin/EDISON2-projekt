using Xunit;

// Více test harnessů (CreateVM v EdisonMainViewModelTests / FavoritesAndUsageTests / AppBootstrapperTests)
// mutuje statický composition-root singleton EDServices.Current přes EDServices.Initialize/Reset.
// Paralelní běh kolekcí xUnitu by na něm závodil → nedeterministické pády. Serializujeme celou assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
