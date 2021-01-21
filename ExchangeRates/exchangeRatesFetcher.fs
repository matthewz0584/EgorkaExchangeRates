module ExchangeRates.ExchangeRatesFetcher

open System
open ExchangeRates.Domain.DayRates

type SynchronizationDecision = | Synchronize of DateTime | DoNothing

let getAllYesterdayRates (today: DateTime) = 
    let yesterday = today.AddDays -1.0
    (yesterday, getDayRates None yesterday)

let synchronizeOrNot (date: DateTime) yesterdayRates = 
    if (Seq.isEmpty yesterdayRates) then Synchronize date else DoNothing
