using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private GameObject _projectile;
    private GameObject projectile, hole;
    [SerializeField] private float _projectileSpeed = 5f;
    [SerializeField] private float _cooldown = 0.5f;
    [SerializeField] private float _spawnDist = 1f;

    private float _lastFired = float.MinValue;
    private float club = 1;
    private bool _fired;
    private bool projectileSpawned = false;
    private bool inPlay, isShooting = false;


    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetMouseButtonDown(0) && inPlay)
        {
            var dir = transform.forward + transform.up/2;

            // NOTES -- TEST NOT FLATTENING Vec3 TO CREATE BETTER ANGLE SHOTS
            if (projectileSpawned && Vector3.Distance(transform.position, projectile.transform.position) < 3f) {
                isShooting = true;
                dir = hole.transform.Find("Hole").position - projectile.transform.position;
                dir.y = 0f;
                dir.Normalize();
                switch (club)
                {
                    case 0: 
                        dir.y = 0.3f;
                        _projectileSpeed = 50f;
                        break;
                    case 1:
                        dir.y = 0.5f;
                        _projectileSpeed = 25f;
                        break;
                    case 2:
                        _projectileSpeed = 10f;
                        break;
                }
            }

            // Send off the request to be executed on all clients
            RequestFireServerRpc(dir);

            // Fire locally immediately
            ExecuteShoot(dir);
            StartCoroutine(ToggleLagIndicator());
        }
    }

    [ServerRpc]
    private void RequestFireServerRpc(Vector3 dir)
    {
        FireClientRpc(dir);
    }

    [ClientRpc]
    private void FireClientRpc(Vector3 dir)
    {
        if (!IsOwner) ExecuteShoot(dir);
    }

    private void ExecuteShoot(Vector3 dir)
    {
        if (!projectileSpawned) {
            projectile = Instantiate(_projectile, transform.position + transform.forward * _spawnDist, Quaternion.identity);
            projectileSpawned = true;
        } else if (isShooting) {
            projectile.GetComponent<Rigidbody>().AddRelativeForce((dir * _projectileSpeed), ForceMode.Impulse);
            isShooting = false;
        }
        //AudioSource.PlayClipAtPoint(_spawnClip, transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Tee") && !projectileSpawned) {
            inPlay = true;
            club = 0;
            hole = other.transform.parent.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Tee") && !projectileSpawned) {
            inPlay = false;
        }
        club = 1; 
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        if (_fired) GUILayout.Label("FIRED LOCALLY");

        GUILayout.EndArea();
    }

    /// <summary>
    /// If you want to test lag locally, go into the "NetworkButtons" script and uncomment the artificial lag
    /// </summary>
    /// <returns></returns>
    private IEnumerator ToggleLagIndicator()
    {
        _fired = true;
        yield return new WaitForSeconds(0.2f);
        _fired = false;
    }
}