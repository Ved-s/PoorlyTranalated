using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace PoorlyTranslated
{
    public static class Extensions
    {
        public static OpSimpleButton AddOnClick(this OpSimpleButton button, OnSignalHandler handler)
        {
            button.OnClick += handler;
            return button;
        }
        public static TElement SetDescription<TElement>(this TElement element, string description) where TElement : UIelement
        {
            element.description += description;
            return element;
        }
    }
}
