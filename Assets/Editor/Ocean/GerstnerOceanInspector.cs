using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(GerstnerOcean))]
public class OceanInspector : Editor
{
    public override void OnInspectorGUI()
    {
        GerstnerOcean bsdf = target as GerstnerOcean;

        Undo.RecordObject(bsdf, "Record BSDFMaterial");

        

    }
}
