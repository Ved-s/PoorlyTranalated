using System;
using System.Collections.Generic;
using System.Linq;

namespace PoorlyTranslated.TranslationTasks
{
    public class ArrayStringStorage : StringStorage<int>
    {
        public string[] Array { get; }
        public int Start { get; }
        public int? Length { get; }

        public int Count => Length ?? Math.Max(0, Array.Length - Start);

        public override IEnumerable<int> Keys => Enumerable.Range(Start, Count);

        public ArrayStringStorage(string[] array, int start = 0, int? length = null)
        {
            Array = array;
            Start = start;
            Length = length;
        }

        public override bool TryGet(int key, out string? value)
        {
            value = null;
            if (key < Start || key >= Start + Count || key < 0 || key >= Array.Length)
                return false;
            value = Array[key];
            return true;
        }

        public override void Set(int key, string value)
        {
            if (key < Start || key >= Start + Count || key < 0 || key >= Array.Length)
                return;

            Array[key] = value;
        }
    }
}
