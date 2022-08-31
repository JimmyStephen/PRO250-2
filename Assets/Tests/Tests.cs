using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Projectiles;
using UnityEngine;
using UnityEngine.TestTools;


public class Tests
{

    [Test]
    public void maxHealth()
    {
        var health = new Health();
        Assert.AreEqual(health.MaxHealth, 100);
    }

    [Test]
    public void resetHealth()
    {
        var health = new Health();
        health.CurrentHealth = 60;
        health.ResetHealth();
        Assert.AreEqual(health.CurrentHealth, 100);
    }

    [Test]
    public void spawnerTimer()
    {
        var spawner = new WeaponSpawner();
        Assert.IsTrue(spawner.getTimer() > spawner.minTime && spawner.getTimer() < spawner.maxTime);
    }

    [Test]
    public void testTeleportOnCD()
    {
        var teleport = new Teleporter();
        teleport.cooldown = 5;
        Assert.AreEqual(0, teleport.OnTestTriggerEnter(null));        
    }

    [Test]
    public void testTeleportOffCD()
    {
        var teleport = new Teleporter();
        teleport.cooldown = -1;
        Assert.AreEqual(1, teleport.OnTestTriggerEnter(null));
    }

    [Test]
    public void testMultiTeleportNoLocations()
    {
        var teleport = new MultiTeleporter();
        Assert.AreEqual(teleport.GetValidLocation("Team"), null);
    }
}
