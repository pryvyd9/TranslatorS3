module internal SyntaxPredescenceTableParser.Rel

open System.Linq
let private distinguishRelationship relationship:Rel seq =
    match relationship with
    | Rel.Undefined -> Seq.empty
    |_ ->
        (System.Enum.GetValues(typeof<Rel>) :?> array<Rel>)
            .Select(fun x -> x &&& relationship)
            .Where(fun x-> x <> Rel.Undefined)
    
let private getRelationship left right (table:Table) =
    if table.ContainsKey(left) |> not ||
        table.[left].Relashionships.ContainsKey(right) |> not
    then Rel.Undefined
    else
        table.[left].Relashionships.[right]
                                    
let internal getRel left right table =
    getRelationship left right table
    |> distinguishRelationship 
    |> List.ofSeq
