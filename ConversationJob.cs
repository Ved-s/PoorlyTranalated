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

namespace PoorlyTranslated
{
    public class ConversationJob : SyncedJob
    {
        private readonly string FilePath;
        private readonly string ConversationName;
        private readonly InGameTranslator.LanguageID Language;

        public TranslationTaskBatch<int>? Batch;

        public override string Status => $"Translating conversation {ConversationName} ({Batch?.Remaining.ToString() ?? "Unknown"} lines remaining)";

        public ConversationJob(string filePath, InGameTranslator.LanguageID language)
        {
            FilePath = filePath;
            Language = language;
            ConversationName = Path.GetFileNameWithoutExtension(filePath);
        }

        public override async Task Run()
        {
            if (!int.TryParse(ConversationName.Split('-')[0], NumberStyles.Any, CultureInfo.InvariantCulture, out int convId))
                return;

            Dictionary<int, string> replacements = new();
            List<ConvRepl> conv = new();

            int count = 0;

            string? filepath = PoorlyTranslated.ResolveFile(FilePath);
            if (filepath is null)
                return;

            string text = File.ReadAllText(filepath, Encoding.UTF8);
            if (text[0] != '0')
            {
                text = Custom.xorEncrypt(text, 54 + convId + (int)PoorlyTranslated.RainWorld.inGameTranslator.currentLanguage * 7);
            }
            text = text.Substring(1);

            string[] array = Regex.Split(text, "\r\n");
            for (int i = 0; i < array.Length; i++)
            {
                string line = array[i];
                if (i <= 0)
                {
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
                    replacements[count] = line;
                    conv.Add(new(null, count));
                    count++;
                    continue;
                }

                if (index > 0)
                    conv.Add(new(string.Join(" : ", split.Take(index)) + " : "));
                conv.Add(new(null, count));
                if (index < split.Length - 1)
                    conv.Add(new(" : " + string.Join(" : ", split.Skip(index+1))));

                replacements[count] = split[index];
                count++;
            }

            string outpath = Path.Combine(PoorlyTranslated.Mod.path, FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outpath));

            if (replacements.Count > 0)
            {
                Batch = new(replacements, PoorlyTranslated.ConvertLanguage(Language), 5);
                await Batch.Translate();
            }
            
            using FileStream fs = File.Create(outpath);
            using StreamWriter writer = new(fs);

            writer.Write(0);

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
