module ExchangeRates.HttpHandlers

open ExchangeRates.DataTransfer.DayRates
open ExchangeRates.DataTransfer.HistoricalRates
open ExchangeRates.Persistence
open ExchangeRates.Utils
open ExchangeRates.Utils.Result
open FSharp.Control.Tasks.ContextInsensitive
open FSharp.Data.Sql
open Giraffe
open Giraffe.GoodRead
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open System

let bindJsonAndHandleError<'T> f he: HttpHandler = 
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        try return! bindJson<'T> f next ctx 
        with :? Newtonsoft.Json.JsonReaderException as e -> return! (he e) next ctx 
    }

let serveDayRates dbContext qDto = 
    let (q, yesterday, applyQuery) = buildYesterdayRatesDbQuery qDto DateTime.Today
    getRatesData dbContext |> applyQuery |> Seq.executeQueryAsync |> asyncMap (buildDayRatesResponse q yesterday)

let yesterdayRatesHandler (logger: ILogger) dbContext: HttpHandler =
    bindJsonAndHandleError<YesterdayRatesQueryDto>
        (fun qDto ->
            fun (next : HttpFunc) (ctx : HttpContext) -> task {
                logger.LogInformation("Yesterday rates queried with filter {@filter} requested", qDto)
                let! bodyR = serveDayRates dbContext qDto
                logger.LogDebug("Yesterday rates responsed with {@response}", bodyR)
                let h = cata (json >> Successful.ok) (json >> RequestErrors.notFound) bodyR
                return! h next ctx 
            })
        (fun e -> 
            (text e.Message) |> RequestErrors.badRequest)

let serveHistoricalRates dbContext qDto = 
    let (q, applyQuery) = buildHistoricalRatesDbQuery qDto
    getRatesData dbContext |> applyQuery |> Seq.executeQueryAsync |> asyncMap (buildHistoricalRatesResponse q)

let historicalRatesHandler (logger: ILogger) dbContext: HttpHandler =
    bindJsonAndHandleError<HistoricalRatesQueryDto>
        (fun qDto ->
            fun (next : HttpFunc) (ctx : HttpContext) -> task {
                logger.LogInformation("Historical rates queried with filter {@filter} requested", qDto)
                let! bodyR = serveHistoricalRates dbContext qDto
                logger.LogDebug("Historical rates responsed with {@response}", bodyR)
                let h = cata (json >> Successful.ok) (json >> RequestErrors.notFound) bodyR
                return! h next ctx 
            })
        (fun e -> 
            (text e.Message) |> RequestErrors.badRequest)

let webApp: HttpHandler =
    choose [
        GET >=>
            choose [
                route "/" >=> redirectTo true "/rates/yesterday"
                route "/rates/yesterday" >=> Require.services<ILogger<HttpHandler>, ExchangeRatesDb.dataContext>(yesterdayRatesHandler)
                route "/rates/historical" >=> Require.services<ILogger<HttpHandler>, ExchangeRatesDb.dataContext>(historicalRatesHandler)
            ]
        setStatusCode 404 >=> text "Not Found" ]

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message
