namespace RpnParser

module List =
    let iList collection =
        new System.Collections.Generic.List<'a>(collection:'a seq) :> System.Collections.Generic.IList<'a>

[<AutoOpen>]
module Types =
    type OperatorStackNode = {grammarNode:Core.IDefinedOperator;executionNode:Core.IExecutionStreamNode}
    
    type OperatorStack = OperatorStackNode list

    type Type = Core.StreamControlNodeType

module Parse = 
    

    let parse rootScope priorityTable (nodes:Core.INode seq) =
        //let prototypeScope = {childrenScopes = []; parentScope = None; stream = []; variables = []}


        let rec processScope (scope:Core.IScope) =
            if scope.Stream <> null
            then
                let newRpnStream = getRpnStream scope.Stream.Tokens
                scope.RpnStream <- {new Core.IExecutionStream with member __.Tokens = newRpnStream}
                ()
            else
                ()
        and getRpnStream stream =
            //let mutable (operatorStack:(Core.IDefinedOperator*Core.IExecutionStreamNode) list) = []


            let mutable stacks:OperatorStack ref list  = [ref[]]

            let h = 
                [
                    for node in stream do
                        match box node with
                        | :? Core.IVariable | :? Core.ILiteral -> yield node
                        | :? Core.IDelimiter as delimiter ->
                            match delimiter.Type with
                            | Type.ScopeIn -> 
                                processScope delimiter.ChildScope

                            | Type.ScopeOut | Type.Breaker | Type.Streamer | Type.None -> 
                                ()

                            | Type.ParensIn -> 
                                stacks <- ref[] :: stacks

                            | Type.ParensOut ->
                                // Copy last(top) stack remains to stream.
                                for node in stacks.Head.Value -> node.executionNode
                                stacks <- stacks.Tail

                            | Type.Statement ->
                                failwith("Delimiter cannot be a statement.")

                            | _ ->
                                failwith("Undefined delimiter type")

                        | :? Core.IOperator as operator ->
                            let grammarNode = nodes |> Seq.find(fun x -> x.Id = operator.GrammarNodeId) 

                            match grammarNode with
                            | :? Core.IDefinedOperator as definedOperator ->
                                while not stacks.Head.Value.IsEmpty && definedOperator.Priority < (stacks.Head.Value.Head.grammarNode).Priority do
                                    yield stacks.Head.Value.Head.executionNode
                                    stacks.Head.Value <- stacks.Head.Value.Tail
                                stacks.Head.Value <- {grammarNode = definedOperator; executionNode = node} :: stacks.Head.Value

                            | _ ->
                                failwith("not implemented")
                                //yield node
                        | :? Core.IStatement as statement ->
                            statement.RpnStreams <- [
                                for stream in statement.Streams ->
                                    let innerStream = getRpnStream stream.Tokens
                                    {new Core.IExecutionStream with member __.Tokens = innerStream}
                            ]

                        | _ ->
                            failwith("")

                    // Copy all stacks remains to stream.
                    for stack in stacks do 
                        for node in stack.Value -> 
                            node.executionNode
                ]
            //let g = h |> List.map(fun x -> nodes |> Seq.find(fun y -> y.Id = x.GrammarNodeId))
            let g = h |> List.map(function | :? Core.IVariable as v -> v.Name | :? Core.ILiteral as l -> string l.Value | x -> (nodes |> Seq.find(fun y -> y.Id = x.GrammarNodeId)).Name )
            printfn "%A" g
            Seq.ofList h;

        processScope rootScope 

        let j = rootScope.GetRpnConsistentStream();
        let jj = rootScope.GetConsistentStream();
        

        let g = 
            rootScope.GetRpnConsistentStream() 
            |> Seq.map(function 
                | :? Core.IVariable as v -> v.Name 
                | :? Core.ILiteral as l -> string l.Value 
                | x -> (nodes |> Seq.find(fun y -> y.Id = x.GrammarNodeId)).Name 
            ) |> List.ofSeq
        printfn "%A" g


type RpnParser(grammarNodes:Core.INode seq) =
    let operators = grammarNodes |> Seq.choose(function | :? Core.IDefinedOperator as op -> Some(op) | _ -> None)

    let priorityTable = dict [ for op in operators -> op, op.Priority ]

    member val RootScope:Core.IScope = null with get, set
    member val ParsedTokens:Core.IParsedToken seq = null with get, set


    member this.Parse() =
        //let executionStream = this.RootScope.GetConsistentStream


        Parse.parse this.RootScope priorityTable grammarNodes |> ignore

        failwith("")
    interface Core.IRpnParser with
        member this.Parse():obj = this.Parse()
        member this.Parse():Core.IRpnParserResult = this.Parse()
        member this.RootScope with set v = this.RootScope <- v
        member this.ParsedTokens with set v = this.ParsedTokens <- v
