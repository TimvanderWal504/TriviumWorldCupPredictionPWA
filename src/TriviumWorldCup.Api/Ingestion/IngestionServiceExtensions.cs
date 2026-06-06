using Quartz;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Ingestion;

/// <summary>
/// Registers the result ingestion pipeline: FootballApiClient, Quartz scheduler, and ResultIngestionJob.
/// Call from Program.cs: <c>builder.Services.AddIngestion(builder.Configuration);</c>
/// </summary>
public static class IngestionServiceExtensions
{
    public static IServiceCollection AddIngestion(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var apiKey = configuration["Football:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Log a warning at startup — ingestion will be disabled until the key is supplied.
            // Do NOT throw: the app must still start in dev/demo mode without the key.
            Console.WriteLine("[WARN] Football:ApiKey is not configured. " +
                              "Result ingestion is disabled until the key is provided " +
                              "via the Football__ApiKey environment variable.");
        }

        // Register ScoringRecomputeService only if it hasn't been registered already.
        // TWC-8 registers it as AddScoped in Program.cs. This guard prevents double registration.
        if (!services.Any(d => d.ServiceType == typeof(ScoringRecomputeService)))
        {
            services.AddScoped<ScoringRecomputeService>();
        }

        // Typed HTTP client for API-Football v3.
        // Register against the interface so ResultIngestionJob can resolve IFootballApiClient.
        services.AddHttpClient<IFootballApiClient, FootballApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://v3.football.api-sports.io/");
            client.DefaultRequestHeaders.Add("x-apisports-key", apiKey ?? "");
        });

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // No key — skip Quartz entirely. The job would fail every 90 seconds otherwise.
            return services;
        }

        // Quartz scheduler — single trigger, every 20 seconds
        // Only fires API calls during live windows (DB check gates each execution).
        // Pro plan: 7,500 req/day; worst case 4 matches × 630 polls = 2,520/day.
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("ResultIngestionJob", "Ingestion");

            q.AddJob<ResultIngestionJob>(opts => opts
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution());

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("ResultIngestionTrigger", "Ingestion")
                .WithSimpleSchedule(s => s
                    .WithIntervalInSeconds(20)
                    .RepeatForever())
                // Start 10 seconds after app startup to allow the DB to initialise
                .StartAt(DateBuilder.FutureDate(10, IntervalUnit.Second)));
        });

        services.AddQuartzHostedService(opts =>
        {
            // Wait for the scheduler to shut down cleanly before the process exits
            opts.WaitForJobsToComplete = true;
        });

        return services;
    }
}
