# Créditos

## Modelos de terceros
| Recurso | Autor | Enlace | Uso |
|---|---|---|---|
| Free Pack – Gun (FN Five-seven, HK MP5, M16A1, AK-47, Barrett M82A1) | PolyOne Studio | https://polyonestudio.gumroad.com/l/qbafpm | Las cinco armas (`Assets/PolyOne`, sin modificar; las correcciones van en copias propias como `HK_MP5_Body_Fixed.fbx`) |

## Tipografía
| Recurso | Autor | Licencia | Uso |
|---|---|---|---|
| Rye | Sorkin Type Co | SIL Open Font License 1.1 (`Assets/Fonts/OFL.txt`) | Títulos y carteles de la interfaz |

## Sonidos
Todos de [Freesound](https://freesound.org). Procesados (recorte, mono, fade y normalizado)
para el juego; los originales están en `AudioSource/raw/`.

| Título | Autor | Enlace | Licencia | Uso |
|---|---|---|---|---|
| Single Pistol Gunshot 3.wav | morganpurkis | https://freesound.org/s/385811/ | CC0 | Disparo FiveSeven |
| MP5 submachine gun down the street.aif | Franki-01234 | https://freesound.org/s/201671/ | CC0 | Disparo MP5 |
| AK47 Fire into Street.aif | Franki-01234 | https://freesound.org/s/201670/ | CC0 | Disparo AK47 |
| M16 Single Shot 5.flac | qubodup | https://freesound.org/s/162403/ | CC0 | Disparo M16A1 |
| Sniper Rifle | SuperPhat | https://freesound.org/s/514228/ | CC0 | Disparo Barrett |
| Assault Rifle Reload | Kodack | https://freesound.org/s/256912/ | CC0 | Recarga de rifle |
| pistol reload sound.mp3 | GFL7 | https://freesound.org/s/276959/ | CC0 | Recarga de pistola |

## Ambiente
Todos de [Freesound](https://freesound.org), en CC0. Se usa la vista previa HQ (OGG) y no el
original, que exige iniciar sesión; para ambiente de fondo la calidad sobra. Procesados
(fragmento, bucle con fundido cruzado, mono y normalizado); los originales están en
`AudioSource/raw/`.

| Título | Autor | Enlace | Licencia | Uso |
|---|---|---|---|---|
| G28-21-Crowd Walla at Outdoor Event.wav | craigsmith | https://freesound.org/s/438381/ | CC0 | Murmullo de fondo |
| Mechanical Fun Fair Ride.wav | sertuser | https://freesound.org/s/661925/ | CC0 | Tiovivo y noria |
| S41-28 Carnival Ambiance; mainly voices; barkers calling to crowd.wav | craigsmith | https://freesound.org/s/675012/ | CC0 | Pregones (puesto izquierdo) |
| S12-01 Crowd reactions; oohs; ahh; cheering; laughing; moderate emotion; exterior.wav | craigsmith | https://freesound.org/s/675056/ | CC0 | Reacciones de la gente (puestos) |

## Música
Todas de [OpenGameArt](https://opengameart.org). Procesadas (fundidos de 2 ms en la costura y
normalizado de sonoridad) para el juego; los originales están en `AudioSource/raw/`.

| Título | Autor | Enlace | Licencia | Uso |
|---|---|---|---|---|
| Carnival Rides | Écrivain | https://opengameart.org/content/carnival-rides | CC0 | Menús |
| Children's March Theme | CleytonKauffman | https://opengameart.org/content/childrens-march-theme | CC0 | Nivel 1 (Bosque) |
| Underwater Theme II | CleytonKauffman | https://opengameart.org/content/underwater-theme-ii | CC0 | Nivel 2 (Fondo marino) |

## Efectos del combate contra el boss (propios)
Sintetizados por código para el juego, sin muestras de terceros: toma de aire del bocinazo, bocinazo, aturdido,
aviso del esbirro, golpe recibido, tartazo, corazón recuperado, cambio de fase, fanfarria de victoria y trombón
de derrota (`Assets/Audio/Boss`). El generador está en `AudioSource/Synth/boss_sfx.py` (numpy + scipy).
Los de la transición entre niveles (telón, golpes tras el telón, cartel y cuenta atrás, `Assets/Audio/Transition`)
se generan igual con `AudioSource/Synth/transition_sfx.py`.

## Arte propio de la interfaz
Corazones, viñeta, salpicadura de tarta y relleno de barra (`Assets/UI/Boss`) se generan con
`ArtSource/UI/hud_sprites.py` (Pillow). Los iconos de la tienda (`Assets/UI/Icons`) se renderizan desde
`ArtSource/Icons.blend`, y los proyectiles del boss (tarta, bola y maza) están en `ArtSource/Booth.blend`.
