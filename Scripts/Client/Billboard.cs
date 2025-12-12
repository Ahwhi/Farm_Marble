using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour {
    Camera cam;
    void Start() { cam = Camera.main; }
    void LateUpdate() {
        if (!cam) return;
        var fwd = (transform.position - cam.transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }
}
