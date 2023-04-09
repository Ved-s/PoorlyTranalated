using Kittehface.Framework20;
using PoorlyTranslated.TranslationTasks;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace PoorlyTranslated.Jobs
{
    public class StringsJob : SyncedJob
    {
        string FilePath;
        InGameTranslator.LanguageID Language;

        PoorStringProvider? StatusProvider;
        TranslationTaskBatch<string>? Batch;

        public override string Status => (!Running || StatusProvider is null) ? "Job is not running" :
            StatusProvider.Template
            .Replace("<LINE>", "\n")
            .Replace("<Remaining>", Batch?.Remaining.ToString() ?? "Unknown");

        public StringsJob(string filePath, InGameTranslator.LanguageID language)
        {
            FilePath = filePath;
            Language = language;
        }

        public override void Update()
        {
            StatusProvider?.Update();
        }

        public override async Task Run()
        {
            StatusProvider = new($"Translating strings into {Language.value} (<Remaining> strings remaining)", PoorlyTranslated.ConvertLanguage(Language), 120, Cancellation);

            string langGT = PoorlyTranslated.ConvertLanguage(Language);
            Logger.LogInfo($"Strings job started (lang={Language.value}, langgt={langGT})");

            Dictionary<string, string> strings = new();

            foreach (string extStringsPath in PoorlyTranslated.ResolveFiles(FilePath))
                PoorlyTranslated.LoadStrings(extStringsPath, strings);

            Logger.LogInfo($"Loaded {strings.Count} strings");

            Batch = new(strings, langGT, 5);

            Logger.LogInfo($"Waiting for translator to finish...");

            await Batch.Translate(Cancellation);

            Logger.LogInfo($"Writing strings...");

            using StreamWriter writer = PoorlyTranslated.CreateModFile(FilePath);

            writer.Write('0');

            bool first = true;
            foreach (var kvp in strings.OrderBy(kvp => kvp.Key.Length))
            {
                if (!first)
                    writer.Write("\r\n");
                first = false;

                writer.Write(kvp.Key);
                writer.Write("|");
                writer.Write(kvp.Value);
            }
            writer.Flush();
        }
    }
}
