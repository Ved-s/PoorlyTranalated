using BepInEx.Logging;
using PoorlyTranslated.Jobs;
using PoorlyTranslated.UI;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PoorlyTranslated
{
    public class SyncedJobRunner
    {
        public bool HasWork
        {
            get
            {
                lock (Lock)
                {
                    if (Jobs.Count > 0 || ActiveJobs.Count > 0)
                        return true;
                }
                return false;
            }
        }

        readonly RainWorld RainWorld;
        Queue<SyncedJob> Jobs = new();

        TaskWaitDialog? ProgressDialog;
        List<Thread> WorkerThreads = new();

        List<SyncedJob> ActiveJobs = new();
        object Lock = new();
        int Counter;

        static int MaxJobsCount = 10;
        static string StatusTemplate = "<Tasks> tasks remaining, <Translations> strings translated\n<WorkerThreads> worker threads, <ThreadsWMax> max pool threads";

        ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("JobRunner");
        PoorStringProvider? StatusProvider;
        StringBuilder StatusBuilder = new();
        CancellationTokenSource CancellationSource = new();

        public SyncedJobRunner(RainWorld rainWorld)
        {
            RainWorld = rainWorld;
        }

        public Task EnqueueJob(SyncedJob job)
        {
            job.Logger = BepInEx.Logging.Logger.CreateLogSource($"SyncedJob {Counter}");
            job.Cancellation = CancellationSource.Token;
            lock (Lock)
            {
                Jobs.Enqueue(job);
            }
            
            ValidateThread();
            UpdateDialogVisibility();
            Counter++;
            return job.Task;
        }

        public void Update()
        {
            lock (Lock)
            {
                StatusProvider?.Update();
                ProgressDialog?.Update();
                foreach (SyncedJob job in ActiveJobs)
                    job.Update();

                if (ProgressDialog is not null)
                {
                    RainWorld.processManager.fadeToBlack = 1;
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        CancellationSource.Cancel();
                        ThreadedStringsTranslator.Cancel();
                        foreach (Thread thread in WorkerThreads)
                            thread.Interrupt();
                        ActiveJobs.Clear();
                        Jobs.Clear();
                        ProgressDialog.bottomLabel.text = "Cancelling...";
                        UpdateDialogVisibility();
                    }
                }

                if (CancellationSource.IsCancellationRequested && !HasWork && ProgressDialog is null)
                    CancellationSource = new();
            }
            UpdateDialogText();
        }

        void UpdateDialogVisibility()
        {
            Menu.Menu? menu = RainWorld.processManager.currentMainLoop as Menu.Menu;

            bool work = HasWork;
            lock (Lock)
            {
                if (work && ProgressDialog is null && menu is not null)
                {
                    Logger.LogInfo("Creating dialog");
                    ProgressDialog = new TaskWaitDialog(menu, menu.pages[0], "");
                    menu.pages[0].subObjects.Add(ProgressDialog);

                    RainWorld.processManager.fadeSprite = new FSprite("Futile_White", true);
                    RainWorld.processManager.fadeToBlack = 1;

                    StatusProvider = new(StatusTemplate, PoorlyTranslated.ConvertLanguage(RainWorld.inGameTranslator.currentLanguage), 120, CancellationSource.Token);
                }
                else if (!work && ProgressDialog is not null)
                {
                    ProgressDialog.menu.pages[0].subObjects.Remove(ProgressDialog);
                    ProgressDialog.RemoveSprites();
                    ProgressDialog = null;
                    RainWorld.processManager.fadeToBlack = 0;
                    StatusProvider = null;
                }
            }
            UpdateDialogText();
        }

        void UpdateDialogText()
        {
            lock (Lock)
            {
                if (ProgressDialog is null)
                    return;

                StatusBuilder.Clear();

                ThreadPool.GetMaxThreads(out int wmax, out _);
                ThreadPool.GetAvailableThreads(out int wavail, out _);

                StatusBuilder.Append(StatusProvider?.Template
                    .Replace("<Tasks>", Jobs.Count.ToString())
                    .Replace("<Translations>", Translator.TranslationsDone.ToString()) ?? "<empty>")
                    .Replace("<WorkerThreads>", $"{WorkerThreads.Count(t => t.IsAlive)}+{ThreadedStringsTranslator.ThreadsAlive}")
                    .Replace("<ThreadsWMax>", (wmax-wavail).ToString());

                foreach (SyncedJob job in ActiveJobs)
                {
                    string s = job.Status;
                    if (s.Length > 0)
                    {
                        if (StatusBuilder.Length > 0)
                            StatusBuilder.AppendLine();
                        StatusBuilder.Append(s);
                    }
                }

                ProgressDialog.SetText(StatusBuilder.ToString());
            }
        }

        void ValidateThread()
        {
            lock (Lock)
            {
                WorkerThreads.RemoveAll(t => !t.IsAlive);
                int remaining = Jobs.Count;

                while (WorkerThreads.Count < MaxJobsCount && remaining > 0)
                {
                    Thread thread = new(WorkerThreadMethod);
                    thread.Name = "Synced worker";
                    thread.Start();
                    WorkerThreads.Add(thread);
                    remaining--;
                }
            }
        }

        void WorkerThreadMethod()
        {
            try
            {
                while (true)
                {
                    SyncedJob job;
                    lock (Lock)
                    {
                        if (Jobs.Count == 0)
                            break;

                        job = Jobs.Dequeue();
                        ActiveJobs.Add(job);
                    }

                    job.Running = true;
                    job.Runner = this;
                    UpdateDialogText();
                    try
                    {
                        Task.Run(job.Run, CancellationSource.Token).Wait();
                    }
                    catch (TaskCanceledException)
                    {
                        Logger.LogInfo("Worker task canceled");
                    }
                    job.TaskCompletion.SetResult(true);
                    job.Running = false;
                    job.Runner = null;
                    lock (Lock)
                    {
                        ActiveJobs.Remove(job);
                    }
                }
                UpdateDialogVisibility();
                //Logger.LogInfo("Worker thread stopped");
            }
            catch (ThreadInterruptedException) 
            {
                return;
            }
        }
    }
}
