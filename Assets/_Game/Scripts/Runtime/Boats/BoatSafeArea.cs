using UnityEngine;

namespace VuotThac.Boats
{
    public class BoatSafeArea : MonoBehaviour
    {
        Rect previousArea; int previousWidth, previousHeight;
        void LateUpdate()
        {
            if (!Application.isMobilePlatform) return;
            var area = Screen.safeArea;
            if (area == previousArea && Screen.width == previousWidth && Screen.height == previousHeight) return;
            previousArea=area; previousWidth=Screen.width; previousHeight=Screen.height;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
