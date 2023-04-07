using System.Collections.Generic;

namespace PoorlyTranslated.TranslationTasks
{
    public class DictionaryStringStorage<TKey> : StringStorage<TKey>
    {
        public Dictionary<TKey, string> Dictionary { get; }
        public override IEnumerable<TKey> Keys => Dictionary.Keys;

        public DictionaryStringStorage(Dictionary<TKey, string> dictionary)
        {
            Dictionary = dictionary;
        }

        public override bool TryGet(TKey key, out string value)
        {
            return Dictionary.TryGetValue(key, out value);
        }

        public override void Set(TKey key, string value)
        {
            Dictionary[key] = value;
        }
    }
}
