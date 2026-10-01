using Xunit;

// Každý test ovládá skutečný shell na ploše a sdílí E2E GUID - dva naráz by si přebíraly okna i zámek.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
