using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace PoorlyTranslated
{
    public class StringsJob : SyncedJob
    {
        string FilePath;
        InGameTranslator.LanguageID Language;

        ThreadedStringsTranslator<string>? Translator;
        PoorStringProvider StatusProvider;

        public override bool UpdateStatusEveryTick => true;

        public override string Status => !Running ? "Job is not running" : StatusProvider.Template
            .Replace("<LINE>", "\n")
            .Replace("<Remaining>", Translator?.Remaining.ToString() ?? "Unknown");

        public StringsJob(string filePath, InGameTranslator.LanguageID language)
        {
            StatusProvider = new($"Translating strings for {language.value}...<LINE><Remaining> strings remaining", PoorlyTranslated.ConvertLanguage(language), 120);

            FilePath = filePath;
            Language = language;
        }

        public override void Update()
        {
            StatusProvider.Update();
        }

        public override async Task Run()
        {
            string langGT = PoorlyTranslated.ConvertLanguage(Language);
            Logger.LogInfo($"Strings job started (lang={Language.value}, langgt={langGT})");

            Dictionary<string, string> strings = new();

            foreach (string extStringsPath in PoorlyTranslated.ResolveFiles(FilePath))
                LoadStrings(extStringsPath, strings);

            Logger.LogInfo($"Loaded {strings.Count} strings");

            Translator = new(strings, 32, langGT, 5);

            Logger.LogInfo($"Waiting for translator to finish...");

            await Translator.Task;

            Logger.LogInfo($"Writing strings...");

            string filepath = Path.Combine(PoorlyTranslated.Mod.path, FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filepath));

            using FileStream fs = File.Create(filepath);
            using StreamWriter writer = new(fs);

            writer.Write("0");

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

        public static void LoadStrings(string path, Dictionary<string, string> dictionary)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (text[0] == '1')
            {
                text = Custom.xorEncrypt(text, 12467);
            }
            else if (text[0] == '0')
            {
                text = text.Remove(0, 1);
            }
            string[] array = Regex.Split(text, "\r\n");
            for (int j = 0; j < array.Length; j++)
            {
                if (array[j].Contains("///"))
                {
                    array[j] = array[j].Split('/')[0].TrimEnd(Array.Empty<char>());
                }
                string[] array2 = array[j].Split('|');
                if (array2.Length >= 2 && !string.IsNullOrEmpty(array2[1]))
                {
                    dictionary[array2[0]] = array2[1];
                }
            }
        }
    }
}
