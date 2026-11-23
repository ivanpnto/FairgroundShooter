# AudioSource

Originales de Freesound y OpenGameArt (en `raw/`, sin modificar) y cómo se han procesado los
clips de `Assets/Audio/Weapons/` (disparos, recargas, explosión y acierto), `Assets/Audio/Music/` y `Assets/Audio/Ambience/`. Créditos y licencias en `CREDITS.md`, en la raíz del repo.

## Procesado común (Audacity)
1. Importar el original.
2. Borrar desde 0 hasta el inicio (el ataque del sonido, en el cruce por cero
   inmediatamente anterior) y desde inicio + duración hasta el final.
3. Stereo to Mono.
4. Fade Out lineal en los últimos N segundos.
5. Normalize a −1 dB (con eliminación de DC).
6. Exportar WAV 16 bits, 44100 Hz, mono.

Los originales a 48 kHz se remuestrean a 44,1 kHz al exportar.

## Clips
| Exportado | Original | Inicio (s) | Duración (s) | Fade out (s) | Original estéreo |
|---|---|---|---|---|---|
| FiveSeven_Shot.wav | fiveseven_raw.wav (44,1 kHz) | 0.00034 | 0.60 | 0.15 | Sí |
| MP5_Shot.wav | mp5_raw.aiff (48 kHz) | 1.71202 | 0.35 | 0.10 | Sí |
| AK47_Shot.wav | ak47_raw.aiff (48 kHz) | 2.86704 | 0.40 | 0.10 | Sí |
| M16A1_Shot.wav | m16a1_raw.flac (44,1 kHz) | 0.00075 | 0.50 | 0.12 | Sí |
| Barrett_Shot.wav | barrett_raw.wav (48 kHz) | 0.02656 | 2.00 | 0.60 | Sí |
| Rifle_MagOut.wav | rifle_reload_raw.wav (44,1 kHz) | 0.53163 | 0.67 | 0.05 | Sí |
| Rifle_MagIn.wav | rifle_reload_raw.wav (44,1 kHz) | 1.23175 | 1.10 | 0.05 | Sí |
| Pistol_MagOut.wav | pistol_reload_raw.mp3 (44,1 kHz) | 0.12608 | 0.30 | 0.05 | Sí |
| Pistol_MagIn.wav | pistol_reload_raw.mp3 (44,1 kHz) | 0.77803 | 1.06 | 0.05 | Sí |

## Notas
- **Ráfagas (MP5, AK47):** los disparos se suceden cada ~88 ms (MP5) y ~100 ms (AK47),
  así que cualquier disparo intermedio llevaría dentro los 3–4 siguientes. Se usa el
  **último disparo de la ráfaga**: detrás solo tiene su propia cola, y delante, la del
  anterior, que queda tapada por el ataque.
- **Barrett:** el original está muy saturado y, al remuestrear de 48 a 44,1 kHz, los picos
  superaban 0 dBFS (había muestras recortadas). Se hace en dos pasadas: procesado y
  normalizado a −3 dB → exportar a 44,1 kHz → reimportar ese WAV → Normalize a −1 dB →
  exportar.
- **pistol_reload_raw.mp3:** Audacity lo decodifica con la misma duración que ffmpeg
  (2.378 s); el silencio inicial (0–0.126 s) queda eliminado por el corte.
- **Recarga de rifle:** se descartan los golpes de 2.4–3.4 s (parecen la palanca de carga).
- **Recarga de pistola:** MagOut = el clic de 0.13–0.35 s; MagIn = el golpe de 0.78–1.24 s
  más la corredera de 1.50–1.84 s.

