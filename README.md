# Fairground Shooter

Juego de disparos en primera persona ambientado en una **galería de tiro de feria**: el jugador está delante de la
caseta y derriba recortes de animales «de chapa» que aparecen tras el decorado. Inspirado en Duck Hunt y en las
casetas de tiro clásicas.

Proyecto de la asignatura **Computer Animation and Video Games** (Grado en Ingeniería Informática).

**Unity 6.3 LTS** · URP · C# · Blender · Windows · versión 1.0.0

![Nivel 1: el bosque nocturno](Docs/img/nivel1-bosque.png)

---

## Índice

1. [El juego](#el-juego)
2. [Cómo se juega](#cómo-se-juega)
3. [Progresión: créditos, tienda y armas](#progresión-créditos-tienda-y-armas)
4. [Abrir, jugar y compilar el proyecto](#abrir-jugar-y-compilar-el-proyecto)
5. [Herramientas de desarrollo y pruebas](#herramientas-de-desarrollo-y-pruebas)
6. [Conceptos de la asignatura en el proyecto](#conceptos-de-la-asignatura-en-el-proyecto)
7. [Arquitectura](#arquitectura)
8. [Estructura del repositorio](#estructura-del-repositorio)
9. [Arte, sonido y recursos de terceros](#arte-sonido-y-recursos-de-terceros)
10. [Equipo](#equipo)

---

## El juego

Una partida recorre **tres niveles seguidos de 60 s** con la puntuación acumulada. Cada nivel tiene su decorado,
sus dianas e iluminación propia; es más difícil que el anterior (más vida, más velocidad, más dianas a la vez) y da
más puntos por diana. Dentro de cada nivel, el ritmo de aparición también crece. Entre niveles, el telón se cierra,
las luces bajan, se suma un **bonus de precisión** y un cartel anuncia el siguiente decorado antes de la cuenta atrás
**3-2-1-¡FUEGO!**

| Nivel | Decorado | Dianas | Mecánica propia |
|---|---|---|---|
| 1 · Bosque | Bosque nocturno con luna y luciérnagas | Zorro, conejo, búho que se asoma, pájaro, zorro lejano · **mofeta (prohibida)** | **Animales asustadizos:** si tienes al búho o al conejo en la mira, les sale un «!» que late y enrojece; a los 0,45 s se asustan y se esconden (el búho tras su arbusto, el conejo en su madriguera). Premia disparar rápido |
| 2 · Fondo marino | Arrecife con algas, burbujas y niebla azul | Pez payaso, medusa a impulsos, tortuga resistente · **neumático (prohibido)** | **Almeja con perla:** cada pocos segundos la almeja tiembla, se abre y deja ver una perla diminuta y brillante de muchos puntos durante un segundo; después se entierra y sale en otro punto de la arena. Premia apuntar con la mira |
| 3 · Cielo | Atardecer con nubes en cuatro profundidades y globos | Águila en picado, bandadas, globos que suben, dianas lejanas · **avioneta con cartel (prohibida)** | **Ráfagas de viento:** unas rachas blancas empiezan a cruzar la caseta en el sentido en que va a soplar; después la ráfaga, de fuerza y duración variables y con rachas, empuja todas las dianas y hace ondear los banderines. Obliga a corregir la puntería |

<p>
  <img src="Docs/img/nivel2-mar.png" width="49%" alt="Nivel 2: fondo marino">
  <img src="Docs/img/nivel3-cielo.png" width="49%" alt="Nivel 3: cielo al atardecer">
</p>

Al acabar, la puntuación se convierte en **créditos** para la tienda, y la siguiente partida se juega con mejor
equipo (ver [Progresión](#progresión-créditos-tienda-y-armas)).

### Nivel 4: El Circo (secreto)

En la botica se puede comprar un cuarto nivel, **«Nivel 4: El Circo»**. Se vende y empieza como uno más: telón,
dianas del circo (foca, elefante, muñeco de resorte) y cuenta atrás.

![Nivel 4: el circo, antes de la sorpresa](Docs/img/nivel4-circo.png)

<details>
<summary><b>Qué pasa después (spoiler)</b></summary>

<br>

A los 12 s las luces se apagan, algo golpea la pared del fondo hasta reventarla junto con el techo y el decorado, y
aparece **«El Gran Feriante»**, un payaso animatrónico gigante. A partir de ahí no hay cuenta atrás: el jugador
tiene **3 corazones** y la partida termina al vencer al boss o al quedarse sin ellos.

- **Tres fases.** En cada una se abren unos puntos débiles distintos (ojos, palmas y mandíbula, núcleo del pecho),
  que brillan en cian. El resto del cuerpo para las balas.
- **Ataques que siempre avisan.** Se contrarrestan disparando la lluvia de tartas, bolas y mazas en parábola, los
  esbirros que saltan hacia el jugador y el **bocinazo**. El **rayo** de los ojos, que barre la caseta a la altura de
  la cabeza, es el único que no se puede parar: se esquiva agachándose tras el mostrador (C o Ctrl).
- **El bocinazo** se lee sin explicación: el boss coge aire (se ve cómo le entra en la boca), abre la boca con
  anillos rojos que laten cada vez más deprisa y, alrededor, un aro cian que se llena con cada impacto en la
  mandíbula. Si se completa, el bocinazo se corta y el boss queda aturdido; si no, salen tres ondas de sonido hacia
  la caseta y, al llegar, quitan 2 corazones.
- Los esbirros con contorno **rosa** devuelven un corazón.
- La vida del boss **se escala con el equipo del jugador**, para que siga siendo un reto con todas las mejoras.

<p>
  <img src="Docs/img/boss-combate.png" width="49%" alt="Combate contra El Gran Feriante">
  <img src="Docs/img/boss-golpe.png" width="49%" alt="Viñeta roja al recibir un golpe">
</p>

</details>

---

## Cómo se juega

| Control | Acción |
|---|---|
| Ratón | Apuntar (±65° en horizontal, +35° / −20° en vertical). Sensibilidad e invertir el eje Y en **OPCIONES** |
| Clic izquierdo | Disparar (mantener con las armas automáticas) |
| Clic derecho | Mirar por la mira del arma (cada arma tiene la suya). Desde la cadera el disparo se desvía dentro de la cruceta, que se abre con cada disparo; apuntando va exacto al centro |
| R | Recargar (también se recarga sola al vaciar el cargador) |
| C o Ctrl | Agacharse tras el mostrador (mantener): la única forma de esquivar el rayo del boss |
| Esc | Pausa (continuar, opciones o terminar la partida). En la armería y la botica, volver al menú |

- **Racha:** cada 5 aciertos seguidos el multiplicador sube 0,5, hasta ×3. Fallar (también dar al decorado) o dar a
  una diana prohibida la devuelve a ×1.
- **Dianas prohibidas** (señal roja): restan puntos y rompen la racha.
- **Dianas especiales**, con contorno y halo de color: **dorado** = +3 s de tiempo; **rojo** = cargador lleno;
  **rosa** (solo contra el boss) = +1 corazón.
- **Bonus de precisión** al terminar cada nivel, según el porcentaje de aciertos.

La pantalla **CÓMO JUGAR** del menú resume todo esto, y se muestra sola la primera vez que juega cada perfil.

**Pausa y opciones.** Esc detiene la partida. Desde la pausa se puede seguir, abrir **OPCIONES** (volumen General,
Música, Efectos y Ambiente, sensibilidad del ratón e invertir el eje Y, que se guardan entre sesiones) o **terminar la partida**, que cobra los créditos de lo
conseguido hasta ese momento. En la build, cambiar a otra ventana (Alt+Tab) también pausa.

---

## Progresión: créditos, tienda y armas

Cada jugador tiene un **perfil** con sus créditos, mejoras, armas y récords. Al acabar la partida gana
`créditos = puntuación / 10`. Se gastan en dos carpas de la feria, a las que se llega desde el menú de inicio o
desde el resumen de la partida: la cámara sale de la caseta, recorre el paseo de la feria y entra en la tienda. Todas
las mejoras se combinan entre sí.

- **La botica** (HABILIDADES, carpa azul a la derecha): cada mejora es un frasco de «tónico milagroso» que se llena
  por tercios con cada nivel comprado; las tres mejoras caras son matraces en la balda de arriba y el nivel 4 es un
  billete dorado en una vitrina.
- **La armería** (ARMERÍA, carpa a rayas a la izquierda): las cinco armas cuelgan de la pared con una bombilla encima,
  apagada si no es tuya, encendida si ya la tienes y dorada si es la equipada. Al pasar el ratón el arma se levanta y
  enseña su precio; al hacer clic sale su ficha para comprarla o equiparla.

<p>
  <img src="Docs/img/botica.png" width="49%" alt="La botica: un tónico por mejora">
  <img src="Docs/img/armeria.png" width="49%" alt="La armería: las cinco armas en la pared">
</p>

| Mejora | Niveles | Efecto | Precio (créditos) |
|---|---|---|---|
| Munición | 3 | +25 % de balas por cargador por nivel | 80 / 210 / 360 |
| Cadencia | 3 | −10 % de espera entre disparos por nivel | 130 / 260 / 450 |
| Multiplicador de puntos | 3 | +0,1 por nivel | 160 / 310 / 520 |
| Daño | 3 | +25 % por nivel | 200 / 390 / 650 |
| Penetración | 1 | La bala atraviesa hasta 2 dianas (el decorado la para) | 1550 |
| Disparo doble | 1 | Un segundo rayo a un lado, alternando | 1950 |
| Bala explosiva | 1 | Onda expansiva en cada impacto | 2350 |
| Nivel 4: El Circo | 1 | Añade el nivel secreto al final de la partida | 1200 |

| Arma | Daño | Cargador | Disparos/s | Recarga | Modo | Precio |
|---|---|---|---|---|---|---|
| FN Five-seven | 1 | 12 | 5 | 1,5 s | Semiautomática | Inicial |
| HK MP5 | 1 | 30 | 13 | 2 s | Automática | 400 |
| M16A1 | 1,25 | 20 | 8 | 2,2 s | Semiautomática | 600 |
| AK-47 | 1,5 | 30 | 10 | 2,5 s | Automática | 800 |
| Barrett M82A1 | 3 | 5 | 0,8 | 3,5 s | Semiautomática | 1200 |

El arma se equipa en la armería o con el selector ◀ ▶ del menú de inicio y del resumen de la partida. Los precios
salen de una sesión de balance con un jugador simulado (unos 490 créditos por partida al empezar).

---

## Abrir, jugar y compilar el proyecto

### Requisitos

- **Unity 6000.3.25f1** (Unity 6.3 LTS) con el módulo de Windows.
- **Git LFS**: los modelos, texturas, audio y archivos `.blend` están en LFS. Sin él, el proyecto se abre con
  los recursos rotos.

### Clonar y abrir

```bash
git lfs install
git clone https://github.com/ivanpnto/FairgroundShooter.git
```

1. Abrir la carpeta con Unity Hub (la primera importación tarda unos minutos).
2. Abrir la escena **`Assets/Scenes/Main.unity`** y pulsar Play.
3. Crear un perfil, pulsar **EMPEZAR** y jugar.

### Compilar

*File → Build Profiles → Windows → Build*. La única escena de la build es `Main.unity` y la salida va a `Build/`,
que Git ignora. El juego arranca a pantalla completa.

### Dónde se guardan los perfiles

En `profiles.json`, dentro de la carpeta de datos del juego:
`%USERPROFILE%\AppData\LocalLow\Iván y Jaime\Fairground Shooter\`. Se guarda de forma atómica (archivo temporal
y copia `.bak`), así que un cierre a mitad de escritura no lo corrompe. Para empezar de cero basta con borrarlo.

---

## Herramientas de desarrollo y pruebas

Todo lo de esta sección existe **solo en el editor y en las development builds**: en una build normal no se
compila, así que no sirve para hacer trampas.

| Herramienta | Para qué |
|---|---|
| **F1** · menú de depuración | Dar 500 créditos, reiniciar el perfil, desbloquearlo todo. **Modo prueba rápida**: niveles de 10 s, 10 corazones contra el boss y revelación a los 2 s (sin contar para récords), munición infinita, saltar a cualquier nivel, acabar el nivel y dejar la fase del boss con 0,1 de vida |
| **1–5** | Cambiar entre las armas desbloqueadas en plena partida |
| `BossDebugHUD` | Estado del boss en pantalla (fase, vida, ataque, corazones) |
| `BalanceBot` y `BossTestBot` | Jugadores simulados que disparan por la misma tubería que el jugador y escriben resultados en la consola. Se usaron para fijar precios y dificultad |
| *Tools → Fairground* | Herramientas que montan niveles, iluminación, colliders, tiendas e interfaz en la escena (*Setup Sky Level*, *Setup Boss Level*, *Setup Scenery Colliders*, *Build Armory*, *Build Botica*, *Build Promenade*, *Build Level Transition*, *Build Pause And Options*, *Build Sea Clam*…) |

> **Cuidado:** *Setup Forest Level*, *Setup Sea Level* y *Setup Exterior* reconstruyen sus prefabs desde cero y
> borran lo que se haya añadido después. Las demás se pueden repetir.

### Tests

67 tests de modo Edit (NUnit) en `Assets/Tests/EditMode`: el cálculo de estadísticas de arma con las mejoras
(`WeaponStatsCalculator`), los datos de las mejoras, las fórmulas de balance (daño sostenido de cada arma y escalado
de la vida del boss), el perfil Demo, el guardado del volumen (`VolumeSettings`), la dispersión desde la cadera
(`WeaponSpread`), los ajustes del ratón (`ControlSettings`), la regla para esquivar el rayo (`PlayerCrouch`) y el
límite del empujón del viento (`WindGusts.LimitPush`). Se
ejecutan desde *Window → General → Test Runner → EditMode → Run All*.

El código del juego está en su propio ensamblado (`FairgroundShooter.asmdef`) para que los tests puedan
referenciarlo, y la lógica que se prueba está escrita como **funciones puras**, sin depender de la escena.

---

## Conceptos de la asignatura en el proyecto

| Concepto | Dónde se ve |
|---|---|
| Modelado 3D | Caseta, decorados, dianas, exterior de la feria, boss por piezas y proyectiles modelados en Blender (low-poly, estilo «chapa»). Flujo de exportación FBX documentado |
| Cámara | Primera persona con límites de apuntado, zoom con la mira de cada arma, retroceso, sacudida de cámara por trauma y viajes de cámara por la feria hasta la armería y la botica, con paralaje según el ratón dentro |
| Raycasting | Disparo desde el centro de la cámara: impactos ordenados por distancia, penetración, varios rayos y capas que filtran qué para la bala |
| Colisiones y física | Proyectiles del boss en parábola con `Rigidbody` y gravedad calculada; la pared, el techo y el decorado del circo revientan con física; triggers y matriz de colisiones por capas |
| Animación por código | Movimientos de las dianas (saltos, seno, picado, impulsos), aparición tras el decorado, caída al derribarlas, mandíbula y compuerta del boss, salto de los esbirros, ondas del bocinazo, almeja que se abre y se entierra |
| Animator y clips | Telón (Closed / Opening / Open / Closing) y brazos del boss, con un **Animation Event** que lanza el proyectil en el fotograma exacto |
| Animación en GPU | Shaders de vértice escritos a mano: banderines y algas que se mecen (y se inclinan con las ráfagas de viento, con una variable global del shader), bombillas en cadena y luciérnagas |
| Iluminación y materiales | Iluminación y post-procesado por nivel (noche, submarina, atardecer), luz del arma y tiendas aisladas de la iluminación de los niveles con Rendering Layers, apagón y luces rojas en la revelación del boss, penumbra en la transición entre niveles |
| Partículas y efectos | Fogonazo, chispas, polvo, confeti de acierto, explosión con brasas, trazas, viñeta roja, salpicadura de tarta, rachas de viento, aire que entra en la boca del boss, energía que cargan sus ojos y chispas del rayo |
| Estados de juego | `GameManager` (Waiting / Playing / LevelTransition / GameOver) y máquina de estados del boss |
| Interfaz | Menús de perfiles, récords, Cómo jugar, pausa y opciones; tiendas en 3D con selección por raycast desde el ratón; HUD con racha, munición, corazones y barra de vida del boss |
| Sonido | Música por nivel con fundido cruzado (y en el nivel 4, silencio en la revelación y música propia para el combate), AudioMixer con volumen por grupo, ambiente 3D de la feria y efectos del boss, la transición y las tiendas sintetizados por código |
| Input | Input System de Unity (ratón y teclado) |
| Optimización | *Object pooling* de los proyectiles del boss, caché de referencias de escena y registros que desaparecen de la build |
| Compilación | Build de Windows con PlayerSettings, icono y herramientas de depuración excluidas |

---

## Arquitectura

```
GameManager ─ estados de la partida y fin (tiempo, derrota, victoria)
 ├─ LevelManager ─ LevelConfig[] (ScriptableObjects) y entornos por nivel
 │   ├─ TargetSpawner ─ carriles → dianas (Target + movimiento + animaciones)
 │   └─ GameTimer, WeaponAmmo, MusicManager
 ├─ ScoreManager ─ puntos, racha, precisión
 ├─ ProfileManager ─ perfiles, créditos, mejoras y armas → profiles.json
 ├─ MenuController ─ qué menú se ve · PauseMenuUI + VolumeSettings ─ pausa y volumen
 └─ TentShopController ─ base de ArmoryController (armas) y BoticaController (mejoras)

Player ─ PlayerAim, WeaponController (arma + perfil → WeaponStats), PlayerShoot, PlayerHealth
Boss   ─ BossController (máquina de estados) ← BossConfig · BossWeakPoint · BossProjectile · MinionLunge
```

Decisiones que guían el código:

- **Orientado a eventos.** Los sistemas avisan de lo que pasa (`Target.Killed`, `PlayerShoot.ShotResolved`,
  `BossController.PhaseStarted`, `PlayerHealth.Damaged`…) y los efectos, sonidos e interfaz se enganchan a esos
  eventos. Así, la lógica no conoce a quien la dibuja, y dos personas pueden trabajar en paralelo sin pisarse.
- **Datos en ScriptableObjects.** Niveles, armas, mejoras y boss son assets (`Assets/Data`): ajustar el balance no
  exige tocar código.
- **Interfaces para el disparo y el daño.** `IDamageable` (dianas y puntos débiles del boss) e `IPlayerHazard`
  (proyectiles y esbirros) permitieron añadir el boss sin cambiar la firma de ningún evento existente.
- **La animación decora, no decide.** La máquina de estados del boss está en C#. El Animator solo pinta lo que ya
  ha decidido el código; si un Animation Event no llegara, un tope de tiempo mantiene el juego en marcha.

---

## Estructura del repositorio

```
ArtSource/        Archivos fuente de Blender (Booth.blend, Icons.blend…) y generadores de sprites
AudioSource/      Audio original sin procesar y cómo se procesó (ver su README)
Assets/
├── Art/          Modelos, materiales y animaciones propios
├── Audio/        Música, ambiente, armas y efectos del boss
├── Data/         Niveles, armas, mejoras y boss (ScriptableObjects)
├── Editor/       Herramientas de montaje (menú Tools → Fairground)
├── PolyOne/      Modelos de armas de terceros (no se modifican)
├── Prefabs/      Dianas por nivel, decorados, boss, interfaz y efectos
├── Scenes/       Main.unity (juego) y BossSandbox.unity (pruebas, fuera de la build)
├── Scripts/      Código del juego (+ Armory/, Botica/, Boss/, Progression/, Shops/, UI/)
├── Shaders/      Fairground/Sway y Fairground/AnimatedLights
├── Tests/        Tests de modo Edit
└── UI/           Sprites e iconos de la interfaz
Docs/img/         Capturas de este README
CREDITS.md        Créditos y licencias de los recursos de terceros
```

---

## Arte, sonido y recursos de terceros

Todo el arte del escenario, las dianas, el boss, la armería, la botica y el paseo de la feria es **propio**,
modelado en Blender (`ArtSource/Booth.blend`). Los sprites del HUD del boss se generan con un script de Python y los
efectos de sonido del boss, la transición y las tiendas se sintetizan por código.

De terceros:

- **Modelos de las armas:** pack «Free Pack – Gun» de PolyOne Studio (`Assets/PolyOne`). Las correcciones se hacen
  en copias propias.
- **Tipografía Rye** (Sorkin Type), con licencia SIL Open Font License (`Assets/Fonts/OFL.txt`).
- **Música, disparos, recargas y ambiente:** Freesound y OpenGameArt, todos CC0.

La lista completa con autores, enlaces y licencias está en [CREDITS.md](CREDITS.md).

---

## Equipo

| | Se encargó de |
|---|---|
| **Iván** | Escenario y arte en Blender, iluminación, niveles, dianas y spawner, disparo y mejoras caras, el nivel 4 con el boss (incluidos su HUD, efectos y sonidos), armería y botica en 3D, transición entre niveles, pausa y opciones, balance, tests y optimización |
| **Jaime** | Armas (datos, colocación, retroceso, recarga), miras y crosshair, efectos y sonidos del disparo, pantallas de inicio y fin, sistema de progresión (perfiles, créditos, mejoras, tienda y récords), música y ambiente |
