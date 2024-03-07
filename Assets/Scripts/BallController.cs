using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using Unity.Netcode;

public class BallController : MonoBehaviour
{
    private Rigidbody rb;
    private bool firstShot = true;
    public bool moving = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision other)
    {
        if (firstShot && other.gameObject.CompareTag("Terrain"))
        {
            // Freeze rotation when hitting the ground for the first time
            rb.freezeRotation = true;
            firstShot = false; // Set firstShot to false to prevent further freezing
        }

        // if (moving) {
        //     if (rb.velociy.magnitude )
        // }
    }
}
