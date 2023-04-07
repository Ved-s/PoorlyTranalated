using BepInEx.Logging;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PoorlyTranslated
{
    public static class Patches
    {
        static FieldWrapper<InitializationScreen.InitializationStep> CurreniInitializationStepField = null!;
        static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("PoorlyTranslated_Patches");

        static bool Ignore_OptionsMenu_SetCurrentlySelectedOfSeries = false;

        public static void Apply()
        {
            try
            {
                CurreniInitializationStepField = new(null, typeof(InitializationScreen), "currentStep");
                On.Menu.InitializationScreen.Update += InitializationScreen_Update;
                On.Menu.OptionsMenu.SetCurrentlySelectedOfSeries += OptionsMenu_SetCurrentlySelectedOfSeries;
                On.RainWorld.OnModsInit += RainWorld_OnModsInit;
                On.ModManager.LoadModFromJson += ModManager_LoadModFromJson;
                On.MoreSlugcats.ChatlogData.getChatlog_ChatlogID += ChatlogData_getChatlog_ChatlogID;
                On.MoreSlugcats.ChatlogData.getLinearBroadcast += ChatlogData_getLinearBroadcast;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            MachineConnector.SetRegisteredOI("ved_s.poorlytranslated", new TranslationsOI());
        }
        private static void OptionsMenu_SetCurrentlySelectedOfSeries(On.Menu.OptionsMenu.orig_SetCurrentlySelectedOfSeries orig, OptionsMenu self, string series, int to)
        {
            if (Ignore_OptionsMenu_SetCurrentlySelectedOfSeries)
            {
                orig(self, series, to);
                Ignore_OptionsMenu_SetCurrentlySelectedOfSeries = false;
                return;
            }

            if (series == "Language")
            {
                InGameTranslator.LanguageID prevlang = PoorlyTranslated.RainWorld.options.language;
                InGameTranslator.LanguageID lang = InGameTranslator.LanguageID.Parse(to);
                PoorlyTranslated.RainWorld.options.language = lang;
                InGameTranslator.LoadFonts(lang, null);

                Task t = PoorlyTranslated.VerifyLanguageTranslations(lang);
                if (!t.IsCompleted)
                {
                    Task.Run(async () =>
                    {
                        await t;
                        PoorlyTranslated.RainWorld.options.language = prevlang;
                        Ignore_OptionsMenu_SetCurrentlySelectedOfSeries = true;

                        PoorlyTranslated.MenuLanguageSet = to;
                    });
                    return;
                }
                PoorlyTranslated.RainWorld.options.language = prevlang;
            }
            orig(self, series, to);
        }
        private static void InitializationScreen_Update(On.Menu.InitializationScreen.orig_Update orig, InitializationScreen self)
        {
            ref InitializationScreen.InitializationStep step = ref CurreniInitializationStepField.GetRef(self);
            PoorlyTranslated.InitScreenUpdate(self, ref step);

            orig(self);
        }
        private static ModManager.Mod ModManager_LoadModFromJson(On.ModManager.orig_LoadModFromJson orig, RainWorld rainWorld, string modpath, string consolepath)
        {
            ModManager.Mod mod = orig(rainWorld, modpath, consolepath);
            if (mod.id == "ved_s.poorlytranslated")
                mod.checksumChanged = false;
            return mod;
        }
        private static string[] ChatlogData_getChatlog_ChatlogID(On.MoreSlugcats.ChatlogData.orig_getChatlog_ChatlogID orig, MoreSlugcats.ChatlogData.ChatlogID id)
        {
            string fullpath = $"{PoorlyTranslated.Mod.path}/{PoorlyTranslated.RainWorld.inGameTranslator.SpecificTextFolderDirectory()}/{id.value}.txt";
            if (File.Exists(fullpath))
                return File.ReadAllText(fullpath).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Skip(1).ToArray();

            return orig(id);
        }
        private static string[] ChatlogData_getLinearBroadcast(On.MoreSlugcats.ChatlogData.orig_getLinearBroadcast orig, int id, bool postPebbles)
        {
            string fullpath = $"{PoorlyTranslated.Mod.path}/{PoorlyTranslated.RainWorld.inGameTranslator.SpecificTextFolderDirectory()}/LP_{id}{(postPebbles?"_PEB":"")}.txt";
            if (File.Exists(fullpath))
                return File.ReadAllText(fullpath).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Skip(1).ToArray();

            return orig(id, postPebbles);
        }
    }
}
