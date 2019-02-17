using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace TokenParser
{
    class ParsedToken : IParsedToken
    {
        public string Name { get; internal set; }

        public int Id { get; internal set; }

        public int TokenClassId { get; internal set; }

        public int RowIndex { get; internal set; }

        public int InRowPosition { get; internal set; }

        public int InStringPosition { get; internal set; }

        public override string ToString()
        {
            return Name;
        }
    }

    class TokenParserResult : ITokenParserResult
    {
        public IEnumerable<IParsedToken> ParsedTokens { get; internal set; }

        public IEnumerable<IParserError> Errors { get; internal set; }
    }

    class TokenParserError : IParserError
    {
        public string Message { get; internal set; }

        public string Tag { get; internal set; }

        public IEnumerable<IParsedToken> TokensOnError { get; internal set; }
    }


}
