using Dapr.Actors;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.Logging;
using Polly;

namespace DemoApp;

public class SimpleActorWithReminder : Actor, ISimpleActorWithReminder, IRemindable
{
    private readonly ILogger<SimpleActorWithReminder> logger;

    public SimpleActorWithReminder(ActorHost host, ILogger<SimpleActorWithReminder> logger) : base(host)
    {
        this.logger = logger;
    }

    public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        logger.LogInformation("Received reminder: {reminderName} for actor id: {actorId}", reminderName, Id);
        return Task.CompletedTask;
    }

    public Task Start()
    {
        return Policy.Handle<Exception>().WaitAndRetryForeverAsync(x => TimeSpan.FromMilliseconds(100)).ExecuteAsync(() =>
            RegisterReminderAsync("a-reminder", null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)));
    }

    public Task Stop()
    {
        return Policy.Handle<Exception>().WaitAndRetryForeverAsync(x => TimeSpan.FromMilliseconds(100)).ExecuteAsync(() =>
            UnregisterReminderAsync("a-reminder"));
    }
}

public interface ISimpleActorWithReminder : IActor
{
    Task Start();

    Task Stop();
}