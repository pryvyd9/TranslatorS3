using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using O = Core.Optimize;

namespace Executor
{

    internal class Executor : IExecutor
    {
        private O.INode[] executionNodes;
        private INode[] grammarNodes;

        private int position;

        public IEnumerable<O.INode> ExecutionNodes { get => executionNodes; set => executionNodes = value.ToArray(); }

        public IEnumerable<INode> GrammarNodes { get => grammarNodes; set => grammarNodes = value.ToArray(); }

        public IEnumerable<IVariable> VisibleVariables => throw new NotImplementedException();

        public int[] BreakPositions { get; set; }

        public Action<object> Output { get; set; }

        public State State { get; internal set; }

        public event ExecutionStartedEventHandler Started;
        public event ExecutionEndedEventHandler Ended;
        public event ExecutionInputEventHandler Input;

        private SymbolTable symbolTable = new SymbolTable();



        public void Abort()
        {
            Ended?.Invoke();

            throw new NotImplementedException();
        }

        private void Close()
        {
            Ended?.Invoke();

        }

        public void Run()
        {
            throw new NotImplementedException();
        }

        private async Task<string> ReadAsync()
        {
            State = State.Inputting;

            string result = null;

            Input.Invoke(n => result = (string)n);

            while (result == null)
            {
                await Task.Delay(100);
            }

            State = State.Running;

            return result;
        }


        public async void StepOver()
        {
            if (position == 0)
            {
                Started?.Invoke();
            }

            switch (executionNodes[position])
            {
                case O.IDeclare val:
                    symbolTable.Declare(val);
                    break;
                default:
                    break;
            }

            //var h = await ReadAsync();

            //Console.WriteLine("here");

            //Console.WriteLine(h);

            position++;
        }
    }

    internal class SymbolTable
    {

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
                table[index] = (table[index].obj, value, table[index].next);
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
