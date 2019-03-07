module internal BaseSearching

open Extentions

type Medium = {id:int; cases:int list seq}
    
let contains (element:Core.INode) (root:Core.INode) =
    let mutable checkedNodes = [root]
    let rec inFun (element:Core.INode) (root:Core.INode) =
        if element.Id = root.Id
        then true
        else
            if root :? Core.IMedium
            then
                let root = root :?> Core.IMedium

                let mutable result = false

                for case in root.Cases
                    do
                        if not result
                        then
                            for node in case
                                do
                                    if not result
                                    then
                                        if checkedNodes |> List.contains node |> not
                                        then 
                                            checkedNodes <- node::checkedNodes
                                            result <- inFun element node

                result
            else
                false
    inFun element root


let findBase (collection:int list) (mediums:Core.IMedium list) =
    let cases:Medium list =
        mediums 
        |> List.ofSeq 
        |> List.map (fun x -> 
            {
                id=x.Id;
                cases= x.Cases |> Seq.map (fun y -> y |> Seq.map (fun z -> z.Id) |> List.ofSeq);
            })

    let baseElement = 
        cases 
        |> Seq.tryFind (fun x -> 
            x.cases 
            |> Seq.exists (fun y -> 
                y 
                |> List.equals collection))

    match baseElement with
    | Some s -> 
        Some s.id
    | None ->
        None

let findBaseGreedy (collection:int list) (mediums:Core.IMedium list) =
    let rec inFunc collection mediums =
        match findBase collection mediums with
        | None ->
            collection
        | Some s ->
            inFunc [s] mediums
    inFunc collection mediums