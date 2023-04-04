using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.Jobs;

namespace PoorlyTranslated
{
    public class ThreadedStringsTranslator<TKey>
    {
        Queue<TKey> KeyQueue;
        Dictionary<TKey, string> Dictionary;
        object Lock = new();

        List<Thread> Threads = new();
        int InProgress = 0;

        public int Remaining => KeyQueue.Count + InProgress;

        public string Language;
        public int Iterations;

        public Task<bool> Task => TaskCompletion.Task;
        TaskCompletionSource<bool> TaskCompletion;
        bool TaskSignaled = false;

        public ThreadedStringsTranslator(Dictionary<TKey, string> dictionary, int threads, string language, int iterations)
        {
            Dictionary = dictionary;
            KeyQueue = new(Dictionary.Keys);
            Language = language;
            Iterations = iterations;
            TaskCompletion = new();

            for (int i = 0; i < threads; i++)
            {
                Thread thread = new(ThreadWorker);
                thread.Name = $"Translation worker {i}";
                thread.Start();
                Threads.Add(thread);
            }
        }

        void ThreadWorker()
        {
            Translator translator = new();
            string lang = Language;
            int iter = Iterations;

            while (true)
            {
                TKey key;
                string text;
                lock (Lock)
                {
                    if (KeyQueue.Count == 0)
                        break;
                    key = KeyQueue.Dequeue();
                    Interlocked.Increment(ref InProgress);
                    text = Dictionary[key];
                }
                string newText = translator.PoorlyTranslate(lang, text, iter);

                lock (Lock)
                {
                    Dictionary[key] = newText;
                    Interlocked.Decrement(ref InProgress);
                }
            }
            lock (Lock)
            {
                if (Remaining == 0 && !TaskSignaled)
                {
                    TaskCompletion.SetResult(true);
                    TaskSignaled = true;
                }
            }
        }
    }
}
