using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface IDebuffable
{
    public void ApplyDebuff(float value);
    public void RemoveDebuff(float value);
}