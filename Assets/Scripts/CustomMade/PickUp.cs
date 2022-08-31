using Projectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField]
    Weapon stored;

    private int weaponKeyInput = 1;

    private void OnTriggerEnter(Collider other)
    {
       
        // Debug.Log("We've made contact - " + other.tag);
        if (other.transform.root.CompareTag("Player"))
        {
            // Agent player = collider.GetComponent<Agent>();
            //transform.parent = other.transform;
            Debug.Log("We've made contact - " + other.transform.root.tag);

            Weapons myWeapons = other.transform.root.GetComponent<Weapons>();

            //List<Weapon> weapons = new List<Weapon>();
            //myWeapons.GetAllWeapons(weapons);

            //Debug.Log("name: " + stored.DisplayName);
            //int slot = weapons.IndexOf(stored);
            //Debug.Log("slot: " + slot);

            //myWeapons.AddWeapon(stored);
            weaponKeyInput++;
            myWeapons.PickupWeapon(stored.WeaponSlot, weaponKeyInput);
            myWeapons.SwitchWeapon(stored.WeaponSlot, true);
             
            Destroy(gameObject);
        }
    }

        void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
}
