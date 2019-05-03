using System.Collections.Generic;
using Core;
using Core.Entity;

namespace GrammarParser
{
    class NodeComparer : Comparer<INode>
    {

        public override int Compare(INode x, INode y)
        {
            return GetValue(x).CompareTo(GetValue(y));
        }

        int GetValue(INode v)
        {
            if (v is ITerminal)
                return 0;
            else if (v is IDefinedToken)
                return 1;
            else if (v is IClass)
                return 2;
            else
                return 3;
        }
    }

}

    

