module internal SyntaxPredescenceTableParser.ParseSimple

open Extentions

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

let log buffer (nodes:Core.INode list) =
    let state = 
        buffer 
        |> List.map (fun x ->
            let nodes = nodes

            let foundNode = nodes |> List.tryFind (fun y -> y.Id = snd x)
            match foundNode with
            | Some s ->
                s.ToString()
            | None ->
                "__undefined__"
        )
        |> String.concat ""

    Core.Logger.Add ("syntaxPredescenceParser", state)
    //printfn "%s" state

let getExpected undefined (table:Table) (nodes:Core.INode list) =
   
    table.[snd undefined].Relashionships.Keys 
    |> List.ofSeq  
    |> List.map (fun x -> 
        let ns = 
            nodes 
            |> List.filter (fun y -> y :? Core.ITerminal || y :? Core.IDefinedToken)
            |> List.tryFind (fun y -> y.Id = x)
        match ns with
        | None -> None
        | Some s ->
            Some (s.ToString())
    )
    |> List.choose(function Some x -> Some x | _ -> None)
    |> String.concat " , "

let check stream table (nodes:Core.INode list) axiom =

    let nodeObjects = nodes |> List.map (fun x -> x :> obj)

    let mediums = nodeObjects |> List.ofType<Core.IMedium>

    

    let rec intFunc buffer =
        log buffer nodes

        match buffer |> List.length with
        | 1 ->
            let baseElement = BaseSearching.findBaseGreedy (buffer |> List.map (fun x -> snd x)) mediums
            //Poliz.finalize()

            if baseElement.Head = axiom 
            then {errors = []; position = None;}
            else
                {
                    errors = [{
                        message = "Axiom not found";
                        tag = "syntax";
                        tokensOnError = Seq.empty;
                    }];
                    position = Some (buffer |> List.last |> fst);
                }
        | _ ->
            match findFirstGreater buffer table 0 with
            | Undefined fstGreater ->
                let expected = getExpected fstGreater table nodes
                //Poliz.finalize()

                let message = "Unexpected token. Expected: " + expected
                {
                    errors = [{
                        message = message;
                        tag = "syntax";
                        tokensOnError = Seq.empty;
                    }];
                    position = Some (fst fstGreater);
                }
            | fstGreater ->
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
                    let expected = getExpected (buffer |> List.last) table nodes
                    //Poliz.poliz nodesToChange nodes
                    //Poliz.finalize()

                    {
                        errors = [{
                            message = "Unexpected end of file. Expected: " + expected;
                            tag = "syntax";
                            tokensOnError = Seq.empty;
                        }];
                        position = Some ((stream |> List.length) - 1);
                    }
                | Some basic ->
                    Poliz.poliz nodesToChange nodes


                    let fstPart = buffer |> List.take lstLowerIndex
                    let lstPart = buffer |> List.skip (fstGreaterIndex + 1)
                    let newBuffer = fstPart @ [fstGreaterIndex, basic] @ lstPart
                    intFunc newBuffer

    let indexedStream = stream |> List.zip [0..stream.Length - 1]
     
    let result = intFunc indexedStream

    //Poliz.finalize()

    result

