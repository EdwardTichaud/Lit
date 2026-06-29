using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface IBuffable
{
    public void ApplyBuff(float value);
    public void RemoveBuff(float value);
}