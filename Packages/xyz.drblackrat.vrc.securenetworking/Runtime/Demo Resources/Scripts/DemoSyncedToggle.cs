using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace DrBlackRat.VRC.SecureNetworking.Demo
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)] // The script itself doesn't do the networking, so it can be none.
    public class DemoSyncedToggle : SecureNetworkBehaviour // Needs to be inherited from to use Secure Networking.
    {
        [Header("Toggle")]
        public Toggle toggle;
        public GameObject[] toggleObjects;
        
        [Header("Networking")] 
        public SecureNetworkingInstance secureNetworkingInstance;  // The secure networking instance that will be used for sending and receiving data, you need one per script.
        public DemoUserList allowedUsers;
        
        private bool _isLocalPlayerAllowed;
        private bool _toggleState;
        private bool _defaultToggleState;
        
        private const string ToggleDataKey = "ToggleState"; // Key used for sending and receiving data.
        
        #region VRChat / Unity / User List Events

        private void Start()
        {
            var state = toggle.isOn;
            _toggleState = state;
            _defaultToggleState = state;
            SetToggleObjectsState(state);
            
            secureNetworkingInstance._Connect(this);
            allowedUsers._Connect(this, nameof(_OnAllowedUserListUpdated));
        }

        public void _OnAllowedUserListUpdated()
        {
            _isLocalPlayerAllowed = allowedUsers._ContainsUsername(Networking.LocalPlayer.displayName);
            
            toggle.interactable = _isLocalPlayerAllowed; // Update the slider's interactable state.
            secureNetworkingInstance._ValidateAllowedSenders(); // Tells secure networking if the current authoritative sender is still allowed to send data.
        }
        #endregion
        
        #region Toggle
        /// <summary>
        /// Called by the toggle whenever its state has been changed.
        /// </summary>
        public void _OnToggleChanged()
        {
            if (!_isLocalPlayerAllowed) // If the current user is not allowed to send data, don't send data or update the toggle state.
            {
                return;
            }
            
            var newState = toggle.isOn;
            if (newState == _toggleState)
            {
                return;
            }
            
            _toggleState = newState;
            SetToggleObjectsState(newState);
            secureNetworkingInstance._SendNetworkData(); // Tells the secure networking instance to get the curren state and send it to the other players.
        }

        /// <summary>
        /// Sets the state of all toggle objects.
        /// </summary>
        /// <param name="state">True means enabled, false means disabled.</param>
        private void SetToggleObjectsState(bool state)
        {
            foreach (var toggleObject in toggleObjects)
            {
                if (toggleObject == null)
                {
                    continue;
                }
                toggleObject.SetActive(state);
            }
        }

        /// <summary>
        /// Sets the state of the toggle ui without notifying the listeners.
        /// </summary>
        /// <param name="state">True means enabled, false means disabled.</param>
        private void SetToggleState(bool state)
        {
            _toggleState = state;
            toggle.SetIsOnWithoutNotify(state);
        }
        #endregion

        #region Networking
        public override bool _IsAllowedSender(VRCPlayerApi player)
        {
            return allowedUsers._ContainsUsername(player.displayName); // Checks if the player is on the allowed users list.
        }

        public override VRCPlayerApi _GetAllowedSender()
        {
            return allowedUsers.GetUserInInstance(); // Gets a player on the allowed users list who is in the instance to become the new authoritative sender.
        }

        public override void _OnNetworkDataReceived(DataDictionary receivedData)
        {
            if (!receivedData.TryGetValue(ToggleDataKey, TokenType.Boolean, out DataToken result))
            {
                Debug.LogError("Failed to deserialize toggle data! Data may be malformed, skipping applying net data.");
                return;
            }
            
            var state = result.Boolean;
            SetToggleState(state);
            SetToggleObjectsState(state);
        }

        public override void _ResetNetToDefault()
        {
            SetToggleState(_defaultToggleState);
            SetToggleObjectsState(_defaultToggleState);
        }

        public override DataDictionary _GetNetworkDataForSending()
        {
            var netDict = new DataDictionary();
            netDict.SetValue(ToggleDataKey, _toggleState);
            return netDict;
        }
        #endregion
    }
}
