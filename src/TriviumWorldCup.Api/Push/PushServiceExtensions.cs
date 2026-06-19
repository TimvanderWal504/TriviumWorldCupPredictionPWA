using Microsoft.Extensions.Options;
using Quartz;
using TriviumWorldCup.Api.Scheduling;
using WebPush;

namespace TriviumWorldCup.Api.Push;

/// <summary>
/// Registers Web Push services: the WebPushClient singleton and the PushReminderJob Quartz trigger.
/// Call from Program.cs: <c>builder.Services.AddPushServices(builder.Configuration);</c>
///
/// Guard: if VAPID keys are missing/empty, a warning is logged and the Quartz job is not registered.
/// The WebPushClient is always registered so the DI graph compiles; it just won't be called without keys.
/// </summary>
public static class PushServiceExtensions
{
    public static IServiceCollection AddPushServices(
        this IServiceCollection services,
        IConfiguration configuration,
        TriviumSchedulingOptions? scheduling = null)
    {
        scheduling ??= configuration.GetSection("Scheduling").Get<TriviumSchedulingOptions>()
            ?? new TriviumSchedulingOptions();
        // WebPushClient creates an HttpClient internally.
        // The library is not thread-safe for concurrent calls per instance,
        // but each Quartz job execution is [DisallowConcurrentExecution] so
        // a singleton is safe here.
        services.AddSingleton<WebPushClient>();

        var vapidPublicKey = configuration["Push:VapidPublicKey"];
        if (string.IsNullOrWhiteSpace(vapidPublicKey))
        {
            Console.WriteLine("[WARN] Push:VapidPublicKey is not configured. " +
                              "Push notification reminders are disabled until VAPID keys are provided " +
                              "via environment variables (Push__VapidPublicKey, Push__VapidPrivateKey, Push__VapidSubject).");
            return services;
        }

        // Register the reminder job with a 30-minute trigger
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("PushReminderJob", "Push");

            q.AddJob<PushReminderJob>(opts => opts
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution());

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("PushReminderTrigger", "Push")
                .WithSimpleSchedule(s => s
                    .WithIntervalInMinutes(scheduling.PushReminderIntervalMinutes)
                    .RepeatForever())
                // Start 30 seconds after app startup to let the DB initialise
                .StartAt(DateBuilder.FutureDate(30, IntervalUnit.Second)));
        });

        // AddQuartzHostedService is idempotent — safe to call multiple times.
        services.AddQuartzHostedService(opts =>
        {
            opts.WaitForJobsToComplete = true;
        });

        return services;
    }
}
