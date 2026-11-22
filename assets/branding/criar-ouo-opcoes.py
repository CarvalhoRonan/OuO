"""Gera propostas de ícone e uma folha de comparação para o OuO."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
S = 4
BG = "#101923"
TEAL = "#65e2ca"
BLUE = "#39abc6"
WHITE = "#f0fffb"
MUTED = "#a9c4c7"


def font(size, bold=False):
    path = Path("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf")
    return ImageFont.truetype(str(path), size)


def line(draw, coords, color, width):
    draw.line([(round(x*S), round(y*S)) for x, y in coords], fill=color,
              width=round(width*S), joint="curve")


def circle(draw, box, fill=None, outline=None, width=1):
    draw.ellipse(tuple(round(v*S) for v in box), fill=fill, outline=outline, width=round(width*S))


def icon(kind):
    im = Image.new("RGBA", (128*S, 128*S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((4*S, 4*S, 124*S, 124*S), radius=30*S, fill=BG)
    if kind == "rosto":
        circle(d, (27, 35, 49, 57), outline=TEAL, width=6)
        circle(d, (79, 35, 101, 57), outline=TEAL, width=6)
        d.arc((39*S, 33*S, 89*S, 93*S), 9, 171, fill=TEAL, width=8*S)
        circle(d, (93, 22, 100, 29), fill=BLUE)
    elif kind == "foco":
        for x0, y0, x1, y1 in [(25, 24, 39, 24), (24, 24, 24, 38),
                                (89, 24, 103, 24), (104, 24, 104, 38),
                                (24, 90, 24, 104), (24, 104, 38, 104),
                                (90, 104, 104, 104), (104, 90, 104, 104)]:
            line(d, [(x0, y0), (x1, y1)], BLUE, 4)
        circle(d, (30, 39, 50, 59), fill=TEAL)
        circle(d, (78, 39, 98, 59), fill=TEAL)
        d.arc((39*S, 36*S, 89*S, 96*S), 10, 170, fill=TEAL, width=7*S)
    elif kind == "escudo":
        shield = [(64, 18), (103, 32), (100, 73), (88, 95), (64, 111),
                  (40, 95), (28, 73), (25, 32)]
        d.line([(x*S, y*S) for x, y in shield+[shield[0]]], fill=BLUE, width=6*S, joint="curve")
        circle(d, (39, 46, 54, 61), fill=TEAL)
        circle(d, (74, 46, 89, 61), fill=TEAL)
        d.arc((45*S, 52*S, 83*S, 91*S), 13, 167, fill=TEAL, width=6*S)
    elif kind == "circuito":
        d.rounded_rectangle((25*S, 25*S, 103*S, 103*S), radius=19*S, outline=BLUE, width=5*S)
        for v in (40, 64, 88):
            line(d, [(v, 14), (v, 25)], TEAL, 4)
            line(d, [(v, 103), (v, 114)], TEAL, 4)
            line(d, [(14, v), (25, v)], TEAL, 4)
            line(d, [(103, v), (114, v)], TEAL, 4)
        circle(d, (39, 45, 53, 59), fill=TEAL)
        circle(d, (75, 45, 89, 59), fill=TEAL)
        d.arc((44*S, 51*S, 84*S, 91*S), 12, 168, fill=TEAL, width=6*S)
    else:
        raise ValueError(kind)
    return im.resize((512, 512), Image.Resampling.LANCZOS)


names = [
    ("rosto", "01 · Rosto", "O emoji OuO como marca principal."),
    ("foco", "02 · Foco", "Suporte e diagnóstico visual."),
    ("escudo", "03 · Escudo", "Segurança e escolhas conscientes."),
    ("circuito", "04 · Circuito", "Ferramentas e hardware."),
]

canvas = Image.new("RGB", (1500, 760), "#edf3f5")
d = ImageDraw.Draw(canvas)
d.text((48, 28), "OuO · opções de ícone", font=font(34, True), fill="#152d37")
d.text((49, 80), "Expressão reconhecível, suporte técnico e leitura em tamanho pequeno.",
       font=font(17), fill="#526b73")

for i, (key, title, description) in enumerate(names):
    img = icon(key)
    img.save(ROOT / f"ouo-{key}.png")
    img.save(ROOT / f"ouo-{key}.ico", sizes=[(16, 16), (24, 24), (32, 32),
                                         (48, 48), (64, 64), (128, 128), (256, 256)])
    x = 48 + i*361
    d.rounded_rectangle((x, 135, x+340, 699), radius=20, fill="white", outline="#d8e4e7", width=2)
    d.text((x+24, 161), title, font=font(22, True), fill="#193540")
    d.text((x+24, 200), description, font=font(14), fill="#526b73")
    d.rounded_rectangle((x+20, 245, x+320, 497), radius=14, fill="#101923")
    canvas.paste(img.resize((190, 190), Image.Resampling.LANCZOS), (x+75, 275),
                 img.resize((190, 190), Image.Resampling.LANCZOS))
    d.text((x+24, 523), "TAMANHO DE BARRA DE TAREFAS", font=font(11, True), fill="#6b8188")
    canvas.paste(img.resize((32, 32), Image.Resampling.LANCZOS), (x+25, 556),
                 img.resize((32, 32), Image.Resampling.LANCZOS))
    d.text((x+68, 562), "OuO", font=font(18, True), fill="#193540")
    d.text((x+24, 635), "PNG 512 px  ·  ICO multirresolução", font=font(12), fill="#6b8188")

canvas.save(ROOT / "ouo-opcoes.png")
