using System.Collections;
using System.Collections.Generic;

namespace Core
{
    public class Map<T1, T2> : IEnumerable<KeyValuePair<T1,T2>>
    {
        private Dictionary<T1, T2> _forward = new Dictionary<T1, T2>();
        private Dictionary<T2, T1> _reverse = new Dictionary<T2, T1>();

        public Map(){}

        public Map(IEnumerable<(T1, T2)> collection)
        {
            foreach (var (left, right) in collection)
            {
                Add(left, right);
            }
        }

        public void Add(T1 t1, T2 t2)
        {
            _forward.Add(t1, t2);
            _reverse.Add(t2, t1);
        }


        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        {
            return _forward.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T2 Forward(T1 value) => _forward[value];
        public T1 Reverse(T2 value) => _reverse[value];
    }



}
