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

        public IEnumerable<O.INode> ExecutionNodes { get => executionNodes; set => executionNodes = value.ToArray(); }

        public IEnumerable<INode> GrammarNodes { get => grammarNodes; set => grammarNodes = value.ToArray(); }

        public IEnumerable<IVariable> VisibleVariables => throw new NotImplementedException();

        public int[] BreakPositions { get; set; }

        public Action<object> Output { get; set; }

        public State State { get; private set; }

        public event ExecutionStartedEventHandler Started;
        public event ExecutionEndedEventHandler Ended;
        public event ExecutionInputEventHandler Input;

        public void Abort()
        {
            throw new NotImplementedException();
        }

        private void InputAction(object data)
        {
            throw new NotImplementedException();
        }

        public void Run()
        {
            throw new NotImplementedException();
        }

        private async Task<string> ReadAsync()
        {
            string result = null;

            Input.Invoke(n => result = (string)n);

            while(result == null)
            {
                await Task.Delay(100);
            }

            return result;
        }


        public async void StepOver()
        {
            Started?.Invoke();

            var h = await ReadAsync();

            Console.WriteLine("here");

            Console.WriteLine(h);


            Ended?.Invoke();

            //Input.Invoke(InputAction);

            //throw new NotImplementedException();
        }
    }
}
