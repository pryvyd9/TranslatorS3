using System;
using Core;
using System.Dynamic;
using ImpromptuInterface;

namespace Executor
{
    public class ExecutorProxy : DynamicObject
    {
        private IExecutor origin;

        public event ExecutionStartedEventHandler Started
        {
            add => origin.Started += value;
            remove => origin.Started -= value;
        }
        public event ExecutionEndedEventHandler Ended
        {
            add => origin.Ended += value;
            remove => origin.Ended -= value;
        }
        public event ExecutionInputEventHandler Input
        {
            add => origin.Input += value;
            remove => origin.Input -= value;
        }

        private ExecutorProxy(IExecutor origin)
        {
            this.origin = origin;
        }

        public static IExecutor Create()
        {
            return new ExecutorProxy(new Executor()).ActLike<IExecutor>();
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            switch (binder.Name)
            {
                case "Input":
                    if (origin.State != State.Inputting)
                    {
                        throw new Exception("Executor must await input.");
                    }
                    break;
                case "Run":
                case "StepOver":
                    if (origin.State != State.Idle)
                    {
                        throw new Exception("Executor must be idle.");
                    }
                    break;
                default:
                    break;
            }

            try
            {
                result = typeof(IExecutor).GetMethod(binder.Name).Invoke(origin, args);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                result = null;
                return false;
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            try
            {
                result = typeof(IExecutor).GetProperty(binder.Name).GetValue(origin);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                result = null;
                return false;
            }
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (origin.State != State.Idle)
            {
                throw new Exception("Executor is busy.");
            }

            try
            {
                typeof(IExecutor).GetProperty(binder.Name).SetValue(origin, value);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

    } 
}
