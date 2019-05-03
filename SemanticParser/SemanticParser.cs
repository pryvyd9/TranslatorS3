using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using Core.Entity;

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
                        //.OfType<IDefinedStatement>()
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
                Labels = new List<ILabel>(),
                ChildrenScopes = new List<IScope>(),
            };

            var rootStream = ParseStream(rootScope, null, ParsedTokens.GetEnumerator());

            rootScope.Stream = rootStream;

            // Check.

            CheckIdentifiers(rootScope);

            CheckUndefined();

            CheckLabels(rootScope);

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

        private bool TryFindLabelDeclaration(string name, IScope scope, out ILabel foundLabel)
        {
            if (scope.ParentScope is null)
            {
                foundLabel = scope.Labels.FirstOrDefault(n => n.Name == name);

                if (foundLabel is null)
                    return false;

                return true;
            }

            return TryFindLabelDeclaration(name, scope.ParentScope, out foundLabel);
        }

        private bool IsLastBreaker(IEnumerable<IEnumerable<IExecutionStreamNode>> streams, int[] breakers)
        {
            if (!streams.Any())
                return false;

            var last = streams.Last().Last();

            if (last is IStatement st)
            {
                return IsLastBreaker(st.Streams, breakers);
            }
            else
            {
                return breakers.Contains(((IDefinedStreamNode)last).GrammarNodeId);
                //return last.Type == StreamControlNodeType.Breaker;
            }
        }

        //private bool TryFindLabelDeclaration(string name, IScope scope, out ILabel foundLabel)
        //{
        //    foundLabel = scope.Labels.FirstOrDefault(n => n.Name == name);

        //    if (foundLabel is null)
        //    {
        //        if (scope.ParentScope is null)
        //        {
        //            return false;
        //        }
        //        else
        //        {
        //            return TryFindLabelDeclaration(name, scope.ParentScope, out foundLabel);
        //        }
        //    }
        //    else
        //    {
        //        return true;
        //    }
        //}
        private object GetValueFromParsedToken(IParsedToken token)
        {
            if (token.Name == "true")
                return 1;
            else if (token.Name == "false")
                return 0;

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

            var node = nodes.FirstOrDefault(n => n.Id == parsedToken.Id);

            if (node is null)
            {
                return CreateDelimiter(StreamControlNodeType.None);
            }
            //var node = nodes.First(n => n.Id == parsedToken.Id);

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
                case "label":
                    if (TryFindLabelDeclaration(parsedToken.Name, scope, out var foundLabel))
                    {
                        return foundLabel;
                    }

                    var label = new DefinedLabel
                    {
                        Scope = scope,
                        InStringPosition = parsedToken.InStringPosition,
                        Name = parsedToken.Name,
                        GrammarNodeId = parsedToken.Id,
                    };

                    AddLabel(label, scope);
                    //scope.Labels.Add(label);
                    return label;
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

                    var streams = new List<IEnumerable<IExecutionStreamNode>>();

                    //bool Break() => streams.Last().Last().Type == StreamControlNodeType.Breaker
                    bool Break() => IsLastBreaker(streams, statements[statement.NodeId].breakers)
                                || ((IDefinedStatement) node).IsStreamMaxCountSet
                                && ((IDefinedStatement) node).StreamMaxCount <= streams.Count;
                    do
                    {
                        var str = ParseStream(scope, statement, enumerator);

                        if (!str.Any())
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

            void AddLabel(ILabel label, IScope s)
            {
                if (s.ParentScope is null)
                {
                    s.Labels.Add(label);
                }
                else
                {
                    AddLabel(label, s.ParentScope);
                }
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

        private IEnumerable<IExecutionStreamNode> ParseStream(
            IScope scope, 
            IStatement currentStatement, 
            IEnumerator<IParsedToken> enumerator)
        {
            var stream = new List<IExecutionStreamNode>();
            

            while (enumerator.MoveNext())
            {
                var node = ParseNode(scope, enumerator);
               
                var parsedNode = ParsedTokens
                    .First(n => n.InStringPosition == ((IDefinedStreamNode)node).InStringPosition);

                stream.Add(node);

                // Change scope if braces met.
                switch (node.Type)
                {
                    case StreamControlNodeType.ScopeIn:
                        var innerScope = new Scope
                        {
                            ParentScope = scope,
                            Variables = new List<IVariable>(),
                            Labels = new List<ILabel>(),
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

                var lastStatement = stream.LastOrDefault(n => n is IStatement);

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


        private void CheckLabels(IScope rootScope)
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

                // Set declaration position to first declaration
                if (declarations.Length == 1)
                {
                    var declaredLabels = rootScope.Labels.OfType<IDefinedLabel>().Where(n => n.Name == labelName);

                    foreach (var declaredLabel in declaredLabels)
                    {
                        ((DefinedLabel)declaredLabel).InStringPosition = declarations[0].InStringPosition;
                    }
                }


                // Show errors if multiple declaration
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

        //private void CheckLabels(IScope rootScope)
        //{

        //    var stream = rootScope.GetConsistentStream().ToArray();



        //    var labels = stream
        //        .Select((n,i) => (i,n))
        //        .Where(n => n.n is IDefinedLabel)
        //        .Select(n => (index: n.i, label: (IDefinedLabel)n.n)).ToArray();

        //    if (labels.Length == 0)
        //        return;

        //    var tokens = ParsedTokens.ToList();

        //    // Get declarations

        //    var declarations = labels.Where(n =>
        //    {
        //        if (n.index == 0)
        //            return true;

        //        if (stream[n.index - 1] is IStatement statement
        //            && nodes.First(m => m.Id == statement.GrammarNodeId).Name == "goto")
        //        {
        //            return false;
        //        }

        //        return true;
        //    });

        //    // Check declaration interference

        //    var scopedDeclarations = declarations.GroupBy(n => n.label.Scope);

        //    foreach (var scopedDeclaration in scopedDeclarations.Where(n => n.Count() > 1))
        //    {
        //        // Addressing same label
        //        var equalDeclarations = scopedDeclaration.GroupBy(n => n.label);

        //        foreach (var equalDeclaration in equalDeclarations)
        //        {
        //            var instances = equalDeclaration.Select(n => tokens[n.index]).ToArray();

        //            var message = $"Multiple declaration of label '{instances[0].Name}' " +
        //                $"at {{{string.Join(",", instances.Select(n => $"({n.RowIndex + 1};{n.InRowPosition + 1})"))}}}";

        //            errors.Add(new SemanticParserError
        //            {
        //                Tag = "semantic",
        //                Message = message,
        //                TokensOnError = instances,
        //            });

        //            var referencesToEqualDeclarations = labels.Where(n => !scopedDeclaration.Any(m => m.index == n.index) && equalDeclaration.Contains(n));

        //            instances = referencesToEqualDeclarations.Select(n => tokens[n.index]).ToArray();

        //            message = $"Label reference '{instances[0].Name}' is ambigous " +
        //               $"at {{{string.Join(",", instances.Select(n => $"({n.RowIndex + 1};{n.InRowPosition + 1})"))}}}";

        //            errors.Add(new SemanticParserError
        //            {
        //                Tag = "semantic",
        //                Message = message,
        //                TokensOnError = instances,
        //            });
        //        }

        //    }



        //    void AddError(IParsedToken instance, string message)
        //    {
        //        errors.Add(new SemanticParserError
        //        {
        //            Tag = "semantic",
        //            Message = message,
        //            TokensOnError = new[] { instance },
        //        });
        //    }
        //}


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
                        .TakeWhile(n =>!(n is IDelimiter && GetNodeById(((IDefinedStreamNode)n).GrammarNodeId).Name == StatementDelimiterName))
                        .ToList();

                    var lastMention = rightPart
                        .OfType<IDefinedStreamNode>()
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
