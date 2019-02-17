using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfigView
{
    public interface IOptionAttribute
    {
        string Name { get; }
        bool RequiresRestart { get; }
    }

    public interface ICategoryAttribute
    {
        string Name { get; }
    }
}
