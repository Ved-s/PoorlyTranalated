using BepInEx;
using BepInEx.Logging;
using Kittehface.Framework20;
using RWCustom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine;
using Menu;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using MonoMod.RuntimeDetour.HookGen;
using PoorlyTranslated.Jobs;

namespace PoorlyTranslated
{
    [BepInPlugin("ved_s.poorlytranslated", "Poorly Translated Rain World", "1.1.1")]
    public class PoorlyTranslated : BaseUnityPlugin
    {
        public static PoorlyTranslated Instance = null!;
        public static ModManager.Mod Mod = null!;
        public static RainWorld RainWorld = null!;

        public static InitializationScreen.InitializationStep TranslationStep = (InitializationScreen.InitializationStep)1064;
        public static bool Translated;
        public static SyncedJobRunner Runner = null!;

        public static Regex ConversationRegex = new(@"^\d+(-.+)?.txt$", RegexOptions.Compiled);

        /// To fix calling <see cref="OptionsMenu.SetCurrentlySelectedOfSeries(string, int)"/> from task worker thread
        internal static int? MenuLanguageSet = null;

        public PoorlyTranslated()
        {
            Instance = this;
            Patches.Apply();
        }

        public void Update()
        {
            Runner?.Update();
            ThreadedStringsTranslator.Poke();

            if (MenuLanguageSet.HasValue && RainWorld.processManager.currentMainLoop is OptionsMenu options)
            {
                options.SetCurrentlySelectedOfSeries("Language", MenuLanguageSet.Value);
                MenuLanguageSet = null;
            }
        }

        internal static void InitScreenUpdate(InitializationScreen screen, ref InitializationScreen.InitializationStep step)
        {
            if (!Translated)
            {
                if (step == InitializationScreen.InitializationStep.WAIT_FOR_MOD_INIT_ASYNC)
                {
                    Mod = ModManager.ActiveMods.First(m => m.id == "ved_s.poorlytranslated");
                    RainWorld = screen.manager.rainWorld;
                    Runner = new(RainWorld);
                    VerifyTranslations();
                    if (!Runner.HasWork)
                    {
                        Translated = true;
                        typeof(InGameTranslator)
                            .GetMethod("LoadShortStrings", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(RainWorld.inGameTranslator, null);
                    }
                    else
                    {
                        step = TranslationStep;
                    }
                }
                else if (step == TranslationStep)
                {
                    if (Runner?.HasWork is false or null)
                    {
                        Translated = true;
                        step = InitializationScreen.InitializationStep.WAIT_FOR_MOD_INIT_ASYNC;
                        typeof(InGameTranslator)
                            .GetMethod("LoadShortStrings", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(RainWorld.inGameTranslator, null);
                    }
                }
            }
        }

        public static Task VerifyTranslations()
        {
            Task enTask = VerifyLanguageTranslations(InGameTranslator.LanguageID.English);

            if (RainWorld.inGameTranslator.currentLanguage == InGameTranslator.LanguageID.English)
                return enTask;

            return Task.WhenAll(enTask, VerifyLanguageTranslations(RainWorld.inGameTranslator.currentLanguage));
        }

        public static Task VerifyLanguageTranslations(InGameTranslator.LanguageID lang, bool force = false)
        {
            Instance.Logger.LogInfo($"Verifying files for {lang.value}");
            string path = $"text/text_{LocalizationTranslator.LangShort(lang).ToLower()}";

            List<Task> tasks = EnumerateFileNames(path)
                .Select(f => (path: Path.Combine(path, f), name: f))
                .Where(f => force || !File.Exists(Path.Combine(Mod.path, f.path)))
                .Select(f => VerifyFileTranslations(f.path, f.name, lang))
                .ToList();

            return Task.WhenAll(tasks);
        }

        public static Task VerifyFileTranslations(string path, string filename, InGameTranslator.LanguageID lang)
        {
            if (!filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
                return Task.CompletedTask;

            if (filename == "strings.txt")
                return Runner.EnqueueJob(new StringsJob(path, lang));
            
            if (lang == RainWorld.inGameTranslator.currentLanguage)
            {
                if (ConversationRegex.IsMatch(filename))
                    return Runner.EnqueueJob(new ConversationJob(path, lang));

                if (filename.StartsWith("chatlog_", StringComparison.InvariantCultureIgnoreCase))
                    return Runner.EnqueueJob(new ChatlogJob(path, lang, false));

                if (filename.StartsWith("lp_", StringComparison.InvariantCultureIgnoreCase))
                    return Runner.EnqueueJob(new ChatlogJob(path, lang, true));
            }

            return Task.CompletedTask;
        }

        public static string ConvertLanguage(InGameTranslator.LanguageID lang)
        {
            if (lang == InGameTranslator.LanguageID.English)         return "en";
            else if (lang == InGameTranslator.LanguageID.French)     return "fr";
            else if (lang == InGameTranslator.LanguageID.Italian)    return "it";
            else if (lang == InGameTranslator.LanguageID.German)     return "de";
            else if (lang == InGameTranslator.LanguageID.Spanish)    return "es";
            else if (lang == InGameTranslator.LanguageID.Portuguese) return "pt";
            else if (lang == InGameTranslator.LanguageID.Japanese)   return "ja";
            else if (lang == InGameTranslator.LanguageID.Korean)     return "ko";
            else if (lang == InGameTranslator.LanguageID.Russian)    return "ru";
            else if (lang == InGameTranslator.LanguageID.Chinese)    return "zh-CN";

            foreach (string l in Translator.Languages)
                if (lang.value.StartsWith(l, StringComparison.InvariantCultureIgnoreCase))
                    return l;

            return "en";
        }

        public static string? ReadEncryptedFile(string fullPath, int displace)
        {
            if (!File.Exists(fullPath))
                return null;

            string str = File.ReadAllText(fullPath, Encoding.UTF8);
            if (str[0] == '0')
                return str.Remove(0, 1);

            return Custom.xorEncrypt(str, displace).Remove(0, 1);
        }

        public static StreamWriter CreateModFile(string path)
        {
            string fullpath = Path.Combine(Mod.path, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullpath));
            return File.CreateText(fullpath);
        }

        public static void LoadStrings(string path, Dictionary<string, string> dictionary)
        {
            string? text = ReadEncryptedFile(path, 12467);
            if (text is null)
                return;

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

        public static IEnumerable<string> EnumerateFileNames(string path)
        {
            HashSet<string> returnedFileNames = new();

            foreach (string dir in EnumAssetDirs())
            {
                string dirpath = Path.Combine(dir, path);
                if (!Directory.Exists(dirpath))
                    continue;

                foreach (string file in Directory.EnumerateFiles(dirpath))
                {
                    string filename = Path.GetFileName(file);

                    if (returnedFileNames.Contains(filename))
                        continue;
                    returnedFileNames.Add(filename);
                    yield return filename;
                }
            }
        }
        public static IEnumerable<string> ResolveFiles(string path)
        {
            foreach (string dir in EnumAssetDirs())
            {
                string filepath = Path.Combine(dir, path);
                if (File.Exists(filepath))
                    yield return filepath;
            }
        }
        public static string? ResolveFile(string path)
        {
            foreach (string dir in EnumAssetDirs())
            {
                string filepath = Path.Combine(dir, path);
                if (File.Exists(filepath))
                    return filepath;
            }
            return null;
        }
        public static IEnumerable<string> EnumAssetDirs()
        {
            foreach (ModManager.Mod mod in ModManager.ActiveMods)
            {
                if (mod == Mod)
                    continue;

                yield return mod.path;
            }

            yield return Custom.RootFolderDirectory();
        }
    }
}
