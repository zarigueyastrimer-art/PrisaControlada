using UnityEngine;

public static class TransformExtensions
{
    public static Transform FindRecursive(this Transform transform, string name, bool includeInactive = false)
    {
        foreach(var child in transform.GetComponentsInChildren<Transform>(includeInactive))
        {
            if(child.name.Equals(name))
            {
                return child;
            }
        }
        return null;
    }
}
