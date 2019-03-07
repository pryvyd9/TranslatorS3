module internal SyntaxPredescenceTableParser.Rel

let private distinguishRelationship relationship:Rel seq =
    match relationship with
    | Rel.Undefined -> Seq.empty
    |_ ->
        System.Enum.GetValues(typeof<Rel>) :?> array<Rel>
        |> Array.map (fun x -> x &&& relationship)
        |> Array.filter (fun x -> x <> Rel.Undefined)
        |> Seq.ofArray
    
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
