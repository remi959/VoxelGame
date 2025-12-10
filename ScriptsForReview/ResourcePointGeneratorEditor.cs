using UnityEditor;
using UnityEngine;
using Assets.Scripts.Generation.Resources;

[CustomEditor(typeof(ResourcePointGenerator))]
public class ResourcePointGeneratorEditor : Editor
{
    private Vector3 manualSpawnPosition = Vector3.zero;
    private int selectedInstructionIndex = 0;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        var generator = (ResourcePointGenerator)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
        
        // Generate at test positions
        if (GUILayout.Button("Generate All Test Resources"))
        {
            generator.GenerateAllTestResources();
        }
        
        if (GUILayout.Button("Clear All Resources"))
        {
            generator.ClearAllResources();
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Manual Spawn", EditorStyles.boldLabel);
        
        // Manual position spawn
        manualSpawnPosition = EditorGUILayout.Vector3Field("Spawn Position", manualSpawnPosition);
        
        if (generator.Instructions != null && generator.Instructions.Count > 0)
        {
            string[] options = new string[generator.Instructions.Count];
            for (int i = 0; i < generator.Instructions.Count; i++)
            {
                var inst = generator.Instructions[i];
                options[i] = inst != null ? $"{inst.name} ({inst.ResourceType})" : "(null)";
            }
            
            selectedInstructionIndex = EditorGUILayout.Popup("Instruction", selectedInstructionIndex, options);
            
            if (GUILayout.Button("Spawn At Position"))
            {
                var instruction = generator.Instructions[selectedInstructionIndex];
                instruction?.GenerateResourceAt(manualSpawnPosition);
            }
        }
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Create New Wood Instruction"))
        {
            CreateInstruction<WoodInstructions>("WoodInstruction");
        }
    }

    private void CreateInstruction<T>(string defaultName) where T : ResourceGenerationInstruction
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Instruction", defaultName, "asset", 
            "Choose where to save the instruction");
            
        if (!string.IsNullOrEmpty(path))
        {
            var instruction = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instruction, path);
            AssetDatabase.SaveAssets();
            
            // Add to generator's list
            var generator = (ResourcePointGenerator)target;
            SerializedProperty listProp = serializedObject.FindProperty("generationInstructions");
            listProp.arraySize++;
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = instruction;
            serializedObject.ApplyModifiedProperties();
            
            EditorUtility.SetDirty(generator);
        }
    }
}