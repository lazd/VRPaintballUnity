using UnityEngine;
using System.Collections;

public class Knife : MonoBehaviour
{
    public int damage = 100;

    public AudioClip hitSound;

    private void Update()
    {
        Debug.DrawRay(transform.position - (transform.up * 0.2f), transform.up, Color.red, 0.65f);

        // Check if the knife is stabby
        RaycastHit hit;
        if (Physics.Raycast(transform.position - (transform.up * 0.2f), transform.up, out hit, 0.65f)) {
            var hitObject = hit.collider.gameObject;

            if (hitObject == gameObject) {
                // Don't let the knife paint itself...
                return;
            }

            // Subtract health
            if (hit.rigidbody) {
                var health = hit.rigidbody.gameObject.GetComponent<Health>();
                if (health != null)
                {
                    health.TakeDamage(damage);

                    AudioSource.PlayClipAtPoint(hitSound, transform.position);
                }
            }

            // Add a splat
            GetComponent<BasicCollisionPrinter>().PrintCollision(hit.point, hit.normal, hitObject);
        }
    }
}
