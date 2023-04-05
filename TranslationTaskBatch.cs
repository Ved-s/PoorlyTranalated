using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PoorlyTranslated
{
    public class TranslationTaskBatch<TKey>
    {
        public Dictionary<TKey, string> Dictionary { get; }
        public string Language { get; }
        public int Iterations { get; }
        public int Remaining => RemainingKeys.Count;

        internal HashSet<TKey> RemainingKeys;
        internal object Lock = new();
        TaskCompletionSource<bool> TaskCompletion = new();
        bool Completed = false;

        public TranslationTaskBatch(Dictionary<TKey, string> dictionary, string language, int iterations) : 
            this(dictionary, dictionary.Keys, language, iterations) { }
        public TranslationTaskBatch(Dictionary<TKey, string> dictionary, IEnumerable<TKey> keys, string language, int iterations)
        {
            Dictionary = dictionary;
            RemainingKeys = new(keys);
            Language = language;
            Iterations = iterations;
        }

        public async Task Translate()
        {
            if (RemainingKeys.Count == 0)
                return;

            ThreadedStringsTranslator.AddTasks(RemainingKeys.Select(k => new TranslationTask<TKey>(this, k)));

            await TaskCompletion.Task;
        }

        internal void SetResult(TKey key, string str)
        {
            lock (Lock) 
            {
                if (!RemainingKeys.Contains(key))
                    return;

                RemainingKeys.Remove(key);
                Dictionary[key] = str;

                if (RemainingKeys.Count == 0 && !Completed)
                {
                    Completed = true;
                    TaskCompletion.SetResult(true);
                }
            }
        }
    }
}
