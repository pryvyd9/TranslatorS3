using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace FiniteAutomatonParser
{
    using NextFiniteStates = Dictionary<string, int>;

    public class FiniteAutomatonParser : IParser<FiniteAutomatonParserResult>
    {
        private readonly IClassTable classTable;
        private readonly INodeCollection nodes;
        private IEnumerable<string> unclassifiedTerminals;


        private Map<string, int> TokenClassIndices => classTable.TokenClasses;


        public FiniteAutomatonParser(IClassTable classTable, INodeCollection nodes, IEnumerable<string> unclassifiedTerminals)
        {
            this.classTable = classTable;
            this.nodes = nodes;
            this.unclassifiedTerminals = unclassifiedTerminals;
        }

        /// <summary>
        /// List of classes and characters removed from them.
        /// </summary>
        private Dictionary<string, List<char>> classesWithRemovedSymbols;

        private string undefinedTokenClassName;
        private string unclassifiedTokenClassName;

        private int startState;

        public FiniteAutomatonParserResult Parse()
        {
            classesWithRemovedSymbols = new Dictionary<string, List<char>>();

            undefinedTokenClassName = classTable.UndefinedTokenClassName;
            unclassifiedTokenClassName = classTable.UnclassifiedTokenClassName;

            var finiteStates = GetFiniteAutomatonStates();

            var optimizedFiniteStates = OptimizeFiniteStates(finiteStates);

            var referencedStates = RemoveUnreferencedStates(optimizedFiniteStates, startState);

            var fixedIndexStates = FixIndexingStates(referencedStates);

            return new FiniteAutomatonParserResult
            {
                StartState = startState,
                States = fixedIndexStates
                    .ToDictionary(n=>n.Key, n=>n.Value as IFiniteState),
            };
        }

        #region FiniteStates

        private Dictionary<int, FiniteState> RemoveUnreferencedStates(Dictionary<int, FiniteState> states, int rootState)
        {
            var referencedStates = states
                .Where(n => states.Any(m => m.Value.Links?.Values.Contains(n.Key) ?? false))
                .Prepend(states.First(n => n.Key == rootState))
                .ToDictionary(n => n.Key, n => n.Value);

            return referencedStates;
        }

        private Dictionary<int, FiniteState> FixIndexingStates(Dictionary<int, FiniteState> states)
        {
            var bindedIndices = states.Keys.Select((n, i) => (n, i)).ToDictionary(n => n.n, n => n.i);

            var newStates = states
                .ToDictionary(n =>
                    bindedIndices[n.Key],
                    n =>
                    {
                        n.Value.Links = n.Value.Links?.ToDictionary(m => m.Key, m => bindedIndices[m.Value]);
                        return n.Value;
                    }
                );



            return newStates;
        }

        //private Dictionary<int, FiniteState> MergeLoopReferences(Dictionary<int, FiniteState> states)
        //{

        //}

        /// <summary>
        /// Creates finite automaton based on tokens and unclassified tokens
        /// and creates dedicated state for single delimiters.
        /// </summary>
        /// <returns>List of created states with 0th as the root state.</returns>
        private List<FiniteState> GetFiniteAutomatonStates()
        {
            List<FiniteState> states = new List<FiniteState>();

            FiniteState rootState = new FiniteState();
            states.Add(rootState);

            startState = 0;

            rootState.Links = new NextFiniteStates();

            // Process every token.
            foreach (var token in nodes.OfType<IDefinedToken>().OfType<IMedium>())
            {
                CreateStatesForToken(token, states, rootState);
            }

            // Add states for unclassified terminals
            foreach (string unclassifiedTerminal in unclassifiedTerminals)
            {
                CreateStatesForUnclassifiedTerminals(unclassifiedTerminal, states, rootState, unclassifiedTerminal, unclassifiedTokenClassName);
            }

            // Add state for single delimiters
            AddStateForSingleDelimiter(states, rootState);


            // Now fix all states that have links with modified classes.
            FixLinksWithModifiedClasses(states);



            return states;
        }

        /// <summary>
        /// Create a state for single delimiters.
        /// </summary>
        /// <param name="states">States to add the created one to.</param>
        /// <param name="rootState">State to add link on new state into.</param>
        private void AddStateForSingleDelimiter(List<FiniteState> states, FiniteState rootState)
        {
            int stateIn = states.Count;
            FiniteState stateSingleDelimiter = new FiniteState();
            stateSingleDelimiter.TokenName = unclassifiedTokenClassName;
            stateSingleDelimiter.TokenClass = unclassifiedTokenClassName;

            states.Add(stateSingleDelimiter);

            rootState.Links["singleDelimiter"] = stateIn;
        }


        /// <summary>
        /// Duplicates links with classes modified.
        /// New links point to original's target.
        /// </summary>
        /// <param name="states">States to fix.</param>
        private void FixLinksWithModifiedClasses(List<FiniteState> states)
        {
            // Add new links that lead to original link target with new class as key.
            foreach (FiniteState state in states.Where(n => n.Links != null))
            {
                // Iterate through a copy of nextStats because the original will
                // be modified.
                var copiedNextStates = state.Links.ToDictionary(n => n.Key, n => n.Value);

                foreach (var link in copiedNextStates)
                {
                    // If the link contains modified class then create
                    // duplicates with new classes assigned as keys.
                    if (classesWithRemovedSymbols.ContainsKey(link.Key))
                    {
                        foreach (char ch in classesWithRemovedSymbols[link.Key])
                        {
                            state.Links[ch.ToString()] = link.Value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates states for unclassified terminals.
        /// </summary>
        /// <param name="terminal">The terminal to be converted.</param>
        /// <param name="states">List of all states created so far.</param>
        /// <param name="currentState">The state where terminal will start its way through other states.</param>
        /// <param name="tokenName">Token name to be assigned once the bottom hit.</param>
        private void CreateStatesForUnclassifiedTerminals(
            string terminal,
            List<FiniteState> states,
            FiniteState currentState,
            string tokenName,
            string tokenClassName)
        {
            // Each character of terminal will be treated as 
            // a separate class so we convert it to list of classes.
            List<string> terminalClassNames = GetClassesOfTerminalSymbols(terminal);

            // The state for iterational searching for character from next terminal
            // in nextState links, and next creating new states for each not found
            // character in next terminal so that the whole terminal can be gathered
            // while going through these states.
            FiniteState terminalState;

            // If the first character from the terminal is in one of links
            // then try go through all the states that has the same path 
            // that is to be created.
            if (currentState.Links.ContainsKey(terminalClassNames.First()))
            {
                // Next State in which next char from next terminal is searched for.
                terminalState = states[currentState.Links[terminalClassNames.First()]];


                foreach (string @class in terminalClassNames.Skip(1))
                {
                    // If there is next state for the character then go in and continue search.
                    // Otherwise start creating states for each character.
                    if (terminalState.Links.ContainsKey(@class))
                    {
                        terminalState = states[terminalState.Links[@class]];
                    }
                    else
                    {
                        (FiniteState nextState, int stateIn) = CreateState();

                        terminalState.Links[@class] = stateIn;

                        terminalState = nextState;
                    }
                }
            }
            else
            {
                // As there is no links that has first character of next terminal
                // then create states for each character
                // and terminal state will be current state.
                terminalState = currentState;

                foreach (string @class in terminalClassNames)
                {
                    (FiniteState nextState, int stateIn) = CreateState();

                    terminalState.Links[@class] = stateIn;

                    terminalState = nextState;
                }
            }

            // If it is already assigned and differs from current tokenName
            // then there is an attempt to assign two different tokenNames
            // to one state; therefore, an exception must be thrown.
            if (terminalState.TokenName != null && terminalState.TokenName != tokenName)
            {
                throw new Exception("Attempted assignment of second tokenName to one state.");
            }
            else
            {
                terminalState.TokenName = tokenName;
                terminalState.TokenClass = tokenClassName;
            }

            (FiniteState nextState, int stateIn) CreateState()
            {
                int stateIn = states.Count;
                FiniteState nextState = new FiniteState();
                nextState.Links = new NextFiniteStates();

                states.Add(nextState);

                return (nextState, stateIn);
            }
        }

        /// <summary>
        /// Creates states for token.
        /// </summary>
        /// <remarks>
        /// Loops are gathered in reference list.
        /// Parsed nodes are gathered in parsed nodes list.
        /// When all the branches hit bottom or reached loop point
        /// - resolves loops by duplicating first links from parsed
        /// nodes in place of loop point.
        /// </remarks>
        /// <param name="token">Token to be processed.</param>
        /// <param name="states">List of all states created so far.</param>
        /// <param name="rootState">Parent state of all states a.k.a. 0th state.</param>
        private void CreateStatesForToken(IMedium token, List<FiniteState> states, FiniteState rootState)
        {
            // If there is a recursive case then the token cannot be converted
            // in to finite automaton.
            if (token.Recursion != null)
            {
                throw new Exception($"Finit automaton cannot be built on left-recursive token. " +
                    $"{token}::={(IFactor)token}");
            }

            // List of looped references
            var references = new List<(int stateId, IMedium referencedNode)>();

            // List of parsed nodes and their nextStates
            // Value is a list of links gotten while parsing the node.
            // For example:
            // after parsing a node we got next states:
            // a->state1
            // b->state2
            // c->state3
            // d->state4
            // but part of them were already there from the root state
            // and part were created from processing the node.
            // Assuming that a and b links are related to the node,
            // they will be saved as a list of links for this node.
            // Hence, they will be used to resolve loop points.
            var parsedNodes = new Dictionary<IMedium, NextFiniteStates>();

            parsedNodes[token] = CreateFiniteStatesForTokenFromFactor(token, token, states, rootState, references, parsedNodes, token as IDefinedToken);


            // Now get rid of references
            foreach ((int stateId, IMedium referencedNode) in references)
            {
                // We duplicate nextStates of nodes referenced.
                var nextStatesToInsert = parsedNodes[referencedNode].ToDictionary(n => n.Key, n => n.Value);

                // Try insert next states.
                foreach (var newNext in nextStatesToInsert)
                {
                    if (states[stateId].Links.ContainsKey(newNext.Key))
                    {
                        throw new Exception($"Looped state {stateId} already defines transition with given key {newNext.Key}.");
                    }

                    states[stateId].Links[newNext.Key] = newNext.Value;
                }
            }


        }

        /// <summary>
        /// Searches for every symbol from terminal in classes.
        /// Modifies existing classes and adds new ones if needed.
        /// </summary>
        /// <param name="terminal">Terminal to be processed.</param>
        /// <returns>Classes of terminal.</returns>
        private List<string> GetClassesOfTerminalSymbols(string terminal)
        {
            // Dictionary of created or used classes.
            List<string> terminalClasses = new List<string>();


            // Check each character.
            foreach (char ch in terminal)
            {
                // Get info about symbol.
                var symbolInfo = classTable.GetSymbolInfo(ch);

                // String representation of character.
                string stringChar = ch.ToString();

                // If symbol is classified then there is a class for it created.
                if (symbolInfo.category == SymbolCategory.Classified)
                {
                    // Get symbol class characters.
                    var classCharacters = classTable.SymbolClasses[symbolInfo.@class];

                    // If there are more then 1 character in the class
                    // then remove the character from the class and create a new one.
                    if (classCharacters.Count() > 1)
                    {
                        // Find position of the symbol in the class.
                        int pos = classCharacters.IndexOf(ch);

                        // Remove the symbol from the class.
                        classTable.SymbolClasses[symbolInfo.@class] = classCharacters.Remove(pos, 1);

                        // Add symbol as separate terminal to unclassified terminals.
                        unclassifiedTerminals = unclassifiedTerminals.Append(stringChar);

                        // Create dedicated class.
                        classTable.SymbolClasses[stringChar] = stringChar;


                        // If classesWithRemovedSymbols does not contain class name then 
                        // must create one.
                        if (!classesWithRemovedSymbols.ContainsKey(symbolInfo.@class))
                        {
                            classesWithRemovedSymbols[symbolInfo.@class] = new List<char>();
                        }

                        // Add symbol to list fo removed symbols.
                        classesWithRemovedSymbols[symbolInfo.@class].Add(ch);

                        terminalClasses.Add(stringChar);
                    }
                    else if (classCharacters.Count() == 1)
                    {
                        terminalClasses.Add(symbolInfo.@class);
                    }
                }
                else if (symbolInfo.category == SymbolCategory.Undefined)
                {
                    classTable.SymbolClasses[stringChar] = stringChar;

                    terminalClasses.Add(stringChar);
                }

            }


            return terminalClasses;
        }


        /// <summary>
        /// Creates finite automaton states for tokens.
        /// </summary>
        /// <remarks>
        /// Also addes token terminal symbols to separate classes.
        /// </remarks>
        /// <param name="node">Node to which factor belongs.</param>
        /// <param name="factor">Factor to be processed.</param>
        /// <param name="states">List of all states.</param>
        /// <param name="currentState">State to which links will be added.</param>
        /// <param name="references">List of loop points that refer to one of parsed nodes.</param>
        /// <param name="parsedNodes">List of nodes which have been processed.</param>
        /// <param name="tokenName">Token name.</param>
        /// <returns>List of links.</returns>
        private Dictionary<string, int> CreateFiniteStatesForTokenFromFactor(
            IMedium node,
            IFactor factor,
            List<FiniteState> states,
            FiniteState currentState,
            List<(int stateId, IMedium referencedNode)> references,
            Dictionary<IMedium, Dictionary<string, int>> parsedNodes,
            IDefinedToken token
        )
        {
            // In finite automaton recursion cannot be processed
            // so must throw an exception.
            if (node.Recursion != null)
            {
                throw new Exception("Tokens cannot contain left recursion as it cannot be dealt with a finite automaton." +
                    $" Token {node}.");
            }


            // This dictionary is only needed for resolving loops.
            // Whenever we go into next state we add the link to this dictionary.
            var nextStates = new NextFiniteStates();


            // If factor is interruptable then tokenName 
            // should be assigned to current state.

            // If it is already assigned and differs from current tokenName
            // then there is an attempt to assign two different tokenNames
            // to one state; therefore, an exception must be thrown.
            if (factor.IsInterruptable)
            {
                if (currentState.TokenName != null && currentState.TokenName != token.Name)
                {
                    throw new Exception("Attempted assignment of second tokenName to one state.");
                }
                else
                {
                    currentState.TokenName = token.Name;

                    currentState.TokenClass = token.TokenClass;
                }
            }

            var terminals = factor.FactorCases
                .Where(n => n.Key is ITerminal)
                .Select(n => (n.Key as ITerminal, n.Value));

            var mediums = factor.FactorCases
                .Where(n => n.Key is IMedium)
                .Select(n => (n.Key as IMedium, n.Value));

            var classes = factor.FactorCases
                .Where(n => n.Key is IClass)
                .Select(n => (n.Key as IClass, n.Value));



            foreach ((ITerminal nextNode, IFactor nextFactor) in terminals)
            {
                if (nextFactor == null)
                {
                    // From now on the process is equivalent to that of creating 
                    // states for unclassified terminals.
                    CreateStatesForUnclassifiedTerminals(nextNode.Name, states, currentState, token.Name, token.TokenClass);
                }
                else
                {


                    // Each character of terminal will be treated as 
                    // a separate class so we convert it to list of classes.
                    var terminalClasses = GetClassesOfTerminalSymbols(nextNode.Name);

                    // The state for iterational searching for character from next terminal
                    // in nextState links, and next creating new states for each not found
                    // character in next terminal so that the whole terminal can be gathered
                    // while going through these states.
                    FiniteState terminalState;

                    // If the first character from the terminal is in one of links
                    // then try go through all the states that has the same path 
                    // that is to be created.
                    if (currentState.Links.ContainsKey(terminalClasses.First()))
                    {
                        // Next State in which next char from next terminal is searched for.
                        terminalState = states[currentState.Links[terminalClasses.First()]];


                        foreach (string character in terminalClasses.Skip(1))
                        {
                            // If there is next state for the character then go in and continue search.
                            // Otherwise start creating states for each character.
                            if (terminalState.Links.ContainsKey(character))
                            {
                                terminalState = states[terminalState.Links[nextNode.Name[0].ToString()]];
                            }
                            else
                            {
                                (FiniteState nextState, int stateIn) = CreateState();

                                terminalState.Links[character] = stateIn;

                                terminalState = nextState;
                            }
                        }
                    }
                    else
                    {
                        // As there is no links that has first character of next terminal
                        // then the state to begin creating new states for each character
                        // will be current state.
                        terminalState = currentState;

                        foreach (string character in terminalClasses)
                        {
                            (FiniteState nextState, int stateIn) = CreateState();

                            terminalState.Links[character] = stateIn;

                            terminalState = nextState;
                        }
                    }

                    CreateFiniteStatesForTokenFromFactor(node, nextFactor, states, terminalState, references, parsedNodes, token);
                }
            }

            foreach ((IMedium nextNode, IFactor nextFactor) in mediums)
            {
                if (nextFactor == null)
                {
                    // If next node was already parsed
                    // then put loop.

                    // Otherwise go inside and process the non-terminal.
                    if (parsedNodes.ContainsKey(nextNode))
                    {
                        references.Add((states.Count - 1, nextNode));
                    }
                    else
                    {
                        parsedNodes[nextNode] = null;

                        parsedNodes[nextNode] = CreateFiniteStatesForTokenFromFactor(nextNode, nextNode, states, currentState, references, parsedNodes, token);
                    }
                }
                else
                {
                    throw new Exception("Attempted going inside a non-terminal as a self-inclusion." +
                                " In finite automaton it's impossible.");
                }
            }

            foreach ((IClass nextNode, IFactor nextFactor) in classes)
            {
                string className = nextNode.SymbolClass;

                if (nextFactor == null)
                {
                    // If  the last nextNode is class then current state can be
                    // interrupted here and therefore a tokenName 
                    // should be assigned to current state.

                    // Otherwise must create respective state and only then
                    // go inside.
                    if (currentState.Links.ContainsKey(className))
                    {
                        int stateIn = currentState.Links[className];

                        var nextState = states[stateIn];

                        // As there is no next states followed then token name must be assigned.
                        if (nextState.TokenName != null && nextState.TokenName != token.Name)
                        {
                            throw new Exception("Attempted assignment of second tokenName to one state.");
                        }
                        else
                        {
                            nextState.TokenName = token.Name;
                        }

                        nextStates[className] = stateIn;
                    }
                    else
                    {
                        (FiniteState nextState, int stateIn) = CreateState();

                        // As there is no next states followed then token name must be assigned.
                        nextState.TokenName = token.Name;

                        currentState.Links[className] = stateIn;

                        nextStates[className] = stateIn;

                    }
                }
                else
                {
                    // If next state with the class already exists then go to that state
                    // and process next factor.

                    // Otherwise must create respective state and only then
                    // go inside.
                    if (currentState.Links.ContainsKey(className))
                    {
                        int stateIn = currentState.Links[className];

                        var nextState = states[stateIn];

                        nextStates[className] = stateIn;

                        CreateFiniteStatesForTokenFromFactor(node, nextFactor, states, nextState, references, parsedNodes, token);
                    }
                    else
                    {
                        (FiniteState nextState, int stateIn) = CreateState();

                        currentState.Links[className] = stateIn;

                        nextStates[className] = stateIn;

                        CreateFiniteStatesForTokenFromFactor(node, nextFactor, states, nextState, references, parsedNodes, token);
                    }
                }
            }





            return nextStates;


            (FiniteState nextState, int stateIn) CreateState()
            {
                int stateIn = states.Count;
                FiniteState nextState = new FiniteState
                {
                    Links = new NextFiniteStates()
                };

                states.Add(nextState);

                return (nextState, stateIn);
            }
        }


        private Dictionary<int, FiniteState> OptimizeFiniteStates(List<FiniteState> states)
        {
            // Delete empty links' lists.
            foreach (var state in states)
            {
                if (state.Links?.Count == 0)
                {
                    state.Links = null;
                }
            }

            Dictionary<int, FiniteState> statesDict = states.Select((n, i) => (i, n)).ToDictionary(n => n.i, n => n.n);

            Dictionary<FiniteState, List<FiniteState>> duplicates = new Dictionary<FiniteState, List<FiniteState>>();

            // Find duplicates
            foreach ((int index, FiniteState state) in statesDict.Select(n => (n.Key, n.Value)))
            {
                if (duplicates.Keys.Any(n => n == state))
                {
                    duplicates[duplicates.Keys.Single(n => n == state)].Add(state);
                }
                else
                {
                    duplicates[state] = new List<FiniteState>();
                }
            }

            // Change references to copies - to references to the key.
            foreach ((var key, var copies) in duplicates.Select(n => (n.Key, n.Value)))
            {
                int keyId = getId(key);



                foreach (var copy in copies)
                {
                    int copyId = getId(copy);

                    foreach (var state in states)
                    {
                        if (state.Links != null)
                        {
                            var copyLinks = state.Links.ToDictionary(n => n.Key, n => n.Value);

                            foreach (var link in copyLinks)
                            {
                                if (link.Value == copyId)
                                {
                                    state.Links[link.Key] = keyId;
                                }
                            }
                        }
                    }

                    statesDict.Remove(copyId);
                }
            }

            return statesDict;

            int getId(FiniteState state)
            {
                return states.IndexOf(state);
            }

        }

        #endregion

        object IParser.Parse()
        {
            return Parse();
        }
    }
}
