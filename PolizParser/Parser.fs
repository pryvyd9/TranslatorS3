namespace RpnParser
open StatementRuleParser

module List =
    //open System.Linq

    let iList collection =
        new System.Collections.Generic.List<'a>(collection:'a seq) :> System.Collections.Generic.IList<'a>

    //let ofType<'a> (collection:#obj list) =
    //    //collection |> List.choose(fun x -> match box x with :? 'a as a -> Some a | _ -> None)
    //    collection |> List.choose(fun x -> match box x with :? 'a as a -> Some a | _ -> None)
    //    //collection.OfType<'a>()

[<AutoOpen>]
module Types =

    type OperatorStackNode = {grammarNode:Core.IDefinedOperator;executionNode:Core.IExecutionStreamNode}
    
    type OperatorStack = OperatorStackNode list

    type Type = Core.StreamControlNodeType

module Parse = 
    


    let getString (stream:Core.IExecutionStreamNode list) (nodes:Core.INode seq) =
        stream |> List.map(function 
        | :? Core.IVariable as v -> v.Name 
        | :? Core.ILiteral as l -> string l.Value 
        | :? Core.ICall as c -> (c.ParamCount |> string) + " " + c.Address + " call"
        | :? Core.IUserJump -> "ujmp"
        | :? Core.IJumpConditionalNegative as j -> j.Label.Name + " jn"
        | :? Core.IJump as j -> j.Label.Name + " jmp"
        | :? Core.ILabel as l -> l.Name + ":"
        | x -> 
            match x with
            | :? Core.IDefinedStreamNode as s ->
                (nodes |> Seq.find(fun y -> y.Id = s.GrammarNodeId)).Name
            | _ ->
                "unsupported"
        ) |> String.concat " "

    let parse rootScope (nodes:Core.INode seq) (statementRules:Statement list) =
        let rec processScope (scope:Core.IScope) =
            if scope.Stream <> null
            then
                let newRpnStream = getRpnStream scope.Stream
                scope.RpnStream <- newRpnStream
            
        and getRpnStream stream =
            let mutable stacks:OperatorStack ref list  = [ref[]]

            let h = [
                for node in stream do
                    match box node with
                    | :? Core.IVariable | :? Core.ILiteral -> yield node
                    | :? Core.IDelimiter as delimiter ->
                        match delimiter.Type with
                        | Type.ScopeIn -> 
                            processScope delimiter.ChildScope

                            // Copy last(top) stack remains to stream if not empty.
                            if not stacks.IsEmpty && not stacks.Head.Value.IsEmpty
                            then
                                for node in stacks.Head.Value -> node.executionNode
                                stacks <- stacks.Tail

                            for node in delimiter.ChildScope.RpnStream ->
                                node

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
                    | :? Core.IStatement as statement ->

                        let rpnStream = getStatementRpnStream statement

                        statement.RpnStreamProcessed <- rpnStream

                        for node in rpnStream ->
                            node

                    | :? Core.IDefinedLabel ->
                        yield node
                    | _ ->
                        failwith("")

                // Copy all stacks remains to stream.
                for stack in stacks do 
                    for node in stack.Value -> 
                        node.executionNode
            ]
            let g = getString h nodes
            printfn "%A" g
            Seq.ofList h;
        and getStatementRpnStream (statement:Core.IStatement) =
            let statementName = (nodes |> Seq.find(fun x -> x.Id = statement.NodeId)).Name
            
            let rule = statementRules |> List.find(fun x -> x.name = statementName)
            
            let case = 
                match rule.cases 
                    |> List.tryFind(fun x -> 
                        x.streamCount = (statement.Streams |> Seq.length |> Some)) with
                | None -> rule.cases |> List.find(fun x -> x.streamCount = None)
                | Some case -> case
            
            let rpnStreams = [
                for stream in statement.Streams ->
                    getRpnStream stream 
            ]
            
            let mutable (rpnStream:Core.IExecutionStreamNode list) = []
            
            let definitions = [
                for rule in case.rules do
                    match rule with
                    | Definition definition ->
                        match definition with
                        | Label label ->
                            yield{
                                new Core.ILabel with
                                    member __.Scope = statement.Scope
                                    member __.Type = Type.None
                                    member __.Name = label.name
                            } :> Core.IExecutionStreamNode
                    | _ -> ()
            ]
            
            let toRpmStream x =
                rpnStream <- rpnStream @ [x :> Core.IExecutionStreamNode]

            let labelsWithName name = 
                let labels = box >> (function :? Core.ILabel as a -> Some a | _ -> None) 
                let withName y (x:Core.ILabel) = x.Name = y

                definitions 
                |> List.choose labels
                |> List.find (withName name)

            for rule in case.rules do
                match rule with     
                | Stream stream ->
                    rpnStream <- rpnStream @ (rpnStreams.[stream.id] |> List.ofSeq)
                | Definition definition ->
                    match definition with
                    | Label label -> labelsWithName label.name |> toRpmStream
                          
                | Reference reference ->
                    match reference with
                    | Call call ->
                        {
                            new Core.ICall with
                            member __.Address = call.address
                            member __.ParamCount = call.paramCount
                            member __.Scope = statement.Scope
                            member __.Type = Type.None
                        } |> toRpmStream
                    | Jmp jmp ->
                        {
                            new Core.IJump with
                            member __.Label = labelsWithName jmp.address
                            member __.Scope = statement.Scope
                            member __.Type = Type.None
                        } |> toRpmStream
                    | Jn jn ->
                        {
                            new Core.IJumpConditionalNegative with
                            member __.Label = labelsWithName jn.address
                            member __.Scope = statement.Scope
                            member __.Type = Type.None
                        } |> toRpmStream
                    | Ujmp ->
                        let label = (statement.Streams |> Seq.last |> Seq.rev |> Seq.item 1) :?> Core.IDefinedLabel
                        
                        let rec tryFindLabelDeclaration name (scope:Core.IScope) =
                            match scope.Labels |> Seq.tryFind (fun n -> n.Name = name) with
                            | Some foundLabel ->
                                Some foundLabel
                            | None ->
                                if isNull scope.ParentScope
                                then
                                    None
                                else 
                                    tryFindLabelDeclaration name scope.ParentScope

                        match tryFindLabelDeclaration label.Name statement.Scope with
                        | Some label ->
                            {
                                new Core.IUserJump with
                                member __.Label = label
                                member __.Scope = statement.Scope
                                member __.Type = Type.None
                            } |> toRpmStream
                        | None ->
                            failwith("Undeclared label referenced.")
                        
            
            rpnStream

        processScope rootScope 

        let g = getString (rootScope.GetRpnConsistentStream() |> List.ofSeq) nodes
        printfn "%A" g


type RpnParser(grammarNodes:Core.INode seq, statementRulesXmlPath:string) =
    //let operators = grammarNodes |> Seq.choose(function | :? Core.IDefinedOperator as op -> Some(op) | _ -> None)

    //let priorityTable = dict [ for op in operators -> op, op.Priority ]

    let statementRules = StatementRuleParser.parse statementRulesXmlPath


    member val RootScope:Core.IScope = null with get, set
    member val ParsedTokens:Core.IParsedToken seq = null with get, set


    member this.Parse() =
        
        Parse.parse this.RootScope grammarNodes statementRules |> ignore

        failwith("")
    interface Core.IRpnParser with
        member this.Parse():obj = this.Parse()
        member this.Parse():Core.IRpnParserResult = this.Parse()
        member this.RootScope with set v = this.RootScope <- v
        member this.ParsedTokens with set v = this.ParsedTokens <- v
