using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.KCC;

public class Teleporter : MonoBehaviour
{
    //where to send you to
    [Header("Required")]
    [SerializeField] Teleporter receiver;
    [SerializeField] Transform landingLocation;
    [SerializeField] float afterTeleportCooldown = 5;
    [Header("Optional")]
    //[SerializeField] bool stopMomentum = false;
    [SerializeField] bool setRotation = false;
    [SerializeField] float lookYaw = 0;
    [SerializeField] float lookPitch = 0;

    //[HideInInspector] 
    public float cooldown = 0;

    private void Update()
    {
        cooldown -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (cooldown > 0) return;

        receiver.cooldown = afterTeleportCooldown;

        KCC kcc;
        if(other.transform.root.TryGetComponent<KCC>(out kcc)){
            if (receiver.setRotation)
            {
                kcc.TeleportRPC(receiver.landingLocation.position, receiver.lookPitch, receiver.lookYaw);
            }
            else
            {
                kcc.SetPosition(receiver.landingLocation.position);
            }
        }
    }

    //test trigger enter
    //0 it is on cd, 1 it made it through without teleporting, 2 it teleported
    public int OnTestTriggerEnter(Collider other)
    {
        if (cooldown > 0) return 0;

        receiver.cooldown = afterTeleportCooldown;

        KCC kcc;
        if (other.transform.root.TryGetComponent<KCC>(out kcc))
        {
            if (receiver.setRotation)
            {
                kcc.TeleportRPC(receiver.landingLocation.position, receiver.lookPitch, receiver.lookYaw);
            }
            else
            {
                kcc.SetPosition(receiver.landingLocation.position);
            }
            return 2;
        }

        return 1;
    }
}
