using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion.KCC;

public class MultiTeleporter : MonoBehaviour
{
    //where to send you to
    [Header("Required")]
    [SerializeField] Location[] receivers;
    [Header("How this teleporter should be used")]
    [Header("Only select one")]
    [SerializeField] bool inOrder;
    [SerializeField] bool useTeams;

    private int ReceiverIndex = 0;

    private void OnTriggerEnter(Collider other)
    {
        //check to see if the collision is with a player
        //if it isnt cancel the teleport
        KCC kcc = null;
        other.transform.root.TryGetComponent<KCC>(out kcc);
        if (kcc == null) return;

        //if you are going in order teleport to the next teleporter
        if (inOrder)
        {
            Teleport(receivers[ReceiverIndex], kcc);
            ReceiverIndex = (ReceiverIndex + 1 < receivers.Length) ? ReceiverIndex + 1 : 0;
        }
        //else if you are useing teams get the first valid teleporter that uses the same team tag
        else if (useTeams)
        {
            Teleport(GetValidLocation(other.tag), kcc);
        }
        //finally if nothing is selected choose a random teleporter
        else
        {
            Teleport(receivers[Random.Range(0, receivers.Length)], kcc);
        }
    }

    public Location GetValidLocation(string teamColor)
    {
        foreach(var v in receivers)
        {
            if (v.getTeamColor() == teamColor) return v;
        }
        return receivers[0];
    }
    private void Teleport(Location location, KCC player)
    {
        if (location.setRotation)
        {
            player.TeleportRPC(location.landingLocation.position, location.lookPitch, location.lookYaw);
        }
        else
        {
            player.SetPosition(location.landingLocation.position);
        }
    }
}
