﻿using BepInEx.Logging;
using PoorlyTranslated.TranslationTasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.Jobs;

namespace PoorlyTranslated
{
    public static class ThreadedStringsTranslator
    {
        static List<TranslationTask> Tasks = new();

        private readonly static int NumThreads = 32;
        static object Lock = new();

        static List<Thread> Threads = new();
        static Random Random = new();

        static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("Translator");

        public static void Poke()
        {
            Threads.RemoveAll(t => !t.IsAlive);
            int spawnThreads = Math.Min(Tasks.Count, NumThreads - Threads.Count);

            for (int i = 0; i < spawnThreads; i++)
            {
                Thread thread = new(ThreadWorker);
                thread.Name = $"Translation worker {i}";
                thread.Start();
                Threads.Add(thread);
            }
        }

        public static void AddTasks(IEnumerable<TranslationTask> tasks)
        {
            lock (Lock)
            {
                Tasks.AddRange(tasks);
            }
            Poke();
        }

        static void ThreadWorker()
        {
            Translator translator = new();
            while (true)
            {
                TranslationTask task = default!;
                bool validTask = false;
                try
                {
                    lock (Lock)
                    {
                        if (Tasks.Count == 0)
                            break;

                        int index = Random.Next(Tasks.Count);
                        task = Tasks[index];
                        Tasks.RemoveAt(index);
                        validTask = true;
                    }

                    string? text = task.Text;
                    if (text is null)
                        continue;

                    string newText = translator.PoorlyTranslate(task.Language,text, task.Iterations);

                    task.SetResult(newText);
                }
                catch (Exception e)
                {
                    Logger.LogError($"{e.GetType().Name}: {e.Message}");
                    if (validTask && task.Active)
                    {
                        lock (Lock)
                        {
                            Tasks.Add(task);
                        }
                    }
                }
            }
        }
    }
}
