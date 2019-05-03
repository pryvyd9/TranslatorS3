using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using Core.Entity;

namespace FiniteAutomatonParser
{

    class FiniteState : IFiniteState
    {
        public IDictionary<string, int> Links { get; internal set; }

        public string TokenName { get; internal set; }

        public string TokenClass { get; internal set; }


        public static bool operator ==(FiniteState ob1, FiniteState ob2)
        {
            if (ob1 is null || ob2 is null)
                return false;


            if (ob1.Links is null ^ ob2.Links is null)
                return false;


            bool tokenNames = ob1.TokenClass == ob2.TokenClass &&
                              ob1.TokenName == ob2.TokenName;

            if (!tokenNames)
                return false;

            if (ob1.Links is null)
                return true;

            if (ob1.Links.Count != ob2.Links.Count)
                return false;

            bool links = ob1.Links.All(n => ob2.Links.ContainsKey(n.Key) && ob2.Links[n.Key] == n.Value);

            return links;
        }

        public static bool operator !=(FiniteState ob1, FiniteState ob2)
        {
            return !(ob1 == ob2);
        }

    }

    public class FiniteAutomatonParserResult : IParserResult
    {
        public IDictionary<int, IFiniteState> States { get; internal set; }

        public int StartState { get; internal set; }

        public IEnumerable<IParserError> Errors { get; internal set; }
    }
}
