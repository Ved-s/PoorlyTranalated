using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PoorlyTranslated
{
    public class TranslationsOI : OptionInterface
    {
        public override void Initialize()
        {
            Tabs = new[]
            {
                new OpTab(this, "Main")
            };

            Tabs[0].AddItems(
                new OpLabel(new Vector2(150f, 520f), new Vector2(300f, 30f), "Poorly translated Rain World", FLabelAlignment.Center, true, null),
                new OpSimpleButton(new(210f, 120f), new(200f, 30f), "Reset all translations")
                    .AddOnClick(f =>
                    {
                        string path = Path.Combine(mod.path, $"text");
                        if (Directory.Exists(path))
                            Directory.Delete(path, true);

                        PoorlyTranslated.RainWorld.inGameTranslator.shortStrings.Clear();
                        PoorlyTranslated.RainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                    }),
                new OpSimpleButton(new(210f, 80f), new(200f, 30f), "Reset current translations")
                    .SetDescription("Resets all translations for the current language.")
                    .AddOnClick(f =>
                    {
                        string path = Path.Combine(mod.path, $"text/text_{LocalizationTranslator.LangShort(PoorlyTranslated.RainWorld.inGameTranslator.currentLanguage)}");
                        if (Directory.Exists(path))
                            Directory.Delete(path, true);

                        PoorlyTranslated.RainWorld.inGameTranslator.shortStrings.Clear();
                        PoorlyTranslated.RainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                    }),
                new OpSimpleButton(new(210f, 40f), new(200f, 30f), "Regenerate current translations")
                    .AddOnClick(async f =>
                    {
                        await PoorlyTranslated.VerifyLanguageTranslations(PoorlyTranslated.RainWorld.inGameTranslator.currentLanguage, true);
                        PoorlyTranslated.RainWorld.inGameTranslator.shortStrings.Clear();
                        PoorlyTranslated.RainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                    }));
        }
    }
}
