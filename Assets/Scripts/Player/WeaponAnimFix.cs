using UnityEngine;

namespace PrisaControlada.Player
{
    public class WeaponAnimFix : MonoBehaviour
    {
        private void Awake()
        {
            RuntimeAnimatorController armCtrl = Resources.Load<RuntimeAnimatorController>("ShotgunArmAnimator");
            RuntimeAnimatorController gunCtrl = Resources.Load<RuntimeAnimatorController>("ShotgunGunAnimator");

            foreach (Animator a in GetComponentsInChildren<Animator>(true))
            {
                if (a.runtimeAnimatorController != null) continue;
                if (a.gameObject.name == "ShotgunArm" && armCtrl != null)
                {
                    a.runtimeAnimatorController = armCtrl;
                    Debug.Log($"[WeaponAnimFix] Assigned arm controller");
                }
                if (a.gameObject.name == "ShotgunGun" && gunCtrl != null)
                {
                    a.runtimeAnimatorController = gunCtrl;
                    Debug.Log($"[WeaponAnimFix] Assigned gun controller");
                }
            }
        }
    }
}
