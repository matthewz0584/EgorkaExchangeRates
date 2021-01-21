module ExchangeRates.Domain.Types
open System

type Symbol = Symbol of string
// validation for 3 char strings
// validation for strings from set?
// does canonicalization go before or after validation?

type Rate = Rate of decimal
// must be positive

type SymbolRate = {
    Symbol: Symbol
    Rate: Rate
}

type DataError = 
    | NoDate of DateTime
    | NoSymbol of Symbol
