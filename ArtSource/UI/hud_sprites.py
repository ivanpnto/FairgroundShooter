import numpy as np, math, random
from PIL import Image, ImageDraw, ImageFilter
import sys
out = (sys.argv[1] if len(sys.argv) > 1 else "../../Assets/UI/Boss") + "/"   # uso: python hud_sprites.py [carpeta]
OUTLINE = (20, 12, 22, 255)

def heart_poly(cx, cy, s, n=200):
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((cx + x * s, cy - y * s))
    return pts

def heart(fill, shine, path, scale=4):
    S = 128 * scale
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    c = S / 2
    d.polygon(heart_poly(c, c + 4 * scale, 3.75 * scale), fill=OUTLINE)            # contorno oscuro (chapa)
    d.polygon(heart_poly(c, c + 4 * scale, 3.1 * scale), fill=fill)
    if shine:
        d.ellipse([c - 30 * scale, c - 34 * scale, c - 12 * scale, c - 18 * scale], fill=shine)  # brillo
    im.resize((128, 128), Image.LANCZOS).save(path)

heart((222, 38, 52, 255), (255, 200, 200, 230), out + "Heart_Full.png")
heart((70, 40, 46, 255), None, out + "Heart_Empty.png")

# Viñeta: blanco con alfa que crece hacia los bordes (se tiñe de rojo en Unity)
S = 512; y, x = np.mgrid[0:S, 0:S] / (S - 1) * 2 - 1
r = np.sqrt((x * 1.0) ** 2 + (y * 1.15) ** 2)
a = np.clip((r - 0.55) / 0.75, 0, 1) ** 1.6
img = np.zeros((S, S, 4), np.uint8); img[..., :3] = 255; img[..., 3] = (a * 255).astype(np.uint8)
Image.fromarray(img).save(out + "Vignette.png")

# Salpicadura de tarta: mancha de nata con gotas y chorretones, borde ligeramente más oscuro
random.seed(3); S = 1024
im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
cream, edge = (250, 244, 228, 255), (222, 208, 180, 255)
c = S / 2
blob = []
for i in range(48):
    t = 2 * math.pi * i / 48; rr = 300 + random.uniform(-40, 60) + (110 if i % 6 == 0 else 0)
    blob.append((c + rr * math.cos(t), c + rr * math.sin(t)))
d.polygon(blob, fill=edge)
d.polygon([(c + (px - c) * 0.93, c + (py - c) * 0.93) for px, py in blob], fill=cream)
for k in range(14):                                   # gotas sueltas
    t = random.uniform(0, 2 * math.pi); rr = random.uniform(380, 470); s = random.uniform(16, 42)
    px, py = c + rr * math.cos(t), c + rr * math.sin(t); d.ellipse([px - s, py - s, px + s, py + s], fill=cream)
for k in range(5):                                    # chorretones hacia abajo
    px = c + random.uniform(-220, 220); w = random.uniform(22, 38); L = random.uniform(120, 230)
    d.rounded_rectangle([px - w, c + 150, px + w, c + 300 + L], radius=w, fill=cream)
cherry = (200, 20, 30, 255); d.ellipse([c - 48, c - 70, c + 48, c + 26], fill=OUTLINE); d.ellipse([c - 40, c - 62, c + 40, c + 18], fill=cherry)
d.ellipse([c - 24, c - 52, c - 6, c - 34], fill=(255, 170, 170, 255))
im = im.filter(ImageFilter.GaussianBlur(1.2)).resize((512, 512), Image.LANCZOS); im.save(out + "PieSplat.png")

# Relleno de barra: rectángulo redondeado blanco (9-slice; se tiñe en Unity)
im = Image.new("RGBA", (64, 32), (0, 0, 0, 0)); ImageDraw.Draw(im).rounded_rectangle([0, 0, 63, 31], radius=10, fill=(255, 255, 255, 255)); im.save(out + "BarFill.png")
print("ok")
