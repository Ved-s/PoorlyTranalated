using PoorlyTranslated.TranslationTasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoorlyTranslated.Jobs
{
    public class ChatlogJob : SyncedJob
    {
        private readonly string FilePath;
        private readonly InGameTranslator.LanguageID Language;
        private readonly string Name;
        private readonly string ShortName;
        private readonly bool IsBroadcast;

        private TranslationTaskBatch<int>? Batch;

        public override string Status => $"Translating {(IsBroadcast? "broadcast" : "chatlog")} {ShortName} ({Batch?.Remaining.ToString() ?? "Unknown"} lines remaining)";

        public ChatlogJob(string filePath, InGameTranslator.LanguageID language, bool broadcast)
        {
            IsBroadcast = broadcast;
            FilePath = filePath;
            Language = language;
            Name = Path.GetFileNameWithoutExtension(FilePath);
            ShortName = 
                Name.StartsWith("chatlog_", StringComparison.InvariantCultureIgnoreCase) ? Name.Substring(8) :
                Name.StartsWith("lp_", StringComparison.InvariantCultureIgnoreCase) ? Name.Substring(3) : 
                Name;
        }

        public override async Task Run()
        {
            int sum = 0;
            for (int i = 0; i < Name.Length; i++)
                sum += Name[i] - '0';
            
            string? filepath = PoorlyTranslated.ResolveFile(FilePath);

            if (filepath is null)
                return;

            string? text = PoorlyTranslated.ReadEncryptedFile(filepath, 54 + sum + PoorlyTranslated.RainWorld.inGameTranslator.currentLanguage.Index * 7);
            if (text is null)
                return;

            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string[] namesplit = lines[0].Split(new[] { '-' }, 2);
            if (namesplit.Length < 2 || !Name.Equals(namesplit[1], StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogWarning($"Malformed chatlog: {Name}");
                return;
            }
            
            Batch = new(new ArrayStringStorage(lines, 1), PoorlyTranslated.ConvertLanguage(Language), 5);
            await Batch.Translate();

            using StreamWriter writer = PoorlyTranslated.CreateModFile(FilePath);
            writer.Write('0');

            foreach (string line in lines)
            {
                writer.Write(line);
                writer.Write("\r\n");
            }
        }
    }
}
