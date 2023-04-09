using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PoorlyTranslated.Jobs
{
    public abstract class SyncedJob
    {
        public bool Running { get; internal set; }
        public SyncedJobRunner? Runner { get; internal set; }
        public Task Task => TaskCompletion.Task;
        internal TaskCompletionSource<bool> TaskCompletion { get; } = new();
        protected internal ManualLogSource Logger { get; internal set; } = null!;

        protected internal CancellationToken Cancellation = CancellationToken.None;

        public virtual string Status { get; set; } = "";
        public abstract Task Run();

        public virtual void Update() { }
    }
}
