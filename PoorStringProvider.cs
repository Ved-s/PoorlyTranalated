using BepInEx.Logging;
using Kittehface.Framework20;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace PoorlyTranslated
{
    public class PoorStringProvider
    {
        private readonly string template;
        private readonly string Lang;
        private readonly int Interval;
        private string? TranslatedTemplate;
        Translator Translator;
        int Counter = 0;
        bool InProgress = false;

        public string Template => TranslatedTemplate ?? template;

        public PoorStringProvider(string template, string lang, int interval)
        {
            this.template = template;
            Lang = lang;
            Interval = interval;
            Translator = new();
        }

        public void Update() 
        {
            if (InProgress)
                return;

            if (Counter >= Interval)
            {
                Counter = 0;
                InProgress = true;
                ThreadPool.QueueUserWorkItem((_) => 
                {
                    TranslatedTemplate = Translator.PoorlyTranslate(Lang, template, 5);
                    InProgress = false;
                });
            }
            else 
            {
                Counter++;
            }
        }
    }
}
