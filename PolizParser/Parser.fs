namespace PolizParser


type PolizParser() =
    member this.Parse() =
        failwith("")
    interface Core.IParser<Core.IParserResult> with
        member this.Parse():obj = this.Parse()
        member this.Parse():Core.IParserResult = this.Parse()
