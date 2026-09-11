using UnityEngine;

namespace Windy.Srpg.Game.UI
{
    public static class CanvasClampManager
    {
        public static bool PositionAtWorldPointUnclamped(
            Canvas canvas,
            Camera worldCamera,
            RectTransform canvasRectTransform,
            RectTransform positionTarget,
            Vector3 worldPosition,
            Vector2 screenOffset,
            out Vector3 screenPoint)
        {
            screenPoint = Vector3.zero;
            if (canvas == null || positionTarget == null)
            {
                return false;
            }

            Camera activeWorldCamera = worldCamera != null ? worldCamera : Camera.main;
            screenPoint = RectTransformUtility.WorldToScreenPoint(activeWorldCamera, worldPosition);
            screenPoint.x += screenOffset.x;
            screenPoint.y += screenOffset.y;

            return PositionAtScreenPointUnclamped(
                canvas,
                canvasRectTransform,
                positionTarget,
                screenPoint);
        }

        public static bool PositionAtWorldPoint(
            Canvas canvas,
            Camera worldCamera,
            RectTransform canvasRectTransform,
            RectTransform panelRectTransform,
            RectTransform positionTarget,
            Vector3 worldPosition,
            Vector2 screenOffset,
            Vector2 screenPadding)
        {
            if (canvas == null || positionTarget == null || panelRectTransform == null)
            {
                return false;
            }

            Camera activeWorldCamera = worldCamera != null ? worldCamera : Camera.main;
            Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(activeWorldCamera, worldPosition);
            screenPoint.x += screenOffset.x;
            screenPoint.y += screenOffset.y;

            return PositionAtScreenPoint(
                canvas,
                canvasRectTransform,
                panelRectTransform,
                positionTarget,
                screenPoint,
                screenPadding);
        }

        public static bool PositionAtScreenPointUnclamped(
            Canvas canvas,
            RectTransform canvasRectTransform,
            RectTransform positionTarget,
            Vector3 screenPoint)
        {
            if (canvas == null || canvasRectTransform == null || positionTarget == null)
            {
                return false;
            }

            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            positionTarget.position = canvasRectTransform.TransformPoint(localPoint);
            return true;
        }

        public static bool PositionAtScreenPoint(
            Canvas canvas,
            RectTransform canvasRectTransform,
            RectTransform panelRectTransform,
            RectTransform positionTarget,
            Vector3 screenPoint,
            Vector2 screenPadding)
        {
            if (canvas == null || canvasRectTransform == null || positionTarget == null || panelRectTransform == null)
            {
                return false;
            }

            if (!PositionAtScreenPointUnclamped(
                    canvas,
                    canvasRectTransform,
                    positionTarget,
                    screenPoint))
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            ApplyBoundsClamp(canvasRectTransform, panelRectTransform, positionTarget, screenPadding);
            return true;
        }

        public static Vector2 GetBoundsClampScreenDelta(
            Canvas canvas,
            RectTransform canvasRectTransform,
            RectTransform panelRectTransform,
            Vector2 screenPadding)
        {
            if (canvas == null || canvasRectTransform == null || panelRectTransform == null)
            {
                return Vector2.zero;
            }

            Vector2 localPadding = ScreenDeltaToCanvasDelta(canvas, canvasRectTransform, screenPadding);
            Vector2 localDelta = GetBoundsClampDeltaLocal(canvasRectTransform, panelRectTransform, localPadding);
            return CanvasDeltaToScreenDelta(canvas, canvasRectTransform, localDelta);
        }

        public static Vector3 ClampScreenPosition(RectTransform panelRectTransform, Vector3 screenPoint, Vector2 screenPadding)
        {
            if (panelRectTransform == null)
            {
                return screenPoint;
            }

            Vector2 panelSize = panelRectTransform.rect.size;
            float halfWidth = panelSize.x * 0.5f;
            float halfHeight = panelSize.y * 0.5f;
            float minX = halfWidth + screenPadding.x;
            float maxX = Screen.width - halfWidth - screenPadding.x;
            float minY = halfHeight + screenPadding.y;
            float maxY = Screen.height - halfHeight - screenPadding.y;

            screenPoint.x = Mathf.Clamp(screenPoint.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
            screenPoint.y = Mathf.Clamp(screenPoint.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
            return screenPoint;
        }

        public static Vector2 ClampLocalPosition(
            RectTransform canvasRectTransform,
            RectTransform panelRectTransform,
            Vector2 localPoint,
            Vector2 screenPadding)
        {
            if (canvasRectTransform == null || panelRectTransform == null)
            {
                return localPoint;
            }

            Vector2 canvasSize = canvasRectTransform.rect.size;
            Vector2 panelSize = panelRectTransform.rect.size;
            float minX = -canvasSize.x * 0.5f + panelSize.x * 0.5f + screenPadding.x;
            float maxX = canvasSize.x * 0.5f - panelSize.x * 0.5f - screenPadding.x;
            float minY = -canvasSize.y * 0.5f + panelSize.y * 0.5f + screenPadding.y;
            float maxY = canvasSize.y * 0.5f - panelSize.y * 0.5f - screenPadding.y;

            return new Vector2(
                Mathf.Clamp(localPoint.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX)),
                Mathf.Clamp(localPoint.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY)));
        }

