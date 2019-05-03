using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using Core.Entity;

namespace SyntaxRecursiveParser
{
    class SyntaxRecursiveParserResult : IParserResult
    {
        public IEnumerable<IParserError> Errors { get; internal set; } 

        internal bool IsErrorFound { get; set; }

        internal bool MustReturn { get; set; }

        internal bool IsBottomHit { get; set; }

    }

    class SyntaxRecursiveParserError : IParserError
    {
        public string Message { get; internal set; }

        public string Tag { get; internal set; }

        public IEnumerable<IParsedToken> TokensOnError { get; internal set; }
    }
}
