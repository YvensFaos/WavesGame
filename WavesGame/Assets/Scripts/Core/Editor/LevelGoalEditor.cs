using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    [CustomEditor(typeof(LevelGoal))]
    public class LevelGoalEditor: UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var levelGoal = (LevelGoal)target;
            var factions = levelGoal.Factions;
            
            EditorGUILayout.HelpBox($"Mode: {LevelGoalTypeExtension.LevelGoalTypeToString(levelGoal.Type())}", MessageType.Info);
            
            DrawDefaultInspector();

            if (factions == null) return;
            EditorGUILayout.Space(15);
            EditorGUILayout.HelpBox("Available Factions", MessageType.Info);

            foreach (var faction in factions)
            {
                GUILayout.Label($"{faction.Key}: {faction.Value.ToString()}");
            }
        }
    }
}