        private static void ApplyBoundsClamp(
            RectTransform canvasRectTransform,
            RectTransform panelRectTransform,
            RectTransform positionTarget,
            Vector2 screenPadding)
        {
            Canvas canvas = canvasRectTransform.GetComponentInParent<Canvas>();
            Vector2 localPadding = ScreenDeltaToCanvasDelta(canvas, canvasRectTransform, screenPadding);
            Vector2 deltaLocal = GetBoundsClampDeltaLocal(canvasRectTransform, panelRectTransform, localPadding);
            float deltaX = deltaLocal.x;
            float deltaY = deltaLocal.y;

            if (Mathf.Approximately(deltaX, 0f) && Mathf.Approximately(deltaY, 0f))
            {
                return;
            }

            Vector3 worldDelta = canvasRectTransform.TransformVector(new Vector3(deltaX, deltaY, 0f));
            positionTarget.position += worldDelta;
        }

        private static Vector2 GetBoundsClampDeltaLocal(
            RectTransform canvasRectTransform,
            RectTransform panelRectTransform,
            Vector2 localPadding)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRectTransform, panelRectTransform);
            Rect canvasRect = canvasRectTransform.rect;

            float minAllowedX = canvasRect.xMin + localPadding.x;
            float maxAllowedX = canvasRect.xMax - localPadding.x;
            float minAllowedY = canvasRect.yMin + localPadding.y;
            float maxAllowedY = canvasRect.yMax - localPadding.y;

            return new Vector2(
                GetClampDelta(bounds.min.x, bounds.max.x, bounds.center.x, minAllowedX, maxAllowedX),
                GetClampDelta(bounds.min.y, bounds.max.y, bounds.center.y, minAllowedY, maxAllowedY));
        }

        private static Vector2 ScreenDeltaToCanvasDelta(
            Canvas canvas,
            RectTransform canvasRectTransform,
            Vector2 screenDelta)
        {
            if (canvas == null || canvasRectTransform == null)
            {
                return screenDelta;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                float scaleFactor = Mathf.Max(0.0001f, canvas.scaleFactor);
                return screenDelta / scaleFactor;
            }

            Rect rect = canvasRectTransform.rect;
            float scaleX = Screen.width > 0 ? rect.width / Screen.width : 1f;
            float scaleY = Screen.height > 0 ? rect.height / Screen.height : 1f;
            return new Vector2(screenDelta.x * scaleX, screenDelta.y * scaleY);
        }

        private static Vector2 CanvasDeltaToScreenDelta(
            Canvas canvas,
            RectTransform canvasRectTransform,
            Vector2 canvasDelta)
        {
            if (canvas == null || canvasRectTransform == null)
            {
                return canvasDelta;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvasDelta * Mathf.Max(0.0001f, canvas.scaleFactor);
            }

            Rect rect = canvasRectTransform.rect;
            float scaleX = Mathf.Abs(rect.width) > 0.0001f ? Screen.width / rect.width : 1f;
            float scaleY = Mathf.Abs(rect.height) > 0.0001f ? Screen.height / rect.height : 1f;
            return new Vector2(canvasDelta.x * scaleX, canvasDelta.y * scaleY);
        }

        private static float GetClampDelta(
            float currentMin,
            float currentMax,
            float currentCenter,
            float allowedMin,
            float allowedMax)
        {
            float currentSize = currentMax - currentMin;
            float allowedSize = allowedMax - allowedMin;

            if (currentSize > allowedSize)
            {
                return ((allowedMin + allowedMax) * 0.5f) - currentCenter;
            }

            if (currentMin < allowedMin)
            {
                return allowedMin - currentMin;
            }

            if (currentMax > allowedMax)
            {
                return allowedMax - currentMax;
            }

            return 0f;
        }
    }
}

