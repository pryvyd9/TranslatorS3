using System;
using System.Collections.Generic;
using O = Core.Optimize;

namespace Executor
{
    internal class SymbolTable
    {
        private Dictionary<O.DataType, Type> types = new Dictionary<O.DataType, Type>
        {
            [O.DataType.Int] = typeof(int),
            [O.DataType.String] = typeof(string),
        };

        private readonly List<(O.IDeclare obj, object value, int? next)> table =
            new List<(O.IDeclare obj, object value, int? next)>();

        private Dictionary<int, int> hashTable
            = new Dictionary<int, int>();

        private int FreeIndex => table.Count;
        private int LastIndex => table.Count - 1;

        public void Declare(O.IDeclare val)
        {
            var hash = val.GetHashCode();

            if (hashTable.ContainsKey(hash))
            {
                Replace(hashTable[hash], val);
            }
            else
            {
                hashTable[hash] = FreeIndex;
                Add(val);
            }

        }

        private bool TryGet(int index, O.IDeclare obj, out object value)
        {
            if (table[index].obj != obj)
            {
                if (table[index].next != null)
                {
                    return TryGet(table[index].next.Value, obj, out value);
                }
                else
                {
                    value = default;
                    return false;
                }
            }
            else
            {
                value = table[index].value;
                return true;
            }
        }

        private bool TrySet(int index, O.IDeclare obj, object value)
        {
            if (table[index].obj != obj)
            {
                if (table[index].next != null)
                {
                    return TrySet(table[index].next.Value, obj, value);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                var targetType = types[obj.DataType];
                var convertedValue = Convert.ChangeType(value, targetType);

                if (targetType != value.GetType() && convertedValue is null)
                {
                    throw new Exception("Types don't match.");
                }

                table[index] = (table[index].obj, convertedValue, table[index].next);
                return true;
            }
        }


        public bool TryGet(O.IDeclare val, out object value)
        {
            var hash = val.GetHashCode();

            if (hashTable.ContainsKey(hash))
            {
                return TryGet(hashTable[hash], val, out value);
            }
            else
            {
                value = default;
                return false;
            }
        }

        public bool TrySet(O.IDeclare val, object value)
        {
            var hash = val.GetHashCode();

            if (hashTable.ContainsKey(hash))
            {
                return TrySet(hashTable[hash], val, value);
            }
            else
            {
                return false;
            }
        }


        private void Add(O.IDeclare val)
        {
            table.Add((val, null, null));
        }

        private void Replace(int index, O.IDeclare val)
        {
            table.Add(table[index]);
            table[index] = (val, null, LastIndex);
        }

    }
}
