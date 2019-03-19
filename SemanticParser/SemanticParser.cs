using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace SemanticParser
{

  

    class SemanticParser : ISemanticParser
    {
        public IEnumerable<IParsedToken> ParsedTokens { private get; set; }

        private readonly INodeCollection nodes;

        private readonly IClassTable classTable;

        private List<IParserError> errors;


        private readonly Dictionary<int /* id */, (int[] streamers, int[] breakers)> statements;






        public SemanticParser(IClassTable classTable, INodeCollection nodes)
        {
            this.classTable = classTable;
            this.nodes = nodes;

            //execClasses = nodes
            //    .Where(n => !string.IsNullOrWhiteSpace(n.ExecuteStreamNodeType))
            //    .ToDictionary(n => n.ExecuteStreamNodeType, n => n.Id);

            statements = nodes
                .OfType<ITerminal>()
                .Where(n => n.ExecuteStreamNodeType == "statement")
                .Select(n => (
                    id: n.Id, 
                    streamers: nodes
                        .Where(m => n.Streamers.Contains(m.Name))
                        .Select(m => m.Id)
                        .ToArray(),
                    breakers: nodes
                        .Where(m => n.Breakers.Contains(m.Name))
                        .Select(m => m.Id)
                        .ToArray()))
                .ToDictionary(n => n.id, n => (n.streamers, n.breakers));
        }



        public ISemanticParserResult Parse()
        {
            if (ParsedTokens == null || !ParsedTokens.Any())
                return new SemanticParserResult();

            errors = new List<IParserError>();

            CheckUndefined();

            CheckLabels();

            var rootScope = new Scope
            {
                Variables = new List<IVariable>(),
                ChildrenScopes = new List<IScope>(),
            };

            var rootStream = ParseStream(rootScope, null, ParsedTokens.GetEnumerator());

            rootScope.Stream = rootStream;

           CheckIdentifiers(rootStream, rootScope);

            //CheckIdentifiers();

            return new SemanticParserResult
            {
                Errors = errors,
                RootScope = rootScope,
            };
        }

        #region Scope building

        private bool TryFindVariableDeclaration(string name, IScope scope, out IVariable foundVariable)
        {
            foundVariable = scope.Variables.FirstOrDefault(n => n.Name == name);

            if (foundVariable is null)
            {
                if (scope.ParentScope is null)
                {
                    return false;
                }
                else
                {
                    return TryFindVariableDeclaration(name, scope.ParentScope, out foundVariable);
                }
            }
            else
            {
                return true;
            }
        }

        private object GetValueFromParsedToken(IParsedToken token)
        {
            if (int.TryParse(token.Name, out var value))
            {
                return value;
            }

            throw new Exception("Values other than int are not supported.");
        }

        //private int GetLastStreamNodeId(IExecutionStream stream)
        //{
        //    if (!stream.Tokens.Any())
        //    {
        //        throw new Exception("Stream was empty");
        //    }

        //    return ParsedTokens.First(n => n.InStringPosition == stream.Tokens.Last().InStringPosition).Id;
        //}


        private IExecutionStreamNode ParseNode(
            IScope scope,
            IEnumerator<IParsedToken> enumerator)
        {
             var parsedToken = enumerator.Current
                              ?? throw new NullReferenceException(
                                  "In parsed tokens there was a null element.");


            var node = nodes.First(n => n.Id == parsedToken.Id);

            switch (node.ExecuteStreamNodeType)
            {
                case "variable":
                    if (TryFindVariableDeclaration(parsedToken.Name, scope, out var foundVariable))
                    {
                        return foundVariable;
                    }

                    var variable = new Variable
                    {
                        Scope = scope,
                        InStringPosition = parsedToken.InStringPosition,
                        Name = parsedToken.Name,
                        GrammarNodeId = parsedToken.Id,
                    };

                    scope.Variables.Add(variable);
                    return variable;
                case "literal":
                    var literal = new Literal
                    {
                        Scope = scope,
                        InStringPosition = parsedToken.InStringPosition,
                        Value = GetValueFromParsedToken(parsedToken),
                        GrammarNodeId = parsedToken.Id,
                    };
                    return literal;
                case "operator":
                    var @operator = new Operator
                    {
                        Scope = scope,
                        InStringPosition = parsedToken.InStringPosition,
                        GrammarNodeId = parsedToken.Id,
                    };
                    return @operator;
                case "statement":
                {
                    var statement = new Statement
                    {
                        InStringPosition = parsedToken.InStringPosition,
                        NodeId = parsedToken.Id,
                        Type = StreamControlNodeType.Statement,
                        Scope = scope,
                        GrammarNodeId = parsedToken.Id,
                    };

                    var streams = new List<IExecutionStream>();

                    bool Break() => streams.Last().Tokens.Last().Type == StreamControlNodeType.Breaker
                                || ((ITerminal) node).IsStreamMaxCountSet
                                && ((ITerminal) node).StreamMaxCount <= streams.Count;
                    do
                    {
                        var str = ParseStream(scope, statement, enumerator);

                        if (!str.Tokens.Any())
                        {
                            break;
                        }

                        streams.Add(str);

                    } while (!Break());

                    statement.Streams = streams;

                    return statement;
                }
                case "delimiter":
                    return CreateDelimiter(StreamControlNodeType.None);
                case "scope-in":
                    return CreateDelimiter(StreamControlNodeType.ScopeIn);
                case "scope-out":
                    return CreateDelimiter(StreamControlNodeType.ScopeOut);
                default:
                    return CreateDelimiter(StreamControlNodeType.None);
            }

            Delimiter CreateDelimiter(StreamControlNodeType type)
            {
                return new Delimiter
                {
                    Scope = scope,
                    GrammarNodeId = parsedToken.Id,
                    InStringPosition = parsedToken.InStringPosition,
                    Type = type,
                };
            }

        }

        private IExecutionStream ParseStream(
            IScope scope, 
            IStatement currentStatement, 
            IEnumerator<IParsedToken> enumerator)
        {
            var stream = new ExecutionStream
            {
                Tokens = new List<IExecutionStreamNode>(),
            };


            while (enumerator.MoveNext())
            {
                var node = ParseNode(scope, enumerator);

                // Change scope if braces met.
                switch (node.Type)
                {
                    case StreamControlNodeType.ScopeIn:
                        scope = new Scope
                        {
                            ParentScope = scope,
                            Variables = new List<IVariable>(),
                            ChildrenScopes = new List<IScope>(),
                            Stream = stream,
                        };

                        scope.ParentScope.ChildrenScopes.Add(scope);

                        break;
                    case StreamControlNodeType.ScopeOut:
                        scope = scope.ParentScope;
                        break;
                }

                var parsedNode = ParsedTokens.First(n => n.InStringPosition == node.InStringPosition);

                stream.Tokens.Add(node);


                if (currentStatement is null) continue;

                var lastStatement = stream.Tokens.LastOrDefault(n => n is IStatement);

                if (lastStatement != null && lastStatement.Scope == currentStatement.Scope)
                {
                    return stream;
                }

                var (streamers, breakers) = statements[currentStatement.NodeId];


                // Breaker has higher priority and thus comes first.
                if (breakers.Contains(parsedNode.Id))
                {
                    ((ExecutionNode)node).Type = StreamControlNodeType.Breaker;

                    if (node.Scope == currentStatement.Scope)
                    {
                        return stream;
                    }
                }

                if (streamers.Contains(parsedNode.Id))
                {
                    ((ExecutionNode)node).Type = StreamControlNodeType.Streamer;

                    if (node.Scope == currentStatement.Scope)
                    {
                        return stream;
                    }
                }
            }

            
            return stream;
        }


        //private IExecutionStreamNode ParseNode(
        //    IScope scope,
        //    IEnumerator<IParsedToken> enumerator)
        //{
        //    var parsedToken = enumerator.Current
        //                     ?? throw new NullReferenceException(
        //                         "Even though MoveNext() returned true the item was null.");


        //    var node = nodes.First(n => n.Id == parsedToken.Id);

        //    switch (node.ExecuteStreamNodeType)
        //    {
        //        case "variable":
        //            if (TryFindVariableDeclaration(parsedToken.Name, scope, out var foundVariable))
        //            {
        //                return foundVariable;
        //            }

        //            var variable = new Variable
        //            {
        //                Scope = scope,
        //                InStringPosition = parsedToken.InStringPosition,
        //                Name = parsedToken.Name,
        //            };

        //            scope.Variables.Add(variable);
        //            return variable;
        //        case "literal":
        //            var literal = new Literal
        //            {
        //                Scope = scope,
        //                InStringPosition = parsedToken.InStringPosition,
        //                Value = GetValueFromParsedToken(parsedToken)
        //            };
        //            return literal;
        //        case "operator":
        //            var @operator = new Operator
        //            {
        //                Scope = scope,
        //                InStringPosition = parsedToken.InStringPosition,
        //            };
        //            return @operator;
        //        case "statement":
        //            {
        //                var statement = new Statement
        //                {
        //                    InStringPosition = parsedToken.InStringPosition,
        //                    NodeId = parsedToken.Id,
        //                    Type = StreamControlNodeType.Statement,
        //                    Scope = scope,
        //                };

        //                var streams = new List<IExecutionStream>();

        //                StreamControlNodeType lastNodeType;

        //                bool Break() => lastNodeType == StreamControlNodeType.Breaker
        //                            || ((ITerminal)node).IsStreamMaxCountSet
        //                            && ((ITerminal)node).StreamMaxCount <= streams.Count;
        //                do
        //                {
        //                    var str = ParseStream(scope, statement, enumerator, out lastNodeType);
        //                    streams.Add(str);
        //                } while (!Break());

        //                statement.Streams = streams;

        //                return statement;
        //            }
        //        case "delimiter":
        //            return CreateDelimiter(StreamControlNodeType.None);
        //        case "scope-in":
        //            return CreateDelimiter(StreamControlNodeType.ScopeIn);
        //        case "scope-out":
        //            return CreateDelimiter(StreamControlNodeType.ScopeOut);
        //        default:
        //            return CreateDelimiter(StreamControlNodeType.None);
        //    }

        //    Delimiter CreateDelimiter(StreamControlNodeType type)
        //    {
        //        return new Delimiter
        //        {
        //            Scope = scope,
        //            InStringPosition = parsedToken.InStringPosition,
        //            Type = type,
        //        };
        //    }

        //}

        //private IExecutionStream ParseStream(
        //    IScope scope,
        //    IStatement currentStatement,
        //    IEnumerator<IParsedToken> enumerator,
        //    out StreamControlNodeType lastNodeType)
        //{
        //    if (scope is null)
        //    {
        //        var newScope = new Scope
        //        {
        //            Variables = new List<IVariable>(),
        //        };

        //        return ParseStream(newScope, currentStatement, enumerator, out lastNodeType);
        //    }

        //    var stream = new ExecutionStream
        //    {
        //        Tokens = new List<IExecutionStreamNode>(),
        //    };

        //    lastNodeType = StreamControlNodeType.None;

        //    while (enumerator.MoveNext())
        //    {
        //        var node = ParseNode(scope, enumerator);

        //        switch (node.Type)
        //        {
        //            case StreamControlNodeType.ScopeIn:
        //                scope = new Scope
        //                {
        //                    ParentScope = scope,
        //                    Variables = new List<IVariable>(),
        //                };
        //                break;
        //            case StreamControlNodeType.ScopeOut:
        //                scope = scope.ParentScope;
        //                break;
        //            case StreamControlNodeType.Statement:
        //                lastNodeType = StreamControlNodeType.Statement;
        //                break;
        //        }

        //        var parsedNode = ParsedTokens.First(n => n.InStringPosition == node.InStringPosition);

        //        stream.Tokens.Add(node);


        //        if (currentStatement is null) continue;

        //        if (lastNodeType == StreamControlNodeType.Statement
        //            && node.Scope == currentStatement.Scope)
        //        {
        //            return stream;
        //        }

        //        var (streamers, breakers) = statements[currentStatement.NodeId];


        //        if (breakers.Contains(parsedNode.Id))
        //        {
        //            lastNodeType = ((ExecutionNode)node).Type = StreamControlNodeType.Breaker;
        //            if (node.Scope == currentStatement.Scope.ParentScope)
        //            {
        //                return stream;
        //            }
        //            //return stream;
        //        }

        //        if (streamers.Contains(parsedNode.Id))
        //        {
        //            lastNodeType = ((ExecutionNode)node).Type = StreamControlNodeType.Streamer;
        //            if (node.Scope == currentStatement.Scope.ParentScope)
        //            {
        //                return stream;
        //            }
        //            //return stream;
        //        }
        //    }


        //    return stream;
        //}

        #endregion


        private IEnumerable<IExecutionStreamNode> GetConsistentStream(IExecutionStream stream)
        {
            foreach (var node in stream.Tokens)
            {
                yield return node;

                if (node is IStatement st)
                {
                    foreach (var innerStream in st.Streams)
                    {
                        foreach (var innerNodes in GetConsistentStream(innerStream))
                        {
                            yield return innerNodes;
                        }
                    }
                }
            }
        }

        private void CheckUndefined()
        {
            // Check Undefined lexems
            var undefinedTokens = GetTokensOfClass("undefined");

            if (!undefinedTokens.Any())
                return;

            foreach (var token in undefinedTokens)
            {
                string message = $"Found undefined token {token.Name} at ({token.RowIndex + 1};{token.InRowPosition + 1})";

                errors.Add(new SemanticParserError
                {
                    Tag = "lexical",
                    Message = message,
                    TokensOnError = new List<IParsedToken> { token },
                });
            }
        }

        private void CheckLabels()
        {
            var labels = GetTokensOfClass("label");

            if (labels.Count() == 0)
                return;

            var labelNames = labels.Select(n => n.Name).Distinct();

            int labelClassIndex = labels.First().TokenClassId;

            var tokens = ParsedTokens.ToList();

            // Check labels
            foreach (var labelName in labelNames)
            {
                var instances = tokens.FindAll(n => n.Name == labelName);

                var declarations = instances.Where((instance) =>
                {
                    int index = tokens.IndexOf(instance);

                    if (index == 0)
                        return true;

                    if (index > 0)
                    {
                        if (tokens[index - 1].Name != "goto")
                        {
                            return true;
                        }
                    }

                    return false;
                });

                if (declarations.Count() > 1)
                {
                    var positions = declarations.Select(n => (n.RowIndex, n.InRowPosition));

                    var positionsString = string.Join(",", positions.Select(n => $"({n.RowIndex + 1};{n.InRowPosition + 1})"));

                    string message = $"Attempt to declare lable {labelName} more then once. " +
                        $"Found {declarations.Count()} declarations " +
                            $"at {{{positionsString}}}";

                    errors.Add(new SemanticParserError
                    {
                        Tag = "semantic",
                        Message = message,
                        TokensOnError = declarations,
                    });
                }
                else if (declarations.Count() == 0)
                {

                    var undeclaredInstances = instances.Except(declarations);

                    var positions = undeclaredInstances.Select(n => (n.RowIndex, n.InRowPosition));

                    var positionsString = string.Join(",", undeclaredInstances.Select(n => $"({n.RowIndex + 1};{n.InRowPosition + 1})"));

                    string message = $"Attempt to use an undeclared label {labelName} " +
                        $"at {{{positionsString}}}";

                    errors.Add(new SemanticParserError
                    {
                        Tag = "semantic",
                        Message = message,
                        TokensOnError = undeclaredInstances,
                    });
                }
            }
        }


        private INode GetNodeById(int id)
        {
            return nodes.First(n => n.Id == id);
        }

        private void CheckIdentifiers(IExecutionStream rootStream, IScope rootScope)
        {
            var declaredVariables = new List<IVariable>();

            // Level all streams to one.
            var allNodes = GetConsistentStream(rootStream).ToList();

            for (int i = 0; i < allNodes.Count; i++)
            {
                // If declared variables does not contain the variable;
                // otherwise declared.
                if (allNodes[i] is IVariable variable 
                    && !declaredVariables.Contains(variable))
                {
                    // If next node is an assignment operator
                    // then declare variable.
                    // Otherwise throw an error.
                    if (i < allNodes.Count - 1
                        && allNodes[i + 1] is IOperator op 
                        && nodes.First(n => n.Id == op.GrammarNodeId).Name == "=")
                    {
                        // Check right part of the expression.
                        // It must not make use of declaring variable.
                        // This is applicable though
                        // x = 2 + x = 7;
                        // as x is assigned before the use of it.

                        // Fix declaration position if it is the case.

                        var rightPart = allNodes
                            .Skip(i + 1)
                            .TakeWhile(n =>!(n is IDelimiter && GetNodeById(n.GrammarNodeId).Name == ";"))
                            .ToList();

                        var lastMention = rightPart
                            .LastOrDefault(n => n.InStringPosition == variable.InStringPosition);

                        if (lastMention != null)
                        {
                            var lastMentionPosition = rightPart.LastIndexOf(lastMention);

                            var lastMentionPositionFixed = lastMentionPosition + i + 1;

                            var parsedToken = ParsedTokens.ElementAt(lastMentionPositionFixed);


                            if (allNodes[lastMentionPositionFixed + 1] is IOperator op1
                                && nodes.First(n => n.Id == op1.GrammarNodeId).Name == "=")
                            {
                                // If variable is declared on the right part of the 
                                // use of this variable, like
                                // x = 2 + x = 7;
                                // Then fix declaration position.

                                ((Variable)variable).InStringPosition = parsedToken.InStringPosition;

                                declaredVariables.Add((IVariable)lastMention);
                            }
                            else
                            {
                                // Variable used before it was assigned.

                                string message = $"Attempt to use variable {parsedToken.Name} in its own declaration" +
                                                 $" at ({parsedToken.RowIndex + 1};{parsedToken.InRowPosition + 1}).";

                                AddError(parsedToken, message);
                            }
                        }
                        else
                        {
                            declaredVariables.Add(variable);
                        }
                    }
                    else if (i > 0
                             && allNodes[i - 1] is IStatement st 
                             && GetNodeById(st.GrammarNodeId).Name == "read")
                    {
                        // Assigned from read stream.
                        declaredVariables.Add(variable);
                    }
                    else
                    {
                        // Variable used before it was assigned.
                        //var parsedToken = ParsedTokens.First(n => n.InStringPosition == variable.InStringPosition);

                        var parsedToken =  ParsedTokens.ElementAt(allNodes.IndexOf(variable));


                        string message = $"Attempt to use an unassigned variable {parsedToken.Name}" +
                                         $" at ({parsedToken.RowIndex + 1};{parsedToken.InRowPosition + 1}).";

                        AddError(parsedToken, message);

                    }
                }
            }
            void AddError(IParsedToken instance, string message)
            {
               
                errors.Add(new SemanticParserError
                {
                    Tag = "semantic",
                    Message = message,
                    TokensOnError = new List<IParsedToken> { instance },
                });
            }

            //void AddError(IParsedToken instance)
            //{
            //    string message = $"Attempt to use an unassigned" +
            //                     $" variable {instance.Name} at ({instance.RowIndex + 1};{instance.InRowPosition + 1}).";

            //    errors.Add(new SemanticParserError
            //    {
            //        Tag = "semantic",
            //        Message = message,
            //        TokensOnError = new List<IParsedToken> { instance },
            //    });
            //}

        }


        private IEnumerable<IParsedToken> GetTokensOfClass(string @class)
        {
            var classId = classTable.TokenClasses.Forward(@class);

            return ParsedTokens.Where(n => n.TokenClassId == classId);
        }


        object IParser.Parse()
        {
            return Parse();
        }
    }
}
