using System;
using Core;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxRecursiveParser
{
    public struct Log
    {
        public string Message { get; set; }
        public string CurrentFactor { get; set; }
        public string CurrentToken { get; set; }
        public string Stack { get; set; }
    }

    class SyntaxRecursiveParser : ISyntaxParser
    {
        class Trace
        {
            public IParsedToken Token { get; }

            public IFactor CurrentFactor { get; }

            public IEnumerable<IFactor> Stack { get; }

            public Trace(IParsedToken token, IFactor currentFactor, IEnumerable<IFactor> stack)
            {
                Token = token;
                CurrentFactor = currentFactor;
                Stack = stack;
            }
        }


        public IEnumerable<IParsedToken> ParsedTokens { private get; set; }

        private readonly bool shouldIgnoreUndefinedTokens;

        private readonly int undefinedTokenClassId;



        private readonly IMedium axiom;



        private List<Trace> trace;

        public SyntaxRecursiveParser(
            bool shouldIgnoreUndefinedTokens,
            int undefinedTokenClassId,
            IMedium axiom
        )
        {
            this.shouldIgnoreUndefinedTokens = shouldIgnoreUndefinedTokens;
            this.undefinedTokenClassId = undefinedTokenClassId;
            this.axiom = axiom;
        }


        public IParserResult Parse()
        {
            Logger.Clear("syntaxRecursiveParser");

            if (ParsedTokens == null || ParsedTokens.Count() == 0)
            {
                return new SyntaxRecursiveParserResult
                {
                    Errors = new List<IParserError> {
                        new SyntaxRecursiveParserError
                        {
                            Tag = "system",
                            Message = "Syntax recursive parser did not receive any tokens.",
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
                    Logger.Add("syntaxRecursiveParser", message);

                    return new SyntaxRecursiveParserResult
                    {
                        Errors = new List<IParserError>
                    {
                        new SyntaxRecursiveParserError
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
            Stack<IFactor> factorStack = new Stack<IFactor>();

            LinkedListNode<IParsedToken> currentToken = tokens.First;

            

            // Dive into axiom. 
            var result = Dive(currentToken, axiom, factorStack);

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
                        .Select(n => n.CurrentFactor);

                    // Select tokens from token links and grammar nodes from non-terminal links.
                    var expected = statesOnError.SelectMany(
                        n => n.FactorCases.Keys
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
                        Logger.Add("syntaxRecursiveParser", message);

                        result.Errors = new List<IParserError>
                        {
                            new SyntaxRecursiveParserError
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
                        Logger.Add("syntaxRecursiveParser", message);

                        result.Errors = new List<IParserError>
                        {
                            new SyntaxRecursiveParserError
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


        /// <summary>
        /// Checks if given tokens suit the syntax using recursive
        /// algotithm.
        /// </summary>
        /// <param name="currentToken">Enumerator of collection to get tokens from.</param>
        /// <param name="currentFactor">Factor to search for suitable node in.</param>
        /// <param name="factorStack">Stack of factors to be gone into on hitting the bottom.</param>
        /// <returns>Result with IsBottomHit=true when the last character was processed,
        /// IsErrorFound=true when an error was found.</returns>
        private SyntaxRecursiveParserResult Dive(LinkedListNode<IParsedToken> currentToken, IFactor currentFactor, Stack<IFactor> factorStack)
        {
            // If current token is null then we have hit the end of the token list;
            // therefore no errors were found along the way so return success.
            if (currentToken == null)
            {
                if (currentFactor.IsInterruptable &&
                   factorStack.Count == 0)
                {
                    Log("success");


                    return new SyntaxRecursiveParserResult
                    {
                        IsBottomHit = true,
                        IsErrorFound = false,
                        MustReturn = true,
                    };
                }



                // Create error message
                var expected = currentFactor.FactorCases.Select(n => n.Key);




                if (factorStack.Count > 0)
                {
                    expected = expected
                        .Concat(factorStack.SelectMany(n => n.FactorCases.Select(m => m.Key)));

                }

                var expectedWithFactorNodesFixed = expected.SelectMany(n =>
                {
                    if (n is IFactor fn)
                    {
                        return fn.FactorCases.Keys.Select(m => m.ToString());
                    }
                    else if (n is IDefinedToken)
                    {
                        return new List<string> { n.ToString() };
                    }
                    else
                    {
                        return new List<string>();
                    }
                }).Distinct();

                string message = $"Unexpected end of script. Expected: {string.Join(" , ", expectedWithFactorNodesFixed)}";


                Log(message);


                return new SyntaxRecursiveParserResult
                {
                    Errors = new List<IParserError> {
                        new SyntaxRecursiveParserError
                        {
                            Tag = "syntax",
                            Message = message,
                            TokensOnError = new []{ ParsedTokens.Last() },
                            //TokensOnError = new List<IParsedToken> { currentToken.Value },
                        },
                    },
                    IsBottomHit = true,
                    IsErrorFound = true,
                    MustReturn = true,
                };
            }


            // If curent token is undefined then no need to wait,
            // send error immidiatly.
            if (currentToken.Value.Id == undefinedTokenClassId)
            {
                // Create error message
                var expected = currentFactor.FactorCases.Select(n => n.Key);




                if (factorStack.Count > 0)
                {
                    expected = expected
                        .Concat(factorStack.SelectMany(n => n.FactorCases.Select(m => m.Key)));

                }

                var expectedWithFactorNodesFixed = expected.SelectMany(n =>
                {
                    if (n is IFactor fn)
                    {
                        return fn.FactorCases.Keys.Select(m => m.ToString());
                    }
                    else if (n is IDefinedToken)
                    {
                        return new List<string> { n.ToString() };
                    }
                    else
                    {
                        return new List<string>();
                    }
                }).Distinct();

                string message = $"Unexpected token {currentToken.Value}. Expected: {string.Join(" , ", expectedWithFactorNodesFixed)}";

                Log(message);

                return new SyntaxRecursiveParserResult
                {
                    Errors = new List<IParserError>
                    {
                        new SyntaxRecursiveParserError
                        {
                            Tag = "syntax",
                            Message = message,
                            TokensOnError = new List<IParsedToken> { currentToken.Value },
                        },
                    },
                    IsErrorFound = true,
                    MustReturn = true,
                };
            }


            // Write position of the token so that an error cause can be found
            // later by getting token with max string position.
            trace.Add(new Trace(currentToken.Value, currentFactor, factorStack.ToList()));

            Log("dived");

            var nextCases = currentFactor.FactorCases.Select(n => (n.Key, n.Value));

            var tokens = nextCases.Where(n => n.Key is IDefinedToken)
                .Select(n => (n.Key.Id, n.Value));

            var nonTokens = nextCases.Where(n => !(n.Key is IDefinedToken))
                .Select(n => (n.Key as IFactor, n.Value));


            foreach ((int nextNodeId, IFactor nextFactor) in tokens)
            {
                if (currentToken.Value.Id == nextNodeId)
                {
                    if (nextFactor != null)
                    {
                        Log($"dive into {GetName(nextFactor)}");

                        var result = Dive(currentToken.Next, nextFactor, factorStack);

                        if (result.MustReturn)
                            return result;

                        Log("find alternative");
                    }
                    else
                    {
                        if (factorStack.Count > 0)
                        {
                            var next = factorStack.Pop();

                            Log($"dive into {GetName(next)}");

                            var result = Dive(currentToken.Next, next, factorStack);

                            if (result.MustReturn)
                                return result;

                            Log("find alternative");

                            factorStack.Push(next);
                        }
                    }
                }
            }

            foreach ((IFactor nextNode, IFactor nextFactor) in nonTokens)
            {
                // If next factor is not null then push it and dive then.
                // Otherwise just dive.
                if (nextFactor != null)
                {
                    factorStack.Push(nextFactor);

                    Log($"dive into {GetName(nextNode)}");

                    var result = Dive(currentToken, nextNode, factorStack);

                    if (result.MustReturn)
                        return result;

                    Log("find alternative");

                    factorStack.Pop();
                }
                else
                {
                    Log($"dive into {GetName(nextNode)}");

                    var result = Dive(currentToken, nextNode, factorStack);

                    if (result.MustReturn)
                        return result;

                    Log("find alternative");
                }
            }


            // If current factor is interrutable it means that 
            // this factor also has case with no further elements;
            // hence can be safely interrupted and therefore
            // from this point we can dive into stack factors.
            if (currentFactor.IsInterruptable)
            {
                if (factorStack.Count > 0)
                {
                    var next = factorStack.Pop();

                    Log($"dive into {GetName(next)}");

                    var result = Dive(currentToken, next, factorStack);

                    if (result.MustReturn)
                        return result;

                    Log("find alternative");

                    factorStack.Push(next);
                }
            }

            // If no suitable node was found then return failure.
            return new SyntaxRecursiveParserResult
            {
                IsBottomHit = false,
                IsErrorFound = true,
            };

            string GetName(INode node)
            {
                return !(node is IMedium) && node is IFactor factor ? factor.ToString() : node.ToString();
            }

            void Log(string message = null)
            {
                Logger.Add("syntaxRecursiveParser", new Log
                {
                    CurrentToken = currentToken?.Value?.ToString() ?? "null",
                    CurrentFactor = currentFactor.ToString(),
                    Stack = string.Join("#", factorStack.Select(n => n.ToString())),
                    Message = message ?? string.Empty,
                });
            }
        }

        object IParser.Parse()
        {
            return Parse();
        }
    }
}
