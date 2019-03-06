module internal Extentions

[<RequireQualifiedAccess>]
module Seq =
    let ofType<'T> collection =
        seq {for c in collection
                do
                    if box c :? 'T
                    then yield box c :?> 'T
        }
    
[<RequireQualifiedAccess>]
module List =
    let findIndexBackOf (element:'a) (collection:'a list) =
        collection |> List.findIndexBack(fun x -> x = element)

    let findIndexOf (element:'a) (collection:'a list) =
        collection |> List.findIndex(fun x -> x = element)

    let rec equals l1 l2 =
        match l1, l2 with
        | h1::t1, h2::t2 when h1 = h2 -> equals t1 t2
        | [], [] -> true
        | _ -> false

    let ofType<'T> collection =
        [
            for c in collection do
                    if box c :? 'T
                    then yield box c :?> 'T
        ]

    //let ranges (func:'a->bool) (collection:'a list) =
    //    let mutable l = []
    //    let mutable temp = []

    //    for i in collection
    //        do
    //            if func i
    //            then
    //                temp <- temp @ [i]
    //            else
    //                l <- l @ [temp]
    //                temp <- []

    //    l

    
    let ranges (func:'a->'b option) (collection:'a list) =
        let mutable l = []
        let mutable temp = []

        for i in collection
            do
                match func i with
                | Some i ->
                    temp <- temp @ [i]
                | _ ->
                    l <- l @ [temp]
                    temp <- []
               
        if l.Length = 0
        then [temp]
        else l
