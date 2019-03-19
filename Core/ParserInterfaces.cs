using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public interface IParser
    {
        object Parse();
    }

    public interface IParser<T> : IParser where T : IParserResult
    {
        new T Parse();
    }

    public interface IParserResult
    {
        IEnumerable<IParserError> Errors { get; }
    }

    public interface IParserError
    {
        string Message { get; }
        string Tag { get; }
        IEnumerable<IParsedToken> TokensOnError { get; }
    }

    #region Token Parser

    public interface ITokenParser : IParser<ITokenParserResult>
    {
        string Script { set; }

        int TabIndent { set; }
    }

    public interface ITokenParserResult : IParserResult
    {
        IEnumerable<IParsedToken> ParsedTokens { get; }
    }

    #endregion

    #region Syntax Parser

    public interface ISyntaxParser : IParser<IParserResult>
    {
        IEnumerable<IParsedToken> ParsedTokens { set; }
    }

    #endregion

    #region Semantic Parser

    public interface ISemanticParserResult : IParserResult
    {
        IScope RootScope { get; }
    }

    public interface ISemanticParser : IParser<ISemanticParserResult>
    {
        IEnumerable<IParsedToken> ParsedTokens { set; }
    }

    #endregion
}
