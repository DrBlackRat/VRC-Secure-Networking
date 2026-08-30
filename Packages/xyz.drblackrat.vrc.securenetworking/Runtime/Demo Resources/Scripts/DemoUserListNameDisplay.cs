using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace DrBlackRat.VRC.SecureNetworking.Demo
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DemoUserListNameDisplay : UdonSharpBehaviour
    {
        public DemoUserList userList;
        public TextMeshProUGUI textMesh;

        private void Start()
        {
            userList._Connect(this, nameof(_OnUserListUpdated));
        }

        public void _OnUserListUpdated()
        {
            var text = userList.GetNamesFormatted();
            text = string.IsNullOrEmpty(text) ? "No Users..." : text;
            textMesh.text = text;
        }
    }
}
