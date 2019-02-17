using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace SyntaxPushdownParser
{
    class SyntaxPushdownParserResult : IParserResult
    {
        public IEnumerable<IParserError> Errors { get; internal set; }


        internal bool IsErrorFound { get; set; }

        internal bool MustReturn { get; set; }

        internal bool IsBottomHit { get; set; }
    }

    class SyntaxPushdownParserError : IParserError
    {
        public string Message { get; internal set; }

        public string Tag { get; internal set; }

        public IEnumerable<IParsedToken> TokensOnError { get; internal set; }
    }

    //class PushdownState : IPushdownState
    //{
    //    public IDictionary<int, (int? stateIn, int? stateOut)> Links { get; internal set; }

    //    public bool IsInterruptable { get; internal set; }
    //}

    //class PushdownAutomaton : IPushdownAutomaton
    //{
    //    public IDictionary<int, IPushdownState> States { get; internal set; }

    //    public int StartState { get; internal set; }
    //}
}
