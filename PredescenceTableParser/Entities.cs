using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Collections.Generic;
using System.Linq;

namespace PredescenceTableParser
{
    public class PredescenceTableParserResult : IParserResult
    {
        public IEnumerable<IParserError> Errors { get; internal set; }

        public IDictionary<int, IPredescenceNode> Nodes { get; internal set; }

    }

    class PredescenceTablePreview
    {
        public IDictionary<INode, IPredescenceNode> Nodes { get; internal set; }
    }


    class PredescenceNodePreview
    {
        public IDictionary<INode, Relationship> Relashionships { get; internal set; }
    }

    class PredescenceTable : IPredescenceTable
    {
        public IDictionary<int, IPredescenceNode> Nodes { get; internal set; }

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

    class PredescenceNode : IPredescenceNode
    {
        public IDictionary<int, Relationship> Relashionships { get; internal set; }
    }

}
