using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Core
{


    public delegate void LoggerUpdatedEventHandler(string key, object newValue);

    public static class Logger
    {
        public static event LoggerUpdatedEventHandler LoggerUpdated;

        public static Dictionary<string, ObservableCollection<object>> Log { get; } = new Dictionary<string, ObservableCollection<object>>();

        public static void Add(string key, object value)
        {
            if (!Log.ContainsKey(key))
                Log[key] = new ObservableCollection<object>();

            if(value is string str)
            {
                Log[key].Add(new StringLog(str));
            }
            else
            {
                Log[key].Add(value);
            }

            LoggerUpdated?.Invoke(key, value);
        }

        public static void AddRange(string key, IEnumerable<object> values)
        {
            if (values.Count() == 0)
                return;

            if (!Log.ContainsKey(key))
                Log[key] = new ObservableCollection<object>();

            foreach (var value in values)
            {
                if (value is string str)
                {
                    Log[key].Add(new StringLog(str));
                }
                else
                {
                    Log[key].Add(value);
                }
            }

            LoggerUpdated?.Invoke(key, values.Last());
        }

        public static void Clear(string key)
        {
            if (!Log.ContainsKey(key))
                return;

            Log[key].Clear();
        }

        public struct StringLog
        {
            public string Message { get; }

            public StringLog(string message)
            {
                Message = message;
            }
        }
    }



}
