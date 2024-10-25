using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkTimer
{
    float timer;

    public float minTimeBetweenTicks { get; }
    public int currentTick;

    public NetworkTimer(float serverTickRate)
    {
        minTimeBetweenTicks = 1f / serverTickRate;
    }

    public void Update(float deltaTime)
    {
        timer += deltaTime;
    }
    public bool ShouldTick()
    {
        if(timer >= minTimeBetweenTicks){
            timer -= minTimeBetweenTicks;
            currentTick++;
            return true;
        }

        return false;
    }

}
