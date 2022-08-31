using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SniperZoom : MonoBehaviour
{
    //[SerializeField] public Camera playerCamera;
    // Start is called before the first frame update

    Camera playerCamera;
    float fieldOfView = 0;

    private void Awake()
    {
        playerCamera = Camera.main;
        fieldOfView = playerCamera.fieldOfView;

        SniperScopeBase playerScope = GetComponent<SniperScopeBase>();
        playerScope.OnRifleDown += playerScope_OnRifleDown;
        playerScope.OnRifleUp += playerScope_OnRifleUp;
        playerScope.OnZoomIn += playerScope_OnZoomIn;
        playerScope.OnZoomOut += playerScope_OnZoomOut;
    }

    
    void OnDisable()
    {
        playerCamera.fieldOfView = fieldOfView;
    }


    private void playerScope_OnZoomOut(object sender, EventArgs e)
    {
        playerCamera.fieldOfView = fieldOfView;
    }

    private void playerScope_OnZoomIn(object sender, EventArgs e)
    {
        playerCamera.fieldOfView = 20;
        
    }

    private void playerScope_OnRifleUp(object sender, EventArgs e)
    {
        playerCamera.fieldOfView = 30;
    }

    private void playerScope_OnRifleDown(object sender, EventArgs e)
    {
        playerCamera.fieldOfView = 30;
    }
}
