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

namespace PoorlyTranslated
{
    [BepInPlugin("ved_s.poorlytranslated", "Poorly Translated Rain World", "0.3")]
    public class PoorlyTranslated : BaseUnityPlugin
    {
        public static PoorlyTranslated Instance = null!;
        public static ModManager.Mod Mod = null!;
        public static RainWorld RainWorld = null!;

        public static InitializationScreen.InitializationStep TranslationStep = (InitializationScreen.InitializationStep)1064;
        public static bool Translated;
        public static SyncedJobRunner Runner = null!;

        public static FieldWrapper<InitializationScreen.InitializationStep> CurreniInitializationStepField = new(null, typeof(InitializationScreen), "currentStep");

        public static Regex ConversationRegex = new(@"^\d+(-.+)?.txt$", RegexOptions.Compiled);

        public PoorlyTranslated()
        {
            Instance = this;
            On.Menu.InitializationScreen.Update += InitializationScreen_Update;
            On.Menu.OptionsMenu.SetCurrentlySelectedOfSeries += OptionsMenu_SetCurrentlySelectedOfSeries;
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            MachineConnector.SetRegisteredOI("ved_s.poorlytranslated", new TranslationsOI());
        }

        public void Update()
        {
            Runner?.Update();
        }

        private void OptionsMenu_SetCurrentlySelectedOfSeries(On.Menu.OptionsMenu.orig_SetCurrentlySelectedOfSeries orig, OptionsMenu self, string series, int to)
        {
            if (series == "Language")
            {
                InGameTranslator.LanguageID prevlang = RainWorld.options.language;
                InGameTranslator.LanguageID lang = InGameTranslator.LanguageID.Parse(to);
                RainWorld.options.language = lang;
                InGameTranslator.LoadFonts(lang, null);

                Task t = VerifyLanguageTranslations(lang);
                if (!t.IsCompleted)
                {
                    Task.Run(async () =>
                    {
                        await t;
                        RainWorld.options.language = prevlang;
                        self.SetCurrentlySelectedOfSeries(series, to);
                    });
                    return;
                }
                RainWorld.options.language = prevlang;
            }
            orig(self, series, to);
        }

        private void InitializationScreen_Update(On.Menu.InitializationScreen.orig_Update orig, InitializationScreen self)
        {
            if (!Translated)
            {
                var step = CurreniInitializationStepField.Get(self);
                if (step == InitializationScreen.InitializationStep.WAIT_FOR_MOD_INIT_ASYNC)
                {
                    Mod = ModManager.ActiveMods.First(m => m.id == "ved_s.poorlytranslated");
                    RainWorld = self.manager.rainWorld;
                    Runner = new(RainWorld);
                    VerifyTranslations();
                    if (!Runner.HasWork)
                    {
                        Translated = true;
                    }
                    else
                    {
                        CurreniInitializationStepField.Set(self, TranslationStep);
                    }
                }
                else if (step == TranslationStep)
                {
                    if (Runner?.HasWork is false or null)
                    {
                        Translated = true;
                        CurreniInitializationStepField.Set(self, InitializationScreen.InitializationStep.WAIT_FOR_MOD_INIT_ASYNC);
                        typeof(InGameTranslator)
                            .GetMethod("LoadShortStrings", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(RainWorld.inGameTranslator, null);
                    }
                }
            }
        
            orig(self);
        }

        public Task VerifyTranslations()
        {
            Task enTask = VerifyLanguageTranslations(InGameTranslator.LanguageID.English);

            if (RainWorld.inGameTranslator.currentLanguage == InGameTranslator.LanguageID.English)
                return enTask;

            return Task.WhenAll(enTask, VerifyLanguageTranslations(RainWorld.inGameTranslator.currentLanguage));
        }

        public Task VerifyLanguageTranslations(InGameTranslator.LanguageID lang, bool force = false)
        {
            Logger.LogInfo($"Verifying files for {lang.value}");
            string path = $"text/text_{LocalizationTranslator.LangShort(lang).ToLower()}";

            List<Task> tasks = EnumerateFileNames(path)
                .Select(f => (path: Path.Combine(path, f), name: f))
                .Where(f => force || !File.Exists(Path.Combine(Mod.path, f.path)))
                .Select(f => VerifyFileTranslations(f.path, f.name, lang))
                .ToList();

            return Task.WhenAll(tasks);
        }

        public Task VerifyFileTranslations(string path, string filename, InGameTranslator.LanguageID lang)
        {
            if (filename == "strings.txt")
                return Runner.EnqueueJob(new StringsJob(path, lang));
            else if (ConversationRegex.IsMatch(filename) && lang == RainWorld.inGameTranslator.currentLanguage)
                return Runner.EnqueueJob(new ConversationJob(path, lang));

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
