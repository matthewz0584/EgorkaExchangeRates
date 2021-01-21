module ExchangeRates.DataTransfer.DayRates

open ExchangeRates.Domain.Types
open ExchangeRates.Domain.DayRates
open ExchangeRates.Utils
open ExchangeRates.Utils.Result
open System
open System.Collections.Generic

[<CLIMutable>]
type YesterdayRatesQueryDto = {
    Symbols: string list option }

type DayRatesDataDto = Dictionary<string, decimal>

[<CLIMutable>]
type DayRatesDto = {
    Base: string
    Date: DateTime
    Rates: DayRatesDataDto }

[<CLIMutable>]
type NotFoundDto = {
    Dates: DateTime array
    Symbols: string array }

let validateQuery (q: YesterdayRatesQueryDto) : YesterdayRatesQuery = 
// no real validation yet
    { Symbols = q.Symbols |> Option.map (List.map Symbol) }

let dtoOfDayRates dr = 
    dr |> Seq.map (fun (KeyValue (Symbol s, Rate r)) -> s, r) |> dict |> Dictionary

let composeSuccessDto baseSymbol date drs = {
    Base = baseSymbol
    Date = date
    Rates = dtoOfDayRates drs
}

let composeNotFoundDto des = 
    let (ds, ss) = List.fold (fun (ds, ss) -> function
        | NoDate d -> (d :: ds, ss)
        | NoSymbol s -> (ds, s :: ss)) ([], []) des
    {
        Dates = ds |> Array.ofList
        Symbols = ss |> List.map (fun (Symbol s) -> s) |> Array.ofList
    }

let composeDayRatesDto baseSymbol date = either (composeSuccessDto baseSymbol date) composeNotFoundDto

let buildYesterdayRatesDbQuery (qDto: YesterdayRatesQueryDto) (today: DateTime) = 
    let q = qDto |> optionOfObj2 |> Option.defaultValue ({ Symbols = None }) |> validateQuery |> canonicalize
    let yesterday = today.AddDays -1.0
    (q, yesterday, getDayRates q.Symbols yesterday)

let buildDayRatesResponse (q: YesterdayRatesQuery) (date: DateTime) = 
    dict >> validateDayRates q.Symbols date >> composeDayRatesDto "USD" date
