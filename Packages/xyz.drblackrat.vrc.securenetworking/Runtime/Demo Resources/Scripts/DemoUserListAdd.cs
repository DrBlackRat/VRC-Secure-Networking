using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace DrBlackRat.VRC.SecureNetworking.Demo
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DemoUserListAdd : UdonSharpBehaviour
    {
        public DemoUserList userList;
        public TMP_InputField inputField;
        public bool removeMode;

        public void _OnInputFinished()
        {
            var username = inputField.text;
            if (removeMode)
            {
                userList._RemoveUsername(username);
            }
            else
            {
                userList._AddUsername(username);
            }
            
            inputField.text = "";
        }
    }
}
