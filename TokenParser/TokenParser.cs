using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using Core.Entity;

namespace TokenParser
{
    public struct TokenLog
    {
        public string Message { get; set; }
        public SymbolCategory SymbolCategory { get; set; }
        public string SymbolClass { get; set; }
        public char Character { get; set; }
        public int State { get; set; }

        public string TokenName { get; set; }
        public string TokenClass { get; set; }
    }

    internal class TokenParser : ITokenParser
    {
        public string Script { private get; set; }

        public int TabIndent { private get; set; }



        private readonly INodeCollection nodes;

        private readonly IClassTable classTable;

        private readonly IFiniteAutomaton finiteAutomaton;


        public TokenParser(INodeCollection nodes, IClassTable classTable, IFiniteAutomaton finiteAutomaton)
        {
            this.nodes = nodes;
            this.classTable = classTable;
            this.finiteAutomaton = finiteAutomaton;
        }


        private int inStringPosition;
        private int rowIndex;
        private int inRowPosition;

        private StringBuilder buffer;

        private List<IParsedToken> tokens;

        private IFiniteState currentState;

        private IFiniteState StartState => GetState(finiteAutomaton.StartState);

        private int UndefinedTokenClassId => classTable.TokenClasses.Forward(classTable.UndefinedTokenClassName);



        public ITokenParserResult Parse()
        {
            Logger.Clear("tokenParser");
            Logger.Clear("parsed-nodes");
            Logger.Clear("identifiers");
            Logger.Clear("constants");
            Logger.Clear("labels");

            if (string.IsNullOrWhiteSpace(Script))
            {
                Logger.Add("tokenParser", new TokenLog() { Message = "Token parser recieved an empty script." });

                return new TokenParserResult
                {
                    Errors = new List<IParserError>
                    {
                        new TokenParserError
                        {
                            Message = "Token parser recieved an empty script.",

                            Tag = "lexical",
                        }
                    },
                };
            }

            // Reset start values;
            inStringPosition = 0;
            rowIndex = 0;
            inRowPosition = 0;

            buffer = new StringBuilder();
            currentState = StartState;

            tokens = new List<IParsedToken>();


            foreach (char ch in Script + classTable.PotentialWhiteDelimiter)
            {
                (SymbolCategory symbolCategory, string symbolClass) = classTable.GetSymbolInfo(ch);

                Logger.Add("tokenParser", new TokenLog
                {
                    SymbolCategory = symbolCategory,
                    SymbolClass = symbolClass,
                    Character = ch,
                    State = finiteAutomaton.States.First(n=>n.Value == currentState).Key
                });

                // If a buffered token was created then the state had been returned to start state
                // and buffer cleared so the symbol has to be processed second time.

                // We need two symbols to determine what token is to be returned;
                // Second process can create only undefined tokens 
                // which are created in one go.
                // Therefore we don't need any value returned from the second go.

                ProcessSymbol(ch, symbolCategory, symbolClass, out bool isBufferedTokenCreated);

                if (isBufferedTokenCreated)
                {
                    ProcessSymbol(ch, symbolCategory, symbolClass, out _);
                }

                // Moves inStringPosition, row, inRowPosition
                // so that each token's location can be later found.
                SetNextPosition(ch);
            }



            // Show tables

            ParsedNodesTable parsedNodesTable = new ParsedNodesTable(tokens);

            Logger.AddRange("parsed-nodes", parsedNodesTable.GetTableEntities(null));
            Logger.AddRange("identifiers", parsedNodesTable.GetTableEntities(1));
            Logger.AddRange("constants", parsedNodesTable.GetTableEntities(2));
            Logger.AddRange("labels", parsedNodesTable.GetTableEntities(3));

            return new TokenParserResult
            {
                ParsedTokens = tokens,
            };
        }

        /// <summary>
        /// Determines whether should move on to the next state
        /// or create new token and then return.
        /// Also fills respective tables with newly created tokens.
        /// </summary>
        /// <param name="ch">Current symbol to process.</param>
        /// <param name="symbolCategory">Category of current symbol.</param>
        /// <param name="symbolClass">Class of current symbol.</param>
        /// <param name="isBufferedTokenCreated">Whether token was created from buffer or not.</param>
        private void ProcessSymbol(char ch, SymbolCategory symbolCategory, string symbolClass, out bool isBufferedTokenCreated)
        {
            isBufferedTokenCreated = false;

            // If current state has link for this symbol 
            // then move to the next state and fill buffer with the character.
            if (symbolClass != null &&
                currentState.Links != null &&
                currentState.Links.ContainsKey(symbolClass))
            {
                buffer.Append(ch);
                currentState = GetState(currentState.Links[symbolClass]);

                return;
            }

            // if the buffer is empty and symbol is undefined
            // then an undefined token will be created.
            if (string.IsNullOrWhiteSpace(buffer.ToString()))
            {
                if (symbolCategory == SymbolCategory.Undefined)
                {
                    ParsedToken undefinedToken = new ParsedToken
                    {
                        Id = -1,
                        TokenClassId = -1,
                        Name = ch.ToString(),
                        InRowPosition = inRowPosition,
                        RowIndex = rowIndex,
                        InStringPosition = inStringPosition,
                    };

                    tokens.Add(undefinedToken);
                }

                return;
            }


            CreateBuffered();

            isBufferedTokenCreated = true;

        }

        /// <summary>
        /// Creates buffered token.
        /// </summary>
        private void CreateBuffered()
        {
            string tokenName = buffer.ToString();

            ParsedToken token = new ParsedToken
            {
                Name = tokenName,
                InRowPosition = inRowPosition - buffer.Length,
                RowIndex = rowIndex,
                InStringPosition = inStringPosition - buffer.Length,
            };

            // If the state doesn't have a link for the char
            // but has token name then the state  
            // returns token of defined in the state type.

            // Otherwise if buffer was not empty 
            // but there is no token name then
            // the automaton was interrupted where it was not meant
            // to be so we create an undefined token
            // and return to start state.
            if (currentState.TokenName != null)
            {
                IDefinedToken definedToken;

                if (currentState.TokenName == classTable.UnclassifiedTokenClassName ||
                    nodes.Terminals.Any(n => n.Name == tokenName))
                {
                    definedToken = (IDefinedToken)nodes.Terminals.Single(n => n.Name == tokenName);
                }
                else
                {
                    definedToken = nodes.Tokens.Single(n => n.Name == currentState.TokenName && n.TokenClass == currentState.TokenClass);
                }

                token.Id = definedToken.Id;
                token.TokenClassId = definedToken.TokenClassId;
            }
            else
            {
                token.Id = UndefinedTokenClassId;
                token.TokenClassId = UndefinedTokenClassId;
            }



            buffer.Clear();
            currentState = StartState;

            tokens.Add(token);

            Logger.Add("tokenParser", new TokenLog
            {
                TokenClass = token.TokenClassId.ToString(),
                TokenName = token.Name, Message="created token",
            });
        }

        /// <summary>
        /// Sets correct position in string, in row and row index
        /// based on symbol given.
        /// </summary>
        /// <param name="ch">Symbol to move on.</param>
        private void SetNextPosition(char ch)
        {
            switch (ch)
            {
                case '\t':
                    inRowPosition += TabIndent;
                    break;
                case '\n':
                    rowIndex++;
                    break;
                case '\r':
                    inRowPosition = 0;
                    break;
                default:
                    inRowPosition++;
                    break;
            }

            inStringPosition++;
        }


        private IFiniteState GetState(int id)
        {
            return finiteAutomaton.States[id];
        }

        object IParser.Parse()
        {
            return Parse();
        }
    }
}
