using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using O = Core.Optimize;
using E = Core.Entity;

namespace Executor
{

    internal class Executor : IExecutor
    {
        private O.INode[] stream;
        private E.INode[] grammarNodes;

        private int position;

        public IEnumerable<O.INode> ExecutionNodes { get => stream; set => stream = value.ToArray(); }

        public IEnumerable<E.INode> GrammarNodes { get => grammarNodes; set => grammarNodes = value.ToArray(); }

        public int[] BreakPositions { get; set; }

        public Action<object> Output { get; set; }

        public State State { get; internal set; }

        public event ExecutionStartedEventHandler Started;
        public event ExecutionEndedEventHandler Ended;
        public event ExecutionInputEventHandler Input;

        private SymbolTable symbolTable = new SymbolTable();

        internal Stack<O.INode> stack = new Stack<O.INode>();




        internal class Literal : O.ILiteral
        {
            public object Value { get; set; }

            public O.DataType DataType { get; set; }

            public int Id { get; set; }
        }

        internal static O.ILiteral CreateLiteral(object value, O.DataType dataType, int id)
        {
            return new Literal
            {
                Value = value,
                DataType = dataType,
                Id = id,
            };
        }




        public async Task Abort()
        {
            await Close();
        }

        private async Task Close()
        {
            position = 0;
            State = State.Idle;
            Ended?.Invoke();
        }

        public async Task Run()
        {
            if (State == State.Idle)
            {
                State = State.Paused;

                while (State == State.Paused)
                {
                    await StepOver();
                }
            }
        }

        private async Task Start()
        {
            Started?.Invoke();
            stack.Clear();
            Logger.Clear("executor");
        }

        internal async Task<string> ReadAsync()
        {
            var oldState = State;

            State = State.Inputting;

            string result = null;

            Input.Invoke(n => result = (string)n);

            while (result == null)
            {
                await Task.Delay(100);
            }

            State = oldState;

            return result;
        }

        internal bool TryGetValue(O.IValueHolder obj, out object value)
        {
            if (obj is O.ILiteral l)
            {
                value = l.Value;
                return true;
            }

            switch (obj)
            {
                case O.IDeclare v:
                    {
                        return symbolTable.TryGet(v, out value);
                    }
                case O.IReference v:
                    {
                        return symbolTable.TryGet((O.IDeclare)stream[v.Address], out value);
                    }
                default:
                    throw new Exception("unsupported object");
            }

        }

        internal bool TrySetValue(O.IValueHolder obj, object value)
        {
            if (obj is O.ILiteral)
            {
                throw new Exception("literal cannot be overriden.");
            }

            switch (obj)
            {
                case O.IDeclare v:
                    {
                        return symbolTable.TrySet(v, value);
                    }
                case O.IReference v:
                    {
                        return symbolTable.TrySet((O.IDeclare)stream[v.Address], value);
                    }
                default:
                    throw new Exception("unsupported object");
            }

        }


        internal void Print(O.IValueHolder obj)
        {
            Output(GetString(obj));
           
        }

        private string GetString(O.IValueHolder obj)
        {
            if (!TryGetValue(obj, out var value))
                throw new Exception("Unsupported ValueHolder.");

            return value?.ToString() ?? "None";
          
        }

        private void Jump(O.IJump jump)
        {
            var reference = (O.IReference)stack.Pop();
            switch (jump.JumpType)
            {
                case O.JumpType.Unconditional:
                    Log("jmp");
                    position = reference.Address - 1;
                    break;
                case O.JumpType.Positive:
                    {
                        var valueHolder = (O.IValueHolder)stack.Pop();
                        if (TryGetValue(valueHolder, out var value) && (int)value != 0)
                        {
                            Log("jp");
                            position = reference.Address - 1;
                        }
                    }
                    break;
                case O.JumpType.Negative:
                    {
                        var valueHolder = (O.IValueHolder)stack.Pop();
                        if (TryGetValue(valueHolder, out var value) && (int)value == 0)
                        {
                            Log("jn");
                            position = reference.Address - 1;
                        }
                    }
                    break;
                default:
                    throw new Exception("Unsupported jump type.");
            }

        }

        internal void Log(string message)
        {
            var values = this.stack
                .OfType<O.IValueHolder>()
                .Select(n => GetString(n));

            var stack = string.Join("|", values);

            Logger.Add("executor", new { Id = position, Mesage = message, Stack = stack });
        }

        public async Task StepOver()
        {
            if (position >= stream.Length)
            {
                await Close();
                return;
            }
            else if (position == 0)
            {
                await Start();
            }

            State = State.Running;

            switch (stream[position])
            {
                case O.IDeclare declaration:
                    Log($"decl:{declaration.Name}");
                    symbolTable.Declare(declaration);
                    stack.Push(declaration);
                    break;
                case O.IReference reference:
                    Log($"&{reference.Address}:{reference.Name}");
                    stack.Push(reference);
                    break;
                case O.ILiteral literal:
                    Log(literal.Value.ToString());
                    stack.Push(literal);
                    break;
                case O.ICall call:
                    await Invoker.Invoke(call, this);
                    break;
                case O.IJump jump:
                    Jump(jump);
                    break;
                default:
                    throw new Exception("Unsupported execution node.");
            }

            position++;
            State = State.Paused;
        }
    }
}
