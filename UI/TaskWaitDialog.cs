using Menu;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoorlyTranslated.UI
{
    public class TaskWaitDialog : MenuDialogBox
    {
        private AtlasAnimator loadingSpinner;

        public MenuLabel bottomLabel;

        public TaskWaitDialog(Menu.Menu menu, MenuObject owner, string text)
            : base(menu, owner, text,
                  new Vector2(PoorlyTranslated.RainWorld.options.ScreenSize.x / 2f - 240f + (1366f - PoorlyTranslated.RainWorld.options.ScreenSize.x) / 2f, 184f),
                  new Vector2(480f, 400f), false)
        {
            loadingSpinner = new AtlasAnimator(0, new Vector2((int)(pos.x + size.x / 2f) - Menu.Menu.HorizontalMoveToGetCentered(menu.manager), (int)(pos.y + 100f)), "sleep", "sleep", 20, loop: true, reverse: false);
            loadingSpinner.animSpeed = 0.25f;
            loadingSpinner.specificSpeeds = new Dictionary<int, float>();
            loadingSpinner.specificSpeeds[1] = 0.0125f;
            loadingSpinner.specificSpeeds[13] = 0.0125f;
            loadingSpinner.AddToContainer(owner.Container);

            bottomLabel = new MenuLabel(menu, owner, "Press Esc to cancel", new Vector2(pos.x + size.x * 0.07f, pos.y + 10f), new Vector2(size.x * 0.86f, 10f), bigText: false);
            owner.subObjects.Add(bottomLabel);
        }

        public override void Update()
        {
            base.Update();
            loadingSpinner.Update();
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);
        }

        public override void RemoveSprites()
        {
            base.RemoveSprites();
            loadingSpinner.RemoveFromContainer();
            owner.subObjects.Remove(bottomLabel);
            bottomLabel.RemoveSprites();
        }

        public void SetText(string caption)
        {
            descriptionLabel.text = caption;
        }
    }
}
