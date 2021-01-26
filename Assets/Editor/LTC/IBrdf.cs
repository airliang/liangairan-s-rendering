using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBrdf 
{
    float Eval(Vector3 V, Vector3 L, float alpha, ref float pdf);

    Vector3 Sample(Vector3 V, float alpha, float U1, float U2);
}
