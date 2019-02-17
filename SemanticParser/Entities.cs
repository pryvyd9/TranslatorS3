using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace SemanticParser
{
    class SemanticParserResult : IParserResult
    {
        public IEnumerable<IParserError> Errors { get; internal set; }
    }

    class SemanticParserError : IParserError
    {
        public string Message { get; internal set; }

        public string Tag { get; internal set; }

        public IEnumerable<IParsedToken> TokensOnError { get; internal set; }
    }
}
