using System.Text;
using System.Threading.Tasks.Dataflow;
using Dapr.Actors;
using Dapr.Actors.Client;
using DemoApp;
using Microsoft.AspNetCore.Mvc;

Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", "None");
Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore.Routing.EndpointMiddleware", "None");
Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics", "None");

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddLogging(config => config.AddConsole());
services.AddDaprClient();
services.AddSingleton<ActorProxyFactory>();
services.AddHttpClient();


services.AddActors(config => {
    config.Actors.RegisterActor<SimpleActorWithReminder>();
});

var app = builder.Build();
app.MapActorsHandlers();

app.MapGet("/start/{actorId}", ([FromServices] ActorProxyFactory actorProxyFactory, string actorId) => {
    var actor = actorProxyFactory.CreateActorProxy<ISimpleActorWithReminder>(new ActorId(actorId), nameof(SimpleActorWithReminder));
    return actor.Start();
});

app.MapGet("/stop/{actorId}", ([FromServices] ActorProxyFactory actorProxyFactory, string actorId) => {
    var actor = actorProxyFactory.CreateActorProxy<ISimpleActorWithReminder>(new ActorId(actorId), nameof(SimpleActorWithReminder));
    return actor.Stop();
});

app.MapGet("/test", async ([FromServices] ActorProxyFactory actorProxyFactory, HttpContext context) => {
    context.Response.ContentType = "text/plain";

    using var bodyStream = context.Response.BodyWriter.AsStream();
    var bodyLock = new object();

    lock(bodyLock) {
        bodyStream.Write(Encoding.UTF8.GetBytes("Starting test...\r\n"));
    }

    var ids = Enumerable.Range(0, 1000).Select(x => Guid.NewGuid()).ToList();
    int i = 0;

    var startActionBlock = new ActionBlock<Guid>(async id => {
        var actor = actorProxyFactory.CreateActorProxy<ISimpleActorWithReminder>(new ActorId(id.ToString()), nameof(SimpleActorWithReminder));
        await actor.Start();

        lock(bodyLock) {
            i++;
            bodyStream.Write(Encoding.UTF8.GetBytes($"Started actor {id} ({i})\r\n"));
        }
    }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 20 });

    var stopActionBlock = new ActionBlock<Guid>(async id => {
        var actor = actorProxyFactory.CreateActorProxy<ISimpleActorWithReminder>(new ActorId(id.ToString()), nameof(SimpleActorWithReminder));
        await actor.Stop();

        lock(bodyLock) {
            i++;
            bodyStream.Write(Encoding.UTF8.GetBytes($"Stopped actor {id} ({i})\r\n"));
        }
    }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 20 });

    ids.ForEach(id => startActionBlock.Post(id));
    startActionBlock.Complete();
    await startActionBlock.Completion;

    i = 0;

    ids.ForEach(id => stopActionBlock.Post(id));
    stopActionBlock.Complete();
    await stopActionBlock.Completion;

    lock(bodyLock) {
        bodyStream.Write(Encoding.UTF8.GetBytes("\r\nTest complete"));
    }
});

await app.RunAsync();