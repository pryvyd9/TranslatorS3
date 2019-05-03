using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Entity;

namespace FiniteAutomatonParser
{
    
    public interface IFiniteAutomatonParserResult : Core.IParserResult
    {
        IFiniteAutomaton FiniteAutomaton { get; }
    }
}
