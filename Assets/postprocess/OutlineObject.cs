using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OutlineObject : MonoBehaviour {

	// Use this for initialization
	void Start () {
        OutlineManager.GetInstance().AddOutlineObject(transform);
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
