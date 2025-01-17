using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using Unity.Netcode;
using UnityEngine;


public enum EnemyState
{
    GUARD,
    PATROL,
    CHASE,
    DEAD
}

[RequireComponent(typeof(NavMeshAgent))]
public class NetworkEnemyController : NetworkBehaviour
{
    private NetworkVariable<EnemyState> enemyState = new NetworkVariable<EnemyState>();
    private EnemyState _currentState;
    private NavMeshAgent agent;
    private Animator animator;
    private CharacterStats characterStats;

    public float sightRadius;
    public bool isGuard;
    private float speed;

    public bool isReturnToOrigin = false;
    public GameObject attackTarget;
    private NetworkVariable<float> lastAttackTime = new NetworkVariable<float>();
    public float lookAtTime;
    private float remainLookAtTime;
    private Quaternion guardRotation;
    public LayerMask targetLayer;
    public float groundCheckDistance = 5.0f;
    public LayerMask groundLayer;

    public float patrolRange;
    private Vector3 wayPoint;
    public Vector3 guardPos;

    public float deathDuration;
    private float deathTimer;

    private bool isWalk;
    private bool isChase;
    private bool isFollow;
    private bool isDead;

    public override void OnNetworkSpawn()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharacterStats>();
        speed = agent.speed;
        isGuard = true;
        remainLookAtTime = lookAtTime;
        guardPos = transform.position;
        guardRotation = transform.rotation;

        if (IsServer)
        {
            if (isGuard)
            {
                enemyState.Value = EnemyState.GUARD;
            }
            else
            {
                enemyState.Value = EnemyState.PATROL;
                GetNewWayPointServerRpc();
            }
        }

