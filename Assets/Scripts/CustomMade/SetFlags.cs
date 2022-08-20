using Projectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetFlags : MonoBehaviour
{
    [Header("Should you set certain flags (false means keep current)")]
    [SerializeField] bool setImmortal = false;
    [SerializeField] bool setTeam = false;
    [SerializeField] bool setLives = false;
    [SerializeField] bool resetHealth = false;

    [Header("More Info")]
    [SerializeField] float immortalityDuration = 0;
    [SerializeField] int lives = 1;
    [Header("Team names, leave blank to have it auto choose")]
    [SerializeField] string teamName = null;
    [SerializeField] string[] teamNames;

    private void OnTriggerEnter(Collider other)
    {
        other.transform.root.TryGetComponent<Health>(out Health health);
        if (setImmortal)
        {
            health.ClearImmortality();
            health.SetImmortality(immortalityDuration);
        }

        if (setTeam)
        {
            if(teamName != null && teamName != "")
            {
                other.transform.root.tag = teamName;
            }
        }

        if (setLives)
        {
            //set the lives
            other.transform.root.TryGetComponent<Health>(out Health health);
        }

        if (resetHealth)
        {
            health.ResetHealth();
        }
    }
}
