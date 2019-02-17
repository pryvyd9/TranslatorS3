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

        private readonly IClassTable classTable;

        private List<IParserError> errors;


        public SemanticParser(IClassTable classTable)
        {
            this.classTable = classTable;
        }


        public IParserResult Parse()
        {
            if (ParsedTokens == null || ParsedTokens.Count() == 0)
                return new SemanticParserResult();

            errors = new List<IParserError>();

            CheckUndefined();

            CheckLabels();

            CheckIdentifiers();

            return new SemanticParserResult
            {
                Errors = errors,
            };
        }

        private void CheckUndefined()
        {
            // Check Undefined lexems
            var undefinedTokens = GetTokensOfClass("undefined");

            if (undefinedTokens.Count() == 0)
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

        private void CheckIdentifiers()
        {
            var identifiers = GetTokensOfClass("identifier");

            if (identifiers.Count() == 0)
                return;

            var identifierNames = identifiers.Select(n => n.Name).Distinct();

            int identifierClassIndex = identifiers.First().TokenClassId;


            var tokens = ParsedTokens.ToList();

            // Check Identifiers
            foreach (var identifierName in identifierNames)
            {
                var instances = tokens.FindAll(n => n.Name == identifierName);

                // Add errors for each instance
                // used before its assignment.

                // Once assignment is found - break.
                // All instances left will be valid.
                foreach (var instance in instances)
                {
                    int index = tokens.IndexOf(instance);

                    // Check on the left
                    if (index > 0)
                    {
                        if (tokens[index - 1].Name == "read")
                        {
                            break;
                        }
                    }

                    // Check on the right
                    if (index < tokens.Count - 1)
                    {
                        if (tokens[index + 1].Name == "=")
                        {
                            break;
                        }
                    }

                    AddError(instance);
                }

                void AddError(IParsedToken instance)
                {
                    string message = $"Attempt to use an unassigned" +
                                $" variable {instance.Name} at ({instance.RowIndex + 1};{instance.InRowPosition + 1}).";

                    errors.Add(new SemanticParserError
                    {
                        Tag = "semantic",
                        Message = message,
                        TokensOnError = new List<IParsedToken> { instance },
                    });
                }
            }


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
