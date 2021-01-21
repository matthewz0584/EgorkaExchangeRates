namespace ExchangeRates

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

type TimedHostedService<'discriminator>(period: TimeSpan, action: Async<unit>, logger: ILogger<TimedHostedService<'discriminator>>) =
    
    let cancelCycleSrc = new CancellationTokenSource()

    member _.Period = period

    interface IHostedService with
        member _.StartAsync(ct: CancellationToken) = 
            let rec cycleWithDelay() = async {
                do! Async.Sleep (int period.TotalMilliseconds)
                try
                    do! action
                with e -> logger.LogError("Unexpected exception {@exception} during timed operation", e)
                do! cycleWithDelay()
            }

            let linkToCycleCancelation ct = 
                CancellationTokenSource.CreateLinkedTokenSource(cancelCycleSrc.Token, ct).Token

            let t = Async.StartAsTask(action, ?cancellationToken = Some (linkToCycleCancelation ct)) :> Task
            t.ContinueWith(fun _ -> Async.StartImmediate(cycleWithDelay(), linkToCycleCancelation ct) |> ignore) |> ignore
            t

        member _.StopAsync(ct: CancellationToken) =
            cancelCycleSrc.Cancel()
            Task.CompletedTask

    interface IDisposable with
        member _.Dispose(): unit = 
            cancelCycleSrc.Cancel()


type DoBeforeStartService(action: Async<unit>) =
    
    interface IHostedService with
        member _.StartAsync(cancellationToken: CancellationToken) =
            Async.StartAsTask(action, ?cancellationToken = Some cancellationToken) :> Task

        member _.StopAsync(_: CancellationToken) =
            Task.CompletedTask

[<Extension>]
type DoBeforeStart() =

    [<Extension>]
    static member DoBeforeStart (sc: IServiceCollection, action: IServiceProvider -> Async<unit>) =
        sc.AddHostedService<DoBeforeStartService>(fun sp -> sp |> action |> DoBeforeStartService)

    [<Extension>]
    static member DoBeforeStartAndThenRegularly<'discriminator>(sc: IServiceCollection, action: IServiceProvider -> Async<unit>, period: TimeSpan) =
        sc.AddHostedService(fun sp -> new TimedHostedService<'discriminator>(period, action sp, sp.GetService<ILogger<TimedHostedService<'discriminator>>>()))
