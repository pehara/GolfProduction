using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RagdollOnOff : NetworkBehaviour
{
    public CapsuleCollider mainCollider;
    public Rigidbody playerRB;
    public GameObject playerRig;
    public Animator playerAnimator;
    public GameObject playerClub;
    public BasicPlayerController BasicPlayerController;
    public Collider attack;


    public Transform clubParent;
    private Vector3 clubOffset;
    private Quaternion clubRotation;
    // PLAYER WILL RAGDOLL ON COLLISION WITH PlayerCollision TAG
    // PRESS R TO RESET RAGDOLL

    void Start()
    {
        Physics.IgnoreCollision(gameObject.GetComponent<Collider>(), attack, true);

        GetRagdollBits();
        foreach(Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach(Rigidbody rb in limbsRigidBodies)
        {
            rb.isKinematic = true;
        }

        playerAnimator.enabled = true;
        BasicPlayerController.enabled = true;
        mainCollider.enabled = true;
        playerRB.isKinematic = false;
        
        clubOffset = playerClub.transform.position - clubParent.position;
        clubRotation = Quaternion.Inverse(clubParent.rotation) * playerClub.transform.rotation;
    }

    void Update()
    {
        if(Input.GetKey("r"))
        {
            RagdollModeOff();
        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.tag == "PlayerCollision")
        {
            RagdollModeOn();
        }

    }

    Collider[] ragdollColliders;
    Rigidbody[] limbsRigidBodies;

    void GetRagdollBits()
    {
        ragdollColliders = playerRig.GetComponentsInChildren<Collider>();
        limbsRigidBodies = playerRig.GetComponentsInChildren<Rigidbody>();
    }

    void RagdollModeOn()
    {
        playerAnimator.enabled = false;
        BasicPlayerController.enabled = false;

        foreach(Collider col in ragdollColliders)
        {
            col.enabled = true;
        }
        foreach(Rigidbody rb in limbsRigidBodies)
        {
            rb.isKinematic = false;
        }

        mainCollider.enabled = false;
        playerRB.isKinematic = true;
    }

    void RagdollModeOff()
    {
        foreach(Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach(Rigidbody rb in limbsRigidBodies)
        {
            rb.isKinematic = true;
        }

        playerAnimator.enabled = true;
        BasicPlayerController.enabled = true;
        mainCollider.enabled = true;
        playerRB.isKinematic = false;

        playerClub.transform.position = clubParent.position + clubOffset;
        playerClub.transform.rotation = clubParent.rotation * clubRotation;
        playerClub.transform.SetParent(clubParent);
    }

}
