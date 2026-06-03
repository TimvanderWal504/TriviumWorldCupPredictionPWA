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

        // Typed HTTP client for API-Football v3
        services.AddHttpClient<FootballApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://v3.football.api-sports.io/");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
            }
        });

        // Quartz scheduler — single trigger, every 90 seconds
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
                    .WithIntervalInSeconds(90)
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
