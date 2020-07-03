using Godot;

/*
 * MDControlExtensions
 *
 * Extension class to provide Vector3 methods
 */
public static class MDVector3Extensions
{
    public static Vector2 To2D(this Vector3 Instance)
    {
        return new Vector2(Instance.x, Instance.y);
    }
}