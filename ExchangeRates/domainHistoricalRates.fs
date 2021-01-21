module ExchangeRates.Domain.HistoricalRates

open ExchangeRates.Domain.Types
open ExchangeRates.PersistenceAbstraction
open ExchangeRates.Utils
open ExchangeRates.Utils.Result
open System
open System.Collections.Generic

type HistoricalRates = IDictionary<DateTime, DayRates.DayRates>

let getHistoricalRates symbols dates rates = 
    query {
        for dr in rates do
        // Not very likely to compile to sql properly, need to reimplement in more sql friendly way
        where (DayRates.seqContainsOrSkip dates dr.Date && DayRates.seqContainsOrSkip symbols (Symbol dr.Symbol))
        groupBy dr.Date into g
        select (g.Key, g |> Seq.map (fun {Symbol = s; Rate = r} -> Symbol s, Rate r))
    }

let validateDates ds hr =
    DayRates.ensureQuerySatisfied ds (keys hr) |> mapError (List.map NoDate)

let validateSymbols ss hr =
    hr |> values |> List.ofSeq |> List.map (DayRates.validateSymbols ss) 
    |> listSequenceResultA |> mapError (Set.ofSeq >> List.ofSeq) |> map ignore

let validateHistoricalRates ss ds hr = 
    (retn hr) *> (validateDates ds hr) *> (validateSymbols ss hr)

type HistoricalRatesQuery = {
    Dates: DateTime list option
    Symbols: Symbol list option }

let canonicalizeDate (d: DateTime) = d.Date

let canonicalizeQueryDates = Option.map (List.map canonicalizeDate)

let canonicalize (q: HistoricalRatesQuery) = {
    Symbols = q.Symbols |> DayRates.canonicalizeQuerySymbols
    Dates = q.Dates |> canonicalizeQueryDates }
