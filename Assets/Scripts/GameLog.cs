using System.Diagnostics;
using Object = UnityEngine.Object;

// Mensajes informativos del juego ("Nivel 2: Fondo marino", "Boss: fase 2/3"...) que solo sirven para depurar.
//
// [Conditional]: el compilador BORRA las llamadas a Info cuando no está definido ninguno de los dos símbolos,
// es decir, en una build normal (no de desarrollo). Y no solo la llamada: tampoco se evalúan sus argumentos,
// así que un $"Puntuación: {score}" no construye ningún string. Con Debug.Log a pelo, ese texto se crea y se
// escribe en Player.log en cada acierto aunque nadie lo lea.
//
// Los avisos y errores (Debug.LogWarning / LogError) se dejan tal cual: esos sí interesan en la build final,
// porque acaban en Player.log y ayudan a diagnosticar un fallo en el equipo de otra persona.
public static class GameLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string message, Object context = null)
    {
        UnityEngine.Debug.Log(message, context);
    }
}
