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

