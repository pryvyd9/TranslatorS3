namespace SyntaxPredescenceTableParser

    
module Seq =
    open System.Linq
    
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

    let ofType<'T> collection =
        [
            for c in collection do
                    if box c :? 'T
                    then yield box c :?> 'T
        ]


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
        


module private ParseSimple =

    type RelRes<'T> = 
        | Found of 'T
        | Undefined of 'T
        | NotFound

    let findFirstGreater stream table startIndex =
           let rec internalFunc stream table =
               match stream with
               | f :: s :: _ ->
                   let rel = Rel.getRel (snd f) (snd s) table 
                   match rel with
                   | [Rel.Greater] ->
                       Found f
                   | [Rel.Undefined] | [] ->
                       Undefined f
                   | _ ->
                       internalFunc (stream.Tail) table
               | [] ->
                   NotFound
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



    let check stream table nodes axiom =
        let mediums = nodes |> List.ofType<Core.IMedium>
        let rec intFunc buffer =

            let state = 
                buffer 
                |> List.map (fun x ->((nodes:Core.INode list) |> List.find (fun y -> y.Id = snd x)).ToString())
                |> String.concat ""

            printfn "%s" state

            match buffer |> List.length with
            | 1 ->
                let baseElement = BaseSearching.findBaseGreedy (buffer |> List.map (fun x -> snd x)) mediums
                //printfn "%A" (buffer |> List.map (fun x -> (nodes:Core.INode list) |> List.find (fun y -> y.Id = snd x)))

                if baseElement.Head = axiom 
                then {errors = []; position = None;}
                else
                    {
                        errors = [{
                            message = "Axiom not found";
                            tag = "syntax";
                            tokensOnError = Seq.empty;
                        }];
                        position = Some 1;
                    }
            | _ ->
                let fstGreater = findFirstGreater buffer table 0


                match fstGreater with
                | Undefined u ->
                    let expected = 
                        table.[snd u].Relashionships.Keys 
                        |> List.ofSeq  
                        |> List.map (fun x -> 
                            let ns = nodes |> List.tryFind (fun y -> y.Id = x)
                            match ns with
                            | None -> ""
                            | Some s ->
                                s.ToString()
                        )
                        |> String.concat " , "
                    let message = "Unexpected token. Expected: " + expected
                    {
                        errors = [{
                            message = message;
                            tag = "syntax";
                            tokensOnError = Seq.empty;
                        }];
                        position = Some (fst u);
                    }
                | _ ->
                    let fstGreaterIndex = 
                        match fstGreater with
                        | Found fstGreater -> buffer |> List.findIndexOf fstGreater
                        | NotFound -> buffer.Length - 1
                        | _ -> failwith("undefined behaviour")

                    let lstLower = findLastLower buffer table fstGreaterIndex

                    let lstLowerIndex =
                        match lstLower with
                        | Some lstLower -> buffer |> List.findIndexOf lstLower
                        | None -> 0

                    let count = fstGreaterIndex - lstLowerIndex + 1

                    let nodesToChange = buffer |> List.skip lstLowerIndex |> List.take count

                    let basic = BaseSearching.findBase (nodesToChange|> List.map (fun x -> snd x)) mediums

                    match basic with
                    | None ->
                        {
                            errors = [{
                                message = "Unexpected end of file.";
                                tag = "syntax";
                                tokensOnError = Seq.empty;
                            }];
                            position = Some fstGreaterIndex;
                        }
                    | Some basic ->
                        let fstPart = buffer |> List.take lstLowerIndex
                        let lstPart = buffer |> List.skip (fstGreaterIndex + 1)
                        let newBuffer = fstPart @ [fstGreaterIndex, basic] @ lstPart
                        intFunc newBuffer

        let indexedStream = stream |> List.zip [0..stream.Length - 1]
     
        intFunc indexedStream


//module private ParseSimple =

//    type RelRes = 
//        | Found of int*int
//        | Undefined of int*int
//        | NotFound


//    let findFirstGreater stream table startIndex =
//           let rec internalFunc stream table =
//               match stream with
//               | f :: s :: _ ->
//                   let rel = Rel.getRel (snd f) (snd s) table 
//                   match rel with
//                   | [Rel.Greater] ->
//                       Found f
//                   | [Rel.Undefined] | [] ->
//                       Undefined f
//                   | _ ->
//                       NotFound
//               | [] ->
//                   NotFound
//               | _ ->
//                   internalFunc (stream.Tail) table
//           internalFunc (stream |> List.skip startIndex) table

//    //let findFirstGreater stream table startIndex =
//    //    let rec internalFunc stream table =
//    //        match stream with
//    //        | f :: s :: _  when Rel.getRel (snd f) (snd s) table = [Rel.Greater] ->
//    //            Some f
//    //        | [] ->
//    //            None
//    //        | _ ->
//    //            internalFunc (stream.Tail) table
//    //    internalFunc (stream |> List.skip startIndex) table
            

