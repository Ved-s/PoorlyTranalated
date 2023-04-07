using PoorlyTranslated.TranslationTasks;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace PoorlyTranslated.Jobs
{
    public class ConversationJob : SyncedJob
    {
        private readonly string FilePath;
        private readonly string Name;
        private readonly InGameTranslator.LanguageID Language;

        public TranslationTaskBatch<int>? Batch;

        public override string Status => $"Translating conversation {Name} ({Batch?.Remaining.ToString() ?? "Unknown"} lines remaining)";

        public ConversationJob(string filePath, InGameTranslator.LanguageID language)
        {
            FilePath = filePath;
            Language = language;
            Name = Path.GetFileNameWithoutExtension(filePath);
        }

        public override async Task Run()
        {
            if (!int.TryParse(Name.Split('-')[0], NumberStyles.Any, CultureInfo.InvariantCulture, out int convId))
                return;

            List<string> replacements = new();
            List<ConvRepl> conv = new();

            string? filepath = PoorlyTranslated.ResolveFile(FilePath);
            if (filepath is null)
                return;

            string? text = PoorlyTranslated.ReadEncryptedFile(filepath, 54 + convId + (int)PoorlyTranslated.RainWorld.inGameTranslator.currentLanguage * 7);

            if (text is null)
                return;

            string[] array = Regex.Split(text, "\r\n");
            for (int i = 0; i < array.Length; i++)
            {
                string line = array[i];
                if (i <= 0)
                {
                    string[] namesplit = line.Split(new[] { '-' }, 2);
                    if (namesplit.Length < 2 || !namesplit[1].Equals(Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Logger.LogWarning($"Malformed comversation: {Name}");
                        return;
                    }

                    // Conversation ID
                    conv.Add(new(line));
                    continue;
                }
                else
                {
                    conv.Add(new("\r\n"));
                }

                if (string.IsNullOrWhiteSpace(line) || line.Length == 0)
                    continue;

                string[] split = Regex.Split(line, " : ");
                int index = DetermineTranstaleableTextIndex(split);
                if (index < 0 || split.Length == 1)
                {
                    conv.Add(new(null, replacements.Count));
                    replacements.Add(line);
                    continue;
                }

                if (index > 0)
                    conv.Add(new(string.Join(" : ", split.Take(index)) + " : "));

                conv.Add(new(null, replacements.Count));
                replacements.Add(split[index]);

                if (index < split.Length - 1)
                    conv.Add(new(" : " + string.Join(" : ", split.Skip(index + 1))));
            }

            if (replacements.Count > 0)
            {
                Batch = new(new ListStringStorage(replacements), PoorlyTranslated.ConvertLanguage(Language), 5);
                await Batch.Translate();
            }

            using StreamWriter writer = PoorlyTranslated.CreateModFile(FilePath);

            writer.Write('0');

            foreach (ConvRepl repl in conv)
            {
                if (repl.Text is not null)
                    writer.Write(repl.Text);
                else
                    writer.Write(replacements[repl.ReplIndex]);
            }
        }

        record struct ConvRepl(string? Text, int ReplIndex = -1);

        static int DetermineTranstaleableTextIndex(string[] strings)
        {
            if (strings.Length < 1)
                return -1;

            else if (strings.Length == 1)
                return 0;

            int index = -1;

            bool flag = false;
            bool flag2 = false;

            for (int i = 0; i < strings.Length; i++)
            {
                int num;
                if (i == 0)
                {
                    if (strings[i] == "PEBBLESWAIT" || strings[i] == "SPECEVENT")
                    {
                    }
                    else if (int.TryParse(strings[i], NumberStyles.Any, CultureInfo.InvariantCulture, out num))
                    {
                        flag = true;
                    }
                    else
                    {
                        index = i;
                    }
                }
                else if (i != strings.Length - 1)
                {
                    if (flag && !flag2 && int.TryParse(strings[i], NumberStyles.Any, CultureInfo.InvariantCulture, out num))
                    {
                        flag2 = true;
                    }
                    else
                    {
                        index = i;
                    }
                }
                else if (flag && !flag2 && int.TryParse(strings[i], NumberStyles.Any, CultureInfo.InvariantCulture, out num))
                {
                    flag2 = true;
                }
                else
                {
                    index = i;
                }
            }
            return index;
        }
    }
}
