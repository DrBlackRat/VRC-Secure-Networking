using UnityEngine.UIElements;
using UnityEditor;

namespace DrBlackRat.VRC.SecureNetworking.Editor
{
    [CustomEditor(typeof(SecureNetworkingInstance))]
    public class SecureNetworkingInstanceEditor : UnityEditor.Editor
    {
        public VisualTreeAsset visualTree;
        
        public override VisualElement CreateInspectorGUI()
        {
            return visualTree.CloneTree();
        }
    }
}
