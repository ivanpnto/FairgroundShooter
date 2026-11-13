using UnityEngine;

// Referencia cacheada al ÚNICO objeto de tipo T de la escena (ScoreManager, GameTimer, PlayerHealth...).
//
// Problema que resuelve: Target y MinionLunge llamaban a FindFirstObjectByType en cada diana que aparecía
// y en cada bonus. Esa búsqueda recorre todos los objetos de la escena: es barata una vez, pero no tiene
// sentido repetirla decenas de veces por nivel para obtener siempre el mismo objeto.
//
// Por qué una clase estática GENÉRICA: C# crea una copia de los campos estáticos por cada T, así que
// SceneCache<ScoreManager> y SceneCache<GameTimer> tienen cada una su propia caché sin diccionarios.
//
// Por qué se comprueba "instance == null" en cada Get: Unity sobrecarga == para que un objeto DESTRUIDO
// compare igual a null. Al cambiar de escena (o con "Enter Play Mode" sin recargar el dominio, donde los
// estáticos sobreviven entre partidas) la referencia vieja apunta a un objeto destruido, y así se vuelve
// a buscar sola. Sin esa comprobación, la caché devolvería un objeto muerto.
//
// Como FindFirstObjectByType, no encuentra objetos desactivados: el comportamiento es el mismo que antes.
public static class SceneCache<T> where T : Object
{
    private static T instance;

    public static T Get()
    {
        if (instance == null)
            instance = Object.FindFirstObjectByType<T>();
        return instance;
    }
}
