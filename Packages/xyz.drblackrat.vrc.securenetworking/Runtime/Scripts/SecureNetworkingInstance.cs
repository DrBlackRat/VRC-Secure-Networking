using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace DrBlackRat.VRC.SecureNetworking
{
    // Networking model:
    // - Only the latest received network state is kept. Intermediate states are not guaranteed to be received.
    //   - If newer network data arrives before a retry completes, the older pending state is discarded.
    //   - This is intentional to ensure "latest state wins" behavior and prevent applying stale state.
    // - The player whose data was most recently accepted becomes the authoritative sender and is responsible for
    //   sending the current state to new joiners.
    // - If the sender cannot be validated temporarily (e.g., the whitelist has not been initialized yet or a string
    //   load is still pending), the system will retry a few times before giving up.

    /// <summary>
    /// Allows data to be sent securely over the network without allowing client users to modify it by validating the sender.
    /// Requires a <see cref="SecureNetworkBehaviour"/> to be connected to it.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public sealed class SecureNetworkingInstance : UdonSharpBehaviour
    {
        #region Variables
        // Settings
        [SerializeField]
        [Tooltip("If enabled, Secure Networking check whether the sender of network data is actually allowed to send data." +
                 "\nThis is makes networking secure, but results in networking only working if there are any allowed senders present.")]
        private bool secureNetworkingEnabled = true;
        [SerializeField]
        [Tooltip("If enabled, once the last allowed sender leaves the instance, the data will be reset to it's default state to keep it in sync with newly joined players.")]
        private bool resetIfNoAllowedSenderPresent = true;
        [SerializeField]
        [Tooltip("If enabled, the networked data will be reset to it's default state if the player who send it leaves the instance, even if another allowed sender would be present.")]
        private bool resetIfLastSenderLeaves = false;
        
        [SerializeField]
        [Range(1, 10)]
        [Tooltip("Time to wait in seconds before retrying to apply the networked data if the sender was not allowed on the last attempt.")]
        private float netRetryDelay = 5f;
        [SerializeField]
        [Range(1, 10)]
        [Tooltip("Amount of times to try to apply the networked data if the sender was not allowed on the last attempt.")]
        private int maxNetAttempts = 4;
        
        [SerializeField]
        [Tooltip("How much logging to output.")]
        private LogLevel logLevel = LogLevel.All;
        
        //Receiving Data
        private DataDictionary _netReceivedDataDictionary;
        private VRCPlayerApi _netSendingPlayer;
        private VRCTweenHandle _netReceiveRetryEventHandle;
        private int _netAttempts;
        
        // Sending Networked Data
        private string _netSendingJson;
        private VRCTweenHandle _netSendRetryEventHandle;
        
        // Players
        private VRCPlayerApi _localPlayer;
        private VRCPlayerApi _leavingPlayer;
        private VRCPlayerApi _authoritativeSender;
        /// <summary>
        /// The <see cref="VRCPlayerApi"/> of the player who is responsible for sending the current state to new players, usually the last sender or master.
        /// Can be null if there is currently no valid sender and secure networking is enabled.
        /// </summary>
        public VRCPlayerApi AuthoritativeSender
        {
            get => _authoritativeSender; 
            private set
            {
                if (_authoritativeSender == value)
                    return;
                
                _authoritativeSender = value;
                OnAuthoritativeSenderChanged(value);
            }
        }
        private bool _authoritativeSenderWasReset = true;
        
        // Connection
        private bool _isConnected;
        private SecureNetworkBehaviour _networkBehaviour;
        
        // Other
        private string _logPrefix = "[<color=#ff462e>Secure Networking</color>] ";
        private const int NetMaxQueueSize = 5;
        private const float NetSendDelay = 0.5f;
        
        // Public Data Keys
        
        /// <summary>
        /// The key used to store the player who sent the networked data inside the received data <see cref="DataDictionary"/>.
        /// </summary>
        public readonly DataToken SendingPlayerKey = new DataToken("SendingPlayer");
        #endregion
        
        #region Unity / VRChat Events
        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) ||
                player.isLocal ||
                _localPlayer != AuthoritativeSender ||
                secureNetworkingEnabled && !IsAllowedSender(_localPlayer))
            {
                return;
            }

            var data = GetSerializedData();
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(OnNetworkedDataReceived), player.playerId, data);
            Log($"Sending initial network data to {player.displayName}!", gameObject);

        }
        
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            _leavingPlayer = player;

            if (_leavingPlayer == _authoritativeSender)
            {
                MoveAuthoritativeSenderIfNeeded();
                
                if (resetIfLastSenderLeaves)
                {
                    Log("Resetting to the default state because the sender left the instance.", gameObject);
                    ResetNetToDefault();
                }
            }

            _leavingPlayer = null;
        }
        #endregion
        
        #region Public API
        /// <summary>
        /// Connects a <see cref="SecureNetworkBehaviour"/> to this Secure Networking instance.
        /// </summary>
        /// <param name="networkBehaviour"><see cref="SecureNetworkBehaviour"/> to connect.</param>
        /// <returns>True if the connection was successful, false if not.</returns>
        public bool _Connect(SecureNetworkBehaviour networkBehaviour)
        {
            if (_isConnected)
            {
                LogError("Could not establish connection! This Secure Networking Instance is already connected.", gameObject);
                return false;
            }
            
            _logPrefix += $"[{networkBehaviour.gameObject.name}] ";
            _networkBehaviour = networkBehaviour;
            _localPlayer = Networking.LocalPlayer;
            _isConnected = true;
            return true;
        }

        /// <summary>
        /// Attempts to send network data to all players in the instance.
        /// Only does so if the local player is allowed to send data, and if no other sending attempts are currently in progress.
        /// </summary>
        public void _SendNetworkData()
        {
            _netSendRetryEventHandle.Kill();
            
            var data = GetSerializedData();
            if (string.IsNullOrEmpty(data))
            {
                LogError("Could not send network data! No data was serialized.", gameObject);
                return;
            }
            _netSendingJson = data;
            
            _TrySendNet();
        }

        /// <summary>
        /// Should be called whenever the allowed senders may have changed.
        /// Checks whether the current authoritative sender is valid and, if not, tries to find a new one.
        /// </summary>
        public void _ValidateAllowedSenders()
        {
            MoveAuthoritativeSenderIfNeeded();
        }
        #endregion
        
        #region Connected SecureNetworkBehaviour
        // Sender Authority
        private bool IsAllowedSender(VRCPlayerApi player)
        {
            if (!_isConnected)
            {
                LogError("Could not check if player is allowed to send data! Secure Networking instance is not connected.", gameObject);
                return false;
            }
            return _networkBehaviour._IsAllowedSender(player);
        }

        private VRCPlayerApi GetAllowedSender()
        {
            if (!_isConnected)
            {
                LogError("Could not get allowed sender! Secure Networking instance is not connected.", gameObject);
                return null;
            }
            return _networkBehaviour._GetAllowedSender();
        }
        
        private void OnAuthoritativeSenderChanged(VRCPlayerApi player)
        {
            if (!_isConnected)
            {
                LogError("Could not update authoritative sender! Secure Networking instance is not connected.", gameObject);
                return;
            }
            _networkBehaviour._OnAuthoritativeSenderChanged(player);
        }
        
        // Sending & Receiving
        private void OnNetworkDataReceived(DataDictionary receivedData)
        {
            if (!_isConnected)
            {
                LogError("Could not apply received network data! Secure Networking instance is not connected.", gameObject);
                return;
            }
            _networkBehaviour._OnNetworkDataReceived(receivedData);
        }

        private void ResetNetToDefault()
        {
            if (!_isConnected)
            {
                LogError("Could not reset network data! Secure Networking instance is not connected.", gameObject);
                return;
            }
            _networkBehaviour._ResetNetToDefault();
        }

        private DataDictionary GetNetworkDataForSending()
        {
            if (!_isConnected)
            {
                LogError("Could not get sending network data! Secure Networking instance is not connected.", gameObject);
                return null;
            }
            return _networkBehaviour._GetNetworkDataForSending();
        }
        #endregion
        
        #region Authoritative Sender
        /// <summary>
        /// Verifies the validity and permissions of the current authoritative sender and reassigns it to a new eligible sender if necessary.
        /// </summary>
        private void MoveAuthoritativeSenderIfNeeded()
        {
            if (!Utilities.IsValid(AuthoritativeSender))
            {
                Log("Authoritative Sender is null.", gameObject);
                FindNewAuthoritativeSender();
                return;
            }
            
            if (!AuthoritativeSender.IsValid())
            {
                Log("Authoritative Sender is invalid.", gameObject);
                FindNewAuthoritativeSender();
                return;
            }
            
            if (AuthoritativeSender == _leavingPlayer)
            {
                Log("Authoritative Sender is leaving the instance.", gameObject);
                FindNewAuthoritativeSender();
                return;
            }
            
            if (secureNetworkingEnabled && !IsAllowedSender(AuthoritativeSender))
            {
                Log($"{AuthoritativeSender.displayName} is the Authoritative Sender, but isn't allowed to send data.", gameObject);
                FindNewAuthoritativeSender();
                return;
            }
        }

        /// <summary>
        /// Attempts to find and assign a new authoritative sender.
        /// </summary>
        private void FindNewAuthoritativeSender()
        {
            if (!secureNetworkingEnabled)
            {
                var master = Networking.Master;
                Log($"Transferring Authoritative Sender to the Master ({master.displayName}).", gameObject);
                AuthoritativeSender = master;
                _authoritativeSenderWasReset = false;
                return;
            }
            
            var newOwner = GetAllowedSender();
            if (!Utilities.IsValid(newOwner) || 
                !newOwner.IsValid() ||
                newOwner == _leavingPlayer)
            {
                if (_authoritativeSenderWasReset) // Prevent resetting to if it's already reset
                {
                    return;
                }
                
                Log("Could not find a new player to transfer Authoritative Sender to. Removing current Authoritative Sender to prevent syncing issues.", gameObject);
                AuthoritativeSender = null;
                _authoritativeSenderWasReset = true;

                if (resetIfNoAllowedSenderPresent)
                {
                    Log("Resetting to the default state as there is no valid Authoritative Sender.", gameObject);
                    ResetNetToDefault();
                }
                
                return;
            }
            
            Log($"Transferring Authoritative Sender to {newOwner.displayName}.", gameObject);
            _authoritativeSenderWasReset = false;
            AuthoritativeSender = newOwner;
        }
        #endregion
        
        #region Receiving Data
        /// <summary>
        /// INTERNAL | DO NOT CALL MANUALLY
        /// Handles the receipt of networked data sent to this object.
        /// </summary>
        /// <param name="receiverId">The player ID of the intended recipient. Use -1 for broadcast to all players.</param>
        /// <param name="netJson">The networked JSON being transmitted.</param>
        [NetworkCallable(NetMaxQueueSize)]
        public void OnNetworkedDataReceived(int receiverId, string netJson)
        {
            if (!NetworkCalling.InNetworkCall)
            {
                LogError($"{nameof(OnNetworkDataReceived)} can only be called as a network event!", gameObject);
                return;
            }

            var callingPlayer = NetworkCalling.CallingPlayer;
            if (!Utilities.IsValid(callingPlayer))
            {
                LogError($"{nameof(OnNetworkDataReceived)} called by an invalid player!", gameObject);
                return;
            }

            if (receiverId != -1 && receiverId != _localPlayer.playerId)
            {
                return;
            }

            if (!VRCJson.TryDeserializeFromJson(netJson, out DataToken result))
            {
                LogError($"{nameof(OnNetworkDataReceived)} could not deserialize from JSON due to {result.ToString()}!", gameObject);
                return;
            }

            if (result.TokenType != TokenType.DataDictionary)
            {
                LogError($"{nameof(OnNetworkDataReceived)} received data is not a data dictionary! ({gameObject.name})", gameObject);
                return;
            }

            _netReceivedDataDictionary = result.DataDictionary;
            _netReceivedDataDictionary.SetValue(SendingPlayerKey, new DataToken(callingPlayer));
            _netSendingPlayer = callingPlayer;

            _netAttempts = 0;
            _netReceiveRetryEventHandle.Kill();
            
            _TryApplyNetData();
        }

        /// <summary>
        /// INTERNAL | DO NOT CALL MANUALLY
        /// Attempts to apply network data, validating sender if secure networking is enabled, will retry if the sender couldn't be validated.
        /// </summary>
        public void _TryApplyNetData()
        {
            _netAttempts++;
            
            if (!secureNetworkingEnabled)
            {
                Log($"{_netSendingPlayer.displayName} has sent network data on {gameObject.name}.", gameObject);
                AuthoritativeSender = _netSendingPlayer;
                OnNetworkDataReceived(_netReceivedDataDictionary);

                return;
            }
            
            if (!IsAllowedSender(_netSendingPlayer))
            {
                if (_netAttempts >= maxNetAttempts)
                {
                    LogError( $"{_netSendingPlayer.displayName} is not allowed to send data! Aborting applying net data after {_netAttempts} / {maxNetAttempts} attempts.", gameObject);
                    return;
                }
                
                LogWarning($"{_netSendingPlayer.displayName} is not allowed to send data! Retrying in {netRetryDelay}s, Attempt {_netAttempts} / {maxNetAttempts}.", gameObject);
                _netReceiveRetryEventHandle = VRCTween.DelayedCall(this, nameof(_TryApplyNetData), netRetryDelay);
                return;
            }

            if (_netAttempts > 1)
            {
                LogWarning($"{_netSendingPlayer.displayName} is now allowed to send data! Applying networked data after {_netAttempts} / {maxNetAttempts} attempts.", gameObject);
            }
            else
            {
                Log($"{_netSendingPlayer.displayName} has sent network data.", gameObject);
            }
            
            AuthoritativeSender = _netSendingPlayer;
            OnNetworkDataReceived(_netReceivedDataDictionary);
        }
        #endregion
        
        #region Sending Networked Data
        /// <summary>
        /// Serializes data into a JSON string for network transmission.
        /// The data to be serialized is obtained from the abstract method <see cref="GetNetworkDataForSending"/>.
        /// </summary>
        /// <returns>A serialized JSON string if successful; otherwise, null if serialization fails.</returns>
        private string GetSerializedData()
        {
            if (!VRCJson.TrySerializeToJson(GetNetworkDataForSending(), JsonExportType.Minify, out DataToken result))
            {
                LogError("Failed to serialize data for sending! No data will be sent.", gameObject);
                return null;
            }

            return result.String;
        }
        
        /// <summary>
        /// INTERNAL | DO NOT CALL MANUALLY
        /// Attempts to send network data to other players if the local player is allowed to send.
        /// </summary>
        public void _TrySendNet()
        {
            if (!IsAllowedSender(_localPlayer))
            {
                LogError("You are trying to send network data but are not allowed to!", gameObject);
                return;
            }
            
            if (NetworkCalling.GetQueuedEvents(this, nameof(OnNetworkedDataReceived)) != 0)
            {
                _netSendRetryEventHandle = VRCTween.DelayedCall(this, nameof(_TrySendNet), NetSendDelay);
                Log($"Could not send data! Events are already queued up, will try again in {NetSendDelay}s.", gameObject);
                return;
            }

            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(OnNetworkedDataReceived), -1, _netSendingJson);
            AuthoritativeSender = _localPlayer;
            Log("Sending network data!", gameObject);
        }
        #endregion
        
        #region Logging
        private void Log(string message, Object context = null)
        {
            if (logLevel != LogLevel.All)
            {
                return;
            }
            
            Debug.Log(_logPrefix + message, context);
        }

        private void LogWarning(string message, Object context = null)
        {
            if (logLevel != LogLevel.Warnings && logLevel != LogLevel.All)
            {
                return;
            }
            
            Debug.LogWarning(_logPrefix + message, context);
        }
        
        private void LogError(string message, Object context = null)
        {
            if (logLevel != LogLevel.Errors && logLevel != LogLevel.Warnings && logLevel != LogLevel.All)
            {
                return;
            }
            
            Debug.LogError(_logPrefix + message, context);
        }
        #endregion
    }

    internal enum LogLevel
    {
        None = 0,
        Errors = 1,
        Warnings = 2,
        All = 3
    }
}