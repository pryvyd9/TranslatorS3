namespace SyntaxPredescenceTableParser

    
module Seq =
    open System.Linq
    
    let at index (s:'a seq) = s.ElementAt index
    
    let ofType<'T> collection =
        seq {for c in collection
                do
                    if box c :? 'T
                    then yield box c :?> 'T
        }
    
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
        

module Stack =
    let take count stack =
        match count with
        | x when x > 0 ->
            stack |> List.take (stack.Length - 1 - count)
        | 0 ->
            stack
        | _ ->
            failwith("Count cannot be less than 0.")
            

module private Parse =
  

    let rec findFirstLowerFromEnd table stack =
        match stack with
        | f::s::t when Rel.getRel s f table = [Rel.Lower] ->
            (t |> List.length)
        | _::t ->
            findFirstLowerFromEnd table t
        | _ ->
            0

    let rec simplifyStack stack table nodes =

        if stack |> List.length = 0 
        then stack
        else
           
            let firstLowerIndexFromEnd = findFirstLowerFromEnd table stack
            let elementsToChange = stack |> Stack.take firstLowerIndexFromEnd
        
            let baseElement = BaseSearching.findBase (elementsToChange |> List.rev) nodes
            try
                System.Console.WriteLine((nodes |> List.find (fun x -> x.Id = baseElement.Value)).ToString() )
        
            with _ -> ()
        
        
            match baseElement with
            | Some be ->
                match firstLowerIndexFromEnd with
                | x when x > 0 ->
                    be::(stack |> List.skip firstLowerIndexFromEnd)
                | 0 ->
                    [be]
                //be::(stack |> List.skip firstLowerIndexFromEnd)
            | None ->
                elementsToChange.Head::(simplifyStack elementsToChange.Tail table nodes)
                
    let simplifyStackGreedy stack table nodes =
        let rec internalRec oldStack table nodes =
            let newStack = simplifyStack (stack) table nodes
            if newStack |> List.equals oldStack
            then
                newStack
            else
                internalRec newStack table nodes
        internalRec stack table nodes


    let rec check stack stream table nodes axiom =
        match stack |> List.length, stream |> List.length with
        | 1, 0 ->
            if stack.[0] = axiom
            then {errors = []; countLeft = None}
            else
                let newStack = simplifyStack (stack) table nodes
                if newStack |> List.equals stack 
                then
                    {
                        countLeft = Some 0;
                        errors = [{
                            message = "Axiom was not found";
                            tag = "syntax"; 
                            tokensOnError = Seq.empty
                        }]; 
                    }
                else
                    check newStack [] table nodes axiom
        | _, 0 ->
            let newStack = simplifyStack (stack) table nodes
            if newStack |> List.equals stack 
            then
                {
                    countLeft = Some 0;
                    errors = [{
                        message = "Axiom was not found";
                        tag = "syntax"; 
                        tokensOnError = Seq.empty
                    }]
                }
            else
                check newStack [] table nodes axiom
        | _ ->
            let rel = Rel.getRel stack.Head stream.Head table

            match rel with
            | [Rel.Lower] | [Rel.Equal] ->
                check (stream.Head :: stack) stream.Tail table nodes axiom
            | [Rel.Greater] ->
                let newStack = simplifyStack (stack) table nodes
                
                check (stream.Head :: newStack) stream.Tail table nodes axiom
            | _ ->
                {
                    countLeft = Some stream.Length;
                    errors = [{
                        message = "Undefined relationship";
                        tag = "syntax"; 
                        tokensOnError = Seq.empty
                    }]
                }


module private ParseSimple =

    let findFirstGreater stream table startIndex =
        let rec internalFunc stream table =
            match stream with
            | f :: s :: _  when Rel.getRel (snd f) (snd s) table = [Rel.Greater] ->
                Some f
            | [] ->
                None
            | _ ->
                internalFunc (stream.Tail) table
        internalFunc (stream |> List.skip startIndex) table
            

    let findLastLower stream table startIndex =
        let rec internalFunc stream table =
           match stream with
           | f :: s :: _  when Rel.getRel (snd s) (snd f) table = [Rel.Lower] ->
               Some f
           | [] ->
               None
           | _ ->
               internalFunc (stream.Tail) table
        internalFunc (stream |> List.take (startIndex + 1) |> List.rev) table

    //let simplifyBuffer buffer table fstGreater lstLower =
    //    match fstGreater, lstLower with
    //    | None, Some lstLower ->

    //    | Some fstGreater, None ->

    //    | Some fstGreater, Some lstLower ->

    //    | _ ->



    let check stream table nodes axiom =
            
        let rec intFunc buffer =
            printfn "%A" buffer
            match buffer |> List.length with
            | 1 ->
                let baseElement = BaseSearching.findBaseGreedy (buffer |> List.map (fun x -> snd x)) nodes
                if baseElement.Head = axiom 
                then {errors = []; countLeft = None;}
                else
                    {
                        
                        errors = [{
                            message = "Axiom not found";
                            tag = "syntax";
                            tokensOnError = Seq.empty;
                        }];
                        countLeft = Some 1;
                    }
            | _ ->
                let fstGreater = findFirstGreater buffer table 0

                let fstGreaterIndex = 
                    match fstGreater with
                    | Some fstGreater -> buffer |> List.findIndexOf fstGreater
                    | None -> buffer.Length - 1

                let lstLower = findLastLower buffer table fstGreaterIndex

                let lstLowerIndex =
                    match lstLower with
                    | Some lstLower -> buffer |> List.findIndexOf lstLower
                    | None -> 0

                let count = fstGreaterIndex - lstLowerIndex + 1

                let nodesToChange = buffer |> List.skip lstLowerIndex |> List.take count

                let basic = BaseSearching.findBase (nodesToChange|> List.map (fun x -> snd x)) nodes

                match basic with
                | None ->
                    let message =
                        match fstGreater with
                        | None ->
                            "Unexpected end of file."
                        | Some fstGreater ->
                            let expected = 
                                table.[snd fstGreater].Relashionships.Keys 
                                |> List.ofSeq  
                                |> List.map (fun x -> 
                                    let ns = nodes |> List.tryFind (fun y -> y.Id = x)
                                    match ns with
                                    | None -> ""
                                    | Some s ->
                                        s.ToString()
                                )
                                |> String.concat " , "
                            "Unexpected token. Expected: " + expected

                    {
                        errors = [{
                            message = message;
                            tag = "syntax";
                            tokensOnError = Seq.empty;
                        }];
                        countLeft = Some fstGreaterIndex;
                    }
                | Some basic ->
                    let fstPart = buffer |> List.take lstLowerIndex
                    let lstPart = buffer |> List.skip (fstGreaterIndex + 1)
                    let newBuffer = fstPart @ [fstGreaterIndex, basic] @ lstPart
                    intFunc newBuffer

        let indexedStream = stream |> List.zip [0..stream.Length - 1]
     
        intFunc indexedStream



type SyntaxPredescenceTableParser(
    table:Table,
    tokenSelector:System.Func<Core.IParsedToken seq>, 
    nodes:Core.INodeCollection,
    axiom:Core.IMedium) =

    member val Tokens:Core.IParsedToken seq = Seq.empty with get, set

    member this.Parse() =
        let tokens = tokenSelector.Invoke()

        if tokens = null
        then {errors = []; countLeft = None} :> Core.IParserResult
        else

            let buffer = 
                if tokens |> Seq.length > 0
                then [(tokens |> Seq.head).Id]
                else []



            let stream = 
                tokens 
                |> List.ofSeq 
                //|> List.skip 1
                |> List.choose(fun x -> Some x.Id)

            let mediums = nodes |> Seq.ofType<Core.IMedium> |> List.ofSeq

            // Make internal result type
            // Store original in-stream position 
            // in order to get ParsedToken later

            match ParseSimple.check stream table mediums axiom.Id with
            |{errors=x; countLeft=Some y} as result when x |> Seq.isEmpty ->

                let tokenOnError = 
                    tokens 
                    |> Seq.at ((tokens |> Seq.length) - y)

                let tokenBefore =
                    tokens  
                    |> Seq.at ((tokens |> Seq.length) - y - 1)

                {
                    result with errors = [{
                        message = "Undefined relationship"; 
                        tag = "syntax";
                        tokensOnError = [tokenBefore;tokenOnError] |> Seq.ofList;
                    }]
                }:> Core.IParserResult
            | x ->
                x :> Core.IParserResult

    interface Core.ISyntaxParser with
        member this.Parse():Core.IParserResult = this.Parse()
        member this.Parse():obj = this.Parse() :> obj
        member this.ParsedTokens with set x = this.Tokens <- x
    
    