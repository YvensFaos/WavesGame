using UnityEditor;
using UnityEngine;
using UUtils.Editor;

namespace Core.PlayerTypes.Editor
{
    [CustomEditor(typeof(LlmPlayerType))]
    public class LlmPlayerTypeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            var llmPlayerType = (LlmPlayerType) target;
            if (GUILayout.Button("Rename LlmModelPair"))
            {
                RenameScriptableObjectHelper.RenameAssetFile(llmPlayerType, llmPlayerType.GetName());
            }
        }
    }
}