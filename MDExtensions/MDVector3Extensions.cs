using Godot;

namespace MD
{
    /// <summary>
    /// Extension class to provide Vector3 methods
    /// </summary>
    public static class MDVector3Extensions
    {
        /// <summary>
        /// Converta Vector3 to Vector2
        /// </summary>
        /// <param name="Instance">The vector to convert</param>
        /// <returns>A new vector2 based on the X, Y of the Vector3</returns>
        public static Vector2 To2D(this Vector3 Instance)
        {
            return new Vector2(Instance.x, Instance.y);
        }
    }
}