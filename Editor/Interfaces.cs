using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor
{
    public interface IDocument
    {
        string Name { get; set; }

        string Content { get; set; }
    }
}
