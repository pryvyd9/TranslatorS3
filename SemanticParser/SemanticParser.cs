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

        public string AssignmentName { get; } = "=";
        public string StatementDelimiterName { get; } = ";";




        public SemanticParser(IClassTable classTable, INodeCollection nodes)
        {
            this.classTable = classTable;
            this.nodes = nodes;

            //execClasses = nodes
            //    .Where(n => !string.IsNullOrWhiteSpace(n.ExecuteStreamNodeType))
            //    .ToDictionary(n => n.ExecuteStreamNodeType, n => n.Id);

            statements = nodes
                .OfType<ITerminal>()
                .OfType<IDefinedStatement>()
                //.Where(n => n.ExecuteStreamNodeType == "statement")
                .Select(n => (
                    id: n.Id, 
                    streamers: nodes
                        .OfType<IDefinedStatement>()
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

            // Create scopes and streams.
            var rootScope = new Scope
            {
                Variables = new List<IVariable>(),
                ChildrenScopes = new List<IScope>(),
            };

            var rootStream = ParseStream(rootScope, null, ParsedTokens.GetEnumerator());

            rootScope.Stream = rootStream;

            // Check.

            CheckIdentifiers(rootScope);

            CheckUndefined();

            CheckLabels();

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
                                || ((IDefinedStatement) node).IsStreamMaxCountSet
                                && ((IDefinedStatement) node).StreamMaxCount <= streams.Count;
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
                case "parens-in":
                    return CreateDelimiter(StreamControlNodeType.ParensIn);
                case "parens-out":
                    return CreateDelimiter(StreamControlNodeType.ParensOut);
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
               
                var parsedNode = ParsedTokens.First(n => n.InStringPosition == node.InStringPosition);

                stream.Tokens.Add(node);

                // Change scope if braces met.
                switch (node.Type)
                {
                    case StreamControlNodeType.ScopeIn:
                        var innerScope = new Scope
                        {
                            ParentScope = scope,
                            Variables = new List<IVariable>(),
                            ChildrenScopes = new List<IScope>(),
                        };
                        scope.ChildrenScopes.Add(innerScope);

                        innerScope.Stream = ParseStream(innerScope, null, enumerator);

                        ((Delimiter)node).ChildScope = innerScope;
                        
                        break;
                    case StreamControlNodeType.ScopeOut:
                        return stream;
                }

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


        #endregion


        private void CheckUndefined()
        {
            // Check Undefined lexems
            var undefinedTokens = GetTokensOfClass("undefined").ToArray();

            if (!undefinedTokens.Any())
                return;

            foreach (var token in undefinedTokens)
            {
                string message = $"Found undefined token {token.Name} at ({token.RowIndex + 1};{token.InRowPosition + 1})";

                errors.Add(new SemanticParserError
                {
                    Tag = "lexical",
                    Message = message,
                    TokensOnError = new [] { token },
                });
            }
        }

        private void CheckLabels()
        {
            var labels = GetTokensOfClass("label").ToArray();

            if (labels.Length == 0)
                return;

            var labelNames = labels.Select(n => n.Name).Distinct();

            var tokens = ParsedTokens.ToList();

            // Check labels
            foreach (var labelName in labelNames)
            {
                var instances = tokens.FindAll(n => n.Name == labelName);

                var declarations = instances.Where(instance =>
                {
                    int index = tokens.IndexOf(instance);

                    if (index == 0)
                        return true;

                    return index > 0 && tokens[index - 1].Name != "goto";
                }).ToArray();

                if (declarations.Length > 1)
                {
                    var positions = declarations.Select(n => (n.RowIndex, n.InRowPosition));

                    var positionsString = string.Join(",", positions.Select(n => $"({n.RowIndex + 1};{n.InRowPosition + 1})"));

                    string message = $"Attempt to declare label {labelName} more then once. " +
                        $"Found {declarations.Length} declarations " +
                            $"at {{{positionsString}}}";

                    errors.Add(new SemanticParserError
                    {
                        Tag = "semantic",
                        Message = message,
                        TokensOnError = declarations,
                    });
                }
                else if (declarations.Length == 0)
                {
                    var undeclaredInstances = instances.Except(declarations).ToArray();

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


        private void CheckIdentifiers(IScope rootScope)
        {
            var declaredVariables = new List<IVariable>();

            // Level all streams to one.
            var allNodes = rootScope.GetConsistentStream().ToList();

            for (int i = 0; i < allNodes.Count; i++)
            {
                // Skip if is not a variable or is already declared.
                if (!(allNodes[i] is IVariable variable) || declaredVariables.Contains(variable)) continue;

                // If next node is an assignment operator
                // then declare variable.
                // Otherwise throw an error.
                if (i < allNodes.Count - 1
                    && allNodes[i + 1] is IOperator op 
                    && nodes.First(n => n.Id == op.GrammarNodeId).Name == AssignmentName)
                {
                    // Check right part of the expression.
                    // It must not make use of declaring variable.
                    // This is applicable though
                    // x = 2 + x = 7;
                    // as x is assigned before the use of it.

                    // Fix declaration position if it is the case.

                    var rightPart = allNodes
                        .Skip(i + 1)
                        .TakeWhile(n =>!(n is IDelimiter && GetNodeById(n.GrammarNodeId).Name == StatementDelimiterName))
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
                           // Fix declaration position.

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
                    var parsedToken =  ParsedTokens.ElementAt(i);

                    string message = $"Attempt to use an unassigned variable {parsedToken.Name}" +
                                     $" at ({parsedToken.RowIndex + 1};{parsedToken.InRowPosition + 1}).";

                    AddError(parsedToken, message);
                }
            }

            void AddError(IParsedToken instance, string message)
            {
                errors.Add(new SemanticParserError
                {
                    Tag = "semantic",
                    Message = message,
                    TokensOnError = new [] { instance },
                });
            }
        }


        private INode GetNodeById(int id)
        {
            return nodes.First(n => n.Id == id);
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
