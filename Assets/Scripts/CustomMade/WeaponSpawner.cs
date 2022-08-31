using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponSpawner : MonoBehaviour
{
    [Header("List of what can be spawned")]
    [SerializeField] GameObject[] weapons;
    [Header("How long between weapon spawns")]
    [SerializeField] float minTime = 0;
    [SerializeField] float maxTime = 5;

    private float timer = 0;

    void Start()
    {
        //Set the timer to a random range
        timer = Random.Range(minTime, maxTime);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            SpawnWeapon();
        }
    }

    /// <summary>
    /// This will spawn a random weapon
    /// </summary>
    void SpawnWeapon()
    {
        if (weapons.Length <= 0) return;


        //choose a random weapon
        int weaponIndex = Random.Range(0, weapons.Length);
        //spawn the chosen weapon
        GameObject temp = Instantiate(weapons[weaponIndex], transform.position, transform.rotation);
        //reset the timer
        timer = Random.Range(minTime, maxTime);
        //set the object to destroy when the timer reaches 0
            //This makes sure that there wont be a stack of 1billion items
        Destroy(temp, timer);
    }
}