        enemyState.OnValueChanged += OnSpiderStateChange;


    }

    private void OnSpiderStateChange(EnemyState prev, EnemyState next)
    {
        _currentState = next;
    }

    public override void OnDestroy()
    {
        enemyState.OnValueChanged -= OnSpiderStateChange;
        base.OnDestroy();
    }

    void Update()
    {
        if (IsServer)
        {
            SwitchState();
            lastAttackTime.Value -= Time.deltaTime;
            SwitchAnimation();
            AdaptToTerrain();
        }

    }

    void SwitchAnimation()
    {
        animator.SetBool("Walk", isWalk);
        animator.SetBool("Chase", isChase);
        animator.SetBool("Follow", isFollow);
        animator.SetBool("Death", isDead);
    }

    void SwitchState()
    {
        if (isDead)
        {
            enemyState.Value = EnemyState.DEAD;
        }
        else if (FoundPlayer())
        {
            enemyState.Value = EnemyState.CHASE;
        }

        switch (enemyState.Value)
        {
            case EnemyState.GUARD:
                GuardBehavior();
                break;
            case EnemyState.PATROL:
                PatrolBehavior();
                break;
            case EnemyState.CHASE:
                ChaseBehavior();
                break;
            case EnemyState.DEAD:
                // Implement Death behavior
                DeadBehavior();
                break;
        }
    }

    [ServerRpc]
    void GetNewWayPointServerRpc()
    {
        remainLookAtTime = lookAtTime;

        float randomX = UnityEngine.Random.Range(-patrolRange, patrolRange);
        float randomZ = UnityEngine.Random.Range(-patrolRange, patrolRange);

        Vector3 randomPoint = new Vector3(guardPos.x + randomX, transform.position.y, guardPos.z + randomZ);

        NavMeshHit hit;
        wayPoint = NavMesh.SamplePosition(randomPoint, out hit, patrolRange, 1) ? hit.position : transform.position;
    }

    [ServerRpc]
    void AttackServerRpc()
    {
        if (!IsServer || attackTarget == null) return;

        transform.LookAt(attackTarget.transform);

        if (TargetInAttackRange())
        {
            animator.SetTrigger("Attack");

        }
    }

    bool TargetInAttackRange()
    {
        if (attackTarget != null)
            return Vector3.Distance(attackTarget.transform.position, transform.position) <= characterStats.attackData.attackRange;
        else
            return false;
    }

    bool FoundPlayer()
    {
        if (isReturnToOrigin)
        {
            return false;
        }

        var colliders = Physics.OverlapSphere(transform.position, sightRadius, targetLayer);

        foreach (var target in colliders)
        {
            if (target.gameObject == attackTarget)
            {
                // if player is ragdolled, break and try to find another target
                if (!target.GetComponent<BasicPlayerController>().enabled)
                {
                    attackTarget = null;
                    break;
                }
                return true;
            }
        }

        foreach (var target in colliders)
        {
            if (target.CompareTag("Player"))
            {
                if (!target.GetComponent<BasicPlayerController>().enabled)
                { // dont set player as target if player is ragdolled
                    return false;
                }
                attackTarget = target.gameObject;
                return true;
            }
        }

        attackTarget = null;
        return false;
    }

    void GuardBehavior()
    {
        isChase = false;
        if (transform.position != guardPos)
        {
            isWalk = true;


            agent.isStopped = false;
            agent.destination = guardPos;


            if (Math.Abs(guardPos.x - transform.position.x) < agent.stoppingDistance && Math.Abs(guardPos.z - transform.position.z) < agent.stoppingDistance)
            {
                isWalk = false;
                //transform.rotation = Quaternion.Lerp(transform.rotation, guardRotation, 0.01f);
                isReturnToOrigin = false;
            }

        }
    }

    void PatrolBehavior()
    {
        isChase = false;
        agent.speed = speed * 0.5f;

        if (Vector3.Distance(transform.position, wayPoint) <= agent.stoppingDistance)
        {
            isWalk = false;
            isReturnToOrigin = false;
            if (remainLookAtTime > 0)
                remainLookAtTime -= Time.deltaTime;
            else
                GetNewWayPointServerRpc();
        }
        else
        {
            isWalk = true;
            agent.SetDestination(wayPoint);
        }
    }

    void ChaseBehavior()
    {
        isWalk = false;
        isChase = true;

        agent.speed = speed;

        if (!FoundPlayer())
        {
            isFollow = false;
            if (remainLookAtTime > 0)
            {
                agent.SetDestination(transform.position);
                remainLookAtTime -= Time.deltaTime;
            }
            else
            {
                isReturnToOrigin = true;
                remainLookAtTime = lookAtTime;
                enemyState.Value = isGuard ? EnemyState.GUARD : EnemyState.PATROL;
            }
        }
        else
        {
            isFollow = true;
            agent.isStopped = false;
            agent.SetDestination(attackTarget.transform.position);
        }


        if (TargetInAttackRange())
        {
            isFollow = false;
            agent.isStopped = true;
            remainLookAtTime = lookAtTime;
            if (lastAttackTime.Value <= 0)
            {
                lastAttackTime.Value = characterStats.attackData.coolDown;
                if (!attackTarget.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Strike"))
                {
                    AttackServerRpc();
                }
            }
        }

    }

    private void DeadBehavior()
    {
        isWalk = false;
        isChase = false;
        isFollow = false;
        agent.isStopped = true;
        if (deathTimer > 0)
        {
            deathTimer -= Time.deltaTime;
        }
        else
        {
            isDead = false;
            agent.isStopped = false;
            enemyState.Value = isGuard ? EnemyState.GUARD : EnemyState.PATROL;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PerformDead(other);
    }

    private void OnTriggerStay(Collider other)
    {
        PerformDead(other);
    }

    private void OnTriggerExit(Collider other)
    {
        PerformDead(other);
    }

    private void PerformDead(Collider other)
    {
        if (isDead) return;

        if (other.gameObject.CompareTag("Player"))
        {
            if(attackTarget != null && attackTarget != other.gameObject && other.GetComponent<BasicPlayerController>().enabled)
            {
                attackTarget = other.gameObject;
            }

            if (other.GetComponent<BasicPlayerController>().enabled && other.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Strike"))
            {
                if (!IsOwner) return;
                DeadStateServerRpc();
            }
            
        }
    }

    [ServerRpc]
    private void DeadStateServerRpc()
    {
        isDead = true;
        deathTimer = deathDuration;
    }

    // delay for x second before player ragdolls
    private IEnumerator DelayedPlayerRagdoll(NetworkObject targetNetworkObject)
    {
        yield return new WaitForSeconds(0.5f);
        //attackTarget.GetComponent<BasicPlayerController>()._ragdollOnOff.PerformRagdoll();
        targetNetworkObject.GetComponent<BasicPlayerController>()._ragdollOnOff.PerformRagdoll();
    }

    [ClientRpc]
    void SpiderAttackPlayerClientRpc(ulong targetNetworkObjectId)
    {
        NetworkObject targetNetworkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetNetworkObjectId];
        if (targetNetworkObject != null && targetNetworkObject.IsOwner)
        {
            Debug.Log("Spider attacked player: " + targetNetworkObject.name);
            if (!targetNetworkObject.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Strike"))
            {
                StartCoroutine(DelayedPlayerRagdoll(targetNetworkObject));
            }

        }
    }

    // Animation Event
    [ClientRpc]
    public void HitClientRpc()
    {
        float attackRange = characterStats.attackData.attackRange + 1f;
        float attackAngle = 60f;
        int numberOfRays = 100;
        Vector3 origin = transform.TransformPoint(new Vector3(0, 0.5f, -1f)); 
        float startAngle = -attackAngle;
        float angleIncrement = attackAngle * 2 / numberOfRays;
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        for (int i = 0; i <= numberOfRays; i++)
        {
            float currentAngle = startAngle + (angleIncrement * i);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            Ray ray = new Ray(origin, direction);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, attackRange, targetLayer))
            {
                Debug.DrawLine(ray.origin, hit.point, Color.red, 2f);
                if (!hitTargets.Contains(hit.collider.gameObject))
                {
                    hitTargets.Add(hit.collider.gameObject);
                    NetworkObject targetNetworkObject = hit.collider.gameObject.GetComponent<NetworkObject>();
                    if (targetNetworkObject != null && targetNetworkObject.IsOwner)
                    {
                        SpiderAttackPlayerClientRpc(targetNetworkObject.NetworkObjectId);
                    }
                }

            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * attackRange, Color.yellow, 2f);
            }
        }
    }

    private void AdaptToTerrain()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -transform.up, out hit, groundCheckDistance, groundLayer))
        {
            Debug.DrawRay(transform.position, -transform.up * groundCheckDistance, Color.red);
            Quaternion toRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, Time.deltaTime * 20);
        }
    }

    // void OnDrawGizmosSelected()
    // {
    //     Gizmos.color = Color.blue;
    //     Gizmos.DrawWireSphere(transform.position, sightRadius);
    //     Gizmos.color = Color.green;
    //     Gizmos.DrawWireSphere(guardPos, 0.1f);
    // }


}
