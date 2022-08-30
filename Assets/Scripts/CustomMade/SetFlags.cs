using Fusion.KCC;
using Projectiles;
//using Projectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetFlags : MonoBehaviour
{
    [Header("Should you set certain flags (false means keep current)")]
    [SerializeField] bool setImmortal = false;
    [SerializeField] bool setTeam = false;
    [SerializeField] bool setLives = false;
    [SerializeField] bool useLives = false;
    [SerializeField] bool resetHealth = false;
    [SerializeField] bool setSpawnpoints = false;
    [SerializeField] bool giveGuns = false;

    [Header("More Info")]
    [SerializeField] float immortalityDuration = 0;
    [SerializeField] int lives = 1;
    [Header("What guns to give")]
    [SerializeField] Weapon[] weaponsToGive;
    [Header("Team names, leave blank to have it auto choose")]
    [SerializeField] string teamName = null;
    [SerializeField] string[] teamNames;
    [Header("What are the spawnpoints")]
    [SerializeField] SpawnPoint[] spawnPoints;

    private void OnTriggerEnter(Collider other)
    {
        if (setTeam)
        {
            if (teamName != null && teamName != "")
            {
                other.transform.root.tag = teamName;
            }
        }

        if(giveGuns && weaponsToGive.Length > 0)
        {
            //give the player all guns in the array
            foreach(var weapon in weaponsToGive)
            {
                Debug.Log("Gave weapon: " + weapon.DisplayName);
            }
        }

        other.transform.root.TryGetComponent<KCC>(out KCC kcc);
        if(kcc != null && setSpawnpoints)
        {
            foreach(var sp in spawnPoints)
            {
                sp.SetActive(true);
            }
            kcc.setSpawnpoints(spawnPoints);
        }


        other.transform.root.TryGetComponent<Health>(out Health health);
        Debug.Log("Health Found: " + (health != null));
        if (health == null) return;

        if (setImmortal)
        {
            health.ClearImmortality();
            health.SetImmortality(immortalityDuration);
        }

        if (useLives)
        {
            health.UseLives = true;
        }

        if (setLives)
        {
            //set the lives
            Debug.Log("Set Lives to: " + lives);
            health.CurrentLives = lives;
        }

        if (resetHealth)
        {
            health.ResetHealth();
        }
    }
}
