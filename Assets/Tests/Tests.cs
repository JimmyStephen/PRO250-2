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
}
