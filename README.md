# Download

Secure Networking is available via the [Creator Companion](https://vcc.docs.vrchat.com/) as a VPM package!

### [⬇️ My VPM / Creator Companion Listing](https://vpm.drblackrat.xyz)

For Standalone Unity I also provide a Unity Package with every release.

# Support

If you have questions / need help feel free to join the support Discord!

#### [DrBlackRat Creations Discord](https://discord.drblackrat.xyz)

# What is Secure Networking?

Secure Networking is a networking system for VRChat that lets you control <b>who is allowed to send network data</b>.

Instead of using normal Udon manual syncing, it uses <b>network events with parameters</b>. This means the system can check who actually sent the data before applying it.

It also automatically keeps track of an <b>Authoritative Sender</b>. If they leave or are no longer allowed to send data, Secure Networking will find another valid sender and transfer the authority.

## Core Features

* Validate who sent network data
* Only allow specific players to send data
* Automatic Authoritative Sender handling
* Automatically send the current state to new players
* Retry sender validation when needed
* Latest state wins, preventing old data from overwriting newer data
* Optionally reset the network state when no valid sender is available
* Uses network events instead of normal manual Udon syncing

# How to use it

Secure Networking is made up of two scripts:

### `SecureNetworkBehaviour`

This is the script you inherit from for your own networking system.

You define:

* Who is allowed to send data
* How the network data is created
* What happens when data is received
* What happens when the network resets
* How a new Authoritative Sender should be selected

### `SecureNetworkingInstance`

This handles the actual networking for your `SecureNetworkBehaviour`.

It takes care of sending and receiving the network events, validating the sender, managing the Authoritative Sender, and handling new players joining.

You connect your `SecureNetworkBehaviour` to a `SecureNetworkingInstance` and then use the instance to send your network data.

# Example

Take a look at the included demo scene and example scripts! They should help you with understanding the setup and how everything connects.

You can find it in Unity under:

`Tools > DrBlackRat > Secure Networking`

# Setup
1. Create a script that inherits from `SecureNetworkBehaviour`.
2. Implement its required methods.
3. Add a `SecureNetworkingInstance` prefab to your scene.

   * You can find it under `Tools > DrBlackRat > Secure Networking`.
4. Connect your behaviour to the instance.
5. Call `_SendNetworkData()` whenever you want to send an update.

You can also call `_ValidateAllowedSenders()` whenever the players who are allowed to send data change.

# Important

Secure Networking is meant for syncing <b>state</b>, not for guaranteeing that every single update is received.

If multiple updates are sent, newer data can replace older data that has not been applied yet. This is intentional and helps prevent stale network data from being applied.

# Credits

#### This Asset was made by DrBlackRat:

https://drblackrat.xyz

#### If you like this, feel free to support me on Ko-fi!

https://ko-fi.com/drblackrat
