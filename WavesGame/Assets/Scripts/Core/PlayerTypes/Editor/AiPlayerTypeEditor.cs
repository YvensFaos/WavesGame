using UnityEditor;
using UnityEngine;
using UUtils.Editor;

namespace Core.PlayerTypes.Editor
{
    [CustomEditor(typeof(AiPlayerTypeEditor))]
    public class AiPlayerTypeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            var aiPlayerType = (AIPlayerType) target;
            if (GUILayout.Button("Rename LlmModelPair"))
            {
                RenameScriptableObjectHelper.RenameAssetFile(aiPlayerType, aiPlayerType.GetName());
            }
        }
    }
}