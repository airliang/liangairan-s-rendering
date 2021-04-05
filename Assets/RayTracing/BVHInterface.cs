using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class BVHInterface
{
    [DllImport("BVHLib")]
    public static extern int Add(int a, int b);
}
