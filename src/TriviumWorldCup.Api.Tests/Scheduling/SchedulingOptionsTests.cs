using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TriviumWorldCup.Api.Scheduling;

namespace TriviumWorldCup.Api.Tests.Scheduling;

public class SchedulingOptionsTests
{
    [Fact]
    public void Defaults_MatchPreviouslyHardcodedValues()
    {
        var opts = new TriviumSchedulingOptions();
        Assert.Equal(30, opts.PollIntervalSeconds);
        Assert.Equal(30, opts.LiveWindowMinutes);
        Assert.Equal(2.0, opts.PushReminderLookaheadHours);
        Assert.Equal(30, opts.PushReminderIntervalMinutes);
    }

    [Fact]
    public void DiBinding_EmptySection_UsesDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<TriviumSchedulingOptions>().BindConfiguration("Scheduling");
        services.AddSingleton<IConfiguration>(config);

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<TriviumSchedulingOptions>>().Value;

        Assert.Equal(30, opts.PollIntervalSeconds);
        Assert.Equal(30, opts.LiveWindowMinutes);
        Assert.Equal(2.0, opts.PushReminderLookaheadHours);
        Assert.Equal(30, opts.PushReminderIntervalMinutes);
    }

    [Fact]
    public void Override_PollIntervalSeconds()
    {
        var opts = BindFromDict(new() { ["Scheduling:PollIntervalSeconds"] = "60" });
        Assert.Equal(60, opts.PollIntervalSeconds);
        Assert.Equal(30, opts.LiveWindowMinutes);
    }

    [Fact]
    public void Override_LiveWindowMinutes()
    {
        var opts = BindFromDict(new() { ["Scheduling:LiveWindowMinutes"] = "45" });
        Assert.Equal(45, opts.LiveWindowMinutes);
        Assert.Equal(30, opts.PollIntervalSeconds);
    }

    [Fact]
    public void Override_PushReminderLookaheadHours()
    {
        var opts = BindFromDict(new() { ["Scheduling:PushReminderLookaheadHours"] = "3" });
        Assert.Equal(3.0, opts.PushReminderLookaheadHours);
    }

    [Fact]
    public void Override_PushReminderIntervalMinutes()
    {
        var opts = BindFromDict(new() { ["Scheduling:PushReminderIntervalMinutes"] = "15" });
        Assert.Equal(15, opts.PushReminderIntervalMinutes);
    }

    [Fact]
    public void Override_AllFour_Independent()
    {
        var opts = BindFromDict(new()
        {
            ["Scheduling:PollIntervalSeconds"] = "10",
            ["Scheduling:LiveWindowMinutes"] = "20",
            ["Scheduling:PushReminderLookaheadHours"] = "1",
            ["Scheduling:PushReminderIntervalMinutes"] = "5"
        });
        Assert.Equal(10, opts.PollIntervalSeconds);
        Assert.Equal(20, opts.LiveWindowMinutes);
        Assert.Equal(1.0, opts.PushReminderLookaheadHours);
        Assert.Equal(5, opts.PushReminderIntervalMinutes);
    }

    private static TriviumSchedulingOptions BindFromDict(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<TriviumSchedulingOptions>().BindConfiguration("Scheduling");
        services.AddSingleton<IConfiguration>(config);

        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<TriviumSchedulingOptions>>().Value;
    }
}
