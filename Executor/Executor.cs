using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using O = Core.Optimize;

namespace Executor
{

    internal static class Invoker
    {
        private static readonly Dictionary<string, Func<Stack<O.INode>, Executor, int, Task>> operations =
           new Dictionary<string, Func<Stack<O.INode>, Executor, int, Task>>
           {
                // We omit that left operand must be either declaration or reference.
                // It must be checked on syntax analysis stage.
                ["="] = async (stack, executor, argumentCount) =>
                {
                    var right = (O.IValueHolder)stack.Pop();
                    var left = (O.IValueHolder)stack.Pop();

                    if (!executor.TryGetValue(right, out var value))
                        throw new Exception("Right operand was not declared.");

                    executor.TrySetValue(left, value);
                    stack.Push(left);
                },
                ["<"] = async (stack, executor, argumentCount) =>
                {
                    var resultIndex = stack.Count;

                    var resultType = O.DataType.Int;

                    var right = (O.IValueHolder)stack.Pop();
                    var left = (O.IValueHolder)stack.Pop();

                    if (!executor.TryGetValue(left, out var lValue))
                        throw new Exception("Left operand was not declared.");

                    if (!executor.TryGetValue(right, out var rValue))
                        throw new Exception("Right operand was not declared.");

                    var result = ((int)lValue < (int)rValue) ? 1 : 0;

                    var node = Executor.CreateLiteral(result, resultType, resultIndex);

                    stack.Push(node);
                },
                ["-"] = async (stack, executor, argumentCount) =>
                {
                    var resultIndex = stack.Count;

                    var resultType = O.DataType.Int;

                    var right = (O.IValueHolder)stack.Pop();

                    if (!executor.TryGetValue(right, out var rValue))
                        throw new Exception("Right operand was not declared.");

                    int result;

                    if (argumentCount == 2)
                    {
                        var left = (O.IValueHolder)stack.Pop();

                        if (!executor.TryGetValue(left, out var lValue))
                            throw new Exception("Left operand was not declared.");

                        result = (int)lValue - (int)rValue;
                    }
                    else
                    {
                        result = -(int)rValue;
                    }

                    var node = Executor.CreateLiteral(result, resultType, resultIndex);

                    stack.Push(node);
                },

              

           };

        private static readonly Dictionary<string, Func<Stack<O.INode>, Executor, int, Task>> functions =
            new Dictionary<string, Func<Stack<O.INode>, Executor, int, Task>>
            {
                ["system.io.write"] = async (stack, executor, argumentCount) =>
                {
                    var param = stack.Pop();
                    switch (param)
                    {
                        case O.IValueHolder v:
                            executor.Print(v);
                            return;
                        default:
                            throw new Exception("Non-value-holder object cannot be printed.");
                    }
                },
                ["system.io.read"] = async (stack, executor, argumentCount) =>
                {
                    var param = stack.Pop();
                    switch (param)
                    {
                        case O.IValueHolder v:
                            var s = await executor.ReadAsync();
                            try
                            {
                                if (executor.TrySetValue(v, s))
                                {
                                    stack.Push(param);
                                    return;
                                }
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            throw new Exception("Value could not be set into variable.");
                        default:
                            throw new Exception("Read can only be declaration or reference.");
                    }
                },
            };

        public static async Task Invoke(O.ICall call, Executor executor)
        {
            var stack = executor.stack;

            if (call.CallType == O.CallType.Function)
            {
                if (!functions.ContainsKey(call.Name))
                {
                    Logger.Add("executor", $"Function '{call.Name}' was not found.");
                    throw new Exception("Function was not found.");
                }

                var func = functions[call.Name];
                await func(stack, executor, call.ArgumentNumber);
            }
            else
            {
                if (!operations.ContainsKey(call.Name))
                {
                    Logger.Add("executor", $"Operation '{call.Name}' was not found.");
                    throw new Exception("Operation was not found.");
                }

                var func = operations[call.Name];
                await func(stack, executor, call.ArgumentNumber);
            }
        }
    }

    internal class Executor : IExecutor
    {
        private O.INode[] stream;
        private INode[] grammarNodes;

        private int position;

        public IEnumerable<O.INode> ExecutionNodes { get => stream; set => stream = value.ToArray(); }

        public IEnumerable<INode> GrammarNodes { get => grammarNodes; set => grammarNodes = value.ToArray(); }

        public IEnumerable<IVariable> VisibleVariables => throw new NotImplementedException();

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




        public Task Abort()
        {
            Ended?.Invoke();

            throw new NotImplementedException();
        }

        private void Close()
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
            switch (obj)
            {
                case O.IDeclare v:
                    {
                        if (TryGetValue(v, out var value))
                            Output(value);
                        else
                            Output("None");
                    }
                    break;
                case O.IReference v:
                    {
                        if (TryGetValue(v, out var value))
                            Output(value);
                        else
                            Output("None");
                    }
                    break;
                case O.ILiteral v:
                    {
                        Output(v.Value);
                    }
                    break;
                default:
                    break;
            }
            
        }

        private async Task Jump(O.IJump jump)
        {
            var reference = (O.IReference)stack.Pop();
            switch (jump.JumpType)
            {
                case O.JumpType.Unconditional:
                    position = reference.Address - 1;
                    break;
                case O.JumpType.Positive:
                    {
                        var valueHolder = (O.IValueHolder)stack.Pop();
                        if (TryGetValue(valueHolder, out var value))
                        {
                            if ((int)value != 0)
                            {
                                position = reference.Address - 1;
                            }
                        }
                    }
                    break;
                case O.JumpType.Negative:
                    {
                        var valueHolder = (O.IValueHolder)stack.Pop();
                        if (TryGetValue(valueHolder, out var value))
                        {
                            if ((int)value == 0)
                            {
                                position = reference.Address - 1;
                            }
                        }
                    }
                    break;
                default:
                    throw new Exception("Unsupported jump type.");
            }

        }

        public async Task StepOver()
        {
            if (position >= stream.Length)
            {
                Close();
                return;
            }
            else if (position == 0)
            {
                Started?.Invoke();
                stack.Clear();
            }

            State = State.Running;

            switch (stream[position])
            {
                case O.IDeclare declaration:
                    symbolTable.Declare(declaration);
                    stack.Push(declaration);
                    break;
                case O.IReference reference:
                    stack.Push(reference);
                    break;
                case O.ILiteral literal:
                    stack.Push(literal);
                    break;
                case O.ICall call:
                    await Invoker.Invoke(call, this);
                    break;
                case O.IJump jump:
                    await Jump(jump);
                    break;
                default:
                    throw new Exception("Unsupported execution node.");
            }

            position++;
            State = State.Paused;
        }
    }
}
