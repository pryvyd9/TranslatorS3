using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace TranslatorS3.Entities
{
    class PredescenceTable : IPredescenceTable, IEntity
    {
        public IDictionary<int, IPredescenceNode> Nodes { get; internal set; }

        public IEnumerable<Relationship> DistinguishRelasionship(Relationship relashionship)
        {
            if (relashionship == Relationship.Undefined)
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

        public bool Load()
        {
            throw new NotImplementedException();
        }

        public bool Parse(IParser parser)
        {
            dynamic result = parser.Parse();

            try
            {
                Nodes = result.Nodes;
                //Nodes = (result as Pre).Nodes;

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
            throw new NotImplementedException();
        }
    }
}
