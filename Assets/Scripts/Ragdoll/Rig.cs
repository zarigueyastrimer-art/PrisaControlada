using System.Linq;
using UnityEngine;

//TP2 - Matías Morilla
public class Rig
{
    public Transform Pelvis { get; private set; }

    public Transform LeftHips { get; private set; }
    public Transform LeftKnee { get; private set; }

    public Transform RightHips { get; private set; }
    public Transform RightKnee { get; private set; }

    public Transform LeftArm { get; private set; }
    public Transform LeftElbow { get; private set; }

    public Transform RightArm { get; private set; }
    public Transform RightElbow { get; private set; }

    public Transform MiddleSpine { get; private set; }
    public Transform Head { get; private set; }

    public static RigType[] RigTypes
    {
        get
        {
            return Resources.LoadAll("RigTypes", typeof(RigType)).Cast<RigType>().ToArray();
        }
    }

    private int rigIndex = -1;

    public void DetermineRigType(Transform transform)
    {
        SkinnedMeshRenderer skinnedMeshRenderer = transform.GetComponentInChildren<SkinnedMeshRenderer>();

        if(skinnedMeshRenderer == null)
        {
            Debug.LogWarning($"No skinned mesh renderer found in {transform.name}");
            return;
        }

        RigType[] availableRigs = RigTypes;
        for(int i = 0; i < availableRigs.Length; i++)
        {
            if(skinnedMeshRenderer.rootBone != null &&
               skinnedMeshRenderer.rootBone.name == availableRigs[i].Pelvis)
            {
                rigIndex = i;
                break;
            }
        }

        if(rigIndex == -1)
        {
            Debug.LogWarning($"No suitable rig types found in Resources/RigTypes");
            return;
        }
    }

    // FindRecursive es la extension de TransformExtensions: busca el hueso por
    // nombre aunque este anidado adentro de otro, no solo en el primer nivel.
    public void GetParts(Transform transform)
    {
        RigType[] availableRigs = RigTypes;
        if (rigIndex < 0 || rigIndex >= availableRigs.Length)
        {
            Debug.LogWarning("No se pueden obtener las partes: el rig no fue reconocido.");
            return;
        }

        RigType rig = availableRigs[rigIndex];
        Pelvis = transform.FindRecursive(rig.Pelvis);
        LeftHips = transform.FindRecursive(rig.LeftHips);
        LeftKnee = transform.FindRecursive(rig.LeftKnee);
        RightHips = transform.FindRecursive(rig.RightHips);
        RightKnee = transform.FindRecursive(rig.RightKnee);
        LeftArm = transform.FindRecursive(rig.LeftArm);
        LeftElbow = transform.FindRecursive(rig.LeftElbow);
        RightArm = transform.FindRecursive(rig.RightArm);
        RightElbow = transform.FindRecursive(rig.RightElbow);
        MiddleSpine = transform.FindRecursive(rig.MiddleSpine);
        Head = transform.FindRecursive(rig.Head);
    }
}
