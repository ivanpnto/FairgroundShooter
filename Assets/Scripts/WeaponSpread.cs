using UnityEngine;

// Dispersión del disparo desde la cadera: funciones puras (con tests), como WeaponStatsCalculator.
//
// Problema que resuelve: antes el rayo salía siempre del centro exacto y apuntar (ADS) solo daba zoom, así que
// no había ningún motivo para apuntar. Ahora, desde la cadera, el rayo se desvía al azar dentro de un cono
// que se abre con cada disparo y se cierra solo; apuntando del todo el cono es 0 y la bala va al guion.
//
// La cruceta (CrosshairUI) dibuja este mismo ángulo: lo que se ve abierto es exactamente donde puede caer.
public static class WeaponSpread
{
    // Semiángulo del cono en grados. zoom = PlayerScope.ZoomAmount (0 = cadera, 1 = apuntando del todo)
    public static float Current(float hipSpread, float bloom, float zoom)
    {
        return Mathf.Max(0f, hipSpread + bloom) * (1f - Mathf.Clamp01(zoom));
    }

    // Apertura tras un disparo, con tope (en ráfaga se acumula, pero no saca las líneas de la pantalla)
    public static float AddShot(float bloom, float perShot, float max)
    {
        return Mathf.Clamp(bloom + perShot, 0f, Mathf.Max(0f, max));
    }

    // Vuelve a 0 con decaimiento exponencial: el mismo ritmo a cualquier FPS
    public static float Recover(float bloom, float speed, float deltaTime)
    {
        return bloom * Mathf.Exp(-Mathf.Max(0f, speed) * deltaTime);
    }

    // Desvía la dirección offsetDeg grados (x: a la derecha, y: hacia arriba) alrededor de los ejes de la cámara
    public static Vector3 Deviate(Vector3 direction, Vector3 cameraUp, Vector3 cameraRight, Vector2 offsetDeg)
    {
        Quaternion yaw = Quaternion.AngleAxis(offsetDeg.x, cameraUp);
        Quaternion pitch = Quaternion.AngleAxis(-offsetDeg.y, cameraRight);   // +x de la cámara: girar en − sube
        return yaw * pitch * direction;
    }

    // Grados del cono → píxeles en un Canvas de halfHeight píxeles de media altura, con la vista a fov grados
    public static float ToPixels(float angleDeg, float verticalFov, float halfHeight)
    {
        float halfFov = Mathf.Clamp(verticalFov, 1f, 179f) * 0.5f * Mathf.Deg2Rad;
        return Mathf.Tan(Mathf.Clamp(angleDeg, 0f, 89f) * Mathf.Deg2Rad) / Mathf.Tan(halfFov) * halfHeight;
    }
}
