using System;
using System.Collections.Generic;
using Core;
using System.Collections;
using System.Linq;
using Core.Entity;

namespace TranslatorS3.Entities
{
    class NodeCollection : INodeCollection
    {
        internal List<INode> SortedNodes { private get; set; }

        internal List<INode> UnsortedNodes { private get; set; }


        public IEnumerable<INode> Unsorted => UnsortedNodes;

        public IEnumerable<ITerminal> Terminals => SortedNodes.OfType<ITerminal>();

        public IEnumerable<INonterminal> Nonterminals => SortedNodes.OfType<INonterminal>();

        public IEnumerable<IDefinedToken> Tokens => SortedNodes.OfType<IDefinedToken>();

        public IEnumerable<IMedium> Mediums => SortedNodes.OfType<IMedium>();

        public IEnumerable<IClass> Classes => SortedNodes.OfType<IClass>();

        public IEnumerable<IFactor> Factors => SortedNodes.OfType<IFactor>();

        public IMedium Axiom { get; internal set; }

        public IMedium ExpressionRoot { get; internal set; }


        public int Count => SortedNodes.Count;


        public IEnumerator<INode> GetEnumerator()
        {
            return SortedNodes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return SortedNodes.GetEnumerator();
        }
    }
}

    

