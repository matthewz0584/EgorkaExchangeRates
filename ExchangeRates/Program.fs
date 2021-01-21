namespace ExchangeRates

open ExchangeRates.HttpHandlers
open ExchangeRates.Migrations
open ExchangeRatesFetcher
open FluentMigrator.Runner
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open Serilog
open Serilog.Formatting.Json
open System
open Microsoft.Extensions.Options

[<CLIMutable>]
type ExchangeRatesSourceOptions = {
    Uri: string
    AppId: string
}

type Startup(configuration: IConfiguration) =

    let findPastDatesGaps (now: DateTime) dates = 
        let rec findGaps along = function
            | [] -> Seq.empty
            | h :: t when h = Seq.head along -> findGaps (Seq.tail along) t
            | points -> seq {
                yield Seq.head along
                yield! findGaps (Seq.tail along) points
            }

        dates |> List.distinct |> List.sortDescending |> findGaps (Seq.initInfinite (fun i -> now.AddDays -(float i + 1.0)))

    let synchronizeYesterdayRates (logger: ILogger<Startup>) (uriOpts: IOptions<ExchangeRatesSourceOptions>) dbContext = 
        logger.LogInformation "Yesterday rates synchronization: starting..."
        let (yesterday, queryYesterdayRates) = getAllYesterdayRates DateTime.Today
        let decision = Persistence.getRatesData dbContext |> queryYesterdayRates |> synchronizeOrNot yesterday
        logger.LogInformation("Yesterday rates synchronization: decision - {decision}", decision)
        async {
            match decision with
                | Synchronize d -> do! OpenExchangeRates.fetchDateRates uriOpts.Value.Uri uriOpts.Value.AppId d |> Persistence.saveRates dbContext
                | DoNothing -> ()
            logger.LogInformation "Yesterday rates synchronization: finished"
        }

    let synchronizePastRates (logger: ILogger<Startup>) (uriOpts: IOptions<ExchangeRatesSourceOptions>) dbContext = 
        logger.LogInformation "Past rates synchronization: starting..."
        let datesInDb = query {
            for dr in Persistence.getRatesData dbContext do
            select dr.Date
            distinct
        }
        findPastDatesGaps DateTime.Today (new DateTime(2015, 1, 1) :: (List.ofSeq datesInDb)) |> Seq.truncate 1
        |> Seq.map (fun d ->
            async {
                logger.LogInformation("Past rates synchronization: starting synchronizing date {date}...", d)
                let rs = OpenExchangeRates.fetchDateRates uriOpts.Value.Uri uriOpts.Value.AppId d
                logger.LogInformation("Past rates synchronization: rates for {date} fetched", d)
                return rs
            })
        |> Async.Parallel 
        |> Utils.asyncBind (Seq.concat >> Persistence.saveRates dbContext) 
        |> Utils.asyncMap (fun _ -> logger.LogInformation "Past rates synchronization: rates saved to db")

    // On configs publishing https://stackoverflow.com/questions/38178340/how-can-i-ensure-that-appsettings-dev-json-gets-copied-to-the-output-folder/49778593#49778593
    member _.Configuration : IConfiguration = configuration

    member me.ConfigureServices(services: IServiceCollection) =
        let connStr = me.Configuration.GetConnectionString("ExchangeRatesDb") 

        Log.Logger <- LoggerConfiguration()
            .Enrich.FromLogContext()
            .ReadFrom.ConfigurationSection(me.Configuration.GetSection("Logging").GetSection("Serilog"))
            .WriteTo.File(JsonFormatter(renderMessage = true), "Logs\\exchange-rates.json-log", rollingInterval = RollingInterval.Day)
            .WriteTo.File("Logs\\exchange-rates.txt", rollingInterval = RollingInterval.Day)
            .CreateLogger()

        //services.AddEnrichedExceptionLogging()
        //services.AddCorrelationId()

        services
            .AddLogging(fun lb -> lb.AddSerilog(dispose = true) |> ignore)
            .AddGiraffe()
        
        // Db Ensure created - https://github.com/volkanceylan/Serene/blob/master/Serene/Serene.Core/Initialization/DataMigrations.cs#L37
            .AddFluentMigratorCore()
            .ConfigureRunner(fun rb -> 
                rb.AddSqlServer()
                    .WithGlobalConnectionString(connStr)
                    .ScanIn(typeof<InitialMigration>.Assembly).For.Migrations() |> ignore
            )
            .AddLogging(fun lb -> lb.AddFluentMigratorConsole() |> ignore)

            .AddSingleton<IJsonSerializer>(
                let jsonSettings = 
                    JsonSerializerSettings(
                        ContractResolver = CamelCasePropertyNamesContractResolver (), 
                        Converters = [| FifteenBelow.Json.OptionConverter () :> JsonConverter |],
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore)

                NewtonsoftJsonSerializer jsonSettings)
            
            .AddSingleton<Persistence.ExchangeRatesDb.dataContext>(new Func<IServiceProvider, Persistence.ExchangeRatesDb.dataContext>(fun _ -> 
                Persistence.ExchangeRatesDb.GetDataContext(connStr,  FSharp.Data.Sql.SelectOperations.DatabaseSide)))

            .Configure<ExchangeRatesSourceOptions>(me.Configuration.GetSection "ExchangeRatesSource")

            .DoBeforeStart(fun sp -> async {
                use sc = sp.CreateScope()
                sc.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp() })

            .DoBeforeStartAndThenRegularly<{| YesterdaySynch: obj |}>(fun sp -> async {
                do! synchronizeYesterdayRates 
                        (sp.GetService<ILogger<Startup>>()) 
                        (sp.GetService<IOptions<ExchangeRatesSourceOptions>>()) 
                        (sp.GetService<Persistence.ExchangeRatesDb.dataContext>())
            }, TimeSpan.FromMinutes 2.0)

            .DoBeforeStartAndThenRegularly<{| PastSynch: obj |}>(fun sp -> async {
                do! synchronizePastRates
                        (sp.GetService<ILogger<Startup>>())
                        (sp.GetService<IOptions<ExchangeRatesSourceOptions>>())
                        (sp.GetService<Persistence.ExchangeRatesDb.dataContext>())
            }, TimeSpan.FromMinutes 60.0*2.0)

            |> ignore

    member _.Configure(app: IApplicationBuilder, env: IHostEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore
        else
            app.UseGiraffeErrorHandler errorHandler |> ignore

        app.UseHttpsRedirection() |> ignore

        app.UseGiraffe(webApp) |> ignore


module Program =

    let CreateHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun webBuilder ->
                webBuilder.UseStartup<Startup>() |> ignore
            )

    [<EntryPoint>]
    let main args =
        CreateHostBuilder(args).Build().Run()
        0
