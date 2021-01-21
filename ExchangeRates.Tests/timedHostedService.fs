module ExchangeRates.Tests.TimedHostedService

open ExchangeRates
open FSharp.Control.Tasks.ContextInsensitive
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open NSubstitute
open System
open System.Threading
open System.Threading.Tasks
open Xunit

[<Fact>] 
let ``Start makes action execute immediately`` () = task {
    let action = Substitute.For<Action>()
    use ths = new TimedHostedService<obj>(TimeSpan.FromDays 1.0, async { action.Invoke() }, Substitute.For<ILogger<TimedHostedService<obj>>>())
    do! (ths :> IHostedService).StartAsync CancellationToken.None
    do! Task.Delay (TimeSpan.FromSeconds 1.0)
    action.ReceivedWithAnyArgs().Invoke()
}

[<Fact>] 
let ``Start cancelation prevents both initial action and consequent timed operations`` () = task {
    let action = Substitute.For<Action>()
    use ths = new TimedHostedService<obj>(TimeSpan.FromMilliseconds 200.0, async { action.Invoke() }, Substitute.For<ILogger<TimedHostedService<obj>>>())
    let ts = new CancellationTokenSource()
    ts.Cancel()
    let! _ = Assert.ThrowsAsync<TaskCanceledException>(fun _ -> (ths :> IHostedService).StartAsync ts.Token)
    do! Task.Delay (TimeSpan.FromMilliseconds 300.0)
    action.DidNotReceiveWithAnyArgs().Invoke()
}

[<Fact>]
let ``Start makes action execute every period`` () = task {
    let action = Substitute.For<Action>()
    use ths = new TimedHostedService<obj>(TimeSpan.FromMilliseconds 200.0, async { action.Invoke() }, Substitute.For<ILogger<TimedHostedService<obj>>>())
    do! (ths :> IHostedService).StartAsync CancellationToken.None
    do! Task.Delay (TimeSpan.FromMilliseconds 500.0)
    action.ReceivedWithAnyArgs(2 + 1).Invoke()
}

[<Fact>]
let ``Stop makes action execution stop`` () = task {
    let action = Substitute.For<Action>()
    use ths = new TimedHostedService<obj>(TimeSpan.FromMilliseconds 200.0, async { action.Invoke() }, Substitute.For<ILogger<TimedHostedService<obj>>>())
    do! (ths :> IHostedService).StartAsync CancellationToken.None
    do! (ths :> IHostedService).StopAsync CancellationToken.None
    do! Task.Delay (TimeSpan.FromMilliseconds 500.0)
    action.ReceivedWithAnyArgs(1).Invoke()
}

[<Fact>]
let ``Async action executes all of its steps`` () = task {
    let action = Substitute.For<Action>()
    use ths = new TimedHostedService<obj>(TimeSpan.FromMilliseconds 200.0, async {
        action.Invoke()
        do! Async.Sleep 50
        action.Invoke()
    }, Substitute.For<ILogger<TimedHostedService<obj>>>())
    do! (ths :> IHostedService).StartAsync CancellationToken.None
    do! Task.Delay (TimeSpan.FromMilliseconds 550.0)
    do! (ths :> IHostedService).StopAsync CancellationToken.None
    action.ReceivedWithAnyArgs(4 + 2).Invoke()
}

[<Fact>] 
let ``Timed operation logs its exceptions `` () = task {
    let logger = Substitute.For<ILogger<TimedHostedService<obj>>>()
    use ths = new TimedHostedService<obj>(TimeSpan.FromMilliseconds 100.0, async { failwith "plop" }, logger)
    let! _ = Assert.ThrowsAsync<Exception>(fun _ -> (ths :> IHostedService).StartAsync CancellationToken.None) 
    do! Task.Delay (TimeSpan.FromMilliseconds 250.0)
    //logger.Received(2).LogError(Arg.Is<string>(fun (s: string) -> s.StartsWith "Unexpected exception System.Exception: plop"), Arg.Any())
    logger.ReceivedWithAnyArgs(2).LogError(null)
}
