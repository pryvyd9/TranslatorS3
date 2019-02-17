using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace PredescenceTableParser
{
    class PredescenceTableParser : IParser<PredescenceTableParserResult>
    {
        private readonly INodeCollection nodes;

        public PredescenceTableParser(INodeCollection nodes)
        {
            this.nodes = nodes;
        }

        public PredescenceTableParserResult Parse()
        {
            var equals = FindEquals();

            var lowerThan = EqualsLowerThanFirstThrusting(equals);

            var greaterThan = EqualsLastThrustingGreaterThanFirstThrusting(equals);

            CheckConsistency(equals, lowerThan, greaterThan);


            var nodes = FillPreviewTable(equals, lowerThan, greaterThan);

            var previewTable = new PredescenceTablePreview
            {
                Nodes = nodes.ToDictionary(n => n.Key, n => n.Value as IPredescenceNode),
            };


            //var tt = previewTable.GetRelashionship(Nodes.Terminals.First(n => n.Name == "{"),Nodes.Axiom);


            return new PredescenceTableParserResult
            {
                Nodes = nodes.ToDictionary(
                    n => n.Key.Id,
                    n => new PredescenceNode
                    {
                        Relashionships = n.Value.Relashionships.ToDictionary(m => m.Key.Id, m => m.Value)
                    } as IPredescenceNode
                ),
            };

        }


        private bool IsAcceptableNontoken(INode node)
        {
            return node is IMedium && !(node is IDefinedToken);
        }



        private void CheckConsistency(HashSet<(INode, INode)> equals, HashSet<(INode, INode)> lower, HashSet<(INode, INode)> greater)
        {
         
            foreach (var lowerItem in lower)
            {
                if (equals.Contains(lowerItem))
                {
                    string message = $"Node '{lowerItem.Item1}' has ambigous relationship with '{lowerItem.Item2}' (= and <).";
                    Logger.Add("predescenceParser", message);
                }
            }

            foreach (var greaterItem in greater)
            {
                if (equals.Contains(greaterItem))
                {
                    string message = $"Node '{greaterItem.Item1}' has ambigous relationship with '{greaterItem.Item2}' (= and >).";
                    Logger.Add("predescenceParser", message);
                }
            }

            foreach (var lowerItem in lower)
            {
                if (greater.Contains(lowerItem))
                {
                    string message = $"Node '{lowerItem.Item1}' has ambigous relationship with '{lowerItem.Item2}'(< and >).";
                    Logger.Add("predescenceParser", message);
                }
            }

        }



        private Dictionary<INode, PredescenceNodePreview> FillPreviewTable(HashSet<(INode, INode)> equals, HashSet<(INode, INode)> lower, HashSet<(INode, INode)> greater)
        {
            var table = new Dictionary<INode, PredescenceNodePreview>();

            AddRelationShips(equals, Relationship.Equal);
            AddRelationShips(lower, Relationship.Lower);
            AddRelationShips(greater, Relationship.Greater);

            return table;

            void AddRelationShips(HashSet<(INode, INode)> hashSet, Relationship relashionship)
            {
                foreach (var (left, right) in hashSet)
                {
                    if (table.ContainsKey(left))
                    {
                        if (table[left].Relashionships.ContainsKey(right))
                        {
                            table[left].Relashionships[right] |= relashionship;
                        }
                        else
                        {
                            table[left].Relashionships[right] = relashionship;
                        }
                    }
                    else
                    {
                        table[left] = new PredescenceNodePreview()
                        {
                            Relashionships = new Dictionary<INode, Relationship>
                            {
                                [right] = relashionship,
                            },
                        };
                    }
                }
            }
        }



        private HashSet<(INode, INode)> FindEquals()
        {
            HashSet<(INode, INode)> hashSet = new HashSet<(INode, INode)>();

            foreach (var node in nodes.Where(n=>IsAcceptableNontoken(n)).OfType<IMedium>())
            {
                foreach (var @case in node.Cases)
                {
                    for(int i = 0; i < @case.Count() - 1; i++)
                    {
                        hashSet.Add((@case.ElementAt(i), @case.ElementAt(i + 1)));
                    }
                }
            }

            return hashSet;
        }


        private HashSet<(INode, INode)> EqualsLowerThanFirstThrusting(HashSet<(INode, INode)> equals)
        {
            var lowerThan = new HashSet<(INode, INode)>();

            foreach (var (left, right) in equals)
            {
                if (IsAcceptableNontoken(right))
                {
                    var rightFirst = FirstThrusting(right as IMedium, new HashSet<INode>());

                    lowerThan.UnionWith(rightFirst.Select(n => (left, n)));
                }
            }

            return lowerThan;
        }

        private HashSet<(INode, INode)> EqualsLastThrustingGreaterThanFirstThrusting(HashSet<(INode, INode)> equals)
        {
            var greaterThan = new HashSet<(INode, INode)>();

            foreach (var (left, right) in equals)
            {
                if (IsAcceptableNontoken(left))
                {
                    var leftLast = LastThrusting(left as IMedium, new HashSet<INode>());
                    greaterThan.UnionWith(leftLast.Select(leftItem => (leftItem, right)));

                    if (IsAcceptableNontoken(right))
                    {
                        var rightFirst = FirstThrusting(right as IMedium, new HashSet<INode>());
                        //var leftLast = LastThrusting(left as IMedium, new HashSet<INode>());

                        greaterThan.UnionWith(rightFirst.SelectMany(rightItem => leftLast.Select(leftItem => (leftItem, rightItem))));
                    }
                    //else
                    //{
                    //    var leftLast = LastThrusting(left as IMedium, new HashSet<INode>());

                    //    lowerThan.UnionWith(leftLast.Select(leftItem => (leftItem, right)));
                    //}
                }
                
            }

            return greaterThan;
        }



        private HashSet<INode> FirstThrusting(IMedium factor, HashSet<INode> hashSet)
        {
            var first = factor.Cases.Where(n => n.Count() > 0).Select(n => n.First());

            foreach (var firstItem in first)
            {
                if (hashSet.Contains(firstItem))
                    continue;

                hashSet.Add(firstItem);

                if (IsAcceptableNontoken(firstItem))
                {
                    FirstThrusting((IMedium)firstItem, hashSet);
                }

            }

            return hashSet;
        }

        private HashSet<INode> LastThrusting(IMedium medium, HashSet<INode> hashSet)
        {
            var last = medium.Cases.Where(n => n.Count() > 0).Select(n=>n.Last());

            foreach (var lastItem in last)
            {
                if (hashSet.Contains(lastItem))
                    continue;

                hashSet.Add(lastItem);

                if (IsAcceptableNontoken(lastItem))
                {
                    LastThrusting(lastItem as IMedium, hashSet);
                }
            }

            return hashSet;
        }

        object IParser.Parse()
        {
            return Parse();
        }
    }
}
