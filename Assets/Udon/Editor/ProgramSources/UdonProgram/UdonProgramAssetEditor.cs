using UnityEditor;

[CustomEditor(typeof(UdonProgramAsset))]
public class UdonProgramAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool dirty = false;
        UdonProgramAsset programAsset = (UdonProgramAsset)target;
        programAsset.RunProgramSourceEditor(null, ref dirty);
        if(dirty)
        {
            EditorUtility.SetDirty(target);
        }
    }
}
