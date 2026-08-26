using Actors.Editor;
using UnityEditor;

namespace Actors.AI.Editor
{
    [CustomEditor(typeof(AINavalShip))]
    public class AINavalShipEditor : NavalShipEditor
    {
        private UnityEditor.Editor _navalScriptableObjectEditor;
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            var myTarget = (AINavalShip)target;
            var genes = myTarget.GenesData;
            if (genes == null) return;
            EditorGUILayout.Space(15);
            EditorGUILayout.HelpBox("Genes SO", MessageType.Info);
            CreateCachedEditor(genes, null, ref _navalScriptableObjectEditor);
            _navalScriptableObjectEditor.OnInspectorGUI();
        }
    }
}