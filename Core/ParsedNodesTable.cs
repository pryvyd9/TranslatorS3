using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public class ParsedNodesTable
    {
        public IEnumerable<IParsedToken> parsedTokens { get; }

        private IEnumerable<IGrouping<int, IParsedToken>> Tables =>
            parsedTokens.Distinct(n => n.Name).GroupBy(n => n.TokenClassId);

        public ParsedNodesTable(IEnumerable<IParsedToken> parsedTokens)
        {
            this.parsedTokens = parsedTokens;
        }

        public IDictionary<string, int> GetTable(int classId)
        {
            return Tables
                .FirstOrDefault(m => m.Key == classId)?
                    .Select((m, j) => (j, m.Name)).ToDictionary(m => m.Name, m => m.j) ??
                    new Dictionary<string, int>();
        }

        public IEnumerable<dynamic> GetTableEntities(int? classId)
        {
            if(classId == null)
            {
                return parsedTokens.Select((n, i) => new { Id = i, n.Name, n.TokenClassId, IdInRespectiveTable = GetTable(n.TokenClassId)[n.Name] });
            }
            else
            {
                return GetTable((int)classId).Select(n => new { Id = n.Value, Name = n.Key });
            }
        }
    }



}
