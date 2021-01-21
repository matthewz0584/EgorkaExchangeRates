module ExchangeRates.DataTransfer.HistoricalRates

open ExchangeRates.Domain.Types
open ExchangeRates.Domain.HistoricalRates
open ExchangeRates.Utils
open ExchangeRates.Utils.Result
open System
open System.Collections.Generic

[<CLIMutable>]
type HistoricalRatesQueryDto = {
    Symbols: string list option
    Dates: DateTime list option }

type HistoricalRatesDataDto = Dictionary<DateTime, DayRates.DayRatesDataDto>

[<CLIMutable>]
type HistoricalRatesDto = {
    Base: string
    Rates: HistoricalRatesDataDto }

let validateQuery (q: HistoricalRatesQueryDto) : HistoricalRatesQuery = {
    // no real validation yet
    Symbols = q.Symbols |> Option.map (List.map Symbol) 
    Dates = q.Dates }

let composeSuccessDto baseSymbol hr = {
    Base = baseSymbol
    Rates = hr |> Seq.map (fun (KeyValue (d, dr)) -> d, DayRates.dtoOfDayRates dr) |> dict |> Dictionary
}

let composeHistoricalRatesDto baseSymbol = either (composeSuccessDto baseSymbol) DayRates.composeNotFoundDto

let buildHistoricalRatesDbQuery (qDto: HistoricalRatesQueryDto) = 
    let q = qDto |> optionOfObj2 |> Option.defaultValue ({ Symbols = None; Dates = None }) |> validateQuery |> canonicalize
    (q, getHistoricalRates q.Symbols q.Dates)

let buildHistoricalRatesResponse (q: HistoricalRatesQuery) = 
    Seq.map (fun (d, drs) -> d, dict drs) >> dict >> validateHistoricalRates q.Symbols q.Dates >> composeHistoricalRatesDto "USD"