//    let findLastLower stream table startIndex =
//        let rec internalFunc stream table =
//           match stream with
//           | f :: s :: _  when Rel.getRel (snd s) (snd f) table = [Rel.Lower] ->
//               Some f
//           | [] ->
//               None
//           | _ ->
//               internalFunc (stream.Tail) table
//        internalFunc (stream |> List.take (startIndex + 1) |> List.rev) table



//    let check stream table nodes axiom =
//        let mediums = nodes |> List.ofType<Core.IMedium>
//        let rec intFunc buffer =
//            //printfn "%A" buffer
//            printfn "%s" (buffer |> List.map (fun x ->( (nodes:Core.INode list) |> List.find (fun y -> y.Id = snd x)).ToString() ) |> String.concat "") 
//            match buffer |> List.length with
//            | 1 ->
//                let baseElement = BaseSearching.findBaseGreedy (buffer |> List.map (fun x -> snd x)) mediums
//                //printfn "%A" (buffer |> List.map (fun x -> (nodes:Core.INode list) |> List.find (fun y -> y.Id = snd x)))

//                if baseElement.Head = axiom 
//                then {errors = []; countLeft = None;}
//                else
//                    {
//                        errors = [{
//                            message = "Axiom not found";
//                            tag = "syntax";
//                            tokensOnError = Seq.empty;
//                        }];
//                        countLeft = Some 1;
//                    }
//            | _ ->
//                let fstGreater = findFirstGreater buffer table 0

//                let fstGreaterIndex = 
//                    match fstGreater with
//                    | Some fstGreater -> buffer |> List.findIndexOf fstGreater
//                    | None -> buffer.Length - 1

//                let lstLower = findLastLower buffer table fstGreaterIndex

//                let lstLowerIndex =
//                    match lstLower with
//                    | Some lstLower -> buffer |> List.findIndexOf lstLower
//                    | None -> 0

//                let count = fstGreaterIndex - lstLowerIndex + 1

//                let nodesToChange = buffer |> List.skip lstLowerIndex |> List.take count

//                let basic = BaseSearching.findBase (nodesToChange|> List.map (fun x -> snd x)) mediums

//                match basic with
//                | None ->
//                    let message =
//                        match fstGreater with
//                        | None ->
//                            "Unexpected end of file."
//                        | Some fstGreater ->
//                            let expected = 
//                                table.[snd fstGreater].Relashionships.Keys 
//                                |> List.ofSeq  
//                                |> List.map (fun x -> 
//                                    let ns = nodes |> List.tryFind (fun y -> y.Id = x)
//                                    match ns with
//                                    | None -> ""
//                                    | Some s ->
//                                        s.ToString()
//                                )
//                                |> String.concat " , "
//                            "Unexpected token. Expected: " + expected

//                    {
//                        errors = [{
//                            message = message;
//                            tag = "syntax";
//                            tokensOnError = Seq.empty;
//                        }];
//                        countLeft = Some fstGreaterIndex;
//                    }
//                | Some basic ->
//                    let fstPart = buffer |> List.take lstLowerIndex
//                    let lstPart = buffer |> List.skip (fstGreaterIndex + 1)
//                    let newBuffer = fstPart @ [fstGreaterIndex, basic] @ lstPart
//                    intFunc newBuffer

//        let indexedStream = stream |> List.zip [0..stream.Length - 1]
     
//        intFunc indexedStream



type SyntaxPredescenceTableParser(
    table:Table,
    tokenSelector:System.Func<Core.IParsedToken seq>, 
    nodes:Core.INodeCollection,
    axiom:Core.IMedium) =

    member val Tokens:Core.IParsedToken seq = Seq.empty with get, set

    member this.Parse() =
        let tokens = tokenSelector.Invoke()

        if tokens = null
        then {errors = []; position = None} :> Core.IParserResult
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

            let nodes = nodes (*|> Seq.ofType<Core.IMedium>*) |> List.ofSeq

            // Make internal result type
            // Store original in-stream position 
            // in order to get ParsedToken later

            match ParseSimple.check stream table nodes axiom.Id with
            |{errors=x; position=Some y} as result ->

                let tokenOnError = 
                    tokens 
                    |> Seq.item ((tokens |> Seq.length) - y)

                let tokenBefore =
                    tokens  
                    |> Seq.item ((tokens |> Seq.length) - y - 1)

                if (x |> Seq.isEmpty)
                then

                    {
                        result with errors = [{
                            message = "Undefined relationship"; 
                            tag = "syntax";
                            tokensOnError = [tokenBefore;tokenOnError] |> Seq.ofList;
                        }]
                    }:> Core.IParserResult
                else
                    let j = (x |> Seq.head)
                    {
                        result with errors = [{
                            message = j.Message; 
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
    
    