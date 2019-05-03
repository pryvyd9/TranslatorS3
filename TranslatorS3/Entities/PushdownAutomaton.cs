using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Xml.Linq;
using Core.Entity;

namespace TranslatorS3.Entities
{
    class PushdownAutomaton : IPushdownAutomaton, IEntity
    {
        private class PushdownState : IPushdownState
        {
            public IDictionary<int, (int? stateIn, int? stateOut)> Links { get; set; }

            public bool IsInterruptable { get; set; }
        }

        public IDictionary<int, IPushdownState> States { get; set; }

        public int StartState { get; set; }

        public bool Load()
        {
            try
            {
                var doc = XDocument.Load(Configuration.Path.PushdownAutomatonXml);

                var root = doc.Element("pushdown-automaton");
                StartState = int.Parse(root.Attribute("start-state").Value);

                States = new Dictionary<int, IPushdownState>() as IDictionary<int, IPushdownState>;

                foreach (var stateElement in root.Elements("state"))
                {
                    PushdownState pushdownState = new PushdownState
                    {
                        Links = stateElement.Elements("link").Select(n =>GetLink(n))
                            .ToDictionary(n => n.tokenId, n => (n.stateIn,n.stateOut)),
                    };

                    if(stateElement.Attribute("is-interruptable") != null)
                    {
                        pushdownState.IsInterruptable = bool.Parse(stateElement.Attribute("is-interruptable").Value);
                    }

                    States[int.Parse(stateElement.Attribute("id").Value)] = pushdownState;
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            (int tokenId, int? stateIn, int? stateOut) GetLink(XElement element)
            {
                return (int.Parse(element.Attribute("id").Value),
                    element.Attribute("state-in") != null ? int.Parse(element.Attribute("state-in").Value) : null as int?,
                    element.Attribute("state-out") != null ? int.Parse(element.Attribute("state-out").Value) : null as int?);
            }

            throw new NotImplementedException();
        }

        public bool Parse(IParser parser)
        {
            throw new NotImplementedException();
        }

        public bool Save()
        {
            throw new NotImplementedException();
        }
    }
}
