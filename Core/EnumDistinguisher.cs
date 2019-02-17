using System;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public class EnumDistinguisher<T> where T : Enum
    {
        private IEnumerable<int> Values => Enum.GetValues(typeof(T)).Cast<int>();

        public IEnumerable<T> Distinguish(T obj)
        {
            if (Values.Any(n => n.Equals(0)))
                throw new Exception("Enumeration must not contain item with value = 0");

            return Values.Where(n => (n & Convert.ToInt32(obj)) != 0).Cast<T>();
        }
    }



}
