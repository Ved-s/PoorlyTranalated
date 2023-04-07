using System;
using System.Collections.Generic;
using System.Linq;

namespace PoorlyTranslated.TranslationTasks
{
    public class ListStringStorage : StringStorage<int>
    {
        public List<string> List { get; }
        public int Start { get; }
        public int? Length { get; }

        public int Count => Length ?? Math.Max(0, List.Count - Start);

        public override IEnumerable<int> Keys => Enumerable.Range(Start, Count);

        public ListStringStorage(List<string> list, int start = 0, int? length = null)
        {
            List = list;
            Start = start;
            Length = length;
        }

        public override bool TryGet(int key, out string? value)
        {
            value = null;
            if (key < Start || key >= Start + Count || key < 0 || key >= List.Count)
                return false;
            value = List[key];
            return true;
        }

        public override void Set(int key, string value)
        {
            if (key < Start || key >= Start + Count || key < 0 || key >= List.Count)
                return;

            List[key] = value;
        }
    }
}
