using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Add this to your player prefab (same GameObject with NetworkObject)
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        // Set to false so the owning client controls its own transform
        return false;
    }
}
