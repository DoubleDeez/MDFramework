using Godot;
using System;

/*
 * MDControlExtensions
 *
 * Extension class to provide Vector2 methods
 */
public static class MDVector2Extension
{
    public static Vector3 To3D(this Vector2 Instance)
    {
        return new Vector3(Instance.x, Instance.y, 0);
    }
}
