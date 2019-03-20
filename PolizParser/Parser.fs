namespace RpnParser

module List =
    let iList collection =
        new System.Collections.Generic.List<'a>(collection:'a seq) :> System.Collections.Generic.IList<'a>

//[<AutoOpen>]
//module Types =
    //type Scope = 
    //    {
    //        childrenScopes:List<Core.IScope>;
    //        parentScope:Scope option;
    //        stream:List<Core.IExecutionStreamNode>;
    //        rpnStream:List<Core.IExecutionStreamNode>;
    //        variables:List<Core.IVariable>;
    //    }

    //    member this.getConsistentStream() =
    //        let rec inFunc (stream:seq<Core.IExecutionStreamNode>) =
    //            [
    //                for node in stream do
    //                    yield node
    //                    match box node with
    //                    | :? Core.IStatement as st ->
    //                        for innerStream in st.Streams do
    //                            for innerNodes in inFunc innerStream.Tokens do
    //                                yield innerNodes
    //                    | _ -> ()
    //            ]
    //        inFunc this.stream

    //    interface Core.IScope with
    //        member this.ChildrenScopes = 
    //            List.iList this.childrenScopes
    //        member this.GetConsistentStream() = 
    //            this.getConsistentStream() :> Core.IExecutionStreamNode seq
    //        member this.ParentScope = 
    //            this.parentScope.Value :> Core.IScope
    //        member this.Stream = 
    //            { new Core.IExecutionStream with member __.Tokens = this.stream :> Core.IExecutionStreamNode seq}
    //        member this.Variables =  
    //            List.iList this.variables
    //        member this.RpnStream 
    //            with get() =
    //                {new Core.IExecutionStream with member __.Tokens = this.rpnStream :> Core.IExecutionStreamNode seq}
    //            and set void = 

module Parse = 
    let parse grammarNodes rootScope parsedTokens executionStream =
        //let prototypeScope = {childrenScopes = []; parentScope = None; stream = []; variables = []}


        let rec getScope (scope:Core.IScope) =
            if scope.Stream <> null
            then
                let newRpnStream = getStream scope.Stream.Tokens
                scope.RpnStream <- {new Core.IExecutionStream with member __.Tokens = newRpnStream}
                failwith("")
            else
                failwith("")
           
        and getStream stream =
            let h = 
                [
                    for node in stream do
                        yield node
                        match node with
                        | :? Core.IDelimiter as delimiter ->
                            match delimiter.Type with
                            | Core.StreamControlNodeType.ScopeIn ->
                                let scope = delimiter.Scope
                                let innerScopeIndex = scope.ChildrenScopes |> Seq.findIndex (fun x -> x = scope)
                                let innerScope = scope.ParentScope.ChildrenScopes |> Seq.item innerScopeIndex

                                let innerScope= getScope innerScope

                                failwith("")
                            | Core.StreamControlNodeType.Breaker ->
                                failwith("")
                            | Core.StreamControlNodeType.Streamer ->
                                failwith("")
                            | _ ->
                                failwith("")

                            failwith("")

                        | _ ->
                            failwith("")
                ]

            seq {
                for node in stream do
                    yield node
                    match node with
                    | :? Core.IDelimiter as delimiter ->
                        failwith("")

                    | _ ->
                        failwith("")

            }
            //failwith("")

        getScope rootScope 

type RpnParser(grammarNodes:Core.INodeCollection) =
    member val RootScope:Core.IScope = null with get, set
    member val ParsedTokens:Core.IParsedToken seq = null with get, set
    member this.Parse() =
        let executionStream = this.RootScope.GetConsistentStream

        Parse.parse grammarNodes this.RootScope this.ParsedTokens executionStream |> ignore

        failwith("")
    interface Core.IRpnParser with
        member this.Parse():obj = this.Parse()
        member this.Parse():Core.IRpnParserResult = this.Parse()
        member this.RootScope with set v = this.RootScope <- v
        member this.ParsedTokens with set v = this.ParsedTokens <- v
