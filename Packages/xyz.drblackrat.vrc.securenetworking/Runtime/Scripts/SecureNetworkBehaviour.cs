using UdonSharp;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace DrBlackRat.VRC.SecureNetworking
{
    // If UdonSharp supported interfaces, I would make this an interface instead of an abstract class.
    // But it doesn't, so I'm stuck with this...
    
    // To connect call _Connect() on a Secure Networking Instance.
    // To send data, call _SendNetworkData() on the connected Secure Networking Instance.
    // If allowed senders may have changed, call _ValidateAllowedSender on the connected SecureNetworking instance.
    
    /// <summary>
    /// Base class to inherit from when wanting to use Secure Networking.
    /// Used to be able to communicate with a <see cref="SecureNetworkingInstance"/>.
    /// </summary>
    public abstract class SecureNetworkBehaviour : UdonSharpBehaviour
    {
        #region Sending Authority
        /// <summary>
        /// Should return whether the given player is allowed to send networked data.
        /// </summary>
        /// <param name="player"><see cref="VRCPlayerApi"/> of the player to check.</param>
        /// <returns>True if they can send data, false if not.</returns>
        public abstract bool _IsAllowedSender(VRCPlayerApi player);
        
        /// <summary>
        /// Should return the <see cref="VRCPlayerApi"/> of a player in the instance that is allowed to send networked data.
        /// </summary>
        /// <returns><see cref="VRCPlayerApi"/> of a player allowed sending network data. If none is found, return null instead.</returns>
        public abstract VRCPlayerApi _GetAllowedSender();
        
        /// <summary>
        /// Called when ever the authoritative network sender changes.
        /// </summary>
        /// <param name="player"><see cref="VRCPlayerApi"/> of the new authoritative sender. Null if none is found / available.</param>
        public virtual void _OnAuthoritativeSenderChanged(VRCPlayerApi player)
        {
            
        }
        #endregion

        #region Sending & Reciving
        /// <summary>
        /// Called when networked data is received and the sender was allowed to send it.
        /// The <see cref="DataDictionary"/> returned should contain the same key value pairs used by <see cref="_GetNetworkDataForSending"/>, as well as the key <see cref="SecureNetworkingInstance.SendingPlayerKey"/> containing the <see cref="VRCPlayerApi"/> of the player who sent the data.
        /// </summary>
        /// <param name="receivedData"><see cref="DataDictionary"/> containg the data that was received.</param>
        public abstract void _OnNetworkDataReceived(DataDictionary receivedData);
        
        /// <summary>
        /// Called when the network state should be reset to the default.
        /// Triggered when the sender left, no authoritative sender could be found, etc.
        /// This should NOT result in trying to send networked data.
        /// </summary>
        public abstract void _ResetNetToDefault();
        
        /// <summary>
        /// Called when ever network data is being sent, to get the data that should be sent.
        /// The <see cref="DataDictionary"/> returned should contain the same key value pairs used by <see cref="_OnNetworkDataReceived"/> and needs to be serializable to JSON.
        /// </summary>
        /// <returns><see cref="DataDictionary"/> contating the data that should be sent.</returns>
        public abstract DataDictionary _GetNetworkDataForSending();
        #endregion
    }
}
