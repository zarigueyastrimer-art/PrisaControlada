using UnityEngine;

//TP2 - Matías Morilla
public static class TransformExtensions
{
    // Extension de clase: el "this" del primer parametro hace que se llame
    // como si fuera un metodo propio de Transform (transform.FindRecursive(...)),
    // sin heredar ni modificar la clase original.
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
