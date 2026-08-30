using System.Text;
using UdonSharp;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace DrBlackRat.VRC.SecureNetworking.Demo
{
    /// <summary>
    /// A simple user / whitelist system.
    /// Used to demonstrate the Secure Networking system.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DemoUserList : UdonSharpBehaviour
    {
        private DataDictionary _connectedBehaviours = new DataDictionary();
        private DataList _usernames = new DataList();
        
        #region Adjusting List
        /// <summary>
        /// Adds a username to the user list.
        /// </summary>
        /// <param name="displayName">Name to add.</param>
        /// <returns>True if added successfully, false they were already on the user list.</returns>
        public bool _AddUsername(string displayName)
        {
            if (_usernames.Contains(displayName))
            {
                return false;
            }
            
            _usernames.Add(displayName);
            OnListUpdated();
            return true;
        }
        
        /// <summary>
        /// Removes a username from the user list.
        /// </summary>
        /// <param name="displayName">Name to remove.</param>
        /// <returns>True if removed successfully, false if they weren't on the user list.</returns>
        public bool _RemoveUsername(string displayName)
        {
            if (!_usernames.Contains(displayName))
            {
                return false;
            }
            
            _usernames.Remove(displayName);
            OnListUpdated();
            return true;
        }
        #endregion
        
        #region Checking List / Getting Players
        /// <summary>
        /// Checks if a username is on the user list.
        /// </summary>
        /// <param name="displayName">Name to check.</param>
        /// <returns>True if they are on the user list, false if not.</returns>
        public bool _ContainsUsername(string displayName)
        {
            return _usernames.Contains(displayName);
        }

        /// <summary>
        /// Gets the <see cref="VRCPlayerApi"/> of a user on the list who is in the instance.
        /// </summary>
        /// <returns><see cref="VRCPlayerApi"/> of a user on the list who is in the instance, if none can be found returns null instead.</returns>
        public VRCPlayerApi GetUserInInstance()
        {
            var allPlayers = VRCPlayerApi.GetPlayers();
            foreach (var player in allPlayers)
            {
                if (!player.IsValid())
                {
                    continue;
                }
                
                if (_usernames.Contains(player.displayName))
                {
                    return player;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a string of all usernames on the list, separated by new lines.
        /// </summary>
        /// <returns>Names of all usernames on the list.</returns>
        public string GetNamesFormatted()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _usernames.Count; i++)
            {
                sb.Append(_usernames[i].String);
                if (i != _usernames.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
        #endregion

        #region Connection Management
        /// <summary>
        /// Connects a UdonBehaviour to the user list.
        /// </summary>
        /// <param name="eventReceiver"><see cref="IUdonEventReceiver"/> to send the update event to.</param>
        /// <param name="eventName">Name of the update event to call.</param>
        public void _Connect(IUdonEventReceiver eventReceiver, string eventName)
        {
            _connectedBehaviours.SetValue(new DataToken(eventReceiver), eventName);
            eventReceiver.SendCustomEventDelayedFrames(eventName, 1);
        }

        private void OnListUpdated()
        {
            var connectedBehavioursKeys = _connectedBehaviours.GetKeys();
            for (int i = 0; i < connectedBehavioursKeys.Count; i++)
            {
                var connectedBehaviourKey = connectedBehavioursKeys[i];
                ((IUdonEventReceiver)connectedBehaviourKey.Reference).SendCustomEventDelayedFrames(_connectedBehaviours[connectedBehaviourKey].String, 1);
            }
        }
        #endregion
    }
}
