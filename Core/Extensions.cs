using System;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public static class Extensions
    {
        public static IEnumerable<T> Distinct<T, V>(this IEnumerable<T> collection, Func<T, V> field)
        {
            return collection.GroupBy(n => field(n)).Select(n => n.First());
        }
    }



}
