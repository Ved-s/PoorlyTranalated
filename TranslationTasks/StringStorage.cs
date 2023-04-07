using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;

namespace PoorlyTranslated.TranslationTasks
{
    public abstract class StringStorage<TKey>
    {
        public abstract IEnumerable<TKey> Keys { get; }
        public abstract bool TryGet(TKey key, out string? value);
        public abstract void Set(TKey key, string value);

        public static implicit operator StringStorage<TKey>(Dictionary<TKey, string> dict) => new DictionaryStringStorage<TKey>(dict);
    }
}
