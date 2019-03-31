namespace RpnParser
open StatementRuleParser
open System.Collections.Generic

[<AutoOpen>]
module Types =

    type OperatorStackNode = {grammarNode:Core.IDefinedOperator;executionNode:Core.IExecutionStreamNode}
    
    type OperatorStack = OperatorStackNode list

    type Type = Core.StreamControlNodeType

    type Log = {stream:string; stack:string; rpnStream:string;}
    type IndexedLog = {pos:int;value:string;}


module Indexer =
    let getIndexedNode index node stream =
        match box node with
        | :? Core.IVariable | :? Core.ILiteral | :? Core.IUserJump  -> index, 1, node
        | :? Core.ICall -> index, 3, node
        | :? Core.IJump -> index, 2, node
        | :? Core.IDefinedLabel as l-> 
            //match stream 
            //    |> List.filter((=) node)
            //    |> List.map(fun x -> stream |> List.findIndex((=) x))
            //    |> List.tryFind(fun x -> if stream.Length > (x + 1) then (stream |> (List.item (x + 1)) |> box) :? Core.IUserJump |> not else true)
            //        with
            //        | Some s -> index, 1, node
            //        | _ -> index, 0, node
            index, 1, node
        | :? Core.ILabel -> index, 0, node // We ignore it as we don't want to see it in the command stream
        | x -> 
            match x with
            | :? Core.IDefinedStreamNode ->
                index, 1, node
            | _ ->
                index, 1, null

    let getStringedNode (nodes:Core.INode seq) (indexedList:'a list) (node:'a) =
        let index,_,node = node
        let getObjectPosition item =
            let index, _, _ = indexedList |> List.find(function _,_,node when box node = box item -> true | _ -> false)
            index

        match box node with 
        | :? Core.IVariable as v -> [index, v.Name]
        | :? Core.ILiteral as l -> [index, string l.Value]
        | :? Core.ICall as c -> [index, (c.ParamCount |> string); index + 1, c.Address; index + 2, "call"]
        //| :? Core.IUserJump -> [index, "ujmp"]
        | :? Core.IUserJump as j-> 
            //let targetIndex,_,_ = indexedList |> List.find(function _,_,n when box n = box j.Label -> true | _ -> false)

            let h = indexedList 
                          |> List.filter(function _,_,n when box n = box j.Label -> true | _ -> false)
                          |> List.map(fun x -> indexedList |> List.findIndex((=) x))

            let g = h |> List.find(fun x -> 
                if indexedList.Length > (x + 1) 
                then 
                    let _,_,node = indexedList |> (List.item (x + 1))
                    (node |> box) :? Core.IUserJump |> not 
                else true)

            let targetIndex,_,_ = indexedList |> List.item g
          
                
            [index- 1, targetIndex |> string; index , "ujmp"]
        | :? Core.IJumpConditionalNegative as j -> [index, getObjectPosition j.Label |> string; index + 1, "jn"]
        | :? Core.IJump as j -> [index, getObjectPosition j.Label |> string; index + 1, "jmp"]
        //| :? Core.IDefinedLabel as l ->[]
        | :? Core.IDefinedLabel as l ->
            let pos = indexedList |> List.findIndex(function i,_,_ when i = index -> true | _ -> false)
            if pos < indexedList.Length - 1
            then
                let _,_,next = indexedList.[pos + 1]
                match box next with
                | :? Core.IUserJump -> []
                | _ -> [index, l.Name + "+" + (index |> string)]
            else
                [index, l.Name + "+" + (index |> string)]

        // We ignore it as we don't want to see it in the command stream 
        //| :? Core.ILabel as l -> [index, l.Name + ":@+" + (index |> string)]
        | :? Core.ILabel -> []
        | x -> 
            match x with
            | :? Core.IDefinedStreamNode as s ->
                [index, (nodes |> Seq.find(fun y -> y.Id = s.GrammarNodeId)).Name]
            | _ ->
                [index, "unsupported"]

    let getIndexedList  (stream:Core.IExecutionStreamNode list) =
           let mutable i = 0
           let gg = stream |> List.map (fun x -> 
               let indexed = getIndexedNode i x stream
               let _, length, _ = indexed
               i <- i + length
               indexed
           )
           gg


    let getIndexedNodes (stream:Core.IExecutionStreamNode list) (nodes:Core.INode seq) =
        let indexedNodeList = getIndexedList stream
        let indexedStream = indexedNodeList |> List.collect(getStringedNode nodes indexedNodeList)

        //let lastIndex, lastLength, _ = indexedNodeList |> List.last
        //let totalLength = lastIndex + lastLength
        indexedStream

    
    let getLoggableIndexed (stream:Core.IExecutionStreamNode list) (nodes:Core.INode seq) =
        getIndexedNodes stream nodes 
        |> List.map (fun x -> {pos = fst x; value = snd x})