## Explosión y acierto (ffmpeg)
Mismo resultado que el procesado común (mono, 44,1 kHz, fade out, pico a −1 dB), pero con ffmpeg
y en coma flotante. Los originales son las vistas previas HQ (MP3) de Freesound.
```
ffmpeg -i SRC -af "aformat=sample_fmts=flt,pan=mono|c0=0.5*c0+0.5*c1,aresample=44100,atrim=A:B,asetpts=N/SR/TB,
  afade=t=in:d=0.002,afade=t=out:st=B-A-F:d=F,volume=<−1 − pico medido>dB" -c:a pcm_s16le OUT.wav
```
| Exportado | Original | Inicio A (s) | Fin B (s) | Fade out F (s) | Ganancia | Por qué |
|---|---|---|---|---|---|---|
| Upgrade_Explosion.wav | upgrade_explosion_raw.mp3 (44,1 kHz) | 0 | 0.88 | 0.15 | −0,61 dB | Ataque de 23 ms y grave (centroide 730 Hz): coincide con el impacto y no tapa el disparo. Hasta 0,88 s está por encima de −40 dB |
| Target_Hit.wav | target_hit_raw.mp3 (48 kHz) | 0.015 | final (0.309) | 0.08 | +21,63 dB | Tono casi puro a 1,3 kHz (planitud 0,05; los golpes de casetas reales de craigsmith daban 0,2–0,4, con ruido de fondo), que se distingue entre los disparos. Empieza con 15 ms de silencio y acaba cortado a −30 dB, de ahí el recorte y el fade |

## Música (Audacity)
Las pistas no se recortan, para no cambiar su duración (el bucle depende
de que dure exactamente lo mismo). Pero el salto entre la última muestra y la primera era de
374–1903 (sobre 32767), lo que se oye como un clic en cada vuelta.
1. Importar el original.
2. Fade In en 0–0,002 s y Fade Out en los últimos 0,002 s: la costura queda en 1–4.
3. Loudness Normalization a −18 LUFS (estéreo conjunto, no independiente). Más baja que los
   disparos (picos a −1 dB) para que la música quede de fondo.
4. Exportar WAV 16 bits, 44100 Hz, **estéreo** (la música sí aprovecha el estéreo).

*Determined to Fly* (nivel 3) no es un bucle sino una pieza con final, pero el nivel dura 60 s y
la pista 70 s, así que casi nunca da la vuelta; se le aplica el mismo fundido para que, si la da,
no haya clic (el original empieza en −565 de golpe).

Importación en Unity: Streaming, Vorbis calidad 70, sin Preload y con Load In Background
(son largas y solo suena una a la vez, al revés que los efectos).

| Exportado | Original | Duración (s) | LUFS original | Pico final (dBFS) |
|---|---|---|---|---|
| Music_Menu.wav | music_menu_raw.ogg (Vorbis, 44,1 kHz) | 25.498 | −13,8 | −4,9 |
| Music_Level1_Forest.wav | music_level1_raw.wav (44,1 kHz) | 64.000 | −17,8 | −2,1 |
| Music_Level2_Seabed.wav | music_level2_raw.wav (44,1 kHz) | 104.724 | −17,3 | −3,9 |
| Music_Level3_Sky.wav | music_level3_raw.wav (44,1 kHz) | 70.370 | −16,0 | −2,2 |
| Music_Level4_Circus.wav | music_level4_circus_raw.mp3 (MP3, 44,1 kHz) | 70.583 | −12,4 | −4,7 |
| Music_Level4_Boss.wav | music_level4_boss_raw.mp3 (MP3, 44,1 kHz) | 81.067 (bucle) | −3,3 | −10,6 |

### Nivel 4 (ffmpeg, en coma flotante)
Las dos son de Bobjt y comparten melodía. **Circus** se procesa como las demás (fundidos de 2 ms
y −18 LUFS; costura 0). **Boss** necesita más trabajo:
- **Sonoridad.** El original está masterizado muy alto (−3,3 LUFS, picos a +4,1 dBFS al
  decodificarlo en coma flotante). No está recortado: no hay mesetas en el pico, así que basta
  con aplicar la ganancia (−14,7 dB) **antes** de pasar a 16 bits. Convertir primero a 16 bits
  recortaría un 3–6 % de las muestras.
- **Bucle.** El combate dura varios minutos y la pista, 128 s con un final que se apaga. La
  pieza toca su ciclo dos veces: la similitud espectral entre t y t + L es de 0,95 con
  L = 3 575 040 muestras (81,0667 s), afinado por correlación de la forma de onda. Las dos vueltas
  no son idénticas muestra a muestra (correlación 0,3–0,4), así que un corte seco daría un salto.
  Se hace como el ambiente: el segundo siguiente al ciclo, [L, L + 1 s], se funde (`qsin`) sobre
  [0, 1 s]. La costura queda en 192 (el salto normal entre muestras es de ~7700) y cae tras el
  respiro de 79–80 s, que es un final de frase.
