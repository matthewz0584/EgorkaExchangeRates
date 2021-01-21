module ExchangeRates.PersistenceAbstraction

open System
open System.Linq

type DateRate = {
    Date: DateTime
    Symbol: string
    Rate: decimal }

type RatesTable = IQueryable<DateRate>
