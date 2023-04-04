using BepInEx.Logging;
using Menu;
using System.Collections.Generic;
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

        ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("JobRunner");
        StringBuilder StatusBuilder = new();

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
            lock (Lock)
            {
                ProgressDialog?.Update();
                foreach (SyncedJob job in ActiveJobs)
                    job.Update();

                if (ProgressDialog is not null)
                {
                    RainWorld.processManager.fadeToBlack = 1;
                }
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

                StatusBuilder.Clear();

                StatusBuilder.Append(Jobs.Count);
                StatusBuilder.Append(" tasks remaining, ");
                StatusBuilder.Append(Translator.TranslationsDone);
                StatusBuilder.Append(" translations done");

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
                Logger.LogInfo($"Running job {job}");
                UpdateDialogText();
                Task.Run(job.Run).Wait();
                job.TaskCompletion.SetResult(true);
                Logger.LogInfo($"Finished running job {job}");
                job.Running = false;
                job.Runner = null;
                lock (Lock)
                {
                    ActiveJobs.Remove(job);
                }
            }
            UpdateDialogVisibility();
            Logger.LogInfo("Worker thread stopped");
        }
    }

    public class TaskWaitDialog : MenuDialogBox
    {
        private AtlasAnimator loadingSpinner;

        public TaskWaitDialog(Menu.Menu menu, MenuObject owner, string text)
            : base(menu, owner, text,
                  new Vector2(PoorlyTranslated.RainWorld.options.ScreenSize.x / 2f - 240f + (1366f - PoorlyTranslated.RainWorld.options.ScreenSize.x) / 2f, 184f),
                  new Vector2(480f, 400f), false)
        {
            loadingSpinner = new AtlasAnimator(0, new Vector2((float)(int)(pos.x + size.x / 2f) - Menu.Menu.HorizontalMoveToGetCentered(menu.manager), (int)(pos.y + 100f)), "sleep", "sleep", 20, loop: true, reverse: false);
            loadingSpinner.animSpeed = 0.25f;
            loadingSpinner.specificSpeeds = new Dictionary<int, float>();
            loadingSpinner.specificSpeeds[1] = 0.0125f;
            loadingSpinner.specificSpeeds[13] = 0.0125f;
            loadingSpinner.AddToContainer(owner.Container);
        }

        public override void Update()
        {
            base.Update();
            loadingSpinner.Update();
        }

        public override void RemoveSprites()
        {
            base.RemoveSprites();
            loadingSpinner.RemoveFromContainer();
        }

        public void SetText(string caption)
        {
            descriptionLabel.text = caption;
        }
    }
}
