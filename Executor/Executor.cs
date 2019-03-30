using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Runtime.InteropServices;
using System.Threading;

namespace Executor
{
    //public enum ExecutorState
    //{
    //    // Ready for steps
    //    Idle,
    //    // Input, output
    //    Awaiting,
    //    // Running step
    //    Running,
    //}

    public class Executor : IExecutor
    {
        private IExecutionStreamNode[] stream;

        public IEnumerable<IExecutionStreamNode> ExecutionNodes { set => stream = value.ToArray(); }

        public IEnumerable<INode> GrammarNodes { set; private get; }

        public IEnumerable<IVariable> VisibleVariables => stream.Take(Position).OfType<IVariable>().Distinct();

        public int Position { get; private set; }

        public int[] BreakPositions { get; set; }

        private readonly Stack<IExecutionStreamNode> stack = new Stack<IExecutionStreamNode>();

        public event ExecutionStartedEventHandler Started;
        public event ExecutionEndedEventHandler Ended;


        private bool isAborted;
        private static EventWaitHandle ewh;

        public void Input(object data)
        {
            ewh.WaitOne();
        }

        public void Abort()
        {

            isAborted = true;
            Position = 0;

            stack.Clear();

            Ended?.Invoke();
        }

        public async void Run()
        {
            bool startedFromBreakpoint = breakPositions.Contains(Position);

            while (!isAborted && (breakPositions == null || (startedFromBreakpoint || !breakPositions.Contains(Position))))
            {
                startedFromBreakpoint = false;
                await StepOver();
            }

            if (isAborted)
            {
                isAborted = false;
            }
        }

        public async Task StepOver()
        {
            if (Position >= stream.Length)
            {
                Abort();
                return;
            }
            else if (Position == 0)
            {
                Started?.Invoke();

                isAborted = false;
            }

            var node = stream[Position];

            switch (node)
            {
                case IVariable _:
                case ILiteral _:
                    stack.Push(node);
                    break;
                case IOperator o:
                    var operation = operations[GetNode(o.GrammarNodeId).Name];
                    operation(stack);
                    break;
                case IUserJump uj:
                    {
                        var indices = stream
                            .Select((n, index) => (n, index));

                        var dest = indices
                            .Where(n => n.n is IDefinedLabel && n.index + 1 < stream.Length && !(stream[n.index + 1] is IUserJump));

                        if (!dest.Any())
                        {
                            Abort();
                            return;
                        }


                        Position = dest.First().index;
                        break;
                    }
                case IJumpConditionalNegative j:
                    {
                        var cond = stack.Pop();

                        if (cond is IVariable v && (int)v.Value != 0 || cond is ILiteral l && (int)l.Value != 0)
                        {
                            stack.Push(cond);
                            break;
                        }

                        var indices = stream.Select((n, index) => (n, index));

                        var dest = indices.First(n => n.n == j.Label);

                        if (dest.index == stream.Length - 1)
                        {
                            Abort();
                            return;
                        }
                      

                        Position = dest.index;
                        break;
                    }
                case ICall call:
                    {
                        var action = symbols[call.Address];
                        await action(stack,this);
                        break;
                    }
                default:
                    break;
            }

            Position++;
        }

        private INode GetNode(int id) => GrammarNodes.First(n => n.Id == id);

        private static readonly Dictionary<string, Action<Stack<IExecutionStreamNode>, Executor>> symbols =
           new Dictionary<string, Action<Stack<IExecutionStreamNode>, Executor>>
           {
               ["system.io.write"] = (stack, executor) =>
               {
                   var param = stack.Pop();
                   switch (param)
                   {
                       case IVariable v:
                           if (v.IsAssigned)
                               executor.Write(v.Value.ToString());
                           else
                               executor.Write("None");
                           break;
                       case ILiteral l:
                           Console.WriteLine(l.Value);
                           break;
                       default:
                           throw new Exception("left operand cannot be non-variable type");
                   }
               },
               ["system.io.read"] = (stack, executor) =>
               {
                   var param = stack.Pop();
                   switch (param)
                   {
                       case IVariable v:
                           var s = executor.Read();
                           v.Value = s;
                           stack.Push(param);
                           break;
                      
                       default:
                           throw new Exception("left operand cannot be non-variable type");
                   }
               },
           };


        private static readonly Dictionary<string, Action<Stack<IExecutionStreamNode>>> operations =
            new Dictionary<string, Action<Stack<IExecutionStreamNode>>>
            {
                ["="] = (stack) =>
                {
                    var right = stack.Pop();
                    var left = stack.Pop();
                    switch (right)
                    {
                        case IVariable v:
                            ((IVariable)left).Value = v.Value;
                            stack.Push(left);
                            break;
                        case ILiteral l:
                            ((IVariable)left).Value = l.Value;
                            stack.Push(left);
                            break;
                        default:
                            throw new Exception("left operand cannot be non-variable type");
                    }
                },
            };

       
    }
}
