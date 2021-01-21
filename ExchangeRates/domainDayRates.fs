module ExchangeRates.Domain.DayRates

open ExchangeRates.Domain.Types
open ExchangeRates.PersistenceAbstraction
open ExchangeRates.Utils
open ExchangeRates.Utils.Result
open System.Collections.Generic

type DayRates = IDictionary<Symbol, Rate>

let seqContainsOrSkip xs x = 
    xs |> Option.map (Seq.contains x) |> Option.defaultValue true

let getDayRates symbols date rates = 
    query {
        for dr in rates do
        // Not very likely to compile to sql properly
        where (dr.Date = date && seqContainsOrSkip symbols (Symbol dr.Symbol))
        select (Symbol dr.Symbol, Rate dr.Rate)
    }

let ensureNotEmpty d dr = 
    if not (Seq.isEmpty dr) then Success () else Error [ NoDate d ]

let ensureQuerySatisfied query result =
    match query with
    | None -> Success ()
    | Some xs ->
        let seqDiff l r = Set.difference (Set.ofSeq l) (Set.ofSeq r)
        let diff = seqDiff xs result
        if Seq.isEmpty diff then Success () else Error (diff |> List.ofSeq)

let validateSymbols ss dr =
    ensureQuerySatisfied ss (keys dr) |> mapError (List.map NoSymbol)

let validateDayRates ss d dr = 
    (retn dr) *> (ensureNotEmpty d dr) *> (validateSymbols ss dr)

type YesterdayRatesQuery = {
    Symbols: Symbol list option }

let canonicalizeSymbol (Symbol s) = Symbol (s.ToUpper())

let canonicalizeQuerySymbols = Option.map (List.map canonicalizeSymbol)

let canonicalize (q: YesterdayRatesQuery) = 
    { q with Symbols = q.Symbols |> canonicalizeQuerySymbols }
