using PoorlyTranslated.TranslationTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PoorlyTranslated
{
    public abstract class TranslationTask
    {
        public abstract bool Active { get; }
        public abstract string? Text { get; }
        public abstract string Language { get; }
        public abstract int Iterations { get; }

        public abstract void SetResult(string str);
    }

    public class TranslationTask<TKey> : TranslationTask
    {
        public TranslationTaskBatch<TKey> Batch;
        private readonly TKey Key;

        public TranslationTask(TranslationTaskBatch<TKey> batch, TKey key) : base()
        {
            Batch = batch;
            Key = key;
        }

        public override string? Text { get { lock (Batch.Lock) { return Batch.Storage.TryGet(Key, out string v) ? v : null; } } }
        public override string Language => Batch.Language;
        public override int Iterations => Batch.Iterations;

        public override bool Active { get { lock (Batch.Lock) { return Batch.RemainingKeys.Count > 0 && Batch.RemainingKeys.Contains(Key); } } }

        public override void SetResult(string str)
        {
            Batch.SetResult(Key, str);
        }
    }
}
