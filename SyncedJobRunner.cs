using BepInEx.Logging;
using Menu;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PoorlyTranslated
{
    public class SyncedJobRunner
    {
        public SyncedJob? CurrentJob
        {
            get
            {
                lock (Lock) { return currentJob; }
            }
            private set
            {
                lock (Lock) { currentJob = value; }
            }
        }

        public bool HasWork
        {
            get
            {
                if (CurrentJob is not null)
                    return true;

                lock (Lock)
                {
                    if (Jobs.Count > 0)
                        return true;
                }
                return false;
            }
        }

        readonly RainWorld RainWorld;
        Queue<SyncedJob> Jobs = new();

        DialogBoxAsyncWait? ProgressDialog;
        Thread? WorkerThread;

        SyncedJob? currentJob;
        object Lock = new();
        int Counter;

        ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("JobRunner");

        public SyncedJobRunner(RainWorld rainWorld)
        {
            RainWorld = rainWorld;
        }

        public Task EnqueueJob(SyncedJob job)
        {
            Logger.LogInfo($"Enqueueing job {job}");
            lock (Lock)
            {
                Jobs.Enqueue(job);
            }
            job.Logger = BepInEx.Logging.Logger.CreateLogSource($"SyncedJob {Counter}");
            ValidateThread();
            UpdateDialogVisibility();
            Counter++;
            return job.Task;
        }

        public void Update()
        {
            bool updateStatus = false;
            lock (Lock)
            {
                ProgressDialog?.Update();
                currentJob?.Update();

                if (ProgressDialog is not null)
                {
                    RainWorld.processManager.fadeToBlack = 1;
                    updateStatus = currentJob?.UpdateStatusEveryTick is true;
                }
            }
            if (updateStatus)
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
                    ProgressDialog = new DialogBoxAsyncWait(menu, menu.pages[0], "", new Vector2(RainWorld.options.ScreenSize.x / 2f - 240f + (1366f - RainWorld.options.ScreenSize.x) / 2f, 224f), new Vector2(480f, 320f), false);
                    menu.pages[0].subObjects.Add(ProgressDialog);

                    RainWorld.processManager.fadeSprite = new FSprite("Futile_White", true);
                    RainWorld.processManager.fadeToBlack = 1;
                }
                else if (!work && ProgressDialog is not null)
                {
                    ProgressDialog.menu.pages[0].subObjects.Remove(ProgressDialog);
                    ProgressDialog.RemoveSprites();
                    ProgressDialog = null;
                    RainWorld.processManager.fadeToBlack = 0;
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

                string text = currentJob?.Status ?? "";
                if (Jobs.Count > 0)
                    text = $"{Jobs.Count} tasks remaining\n" + text;

                ProgressDialog.SetText(text);
            }
        }

        void ValidateThread()
        {
            if (WorkerThread is null || !WorkerThread.IsAlive)
            {
                WorkerThread = new(WorkerThreadMethod);
                WorkerThread.Name = "Synced worker";
                WorkerThread.Start();
            }
        }

        void WorkerThreadMethod()
        {
            Logger.LogInfo("Worker thread started");
            while (true)
            {
                SyncedJob job;
                lock (Lock)
                {
                    if (Jobs.Count == 0)
                        break;

                    job = Jobs.Dequeue();
                }

                job.Running = true;
                job.Runner = this;
                CurrentJob = job;
                Logger.LogInfo($"Running job {job}");
                UpdateDialogText();
                Task.Run(job.Run).Wait();
                job.TaskCompletion.SetResult(true);
                Logger.LogInfo($"Finished running job {job}");
                CurrentJob = null;
                job.Running = false;
                job.Runner = null;
            }
            UpdateDialogVisibility();
            Logger.LogInfo("Worker thread stopped");
        }
    }
}
