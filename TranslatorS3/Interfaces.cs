using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace TranslatorS3
{
    internal interface IEntity
    {
        bool Load();
        bool Save();
        bool Parse(IParser parser);
    }
}