module Parse = 
    
    let log x =
        ()
        //Core.Logger.Add( "rpnParser", x)

    let ( ?<- ) object name value =
        object.GetType().GetProperty(name).SetValue(object, value)

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
                match nodes |> Seq.tryFind(fun y -> y.Id = s.GrammarNodeId) with
                | Some n -> n.Name
                | None -> "unsupported"
                //(nodes |> Seq.find(fun y -> y.Id = s.GrammarNodeId)).Name
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

            let mutable rpnStream = []

            let put x = rpnStream <- rpnStream @ [x]
            let putStack x = rpnStream <- rpnStream @ [for y in x -> y.executionNode]
            let putStream x = rpnStream <- rpnStream @ (x |> List.ofSeq)

            let mutable i = 0

            for node in stream do
                log {
                    stream = getString (stream |> List.ofSeq |> List.skip i) nodes; 
                    stack = if stacks.Length = 0 then "" else  getString (stacks.Head.Value |> List.map (fun x -> x.executionNode)) nodes; 
                    rpnStream = getString (rpnStream |> List.ofSeq) nodes; 
                }


                match box node with
                | :? Core.IVariable | :? Core.ILiteral -> put node
                | :? Core.IDelimiter as delimiter ->
                    match delimiter.Type with
                    | Type.ScopeIn -> 
                        processScope delimiter.ChildScope

                        // Copy last(top) stack remains to stream if not empty.
                        if not stacks.IsEmpty && not stacks.Head.Value.IsEmpty
                        then
                            putStack stacks.Head.Value
                            stacks <- stacks.Tail

                        putStream delimiter.ChildScope.RpnStream

                    | Type.ScopeOut | Type.Breaker | Type.Streamer ->
                        ()
                    | Type.ParensIn -> 
                        stacks <- ref[] :: stacks
                    | Type.ParensOut | Type.None -> 
                        // Copy last(top) stack remains to stream.
                        if stacks.Length > 0
                        then
                            putStack stacks.Head.Value
                            stacks <- stacks.Tail

                    | Type.Statement ->
                        failwith("Delimiter cannot be a statement.")

                    | _ ->
                        failwith("Undefined delimiter type")

                | :? Core.IOperator as operator ->
                    let grammarNode = nodes |> Seq.find(fun x -> x.Id = operator.GrammarNodeId) 

                    if i > 0
                    then
                        let prev = stream |> Seq.item (i - 1)
                        match prev with
                        | :? Core.IVariable | :? Core.ILiteral ->
                            operator ? OperandCount <- 2
                        | _ -> 
                            operator ? OperandCount <- 1
                    else
                        operator ? OperandCount <- 1

                    match grammarNode with
                    | :? Core.IDefinedOperator as definedOperator ->
                        if not stacks.IsEmpty
                        then
                            while not stacks.Head.Value.IsEmpty && definedOperator.Priority < (stacks.Head.Value.Head.grammarNode).Priority do
                                put stacks.Head.Value.Head.executionNode
                                stacks.Head.Value <- stacks.Head.Value.Tail
                            stacks.Head.Value <- {grammarNode = definedOperator; executionNode = node} :: stacks.Head.Value
                        else
                            stacks <- [ref [{grammarNode = definedOperator; executionNode = node}]]

                    | _ ->
                        failwith("not implemented")
                | :? Core.IStatement as statement ->
                    let rpnStream = getStatementRpnStream statement
                    statement.RpnStreamProcessed <- rpnStream
                    putStream rpnStream

                | :? Core.IDefinedLabel -> put node
                | _ ->
                    failwith("")

                i <- i + 1

            // Copy all stacks remains to stream.
            for stack in stacks do 
                putStack stack.Value

            Seq.ofList rpnStream;
    

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
            
           
            let put stream x =
                log {
                    stream = stream; 
                    stack = ""; 
                    rpnStream = getString (rpnStream |> List.ofSeq) nodes; 
                }
                rpnStream <- rpnStream @ [x :> Core.IExecutionStreamNode]

            let putStream x =
                log {
                    stream = getString (x |> List.ofSeq) nodes; 
                    stack = ""; 
                    rpnStream = getString (rpnStream |> List.ofSeq) nodes; 
                }
                rpnStream <- rpnStream @ x

            let labelsWithName name = 
                let labels = box >> (function :? Core.ILabel as a -> Some a | _ -> None) 
                let withName y (x:Core.ILabel) = x.Name = y

                definitions 
                |> List.choose labels
                |> List.find (withName name)

            for rule in case.rules do
                match rule with     
                | Stream stream -> rpnStreams.[stream.id] |> List.ofSeq |> putStream
                | Definition definition ->
                    match definition with
                    | Label label -> labelsWithName label.name |> put (label.name)
                          
                | Reference reference ->
                    match reference with
                    | Call call ->
                        {
                            new Core.ICall with
                            member __.Address = call.address
                            member __.ParamCount = call.paramCount
                            member __.Scope = statement.Scope
                            member __.Type = Type.None
                        } |> put ("call " + call.address + " with " + (call.paramCount |> string))
                    | Jmp jmp ->
                        {
                            new Core.IJump with
                            member __.Label = labelsWithName jmp.address
                            member __.Scope = statement.Scope
                            member __.Type = Type.None
                        } |> put ("jmp " + jmp.address)
                    | Jn jn ->
                        {
                            new Core.IJumpConditionalNegative with
                            member __.Label = labelsWithName jn.address
                            member __.Scope = statement.Scope
                            member __.Type = Type.None
                        } |> put  ("jn " + jn.address)
                    | Ujmp ->
                        let label = (statement.Streams |> Seq.last |> Seq.rev |> Seq.item 1) :?> Core.IDefinedLabel
                        {
                            new Core.IUserJump with
                            member __.Label = label:>Core.ILabel
                            member __.Scope = statement.Scope
                            member __.Type = Type.None
                        } |> put  ("ujmp " + label.Name)
            
            rpnStream

        processScope rootScope 

