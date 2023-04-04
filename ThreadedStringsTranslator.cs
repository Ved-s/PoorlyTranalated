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
        private readonly int NumThreads;
        object Lock = new();

        List<Thread> Threads = new();
        int InProgress = 0;

        public int Remaining => KeyQueue.Count + InProgress;

        public string Language;
        public int Iterations;

        public Task<bool> Task => TaskCompletion.Task;
        TaskCompletionSource<bool> TaskCompletion;
        bool TaskSignaled = false;

        static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("Translator");

        public ThreadedStringsTranslator(Dictionary<TKey, string> dictionary, int threads, string language, int iterations) :
            this(dictionary, dictionary.Keys, threads, language, iterations)
        {
            
        }
        public ThreadedStringsTranslator(Dictionary<TKey, string> dictionary, IEnumerable<TKey> keys, int threads, string language, int iterations)
        {
            Dictionary = dictionary;
            NumThreads = threads;
            KeyQueue = new(keys);
            Language = language;
            Iterations = iterations;
            TaskCompletion = new();

            threads = Math.Min(threads, KeyQueue.Count);

            for (int i = 0; i < threads; i++)
            {
                Thread thread = new(ThreadWorker);
                thread.Name = $"Translation worker {i}";
                thread.Start();
                Threads.Add(thread);
            }
        }

        public void Poke()
        {
            Threads.RemoveAll(t => !t.IsAlive);
            int spawnThreads = Math.Min(KeyQueue.Count, NumThreads - Threads.Count);

            for (int i = 0; i < spawnThreads; i++)
            {
                Thread thread = new(ThreadWorker);
                thread.Name = $"Translation worker {i}";
                thread.Start();
                Threads.Add(thread);
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

        void ThreadWorker()
        {
            Translator translator = new();
            string lang = Language;
            int iter = Iterations;

            while (true)
            {
                TKey key = default!;
                bool validKey = false;
                try
                {
                    
                    string text;
                    lock (Lock)
                    {
                        if (KeyQueue.Count == 0)
                            break;
                        key = KeyQueue.Dequeue();
                        validKey = true;
                        Interlocked.Increment(ref InProgress);
                        text = Dictionary[key];
                    }
                    string newText = translator.PoorlyTranslate(lang, text, iter);

                    lock (Lock)
                    {
                        Dictionary[key] = newText;
                        validKey = false;
                        Interlocked.Decrement(ref InProgress);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"{e.GetType().Name}: {e.Message}");
                    if (validKey)
                    {
                        lock (Lock)
                        {
                            KeyQueue.Enqueue(key);
                        }
                        return;
                    }
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
