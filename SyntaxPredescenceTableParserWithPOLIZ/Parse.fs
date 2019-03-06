namespace SyntaxPredescenceTableParser

type SyntaxPredescenceTableParser(
    table:Table,
    tokenSelector:System.Func<Core.IParsedToken seq>, 
    nodes:Core.INodeCollection,
    axiom:Core.IMedium) =

    member val Tokens:Core.IParsedToken seq = Seq.empty with get, set

    member __.Parse() =
        Core.Logger.Clear("syntaxPredescenceParser")


        let tokens = tokenSelector.Invoke()

        Poliz.parsedNodes <- tokens |> List.ofSeq

        if tokens = null
        then {errors = []; position = None} :> Core.IParserResult
        else
            let stream = 
                tokens 
                |> List.ofSeq 
                |> List.choose(fun x -> Some x.Id)

            let nodes = nodes |> List.ofSeq

            // Make internal result type
            // Store original in-stream position 
            // in order to get ParsedToken later

            match ParseSimple.check stream table nodes axiom.Id with
            |{errors=x; position=Some y} as result ->

                let tokenOnError = 
                    if y = stream.Length - 1
                    then 
                        tokens 
                        |> Seq.item (y)
                    else
                        tokens 
                        |> Seq.item (y+1)

                if x |> Seq.isEmpty
                then
                    {
                        result with errors = [{
                            message = "Undefined relationship at (" + (1 + tokenOnError.RowIndex).ToString() + ":" + (1 + tokenOnError.InRowPosition).ToString() + ")"; 
                            tag = "syntax";
                            tokensOnError = [tokenOnError] |> Seq.ofList;
                        }]
                    }:> Core.IParserResult
                else
                    let j = (x |> Seq.head)
                    {
                        result with errors = [{
                            message = j.Message + ". At (" + (1 + tokenOnError.RowIndex).ToString() + ":" + (1 + tokenOnError.InRowPosition).ToString() + ")"; 
                            tag = "syntax";
                            tokensOnError = [tokenOnError] |> Seq.ofList;
                        }]
                    }:> Core.IParserResult
            | x ->
                x :> Core.IParserResult

    interface Core.ISyntaxParser with
        member this.Parse():Core.IParserResult = this.Parse()
        member this.Parse():obj = this.Parse() :> obj
        member this.ParsedTokens with set x = this.Tokens <- x
    
    