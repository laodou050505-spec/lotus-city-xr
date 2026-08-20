using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PicoTowerDefenseEditor
{
    public static class GameViewQualityTools
    {
        [InitializeOnLoadMethod]
        private static void ApplyScaleAfterEditorReload()
        {
            EditorApplication.delayCall += SetGameViewScaleOne;
        }

        [MenuItem("Tools/Spatial Tower Defense/Set Game View Scale 1x")]
        public static void SetGameViewScaleOne()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = FindGameView(gameViewType);
            if (gameView == null)
            {
                Debug.LogWarning("[Spatial Tower Defense] Open the Game tab before setting its preview scale.");
                return;
            }

            const BindingFlags instanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo lowResolutionProperty = gameViewType.GetProperty("lowResolutionForAspectRatios", instanceFields);
            lowResolutionProperty?.SetValue(gameView, false);

            FieldInfo zoomAreaField = gameViewType.GetField("m_ZoomArea", instanceFields);
            object zoomArea = zoomAreaField?.GetValue(gameView);
            FieldInfo scaleField = zoomArea?.GetType().GetField("m_Scale", instanceFields);
            if (zoomArea == null || scaleField == null)
            {
                Debug.LogWarning("[Spatial Tower Defense] This Unity version does not expose the Game View zoom field.");
                return;
            }

            scaleField.SetValue(zoomArea, Vector2.one);
            FieldInfo defaultScaleField = gameViewType.GetField("m_defaultScale", instanceFields);
            defaultScaleField?.SetValue(gameView, 1f);
            gameView.Repaint();
            Debug.Log("[Spatial Tower Defense] Game View preview scale set to 1x.");
        }

        private static EditorWindow FindGameView(Type gameViewType)
        {
            if (gameViewType == null)
            {
                return null;
            }

            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] is EditorWindow editorWindow)
                {
                    return editorWindow;
                }
            }

            return null;
        }
    }
}
