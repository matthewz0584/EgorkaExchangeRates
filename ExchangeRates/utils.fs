module ExchangeRates.Utils

open System.Collections.Generic

type Result<'TS, 'TE> =
    | Success of 'TS
    | Error of 'TE

module Result =
    let cata fs fe = function
        | Success s -> fs s
        | Error e -> fe e

    let either fs fe = cata (fs >> Success) (fe >> Error)

    let map f = either f id

    let mapError f = either id f

    let bind f = cata f Error

    let retn = Success

    let apply fr rr =
        match fr, rr with
        | Success f, Success r -> retn (f r)
        | Success _, Error e | Error e, Success _ -> Error e
        | Error e1, Error e2 -> Error (e1 @ e2)

    let (<*>) = apply
    let ( *> ) l r = retn (fun l r -> l) <*> l <*> r 
    let ( <* ) l r = retn (fun l r -> r) <*> l <*> r 
    let (>>=) = bind

    let extract r = cata id id r

    let extractObj r = cata box box r


let asyncMap f a = async {
    let! r = a
    return f r }

let asyncBind f a = async {
    let! r = a
    return! f r }

let optionOfObj2 n = if isNull (box n) then None else Some n


let keys (dict: IDictionary<'Key, 'Value>) = dict.Keys

let values (dict: IDictionary<'Key, 'Value>) = dict.Values

let (|KeyValue|) (x: KeyValuePair<'a,'b>) = x.Key, x.Value


let listSequenceResultA rs =
    let cons h t = h :: t

    let retn = Result.retn
    let (<*>) = Result.apply

    let folder x s = retn cons <*> x <*> s

    List.foldBack folder rs (retn [])