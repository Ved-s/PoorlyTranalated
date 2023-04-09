using BepInEx.Logging;
using PoorlyTranslated.TranslationTasks;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public static int ThreadsAlive => Threads.Count(t => t.IsAlive);

        public static void Poke()
        {
            lock (Lock)
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
        }

        public static void Cancel()
        {
            lock (Lock)
            {
                Tasks.Clear();
                foreach (Thread thread in Threads)
                    thread.Interrupt();
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

                        while (Tasks.Count > 0)
                        {
                            int index = Random.Next(Tasks.Count);
                            task = Tasks[index];
                            Tasks.RemoveAt(index);
                            validTask = true;

                            if (task.Active && !task.Cancellation.IsCancellationRequested)
                                break;
                        }
                    }

                    Logger.LogInfo($"Starting translation with cancellable token: {task.Cancellation.CanBeCanceled}, canceled: {task.Cancellation.IsCancellationRequested}");

                    string? text = task.Text;
                    if (text is null)
                        continue;

                    int iterations = task.Iterations;
                    if (iterations == 0)
                    {
                        Logger.LogWarning("Translation task had 0 iterations. Setting to 5.");
                        iterations = 5;
                    }

                    int attempts = 0;
                    string? newText = "";
                    while (!task.Cancellation.IsCancellationRequested)
                    {
                        if (attempts >= 5)
                        {
                            Logger.LogWarning($"Failed to translate to {task.Language}: {text}");
                            newText = text;
                            break;
                        }

                        newText = translator.PoorlyTranslate(task.Language, text, iterations, task.Cancellation).Result;
                        attempts++;
                        if (newText is not null && !text.Equals(newText, StringComparison.InvariantCultureIgnoreCase))
                            break;
                    }
                    if (task.Cancellation.IsCancellationRequested)
                        continue;

                    task.SetResult(newText ?? "");
                }

                catch (AggregateException a) when (a.InnerException is OperationCanceledException)
                {
                    continue;
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (ThreadInterruptedException) 
                {
                    return;
                }
                catch (Exception e)
                {
                    if (task.Cancellation.IsCancellationRequested)
                        continue;

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
