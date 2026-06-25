using ETS2LA.Shared;
using System.Numerics;

namespace InternalVisualization;

public static class Utils
{
    public static Vector2 WorldToScreen(Vector3 worldPos, Vector3 center, Vector2 windowSize)
    {
        float scale = InternalVisualizationConstants.Scale;
        float screenX = (float)((worldPos.X - center.X) * scale + windowSize.X / 2);
        float screenY = (float)((worldPos.Z - center.Z) * scale + windowSize.Y / 2);
        return new Vector2(screenX, screenY);
    }

    public static float ToDegrees(float radians)
    {
        return radians * (180f / (float)Math.PI);
    }

    public static float ToRadians(float degrees)
    {
        return degrees * ((float)Math.PI / 180f);
    }
}