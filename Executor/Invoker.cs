using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core;
using O = Core.Optimize;
using System.Linq;

namespace Executor
{
    internal static class Invoker
    {

        private static async Task<object> Execute(
            Stack<O.INode> stack,
            Executor executor,
            int argumentCount,
            Func<object[], object> func)
        {
            var nodes = new O.IValueHolder[argumentCount];

            for (int i = argumentCount - 1; i >= 0; i--)
            {
                nodes[i] = (O.IValueHolder)stack.Pop();
            }


            var values = new object[argumentCount];

            for (int i = 0; i < argumentCount; i++)
            {
                if (!executor.TryGetValue(nodes[i], out values[i]))
                {
                    var message = $"Operand #{i} was not declared.";
                    executor.Log(message);
                    throw new Exception(message);
                }

                executor.Log($"Operand #{i}={values[i]}");
            }

            var result = func(values);

            executor.Log($"Result ={result}");

            return result;
        }

        private static async Task<object> Execute(
           Stack<O.INode> stack,
           Executor executor,
           int argumentCount,
           Func<object[], O.DataType[], object> func)
        {
            var nodes = new O.IValueHolder[argumentCount];

            for (int i = argumentCount - 1; i >= 0; i--)
            {
                nodes[i] = (O.IValueHolder)stack.Pop();
            }

            var types = nodes.Select(n => n.DataType).ToArray();


            var values = new object[argumentCount];

            for (int i = 0; i < argumentCount; i++)
            {
                if (!executor.TryGetValue(nodes[i], out values[i]))
                {
                    var message = $"Operand #{i} was not declared.";
                    executor.Log(message);
                    throw new Exception(message);
                }

                executor.Log($"Operand #{i}={values[i]}");
            }

            var result = func(values, types);

            executor.Log($"Result ={result}");

            return result;
        }


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
                    {
                        var message = "Right operand was not declared.";
                        executor.Log(message);
                        throw new Exception(message);
                    }

                    executor.TrySetValue(left, value);
                    stack.Push(left);
                },


               #region Ariphmetic

               ["-"] = async (stack, executor, argumentCount) =>
               {
                   object result;

                   if (argumentCount == 1)
                   {
                       object func(object[] args) => -(int)args[0];

                       result = await Execute(stack, executor, argumentCount, func);
                   }
                   else
                   {
                       object func(object[] args) => (int)args[0] - (int)args[1];

                       result = await Execute(stack, executor, argumentCount, func);
                   }


                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               ["+"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] + (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               ["*"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] * (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               ["/"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] / (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },


               ["^"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)Math.Pow((int)args[0], (int)args[1]);

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },

               #endregion

               #region Logic

               ["=="] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] == (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               ["<="] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] <= (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               [">="] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] >= (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               ["<"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => ((int)args[0] < (int)args[1]) ? 1 : 0;

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               [">"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] > (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },

               ["and"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] & (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               ["or"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] | (int)args[1];

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               ["not"] = async (stack, executor, argumentCount) =>
               {
                   object func(object[] args) => (int)args[0] == 0 ? 1 : 0;

                   var result = await Execute(stack, executor, argumentCount, func);

                   stack.Push(Executor.CreateLiteral(result, O.DataType.Int, stack.Count));
               },
               #endregion
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
                            catch
                            {
                                var targetTypeName = Enum.GetName(typeof(O.DataType), v.DataType);
                                executor.Log($"Given value '{s}' cannot be converted to target type '{targetTypeName}'.");
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
                    executor.Log($"Function '{call.Name}' was not found.");
                    throw new Exception("Function was not found.");
                }

                executor.Log($"call@{call.Name}#{call.ArgumentNumber}");

                var func = functions[call.Name];
                await func(stack, executor, call.ArgumentNumber);
            }
            else
            {
                if (!operations.ContainsKey(call.Name))
                {
                    executor.Log($"Operation '{call.Name}' was not found.");
                    throw new Exception("Operation was not found.");
                }

                executor.Log($"{call.Name}#{call.ArgumentNumber}");

                var func = operations[call.Name];
                await func(stack, executor, call.ArgumentNumber);
            }
        }
    }
}
