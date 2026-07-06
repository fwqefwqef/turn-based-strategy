using Windy.Srpg.Game.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    internal static class UnitPortraitUiUtility
    {
        private const string PortraitImageObjectName = "RuntimePortraitImage";

        public static void ApplyPortrait(RectTransform portraitAnchor, Unit unit)
        {
            ApplyPortrait(portraitAnchor, unit != null ? unit.GetPortraitSprite() : null);
        }

        public static void ApplyPortrait(RectTransform portraitAnchor, Sprite sprite)
        {
            if (portraitAnchor == null)
            {
                return;
            }

            Image image = EnsurePortraitImage(portraitAnchor);
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        private static Image EnsurePortraitImage(RectTransform portraitAnchor)
        {
            Transform existing = portraitAnchor.Find(PortraitImageObjectName);
            if (existing != null && existing.TryGetComponent(out Image existingImage))
            {
                return existingImage;
            }

            GameObject imageObject = new GameObject(PortraitImageObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(portraitAnchor, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.localScale = Vector3.one;
            imageRect.localRotation = Quaternion.identity;

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.enabled = false;
            return image;
        }
    }
}
