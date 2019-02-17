using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Xml.Linq;

namespace TranslatorS3.Entities
{
    class FiniteAutomaton : IFiniteAutomaton, IEntity
    {
        private class FiniteState : IFiniteState
        {
            public IDictionary<string, int> Links { get; internal set; }

            public string TokenName { get; internal set; }

            public string TokenClass { get; internal set; }
        }

        public IDictionary<int, IFiniteState> States { get; private set; }

        public int StartState { get; private set; }



        public bool Load()
        {
            try
            {
                var doc = XDocument.Load(Configuration.Path.FiniteAutomatonXml);
                var root = doc.Element("finite-automaton");
                StartState = int.Parse(root.Attribute("start-state").Value);

                States = new Dictionary<int, IFiniteState>() as IDictionary<int, IFiniteState>;

                foreach (var stateElement in doc.Element("finite-automaton").Elements("state"))
                {
                    FiniteState finiteState = new FiniteState
                    {
                        TokenClass = stateElement.Attribute("token-class")?.Value,
                        TokenName = stateElement.Attribute("token-name")?.Value,
                        Links = stateElement.Elements("link").Select(n=>
                                (n.Attribute("class").Value, int.Parse(n.Attribute("next-state").Value)))
                            .ToDictionary(n=>n.Value, n=>n.Item2),
                    };

                    States[int.Parse(stateElement.Attribute("id").Value)] = finiteState;
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Parse(IParser parser)
        {
            dynamic result = parser.Parse();

            try
            {
                States = result.States;
                StartState = result.StartState;

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Save()
        {
            try
            {
                Configuration.CreateDirectoryFromPath(Configuration.Path.FiniteAutomatonXml);

                new XDocument(
                    new XElement("finite-automaton",
                        new XAttribute("start-state", StartState),
                        States.Select(n =>
                        {
                            var element = new XElement("state",
                                new XAttribute("id", n.Key),
                                n.Value.Links?.Select(m => new XElement("link",
                                    new XAttribute("class", m.Key),
                                    new XAttribute("next-state", m.Value )
                                ))
                            );

                            if(n.Value.TokenName != null)
                            {
                                element.Add(new XAttribute("token-name", n.Value.TokenName));
                            }

                            if (n.Value.TokenClass != null)
                            {
                                element.Add(new XAttribute("token-class", n.Value.TokenClass));
                            }

                            return element;
                        })
                    )
                ).Save(Configuration.Path.FiniteAutomatonXml);

                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
            

            
        }
    }
}
