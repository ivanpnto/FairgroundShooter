# AudioSource

Originales de Freesound y OpenGameArt (en `raw/`, sin modificar) y cómo se han procesado los
clips de `Assets/Audio/Weapons/` y `Assets/Audio/Music/`. Créditos y licencias en `CREDITS.md`, en la raíz del repo.

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

## Música (Audacity)
Las tres pistas ya son bucles: no se recortan, para no cambiar su duración (el bucle depende
de que dure exactamente lo mismo). Pero el salto entre la última muestra y la primera era de
374–1903 (sobre 32767), lo que se oye como un clic en cada vuelta.
1. Importar el original.
2. Fade In en 0–0,002 s y Fade Out en los últimos 0,002 s: la costura queda en 1–4.
3. Loudness Normalization a −18 LUFS (estéreo conjunto, no independiente). Más baja que los
   disparos (picos a −1 dB) para que la música quede de fondo.
4. Exportar WAV 16 bits, 44100 Hz, **estéreo** (la música sí aprovecha el estéreo).

Importación en Unity: Streaming, Vorbis calidad 70, sin Preload y con Load In Background
(son largas y solo suena una a la vez, al revés que los efectos).

| Exportado | Original | Duración (s) | LUFS original | Pico final (dBFS) |
|---|---|---|---|---|
| Music_Menu.wav | music_menu_raw.ogg (Vorbis, 44,1 kHz) | 25.498 | −13,8 | −4,9 |
| Music_Level1_Forest.wav | music_level1_raw.wav (44,1 kHz) | 64.000 | −17,8 | −2,1 |
| Music_Level2_Seabed.wav | music_level2_raw.wav (44,1 kHz) | 104.724 | −17,3 | −3,9 |