module Optimize =
    open Core.Optimize

    type Reference = 
        {id:int; name:string; entityType:EntityType; dataType:DataType;mutable address: int}
        interface IReference with
            member this.Name = this.name
            member this.EntityType = this.entityType
            member this.DataType = this.dataType
            member this.Address = this.address
            member this.Id = this.id

    // # number of arguments
    // & index in stream
    // @ global name (namespace.namespace...memberName)
    // : local name (label names)
    let getString node =
        match box node with
        | :? IDeclare as d ->
            "decl:" + d.Name
        | :? ILiteral as l ->
            l.Value |> string
        | :? IReference as r ->
            "&" + (r.Address |> string) + ":" + r.Name
        | :? ICall as c ->
            match c.CallType with
            | CallType.Function ->
                "call@" + c.Name + "#" + (c.ArgumentNumber |> string)
            | CallType.Operator ->
                c.Name + "#" + (c.ArgumentNumber |> string)
            | _ -> failwith("Undefined CallType")
        | :? IJump as j ->
            match j.JumpType with
            | JumpType.Unconditional -> "jmp"
            | JumpType.Negative -> "jn"
            | JumpType.Positive -> "jp"
            | _ -> failwith("Undefined JumpType")
        | _ ->
            "undefined"
        

    let optimize (rootScope:Core.IScope) (nodes:Core.INode seq) =
        let rpnStream = rootScope.GetRpnConsistentStream()

        let mutable i = 0
        // original stream index
        let mutable oi = 0
        let mutable currentScope = rootScope

        // Origin, otimizedStream index
        let declarations = Dictionary<Core.IExecutionStreamNode,int>()

        // Origin, optimizedStream index
        let mutable references:(Core.IExecutionStreamNode*int) list = []

        let mutable optimizedStream = []
        


        let inc() = i <- i + 1
        let dec() = i <- i - 1

        let put (node:INode) =
            optimizedStream <- optimizedStream @ [node]

        let getIndex = function _,i -> i 

        let declare name entityType dataType ttl origin =
            let i = i
            let node = {
                new IDeclare with
                    member __.Id = i
                    member __.Name = name
                    member __.EntityType = entityType
                    member __.DataType = dataType
                    member __.TTL = ttl
            } 

            declarations.Add (origin, i)
           
            // Modify references
            for reference in references |> List.filter(fst >> (=) (origin)) do
                let k = optimizedStream.[getIndex reference]
                (k :?> Reference).address <- i


            // Remove modified references
            references <- references  |> List.filter(fst >> (<>) (origin))

            // Label declarations are omited.
            if entityType = EntityType.Label
            then
                dec()
            else
                put node

        let literal dataType value =
            let i = i
            let node = {
                new ILiteral with
                    member __.Id = i
                    member __.Value = value
                    member __.DataType = dataType
            } 

            put node

        let reference name entityType dataType origin =
            let address =
                match declarations.TryGetValue (origin) with
                | true, x -> x
                | _ -> references <- (origin,i) :: references; 0

            let node = {
                id=i;
                name = name; 
                entityType = entityType;
                dataType = dataType;
                address = address;
            } 
            
            put node

        let call name callType argumentNumber =
            let i = i
            let node = {
                new ICall with
                    member __.Id = i
                    member __.CallType = callType
                    member __.Name = name
                    member __.ArgumentNumber = argumentNumber
            } 
            put node
            
        let jump jumpType =
            let i = i
            let node = {
                new IJump with
                    member __.Id = i
                    member __.JumpType = jumpType
            } 
            put node

        for node in rpnStream do
            
            currentScope <- node.Scope

            match node with
            | :? Core.IVariable as v ->
                if declarations.ContainsKey(v)
                then
                    reference v.Name EntityType.Variable DataType.Int node
                else
                    declare v.Name EntityType.Variable DataType.Int TTL.Short node

            | :? Core.ILiteral as l ->
                literal DataType.Int l.Value

            | :? Core.IOperator as o ->
                call (nodes |> Seq.find(fun x -> x.Id = o.GrammarNodeId)).Name CallType.Operator o.OperandCount

            | :? Core.ICall as c ->
                call c.Address CallType.Function c.ParamCount

            | :? Core.IJumpConditionalNegative as jn-> 
                reference jn.Label.Name EntityType.Label DataType.Label jn.Label
                inc()
                jump JumpType.Negative

            | :? Core.IJumpConditional as jc ->
                reference jc.Label.Name EntityType.Label DataType.Label jc.Label
                inc()
                jump JumpType.Positive

            | :? Core.IJump as jmp ->
                reference jmp.Label.Name EntityType.Label DataType.Label jmp.Label
                inc()
                jump JumpType.Unconditional
            | :? Core.IDefinedLabel as l ->
                if rpnStream |> Seq.length > oi + 1 
                    && ((rpnStream |> Seq.item (oi + 1)) :? Core.IJump)
                then
                    //reference l.Name EntityType.Label DataType.Label node
                    dec()
                else
                    declare l.Name EntityType.Label DataType.Label TTL.Short node

            | :? Core.ILabel as l ->
                declare l.Name EntityType.Label DataType.Label TTL.Short node
            //| :? Core.ILabel as l ->
            //    if rpnStream |> Seq.length > oi + 1 
            //        && ((rpnStream |> Seq.item (oi + 1)) :? Core.IJump)
            //    then
            //        reference l.Name EntityType.Label DataType.Label node
            //    else
            //        declare l.Name EntityType.Label DataType.Label TTL.Short node

            | _ ->
                ()
            inc()
            oi <- oi + 1
        optimizedStream

        //getString (rootScope.GetRpnConsistentStream() |> List.ofSeq) nodes


