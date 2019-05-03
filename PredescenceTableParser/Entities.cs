using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Collections.Generic;
using System.Linq;
using E = Core.Entity;

namespace PredescenceTableParser
{
    public class PredescenceTableParserResult : IParserResult
    {
        public IEnumerable<IParserError> Errors { get; internal set; }

        public IDictionary<int, E.IPredescenceNode> Nodes { get; internal set; }

    }

    class PredescenceTablePreview
    {
        public IDictionary<E.INode, E.IPredescenceNode> Nodes { get; internal set; }
    }


    class PredescenceNodePreview
    {
        public IDictionary<E.INode, Relationship> Relashionships { get; internal set; }
    }

    class PredescenceTable : E.IPredescenceTable
    {
        public IDictionary<int, E.IPredescenceNode> Nodes { get; internal set; }

        public IEnumerable<Relationship> DistinguishRelasionship(Relationship relashionship)
        {
            if(relashionship == Relationship.Undefined)
            {
                return new List<Relationship>();
            }

            return ((IEnumerable<Relationship>)Enum.GetValues(typeof(Relationship)))
                .Select(n => n & relashionship).Where(n => n != Relationship.Undefined);
        }

        public Relationship GetRelashionship(int left, int right)
        {
            if (!Nodes.ContainsKey(left) ||
                !Nodes[left].Relashionships.ContainsKey(right))
                return Relationship.Undefined;

            return Nodes[left].Relashionships[right];
        }
    }

    class PredescenceNode : E.IPredescenceNode
    {
        public IDictionary<int, Relationship> Relashionships { get; internal set; }
    }

}
