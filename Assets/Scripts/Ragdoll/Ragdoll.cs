using System.Collections.Generic;
using UnityEngine;

public class Ragdoll : MonoBehaviour
{
    [SerializeField] private List<RagdollComponent> ragdollComponents = new List<RagdollComponent>();
    public List<RagdollComponent> RagdollComponents => ragdollComponents;

    public void EnableRagdoll()
    {
        for(int i = 0; i < ragdollComponents.Count; i++)
        {
            ragdollComponents[i].ComponentRigidbody.isKinematic = false;
            ragdollComponents[i].ComponentRigidbody.linearVelocity = ragdollComponents[i].ComponentRigidbody.angularVelocity = Vector3.zero;

            ragdollComponents[i].ComponentCollider.enabled = true;
        }
    }

    public void DisableRagdoll()
    {
        for(int i = 0; i < ragdollComponents.Count; i++)
        {
            ragdollComponents[i].ComponentRigidbody.isKinematic = true;
            ragdollComponents[i].ComponentCollider.enabled = false;
        }
    }

    public void PushRagdoll(Vector3 force)
    {
        for(int i = 0; i < ragdollComponents.Count; i++)
        {
            ragdollComponents[i].ComponentRigidbody.AddForce(force, ForceMode.Impulse);
        }
    }

    public void PushRagdoll(float force, Vector3 originPosition)
    {
        for(int i = 0; i < ragdollComponents.Count; i++)
        {
            Vector3 forceVector = (ragdollComponents[i].transform.position - originPosition).normalized * force;
            ragdollComponents[i].ComponentRigidbody.AddForce(forceVector, ForceMode.Impulse);
        }
    }

    public void PushRagdollUniform(float force, Vector3 originPosition)
    {
        Vector3 forceVector = (transform.position - originPosition).normalized * force;

        for(int i = 0; i < ragdollComponents.Count; i++)
        {
            ragdollComponents[i].ComponentRigidbody.AddForce(forceVector, ForceMode.Impulse);
        }
    }
}