type RpnParser(grammarNodes:Core.INode seq, statementRulesXmlPath:string) =

    let statementRules = StatementRuleParser.parse statementRulesXmlPath

    member val RootScope:Core.IScope = null with get, set
    member val ParsedTokens:Core.IParsedToken seq = null with get, set

    member this.Parse() =
        
        //Core.Logger.Clear("rpnParser");
        Core.Logger.Clear("rpnParserIndexed");

        //Parse.log {
        //    stream = Parse.getString (this.RootScope.GetConsistentStream() |> List.ofSeq) grammarNodes; 
        //    stack = ""; 
        //    rpnStream = ""; 
        //}

        try
            Parse.parse this.RootScope grammarNodes statementRules
        with x -> 
            Core.Logger.Add("system.RpmParser", x.Message)

        let optimized = Optimize.optimize this.RootScope grammarNodes
        
        let log stream =
            let mutable i = 0
            for node in stream do
                let name = Optimize.getString node
                //printfn "%i\t%s" i name
                Core.Logger.Add("rpnParserIndexed", {pos=i;value=name})
                i <- i + 1

        log optimized

        //Indexer.getLoggableIndexed (this.RootScope.GetRpnConsistentStream() |> List.ofSeq) grammarNodes
        ////Parse.getLoggableIndexed (this.RootScope.GetRpnConsistentStream() |> List.ofSeq) grammarNodes
        //|> List.iter (fun x -> Core.Logger.Add("rpnParserIndexed", x))

        //let str = Parse.getString (this.RootScope.GetRpnConsistentStream() |> List.ofSeq) grammarNodes

        //printfn "%A" str
        //Parse.log {
        //    stream = ""; 
        //    stack = ""; 
        //    rpnStream = str; 
        //}
            
        {
            new Core.IRpnParserResult with
            member __.RpnStream = optimized |> Array.ofList
            member __.Errors = Seq.empty
        }

    interface Core.IRpnParser with
        member this.Parse():obj = this.Parse() :> obj
        member this.Parse():Core.IRpnParserResult = this.Parse()
        member this.RootScope with set v = this.RootScope <- v
        member this.ParsedTokens with set v = this.ParsedTokens <- v
