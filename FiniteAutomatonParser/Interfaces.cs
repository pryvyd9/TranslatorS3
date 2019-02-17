using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiniteAutomatonParser
{
    
    public interface IFiniteAutomatonParserResult : Core.IParserResult
    {
        Core.IFiniteAutomaton FiniteAutomaton { get; }
    }
}
