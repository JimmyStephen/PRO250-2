using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SniperScopeBase: MonoBehaviour
{
    /*[SerializeField] private Camera playerCamera;*/
    // Start is called before the first frame update
    public event EventHandler OnRifleUp;
    public event EventHandler OnRifleDown;
    public event EventHandler OnZoomIn;
    public event EventHandler OnZoomOut;

    [SerializeField] private Animator animator;
    
    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            //animator.SetBool("RifleDown", false);
            OnRifleUp?.Invoke(this, EventArgs.Empty);
        }
        if (Input.GetMouseButtonUp(1))
        {
           // animator.SetBool("RifleDown", true);
            OnRifleDown?.Invoke(this, EventArgs.Empty);

        }

        if (Input.GetMouseButton(1) || Input.GetKey(KeyCode.Z))
        {
            //animator.SetBool("RifleDown", false);
            OnZoomIn?.Invoke(this, EventArgs.Empty);
        }
        else 
        {
            OnZoomOut?.Invoke(this, EventArgs.Empty);
        }
    }
}
