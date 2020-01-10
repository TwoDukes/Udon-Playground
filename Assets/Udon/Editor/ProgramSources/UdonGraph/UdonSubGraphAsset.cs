using UnityEngine;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;

[CreateAssetMenu(menuName = "VRChat/Udon/Udon Sub Graph Asset", fileName = "New Udon Sub Graph Asset")]
public class UdonSubGraphAsset : ScriptableObject, IUdonGraphDataProvider
{
    [SerializeField]
    private UdonGraphData graphData = new UdonGraphData();

    public UdonGraphData GetGraphData()
    {
        return graphData;
    }
}
