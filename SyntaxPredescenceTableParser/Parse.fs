namespace SyntaxPredescenceTableParser

    
module Seq =
    open System.Linq
    
    let at index (s:'a seq) = s.ElementAt index
    
    //let ofType<'T> collection =
    //    seq {for c in collection
    //            do
    //                if c.GetType().IsAssignableFrom(typeof<'T>)
    //                then
    //                    yield c
    //    }

    
    let ofType<'T> collection =
        seq {for c in collection
                do
                    if box c :? 'T
                    then yield box c :?> 'T
        }
    
module List =
    let findIndexBackOf (element:'a) (collection:'a list) =
        collection |> List.findIndexBack(fun x -> x = element)


module private BaseSearching =
    type Medium = {id:int; cases:int list seq}


    let findBase (collection:int list) (mediums:Core.IMedium list) =
        let cases:Medium list =
            mediums 
            |> List.ofSeq 
            |> List.map (fun x -> 
                {
                    id=x.Id;
                    cases= x.Cases |> Seq.map (fun y -> y |> Seq.map (fun z -> z.Id) |> List.ofSeq);
                })
                
        let rec compareLists l1 l2 =
            match l1, l2 with
            | h1::t1, h2::t2 when h1 = h2 -> compareLists t1 t2
            | [], [] -> true
            | _ -> false

        let baseElement = 
            cases 
            |> Seq.tryFind (fun x -> 
                x.cases 
                |> Seq.exists (fun y -> 
                    y 
                    |> compareLists collection))

        match baseElement with
        | Some s -> 
            Some s.id
        | None ->
            None


module private Parse =
    let (|Tail|_|) count collection =
        let length = collection |> Seq.length

        if length < count 
        then
            None
        else
            collection |> List.skip (length - count) |> Some

    let rec findLastLower buffer table =
        match buffer with
        | Tail 2 [first;second] ->
            match Rel.getRel first second table with
            | [Rel.Lower] ->
                first
            | _ ->
                let length = buffer.Length
                findLastLower (buffer |> List.take (length - 1)) table
        | _ -> 
            buffer.[0]
       

    let rec simplifyBuffer buffer table nodes =
        let lastLower = findLastLower buffer table
        let index = buffer |> List.findIndexBackOf lastLower
        let headElements = buffer |> List.skip index
        //let elementsToFindBase = headElements@[stream |> List.head]
        let elementsToFindBase = headElements
        let baseElement = BaseSearching.findBase elementsToFindBase nodes

        match baseElement with
        | Some s ->
            simplifyBuffer (buffer |> List.take index |> (@) [s]) table nodes
        | _ ->
            buffer

    let rec check buffer stream table nodes =
        match buffer |> List.length, stream |> List.length with
        | 1, 0 ->
            {errors = []; countLeft = None}
        | x, 0 when x > 1 ->
            
            {
                countLeft = Some 0;
                errors = [{
                    new Core.IParserError with
                        member __.Message = sprintf "Axiom was not found" 
                        member __.Tag = "syntax"
                        member __.TokensOnError = Seq.empty
                }]
            }
        | _ ->
            let rel = Rel.getRel (buffer |> Seq.last) (stream |> Seq.head) table
            match rel with
            | [Rel.Lower] | [Rel.Equal] ->

                match stream with
                | h::t -> 
                    check (buffer@[h]) t table nodes
                | _ -> 
                    {errors = []; countLeft = None}

            | [Rel.Greater] ->
              
                let newBuffer = simplifyBuffer buffer table nodes
                //let appendedNewBuffer = simplifyBuffer (newBuffer@[stream|>List.head]) table nodes
                //check appendedNewBuffer (stream |> List.tail) table nodes
                check newBuffer (stream |> List.tail) table nodes

            | _ ->
                {
                    countLeft = Some stream.Length;
                    errors = [{
                        new Core.IParserError with
                            member __.Message = sprintf "Undefined relationship" 
                            member __.Tag = "syntax"
                            member __.TokensOnError = Seq.empty
                    }]
                }





type SyntaxPredescenceTableParser(table:Table, tokenSelector:System.Func<Core.IParsedToken seq>, nodes:Core.INodeCollection) =
//type SyntaxPredescenceTableParser(table:Table, tokenSelector:(unit -> Core.IParsedToken seq), nodes:Core.INodeCollection) =

    member val Tokens:Core.IParsedToken seq = Seq.empty with get, set

    member this.Parse() =
        let tokens = tokenSelector.Invoke()

        let buffer = [(tokens |> Seq.head).Id]


        let stream = 
            tokens 
            |> List.ofSeq 
            |> List.skip 1
            |> List.choose(fun x -> Some x.Id)

        let mediums = nodes |> Seq.ofType<Core.IMedium> |> List.ofSeq

        // Make internal result type
        // Store original in-stream position 
        // in order to get ParsedToken later

        match Parse.check buffer stream table mediums with
        |{errors=x; countLeft=Some y} when x |> Seq.isEmpty ->
            let tokenOnError = 
                tokens 
                |> Seq.at ((tokens |> Seq.length) - y)
            let tokenBefore =
                tokens  
                |> Seq.at ((tokens |> Seq.length) - y - 1)

            {
                countLeft = Some y;
                errors = [{
                    new Core.IParserError with
                        member __.Message = sprintf "Undefined relationship" 
                        member __.Tag = "syntax"
                        member __.TokensOnError = [tokenBefore;tokenOnError] |> Seq.ofList
                }] 
            } :> Core.IParserResult
        | x ->
            x :> Core.IParserResult

    interface Core.ISyntaxParser with
        member this.Parse():Core.IParserResult = this.Parse()
        member this.Parse():obj = this.Parse() :> obj
        member this.ParsedTokens with set x = this.Tokens <- x
    

//type private Rel = Core.Relationship


////type PredescenceTable = {nodes:IDictionary<int, Core.IPredescenceNode>} with
////                            member this.DistinguishRelashionship relashionship:IEnumerable<Rel> =
////                                match relashionship with
////                                | Rel.Undefined -> Seq.empty
////                                |_ ->
////                                    (System.Enum.GetValues(typeof<Rel>) :?> array<Rel>)
////                                        .Select(fun x -> x &&& relashionship)
////                                        .Where(fun x-> x <> Rel.Undefined)
////                            member this.GetRelashionship left right =
////                                if not <| this.nodes.ContainsKey(left) ||
////                                    not <| this.nodes.[left].Relashionships.ContainsKey(right)
////                                then Rel.Undefined
////                                else
////                                    this.nodes.[left].Relashionships.[right]

//type Nodes = IDictionary<int, Core.IPredescenceNode>
//type Errors = IEnumerable<Core.IParserError>
//type PredescenceTable = {nodes:Nodes}

//module PredescenceTable =

//    let distinguishRelationship relationship:IEnumerable<Rel> =
//        match relationship with
//        | Rel.Undefined -> Seq.empty
//        |_ ->
//            (System.Enum.GetValues(typeof<Rel>) :?> array<Rel>)
//                .Select(fun x -> x &&& relationship)
//                .Where(fun x-> x <> Rel.Undefined)

//    let getRelationship left right (nodes:Nodes) =
//        if not <| nodes.ContainsKey(left) ||
//            not <| nodes.[left].Relashionships.ContainsKey(right)
//        then Rel.Undefined
//        else
//            nodes.[left].Relashionships.[right]
                                
//    let getRel left right table =
//        getRelationship left right table
//        |> distinguishRelationship 
//        |> List.ofSeq

//type SyntaxPredescenceTableParserResult = {errors:Errors} with
//                                                interface Core.IParserResult with
//                                                    member this.Errors = this.errors

//module Parse =

//    let ( @@ ) collection element = collection@[element]

//    let rec findLastLowerIndex (buffer:int list) (table:Nodes) =
//        let last = buffer |> List.last
//        let previous = (buffer |> List.rev).[0]
//        let relationship = PredescenceTable.getRel previous last table
//        if relationship = [Rel.Lower]
//        then 
//            buffer |> List.findIndexBack (fun x -> x = previous)
//        else
//            findLastLowerIndex (buffer |> List.take (buffer.Length - 1)) table

//    let findReplacement (buffer:IEnumerable<Core.IParsedToken>) (axiom:Core.IMedium):Core.IParsedToken =
//        let rec compareLists l1 l2 =
//            match l1, l2 with
//            | h1::t1, h2::t2 when h1 = h2 -> compareLists t1 t2
//            | [], [] -> true
//            | _ -> false
            

//        let id (x:IEnumerable<Core.INode>) = x.Select(fun y -> y.Id) |> List.ofSeq
//        let b = buffer.Select(fun z -> z.Id) |> List.ofSeq
//        let cmp = id >> compareLists b
//        match axiom.Cases.Any(fun x -> cmp x) with
//        | true ->
//            axiom.Cases.First(fun x -> cmp x)
//        | _ -> 
//        rhen 


//    let rec check (buffer:int list) (tokens:Core.IParsedToken list) table axiom =
//        match tokens with
//        | head::tail ->
//            match buffer with
//            | [] ->
//                check [head.Id] tail table axiom
//            | _ -> 
//                let last = buffer |> List.last
//                let relationships = PredescenceTable.getRel head.Id last table
//                match relationships with
//                | [] | [Rel.Undefined] ->
//                    {
//                        errors = [
//                            {
//                                new Core.IParserError with
//                                    member __.Message = sprintf "Undefined relationship between %i and %i" last head.Id
//                                    member __.Tag = "syntax"
//                                    member __.TokensOnError = Seq.empty
//                            }
//                        ] |> Seq.ofList 
//                    }
//                | [Rel.Lower] | [Rel.Equal] ->
//                    check (buffer@@head.Id) tail table axiom
//                | [Rel.Greater] ->
//                    let lastLowerIndex = buffer |> findLastLowerIndex table
//                    let newBuffer = (buffer |> List.take lastLowerIndex)
//                                        @@ ( findReplacement (buffer |> List.skip lastLowerIndex) axiom)

//                    ()

//            //let relationships = PredescenceTable.getRel first.Id second.Id table
//            //match relationships with
//            //| [Rel.Undefined] | [] -> 
//            //    {
//            //        errors = [
//            //            {
//            //                new Core.IParserError with
//            //                    member __.Message = "Undefined relationship"
//            //                    member __.Tag = "syntax"
//            //                    member __.TokensOnError = [first;second] |> Seq.ofList
//            //            }
//            //        ] |> Seq.ofList 
//            //    }
//            //| [Rel.Equal] | [Rel.Lower] ->
//            //    check (buffer@[])

//            //| _ -> 
//            //    check (second::tail) table



//type SyntaxPredescenceTableParser(table, tokens, axiom:Core.IMedium) =
//    member this.Parse() =
//        Parse.check [] (tokens |> List.ofSeq) table axiom
//        //for token in tokens
//        //    do
        
//        failwith("not implemented")
//    interface Core.IParser<SyntaxPredescenceTableParserResult> with
//        member this.Parse():SyntaxPredescenceTableParserResult = this.Parse()
//        member this.Parse():obj = this.Parse() :> obj

//type Class1 = {errors:IEnumerable<Core.IParserError>; nodes:IDictionary<int, Core.IPredescenceNode>}
//    member this.X = "F#"

