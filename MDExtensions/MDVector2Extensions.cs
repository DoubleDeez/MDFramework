using Godot;

namespace MD
{
    /// <summary>
    /// Extension class to provide Vector2 methods
    /// </summary>
    public static class MDVector2Extensions
    {
        /// <summary>
        /// Converts a Vector2 to Vector3 with Z of 0
        /// </summary>
        /// <param name="Instance">The Vector2 to convert</param>
        /// <returns>New Vector3 based on the Vector2 with 0 as Z value</returns>
        public static Vector3 To3D(this Vector2 Instance)
        {
            return new Vector3(Instance.x, Instance.y, 0);
        }
    }
}