using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace SyntaxPushdownParser
{
    public struct Log
    {
        public string Message { get; set; }
        public int CurrentState { get; set; }
        public string CurrentToken { get; set; }
        public string Stack { get; set; }
    }
    class SyntaxPushdownParser : ISyntaxParser
    {
        class Trace
        {
            public IParsedToken Token { get; }

            public int CurrentStateIndex { get; }

            public IEnumerable<int> Stack { get; }

            public Trace(IParsedToken token, int currentStateIndex, IEnumerable<int> stack)
            {
                Token = token;
                CurrentStateIndex = currentStateIndex;
                Stack = stack;
            }
        }

        public IEnumerable<IParsedToken> ParsedTokens { private get; set; }


        private readonly INodeCollection nodes;
        private readonly IPushdownAutomaton pushdownAutomaton;
        private readonly bool shouldIgnoreUndefinedTokens;
        private readonly int undefinedTokenClassId;



        private List<Trace> trace;

        private int StartState => pushdownAutomaton.StartState;

        
        public SyntaxPushdownParser(
            IPushdownAutomaton pushdownAutomaton, 
            INodeCollection nodes,
            bool shouldIgnoreUndefinedTokens,
            int undefinedTokenClassId
        )
        {
            this.pushdownAutomaton = pushdownAutomaton;
            this.nodes = nodes;
            this.shouldIgnoreUndefinedTokens = shouldIgnoreUndefinedTokens;
            this.undefinedTokenClassId = undefinedTokenClassId;
        }


        public IParserResult Parse()
        {
            Logger.Clear("syntaxPushdownParser");

            if (ParsedTokens == null || ParsedTokens.Count() == 0)
            {
                return new SyntaxPushdownParserResult
                {
                    Errors = new List<IParserError> {
                        new SyntaxPushdownParserError
                        {
                            Tag = "system",
                            Message = "Syntax recursive parser did not recieve any tokens.",
                        },
                    },
                };
            }

            LinkedList<IParsedToken> tokens;

            if (shouldIgnoreUndefinedTokens)
            {
                // Skip undefined tokens
                var definedTokens = ParsedTokens.Where(n => n.TokenClassId != undefinedTokenClassId);

                tokens = new LinkedList<IParsedToken>(definedTokens);
            }
            else
            {
                // If the first token is undefined then send error.
                if (ParsedTokens.First().Id == undefinedTokenClassId)
                {
                    string message = $"Undefined token {ParsedTokens.First().Name}" +
                               $" at ({ParsedTokens.First().RowIndex + 1}:{ParsedTokens.First().InRowPosition + 1})";
                    Logger.Add("syntaxPushdownParser", message);

                    return new SyntaxPushdownParserResult
                    {
                        Errors = new List<IParserError>
                    {
                        new SyntaxPushdownParserError
                        {
                            Tag = "syntax",
                            Message = message,
                            TokensOnError = new List<IParsedToken> { ParsedTokens.First() },
                        },
                    },
                        IsErrorFound = true,
                        MustReturn = true,
                    };
                }

                tokens = new LinkedList<IParsedToken>(ParsedTokens);
            }

            trace = new List<Trace>();

            // Where to move on success.
            Stack<int> factorOutStack = new Stack<int>();

            LinkedListNode<IParsedToken> currentToken = tokens.First;

            var result = CheckPushdownState(currentToken, StartState, factorOutStack);

            if (result.IsErrorFound)
            {
                // If token on error was not found then it's not an undefined token.
                // Cause of error is the token which is the latest of all proccessed tokens.
                // So search for a token with max string position.
                if (result.Errors == null)
                {
                    // Get token which was the last of the checked tokens.
                    // It is the cause of the error.

                    int maxStringPosition = trace.Max(m => m.Token.InStringPosition);
                    var tookenOnError = tokens.First(n => n.InStringPosition == maxStringPosition);



                    // To get list of exected tokens select all nodes, that were compared with 
                    // the error token.
                    var statesOnError = trace
                        .Where(n => n.Token.InStringPosition == maxStringPosition)
                        .Select(n => GetState(n.CurrentStateIndex));

                    // Select tokens from token links and grammar nodes from non-terminal links.
                    var expected = statesOnError.SelectMany(
                        n => n.Links.Select(m => nodes.First(k => k.Id == m.Key))
                    );

                    // Factor nodes are to be deconstructed to their basis' keys.
                    var expectedWithFactorNodesFixed = expected.SelectMany(n =>
                    {
                        if (n is IDefinedToken)
                        {
                            return new List<string> { n.ToString() };
                        }
                        else if (n is IFactor fn)
                        {
                            return fn.FactorCases.Keys
                                .OfType<ITerminal>().Select(m => m.ToString());
                        }
                        else
                        {
                            return new List<string>();
                        }
                    }).Distinct().Reverse();

                    if (result.IsBottomHit)
                    {
                        string message = $"Unexpected end of script. Expected: " + string.Join(" , ", expectedWithFactorNodesFixed);
                        Logger.Add("syntaxPushdownParser", message);

                        result.Errors = new List<IParserError>
                        {
                            new SyntaxPushdownParserError
                            {
                                Tag = "syntax",
                                Message = message,
                                TokensOnError = new List<IParsedToken> { tookenOnError },
                            },
                        };
                    }
                    else
                    {
                        string message = $"Unexpected token {tookenOnError.Name} at" +
                                 $" ({tookenOnError.RowIndex + 1};{tookenOnError.InRowPosition + 1})." +
                                 $" Expected: " + string.Join(" , ", expectedWithFactorNodesFixed);
                        Logger.Add("syntaxPushdownParser", message);

                        result.Errors = new List<IParserError>
                        {
                            new SyntaxPushdownParserError
                            {
                                Tag = "syntax",
                                Message = message,
                                TokensOnError = new List<IParsedToken> { tookenOnError },
                            },
                        };

                    }


                }

            }

            return result;

        }

        private SyntaxPushdownParserResult CheckPushdownState(
            LinkedListNode<IParsedToken> currentToken,
            int currentStateIndex,
            Stack<int> factorOutStack
        )
        {


            IPushdownState currentState = GetState(currentStateIndex);

            // If current token is null then we have hit the end of the token list;
            // therefore no errors were found along the way so return success.
            if (currentToken == null)
            {
                if (currentState.IsInterruptable &&
                   factorOutStack.Count == 0)
                {
                    Log("success");

                    return new SyntaxPushdownParserResult
                    {
                        IsBottomHit = true,
                        IsErrorFound = false,
                        MustReturn = true,
                    };
                }

                // Create error message
                var expected = currentState.Links
                                .Select(m => nodes.First(k => k.Id == m.Key));



                if (factorOutStack.Count > 0)
                {
                    expected = expected
                        .Concat(
                            factorOutStack
                            .SelectMany(n => pushdownAutomaton.States[n].Links
                                .Select(m=>nodes.First(k=>k.Id== m.Key)))
                        );

                }

                var expectedWithFactorNodesFixed = expected.SelectMany(n =>
                {
                    if (n is IDefinedToken)
                    {
                        return new List<string> { n.ToString() };
                    }
                    else if (n is IFactor fn)
                    {
                        return fn.FactorCases.Keys.Select(m => m.ToString());
                    }
                    else
                    {
                        return new List<string>();
                    }
                }).Distinct();

                string message = $"Unexpected end of script. Expected: {string.Join(" , ", expectedWithFactorNodesFixed)}";

                Log(message);


                return new SyntaxPushdownParserResult
                {
                    Errors = new List<IParserError>
                    {
                        new SyntaxPushdownParserError
                        {
                            Tag = "syntax",
                            Message = message,
                            //TokensOnError = new List<IParsedToken> { currentToken.Previous.Value },
                        }
                    },
                    IsBottomHit = true,
                    IsErrorFound = true,
                    MustReturn = true,
                    //ErrorMessage = message,
                };
            }

            // If curent token is undefined then no need to wait,
            // send error immidiatly.
            if (currentToken.Value.Id == undefinedTokenClassId)
            {
                // Create error message
                var expected = currentState.Links
                                .Select(m => nodes.First(k => k.Id == m.Key));



                if (factorOutStack.Count > 0)
                {
                    expected = expected
                        .Concat(
                            factorOutStack
                            .SelectMany(n => pushdownAutomaton.States[n].Links
                                .Select(m => nodes.First(k => k.Id == m.Key)))
                        );

                }

                var expectedWithFactorNodesFixed = expected.SelectMany(n =>
                {
                    if (n is IDefinedToken)
                    {
                        return new List<string> { n.ToString() };
                    }
                    else if (n is IFactor fn)
                    {
                        return fn.FactorCases.Keys.Select(m => m.ToString());
                    }
                    else
                    {
                        return new List<string>();
                    }
                }).Distinct();

                string message = $"Unexpected token {currentToken.Value}. Expected: {string.Join(" , ", expectedWithFactorNodesFixed)}";

                Log(message);


                return new SyntaxPushdownParserResult
                {
                    Errors = new List<IParserError>
                    {
                        new SyntaxPushdownParserError
                        {
                            Tag = "syntax",
                            Message = message,
                            TokensOnError = new List<IParsedToken> { currentToken.Value },
                        },
                    },
                    IsBottomHit = true,
                    IsErrorFound = true,
                    MustReturn = true,
                };

            }

            // Write position of the token so that an error cause can be found
            // later by getting token with max string position.
            trace.Add(new Trace(currentToken.Value, currentStateIndex, factorOutStack.ToList()));

            // If the token is found then check it.
            if (currentState.Links.ContainsKey(currentToken.Value.Id))
            {
                var nextStates = currentState.Links[currentToken.Value.Id];
                var nextStateIn = nextStates.stateIn;
                var nextStateOut = nextStates.stateOut;

                if(nextStateOut != null)
                {
                    factorOutStack.Push((int)nextStateOut);
                }

                if (nextStateIn == null)
                {
                    if (factorOutStack.Count > 0)
                    {
                        var nextState = factorOutStack.Pop();

                        Log($"dive into {nextState}");

                        var result = CheckPushdownState(currentToken.Next, nextState, factorOutStack);

                        if (result.MustReturn || result.IsErrorFound)
                            return result;

                        factorOutStack.Push(nextState);
                    }
                }
                else
                {
                    Log($"dive into {(int)nextStateIn}");

                    var result = CheckPushdownState(currentToken.Next, (int)nextStateIn, factorOutStack);

                    if (result.MustReturn || result.IsErrorFound)
                        return result;
                }

                if (nextStateOut != null)
                {
                    factorOutStack.Pop();
                }
            }

            // If current factor is interrutable it means that 
            // this factor also has case with no further elements;
            // hence can be safely interrupted and therefore
            // from this point we can dive into stack factors.
            if (currentState.IsInterruptable)
            {
                if (factorOutStack.Count > 0)
                {
                    var next = factorOutStack.Pop();

                    Log($"dive into {next}");

                    var result = CheckPushdownState(currentToken, next, factorOutStack);

                    if (result.MustReturn || result.IsErrorFound)
                        return result;

                    factorOutStack.Push(next);
                }
            }

            // If no suitable node was found then return failure.
            return new SyntaxPushdownParserResult
            {
                IsBottomHit = false,
                IsErrorFound = true,
            };

            void Log(string message = null)
            {
                Logger.Add("syntaxPushdownParser", new Log
                {
                    CurrentToken = currentToken?.Value?.ToString() ?? "null",
                    CurrentState = currentStateIndex,
                    Stack = string.Join("#", factorOutStack.Select(n => n.ToString())),
                    Message = message ?? string.Empty,
                });
            }
        }



        private IPushdownState GetState(int id)
        {
            return pushdownAutomaton.States[id];
        }

        object IParser.Parse()
        {
            return Parse();
        }
    }
}