```
ffmpeg -i music_level4_boss_raw.mp3 -filter_complex "[0]atrim=start_sample=0:end_sample=L+X,asetpts=N/SR/TB,asplit=3[h][m][t];
  [h]atrim=end_sample=X,asetpts=N/SR/TB,afade=t=in:d=1:curve=qsin[hi];[t]atrim=start_sample=L:end_sample=L+X,asetpts=N/SR/TB,afade=t=out:d=1:curve=qsin[to];
  [hi][to]amix=inputs=2:normalize=0[x];[m]atrim=start_sample=X:end_sample=L,asetpts=N/SR/TB[body];[x][body]concat=n=2:v=0:a=1[o];[o]volume=-14.7dB"
  -ar 44100 -ac 2 -c:a pcm_s16le Music_Level4_Boss.wav        (L = 3575040, X = 44100)
```
Se descartaron la versión normal (73 s), la *build III* (70 s) y la de piano de *Marionettes*:
ninguna de las dos primeras se repite con una similitud de más de 0,75, y la de piano es solo
piano, cuando el resto de la música lleva arreglo completo.

## Ambiente (ffmpeg)
Hecho con ffmpeg y no con Audacity: el bucle con fundido cruzado exige mezclar el final del
fragmento sobre su principio con precisión de muestra, y así el proceso queda en una orden.
Los originales `amb_*_raw.ogg` son las vistas previas HQ de Freesound (Vorbis; 48 kHz mono
las de craigsmith, 44,1 kHz estéreo la de sertuser).

**Bucles** (fragmento [a, b], fundido X = 2 s): los últimos X s se funden en potencia
constante (`curve=qsin`, porque son ruidos sin correlación) sobre los primeros X s, y el
resultado dura b − a − X. Así la última muestra enlaza con la primera sin salto. Después:
mono, 44,1 kHz y ganancia fija hasta −20 LUFS (medida con `ebur128`, para que sea lineal).
```
ffmpeg -i SRC -filter_complex "[0]atrim=a:b,asetpts=N/SR/TB,aformat=sample_rates=44100:channel_layouts=mono,asplit=3[h][m][t];
  [h]atrim=0:X,asetpts=N/SR/TB,afade=t=in:d=X:curve=qsin[hi];[t]atrim=L-X:L,asetpts=N/SR/TB,afade=t=out:d=X:curve=qsin[to];
  [hi][to]amix=inputs=2:normalize=0[x];[m]atrim=X:L-X,asetpts=N/SR/TB[body];[x][body]concat=n=2:v=0:a=1" -c:a pcm_s16le tmp.wav
ffmpeg -i tmp.wav -af "volume=<−20 − LUFS medidos>dB" -c:a pcm_s16le OUT.wav
```
**Sueltos**: fragmento [a, b] → mono, 44,1 kHz, fade in 0,05 s, fade out 0,5 s, −20 LUFS.
Cada reacción va entre dos silencios del original (< −50 dB), así que empieza y acaba entera.

| Exportado | Original | a (s) | b (s) | Duración (s) |
|---|---|---|---|---|
| Amb_Crowd_Loop.wav | amb_crowd_raw.ogg | 1.0 | 29.5 | 26.5 |
| Amb_Carousel_Loop.wav | amb_rides_raw.ogg | 4.0 | 48.0 | 42.0 |
| Amb_Wheel_Loop.wav | amb_rides_raw.ogg | 140.0 | 176.0 | 34.0 |
| Amb_Barkers_Loop.wav | amb_barkers_raw.ogg | 1.2 | 43.8 | 40.6 |
| Amb_Reaction_01.wav | amb_reactions_raw.ogg | 5.4 | 12.6 | 7.2 |
| Amb_Reaction_02.wav | amb_reactions_raw.ogg | 34.9 | 41.0 | 6.1 |
| Amb_Reaction_03.wav | amb_reactions_raw.ogg | 44.8 | 50.8 | 6.0 |
| Amb_Reaction_04.wav | amb_reactions_raw.ogg | 51.0 | 59.0 | 8.0 |

La noria y el tiovivo salen de la misma grabación, en tramos distintos; la noria además suena
con pitch 0,85 en Unity (`AmbientEmitter.loopPitch`), más grave y lenta.
