module SyntaxPredescenceTableParser.Poliz

open Extentions

let ID = 45
let mutable processed = []


let mutable polizList = []

//let finalize() =
//    processed <- processed @ [polizList]
//    polizList <- []

//let poliz (idsToChange) (nodes:Core.INode list) =

//    let groups = 
//        idsToChange |> List.ranges (fun x -> nodes |> List.tryFind (fun y -> y.Id = snd x && (y :? Core.ITerminal || y :? Core.IDefinedToken)))


//    let root = nodes |> List.find (fun x -> x.Id = ID)

//    for group in groups 
//        do

    
//            if group |> List.forall ( fun x -> BaseSearching.contains x root)
//            then
//                if group.Length = 1
//                then
//                    polizList <- polizList @ group
//                else
//                    ()
//                // do the job
//            else
//                processed <- processed @ [polizList]
//                printfn "%A" polizList
//                polizList <- []
                //()
            
let poliz (idsToChange) (nodes:Core.INode list) =

    
    let root = nodes |> List.find (fun x -> x.Id = ID)
    let tokens = 
        idsToChange 
        |> List.choose (fun x -> nodes |> List.tryFind (fun y -> y.Id = snd x && (y :? Core.ITerminal || y :? Core.IDefinedToken)))

    if tokens |> List.forall ( fun x -> BaseSearching.contains x root)
    then
        if tokens.Length = 1
        then
            polizList <- polizList @ tokens
    else
        processed <- processed @ [polizList]
        printfn "%A" polizList
        polizList <- []
