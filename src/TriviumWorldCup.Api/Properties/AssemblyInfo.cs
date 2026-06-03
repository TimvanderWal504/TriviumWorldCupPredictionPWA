using System.Runtime.CompilerServices;

// Allow the test project to access internal members of TriviumWorldCup.Api.
// This enables unit tests for internal static helpers (e.g. ResultIngestionJob.CreateDeterministicGuid).
[assembly: InternalsVisibleTo("TriviumWorldCup.Api.Tests")]
