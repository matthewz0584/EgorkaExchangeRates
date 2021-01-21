module ExchangeRates.Persistence

open ExchangeRates.PersistenceAbstraction
open FSharp.Data.Sql
open System

let getTestRatesData () = 
    [   { Date = DateTime.Today; Symbol = "USD"; Rate = 1M };
        { Date = DateTime.Today.AddDays(-1.0); Symbol = "USD"; Rate = 1M };
        { Date = DateTime.Today.AddDays(-2.0); Symbol = "USD"; Rate = 1M };
        { Date = DateTime.Today; Symbol = "EUR"; Rate = 2M };
        { Date = DateTime.Today.AddDays(-1.0); Symbol = "EUR"; Rate = 1.5M };
        { Date = DateTime.Today.AddDays(-2.0); Symbol = "EUR"; Rate = 2M };
        { Date = DateTime.Today; Symbol = "RUB"; Rate = 1M/70M };
        { Date = DateTime.Today.AddDays(-1.0); Symbol = "RUB"; Rate = 1M/60M };
        { Date = DateTime.Today.AddDays(-2.0); Symbol = "RUB"; Rate = 1M/50M } ] |> System.Linq.Queryable.AsQueryable

type ExchangeRatesDb = SqlDataProvider<Common.DatabaseProviderTypes.MSSQLSERVER, @"Server=(localdb)\mssqllocaldb;Database=ExchangeRates;Trusted_Connection=True">

let getRatesData (ctx: ExchangeRatesDb.dataContext) =
    query {
        for er in ctx.Dbo.ExchangeRates do
        select { Date = er.Date; Symbol = er.CurrencyCode; Rate = er.Rate }
    }

let saveRates (ctx: ExchangeRatesDb.dataContext) rates =
    rates |> List.ofSeq |> List.map (fun {Date = d; Symbol = s; Rate = r} -> ctx.Dbo.ExchangeRates.``Create(CurrencyCode, Date, Rate)``(s, d, r)) |> ignore
    ctx.SubmitUpdatesAsync() 
