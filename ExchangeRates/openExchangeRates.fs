module OpenExchangeRates

open FSharp.Data
open ExchangeRates.PersistenceAbstraction
open System

type OpenExchangeRates = JsonProvider<"../docs/openexchange_latest_sample.json">

let formatRatesSourceUri (uri: string) appId (d: DateTime) = 
    String.Format(uri, (d.ToString "yyyy-MM-dd"), appId)

let extractRatesFromJson (opExRates: OpenExchangeRates.Root) = 
    // investigate time zones
    //opExRates.Rates.JsonValue.Properties() |> Seq.map (fun (k, v) -> (k, (opExRates.Timestamp |> int64 |> DateTimeOffset.FromUnixTimeSeconds).Date, v.AsDecimal()))
    opExRates.Rates.JsonValue.Properties() |> Seq.map (fun (k, v) -> (k, v.AsDecimal()))

let toDateRates date =
    Seq.map (fun (symbol, rate) -> {Date = date; Symbol = symbol; Rate = rate })

let fetchDateRates uri appId d = 
    formatRatesSourceUri uri appId d |> OpenExchangeRates.Load |> extractRatesFromJson |> toDateRates d
