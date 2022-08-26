using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Location : MonoBehaviour
{
    //This is just a storage script
    [Header("Important")]
    public Transform landingLocation;
    [SerializeField] string teamColor = "";
    [Header("What to do when players arive")]
    public bool setRotation = false;
    public float lookYaw = 0;
    public float lookPitch = 0;

    public string getTeamColor()
    {
        return teamColor;
    }
}
