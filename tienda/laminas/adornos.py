# Los adornos de las láminas para regalar (src/ArbolGenealogico.Movil/Recursos/Laminas/*.svg), hechos a partir de piezas
# (hojas, pétalos, agujas de pino, bayas…) repetidas a lo largo de unas curvas.
#   python tienda/laminas/adornos.py
# Solo usan lo que entiende el dibujo de la aplicación (Dibujo/Svg.cs): g, path, circle, ellipse y rect, con fill, stroke,
# stroke-width, stroke-linecap, opacity y transform (translate, rotate, scale, matrix), y degradados lineales o radiales
# en <defs> (en las coordenadas de la figura, con offset y stop-color/stop-opacity).
import math, os, random

AQUI = os.path.dirname(os.path.abspath(__file__))
SALIDA = os.path.join(AQUI, "..", "..", "src", "ArbolGenealogico.Movil", "Recursos", "Laminas")


def f(x):
    return f"{x:.2f}".rstrip("0").rstrip(".")


class Svg:
    def __init__(self, ancho, alto):
        self.ancho, self.alto = ancho, alto
        self.defs, self.cuerpo, self._n = [], [], 0

    def degradado(self, paradas, radial=False, x1=0, y1=0, x2=0, y2=1, cx=0.5, cy=0.5, r=0.5, fx=None, fy=None):
        """Un degradado en las coordenadas de la figura (0 a 1). paradas: [(offset, color, opacidad)]."""
        self._n += 1
        id = f"d{self._n}"
        stops = "".join(f'<stop offset="{f(o)}" stop-color="{c}"' + (f' stop-opacity="{f(a)}"' if a < 1 else "") + "/>" for o, c, a in paradas)
        if radial:
            foco = f' fx="{f(fx)}" fy="{f(fy)}"' if fx is not None else ""
            self.defs.append(f'<radialGradient id="{id}" cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"{foco}>{stops}</radialGradient>')
        else:
            self.defs.append(f'<linearGradient id="{id}" x1="{f(x1)}" y1="{f(y1)}" x2="{f(x2)}" y2="{f(y2)}">{stops}</linearGradient>')
        return f"url(#{id})"

    def add(self, s):
        self.cuerpo.append(s)

    def guardar(self, nombre):
        os.makedirs(SALIDA, exist_ok=True)
        defs = f"<defs>{''.join(self.defs)}</defs>" if self.defs else ""
        with open(os.path.join(SALIDA, nombre), "w", encoding="utf-8") as fh:
            fh.write(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {f(self.ancho)} {f(self.alto)}" width="{f(self.ancho)}" height="{f(self.alto)}">'
                     + defs + "".join(self.cuerpo) + "</svg>\n")


def tr(x, y, ang=0, esc=1):
    t = f"translate({f(x)} {f(y)})"
    if ang: t += f" rotate({f(ang)})"
    if esc != 1: t += f" scale({f(esc)})"
    return t


# ---------- curvas ----------

def bezier(p0, p1, p2, p3, t):
    u = 1 - t
    return tuple(u**3 * a + 3 * u * u * t * b + 3 * u * t * t * c + t**3 * d for a, b, c, d in zip(p0, p1, p2, p3))


def tangente(p0, p1, p2, p3, t):
    u = 1 - t
    d = tuple(3 * u * u * (b - a) + 6 * u * t * (c - b) + 3 * t * t * (dd - c) for a, b, c, dd in zip(p0, p1, p2, p3))
    return math.degrees(math.atan2(d[1], d[0]))


def camino(p0, p1, p2, p3):
    return f"M{f(p0[0])} {f(p0[1])} C{f(p1[0])} {f(p1[1])} {f(p2[0])} {f(p2[1])} {f(p3[0])} {f(p3[1])}"


# ---------- piezas ----------

def hoja(svg, x, y, ang, largo, ancho, claro, oscuro, nervio=None, op=1):
    """Una hoja apuntada a lo largo del eje x, con un degradado de la base a la punta y el nervio central."""
    g = svg.degradado([(0, oscuro, 1), (1, claro, 1)], x1=0, y1=0.5, x2=1, y2=0.5)
    L, W = largo, ancho
    d = f"M0 0 C{f(L*0.25)} {f(-W*1.05)} {f(L*0.7)} {f(-W*0.9)} {f(L)} 0 C{f(L*0.7)} {f(W*0.9)} {f(L*0.25)} {f(W*1.05)} 0 0Z"
    s = f'<g transform="{tr(x, y, ang)}"' + (f' opacity="{f(op)}"' if op < 1 else "") + f'><path d="{d}" fill="{g}"/>'
    if nervio:
        s += f'<path d="M{f(L*0.04)} 0 Q{f(L*0.5)} {f(-W*0.12)} {f(L*0.9)} 0" fill="none" stroke="{nervio}" stroke-width="{f(max(0.6, W*0.09))}" stroke-linecap="round" opacity="0.7"/>'
    svg.add(s + "</g>")


def eucalipto(svg, x, y, ang, largo, color, borde, rnd):
    """Una ramita de eucalipto: tallo fino con hojas redondas por pares."""
    svg.add(f'<g transform="{tr(x, y, ang)}"><path d="M0 0 Q{f(largo*0.5)} {f(-largo*0.06)} {f(largo)} 0" fill="none" stroke="{borde}" stroke-width="1.1" stroke-linecap="round"/>')
    n = 5
    for i in range(n):
        t = (i + 0.6) / n
        r = 9 * (1 - t * 0.55)
        for lado in (-1, 1):
            cx, cy = largo * t, lado * r * 0.95
            g = svg.degradado([(0, "#DCE7E2", 1), (1, color, 1)], radial=True, cx=0.4, cy=0.35, r=0.75)
            svg.add(f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(r)}" ry="{f(r*0.86)}" fill="{g}" opacity="0.92"/>')
    svg.add(f'<ellipse cx="{f(largo + 4)}" cy="0" rx="5" ry="4" fill="{color}" opacity="0.9"/></g>')


def petalo(L, W):
    return f"M0 0 C{f(-W)} {f(-L*0.25)} {f(-W*0.9)} {f(-L)} 0 {f(-L)} C{f(W*0.9)} {f(-L)} {f(W)} {f(-L*0.25)} 0 0Z"


def rosa(svg, x, y, r, claro, medio, oscuro, rnd, giro=0):
    """Una rosa vista desde arriba: dos coronas de pétalos con degradado y el centro en espiral."""
    s = f'<g transform="{tr(x, y, giro)}">'
    s += f'<circle r="{f(r*0.75)}" fill="{svg.degradado([(0, medio, 1), (1, oscuro, 1)], radial=True)}"/>'
    for corona, (n, L, W, c1, c2, a0) in enumerate([(9, r * 1.05, r * 0.5, claro, medio, 0), (7, r * 0.78, r * 0.42, claro, medio, 20),
                                                     (5, r * 0.52, r * 0.32, medio, oscuro, 8)]):
        g = svg.degradado([(0, c2, 1), (0.55, c1, 1), (1, c1, 1)], x1=0.5, y1=1, x2=0.5, y2=0)
        for i in range(n):
            a = a0 + i * 360 / n + rnd.uniform(-8, 8)
            s += f'<path transform="rotate({f(a)})" d="{petalo(L * rnd.uniform(0.92, 1.05), W)}" fill="{g}" stroke="{oscuro}" stroke-width="0.6" stroke-opacity="0.35"/>'
    # el centro: unos arcos en espiral
    for i in range(3):
        rr = r * (0.26 - i * 0.07)
        s += f'<path d="M{f(-rr)} 0 A{f(rr)} {f(rr)} 0 1 1 {f(rr*0.6)} {f(rr*0.8)}" transform="rotate({f(i*110)})" fill="none" stroke="{oscuro}" stroke-width="{f(1.3 - i*0.2)}" stroke-linecap="round" opacity="0.75"/>'
    svg.add(s + "</g>")


def flor(svg, x, y, r, color, centro, n=5, giro=0, borde=None):
    """Una flor sencilla de n pétalos redondeados."""
    g = svg.degradado([(0, "#FFFFFF", 1), (0.35, color, 1), (1, color, 1)], radial=True, cx=0.5, cy=1, r=1)
    s = f'<g transform="{tr(x, y, giro)}">'
    for i in range(n):
        s += f'<path transform="rotate({f(i*360/n)})" d="{petalo(r, r*0.62)}" fill="{g}"' + (f' stroke="{borde}" stroke-width="0.5" stroke-opacity="0.5"' if borde else "") + "/>"
    s += f'<circle r="{f(r*0.24)}" fill="{centro}"/>'
    for i in range(7):
        a = math.radians(i * 360 / 7)
        s += f'<circle cx="{f(math.cos(a)*r*0.34)}" cy="{f(math.sin(a)*r*0.34)}" r="{f(r*0.055)}" fill="{centro}"/>'
    svg.add(s + "</g>")


def capullo(svg, x, y, ang, r, color, oscuro, verde):
    s = f'<g transform="{tr(x, y, ang)}">'
    s += f'<path d="M0 0 C{f(r*0.4)} {f(-r*0.9)} {f(r*1.6)} {f(-r*0.8)} {f(r*2)} 0 C{f(r*1.6)} {f(r*0.8)} {f(r*0.4)} {f(r*0.9)} 0 0Z" fill="{svg.degradado([(0, oscuro, 1), (1, color, 1)], x1=1, y1=0.5, x2=0, y2=0.5)}"/>'
    s += f'<path d="M{f(-r*0.2)} 0 C{f(r*0.3)} {f(-r*0.8)} {f(r*0.9)} {f(-r*0.5)} {f(r*1.1)} {f(-r*0.1)} C{f(r*0.6)} {f(-r*0.1)} {f(r*0.4)} {f(r*0.2)} {f(-r*0.2)} 0Z" fill="{verde}"/>'
    s += f'<path d="M{f(-r*0.2)} 0 C{f(r*0.3)} {f(r*0.8)} {f(r*0.9)} {f(r*0.5)} {f(r*1.1)} {f(r*0.1)} C{f(r*0.6)} {f(r*0.1)} {f(r*0.4)} {f(-r*0.2)} {f(-r*0.2)} 0Z" fill="{verde}"/>'
    svg.add(s + "</g>")


def bayas(svg, x, y, r, color, oscuro, n, rnd, brillo=True):
    for i in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        d = r * 1.3 * math.sqrt(rnd.uniform(0, 1)) if i else 0
        cx, cy, rr = x + math.cos(a) * d, y + math.sin(a) * d, r * rnd.uniform(0.85, 1.1)
        g = svg.degradado([(0, "#FFFFFF", 1), (0.25, color, 1), (1, oscuro, 1)], radial=True, cx=0.4, cy=0.38, r=0.65, fx=0.35, fy=0.3)
        svg.add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(rr)}" fill="{g}"/>')


# ---------- Floral: un ramillete que abraza la esquina de arriba a la izquierda ----------

def floral():
    rnd = random.Random(7)
    svg = Svg(460, 380)
    VERDE, VERDE_OSC, SALVIA, EUCA = "#9DBB8C", "#4F7449", "#B9CFA8", "#8FAAA3"
    ROSA, ROSA_MED, ROSA_OSC = "#F6D3CB", "#E8A69D", "#B8655F"
    MELOCOTON, MEL_OSC = "#FBE0CC", "#E3A57F"
    # las dos ramas: a lo largo del borde de arriba y del de la izquierda
    ramas = [((40, 40), (160, 18), (300, 30), (440, 22)), ((40, 40), (22, 140), (34, 250), (20, 365))]
    for rama in ramas:
        svg.add(f'<path d="{camino(*rama)}" fill="none" stroke="{VERDE_OSC}" stroke-width="1.6" stroke-linecap="round" opacity="0.8"/>')
    # ramitas finas que salen de las ramas
    for rama in ramas:
        for t in (0.3, 0.55, 0.8):
            x, y = bezier(*rama, t)
            a = tangente(*rama, t)
            for lado in (-1, 1):
                b = a + lado * rnd.uniform(28, 40)
                L = 48 * (1.1 - t * 0.6)
                ex, ey = x + math.cos(math.radians(b)) * L, y + math.sin(math.radians(b)) * L
                svg.add(f'<path d="M{f(x)} {f(y)} Q{f((x+ex)/2 + lado*4)} {f((y+ey)/2 - 4)} {f(ex)} {f(ey)}" fill="none" stroke="{VERDE_OSC}" stroke-width="0.9" opacity="0.6"/>')
                for k in range(3):
                    s = 0.35 + k * 0.3
                    px, py = x + (ex - x) * s, y + (ey - y) * s
                    svg.add(f'<circle cx="{f(px)}" cy="{f(py)}" r="{f(2.2 - k*0.4)}" fill="#F3EDE4" stroke="#D9C9B4" stroke-width="0.4"/>')
    # eucalipto, detrás
    eucalipto(svg, 70, 52, 8, 120, EUCA, "#6F8C85", rnd)
    eucalipto(svg, 52, 72, 84, 118, EUCA, "#6F8C85", rnd)
    eucalipto(svg, 150, 36, -12, 90, EUCA, "#6F8C85", rnd)
    # hojas a lo largo de las ramas, alternando a cada lado y más pequeñas hacia la punta
    for rama in ramas:
        for i in range(14):
            t = 0.12 + i * 0.062
            x, y = bezier(*rama, t)
            a = tangente(*rama, t)
            lado = 1 if i % 2 else -1
            L = 44 * (1.15 - t * 0.75) * rnd.uniform(0.85, 1.1)
            claro, oscuro = (SALVIA, VERDE) if i % 3 else (VERDE, VERDE_OSC)
            hoja(svg, x, y, a + lado * rnd.uniform(35, 55), L, L * 0.32, claro, oscuro, nervio="#F4F8EE")
    # hojas grandes debajo de las flores
    for a, L in ((-20, 70), (25, 66), (70, 72), (115, 64), (-55, 58), (150, 50)):
        hoja(svg, 62, 62, a, L, L * 0.33, SALVIA, VERDE_OSC, nervio="#F4F8EE")
    # flores pequeñas y capullos a lo largo de las ramas
    for rama, ts in ((ramas[0], (0.42, 0.7, 0.93)), (ramas[1], (0.45, 0.72, 0.94))):
        for j, t in enumerate(ts):
            x, y = bezier(*rama, t)
            a = tangente(*rama, t)
            if j == len(ts) - 1:
                capullo(svg, x, y, a, 9, ROSA_MED, ROSA_OSC, VERDE)
            else:
                flor(svg, x + rnd.uniform(-4, 4), y + rnd.uniform(-2, 6), 13 - j * 2, ROSA if j % 2 else MELOCOTON, "#D9A441", giro=rnd.uniform(0, 70), borde=ROSA_OSC)
    # el ramillete de la esquina: una rosa grande, dos medianas y flores sueltas
    rosa(svg, 112, 86, 26, ROSA, ROSA_MED, ROSA_OSC, rnd, giro=10)
    rosa(svg, 62, 62, 38, ROSA, ROSA_MED, ROSA_OSC, rnd)
    rosa(svg, 82, 128, 22, MELOCOTON, "#F0BE9C", MEL_OSC, rnd, giro=40)
    flor(svg, 138, 50, 15, "#FFFFFF", "#D9A441", giro=12, borde="#C9B8A6")
    flor(svg, 34, 112, 13, "#FFFFFF", "#D9A441", giro=30, borde="#C9B8A6")
    flor(svg, 128, 122, 11, ROSA, "#D9A441", giro=50, borde=ROSA_OSC)
    capullo(svg, 160, 70, 20, 10, ROSA_MED, ROSA_OSC, VERDE)
    capullo(svg, 50, 160, 75, 10, "#F0BE9C", MEL_OSC, VERDE)
    bayas(svg, 150, 100, 3.2, "#E7B04F", "#B07A1F", 5, rnd)
    bayas(svg, 104, 150, 3.2, "#E7B04F", "#B07A1F", 4, rnd)
    svg.guardar("floral-esquina.svg")


# ---------- Navidad: una guirnalda de pino, acebo y piñas en la esquina de arriba a la izquierda, y un lazo ----------

def aguja(svg, x, y, ang, L, color, ancho=1.3):
    a = math.radians(ang)
    return f'<path d="M{f(x)} {f(y)} L{f(x + math.cos(a)*L)} {f(y + math.sin(a)*L)}" stroke="{color}" stroke-width="{f(ancho)}" stroke-linecap="round"/>'


def rama_pino(svg, p, rnd, colores, largo_aguja=16, densidad=26):
    """Una rama de pino: el tallo y muchas agujas a los dos lados, inclinadas hacia la punta, en tres tonos."""
    s = f'<g fill="none"><path d="{camino(*p)}" stroke="#5B4330" stroke-width="2.2" stroke-linecap="round"/>'
    for capa, color in enumerate(colores):
        for i in range(densidad):
            t = (i + rnd.uniform(0, 1)) / densidad
            x, y = bezier(*p, t)
            a = tangente(*p, t)
            L = largo_aguja * (1.05 - t * 0.45) * rnd.uniform(0.8, 1.1) * (1 - capa * 0.12)
            for lado in (-1, 1):
                s += aguja(svg, x, y, a + lado * rnd.uniform(28, 58), L, color, 1.25 - capa * 0.2)
    svg.add(s + "</g>")


def acebo(svg, x, y, ang, L, claro, oscuro):
    """Una hoja de acebo: el borde con pinchos y el nervio central."""
    W = L * 0.36
    puntos = []
    n = 4
    for i in range(n + 1):
        t = i / n
        ancho = W * math.sin(math.pi * t) ** 0.8
        puntos.append((L * t, ancho))
    d = "M0 0"
    for i in range(1, n + 1):
        x0, w0 = puntos[i - 1]; x1, w1 = puntos[i]
        d += f" Q{f((x0+x1)/2)} {f(-(w0+w1)/2*0.55)} {f(x1)} {f(-w1 - (W*0.22 if i < n else 0))}"
    for i in range(n - 1, -1, -1):
        x0, w0 = puntos[i + 1]; x1, w1 = puntos[i]
        d += f" Q{f((x0+x1)/2)} {f((w0+w1)/2*0.55)} {f(x1)} {f(w1 + (W*0.22 if i > 0 else 0))}"
    d += "Z"
    g = svg.degradado([(0, oscuro, 1), (0.6, claro, 1), (1, oscuro, 1)], x1=0, y1=0, x2=0, y2=1)
    svg.add(f'<g transform="{tr(x, y, ang)}"><path d="{d}" fill="{g}" stroke="#163A24" stroke-width="0.8" stroke-linejoin="round"/>'
            f'<path d="M2 0 L{f(L*0.95)} 0" stroke="#CFE3C9" stroke-width="1" opacity="0.6" stroke-linecap="round"/></g>')


def pina(svg, x, y, ang, L):
    """Una piña: escamas en filas, más pequeñas hacia la punta."""
    s = f'<g transform="{tr(x, y, ang)}">'
    s += f'<ellipse cx="{f(L/2)}" cy="0" rx="{f(L/2)}" ry="{f(L*0.3)}" fill="#6B4425"/>'
    filas = 6
    for i in range(filas):
        t = (i + 0.5) / filas
        ancho = L * 0.3 * math.sin(math.pi * min(0.95, t * 0.85 + 0.12))
        for k in range(-2, 3):
            cy = k * ancho * 0.42
            if abs(cy) > ancho: continue
            g = svg.degradado([(0, "#B98A56", 1), (1, "#7A4E2A", 1)], x1=0.5, y1=0, x2=0.5, y2=1)
            s += f'<path d="M{f(L*t - L*0.07)} {f(cy)} Q{f(L*t + L*0.05)} {f(cy - L*0.07)} {f(L*t + L*0.09)} {f(cy)} Q{f(L*t + L*0.05)} {f(cy + L*0.07)} {f(L*t - L*0.07)} {f(cy)}Z" fill="{g}" stroke="#4A2E17" stroke-width="0.5"/>'
    svg.add(s + "</g>")


def navidad():
    rnd = random.Random(12)
    svg = Svg(460, 360)
    PINOS = ["#1E4A2E", "#2F6B40", "#4F8A5A"]
    ramas = [((30, 34), (150, 14), (300, 34), (450, 20)), ((30, 34), (14, 140), (30, 240), (16, 350)),
             ((40, 40), (100, 46), (170, 60), (230, 54)), ((40, 40), (48, 100), (60, 160), (54, 215))]
    for p in ramas:
        rama_pino(svg, p, rnd, PINOS, largo_aguja=21 if p[0][0] == 30 else 17, densidad=70 if p[0][0] == 30 else 40)
    # acebo y bayas en la esquina y a mitad de cada rama
    for (x, y), angs in (((72, 70), (-30, 20, 75, 125)), ((210, 40), (-150, -40, 20)), ((34, 214), (60, 120, -60))):
        for a in angs:
            acebo(svg, x, y, a, 46 if x == 72 else 36, "#3E8A4E", "#1F5A30")
        bayas(svg, x + 2, y + 2, 7 if x == 72 else 5.5, "#E0343A", "#8E1418", 5 if x == 72 else 3, rnd)
    pina(svg, 118, 92, 35, 46)
    pina(svg, 98, 112, 70, 40)
    # bolas doradas
    for x, y, r in ((150, 64, 9), (60, 150, 8)):
        g = svg.degradado([(0, "#FFF4C8", 1), (0.3, "#E7C15A", 1), (1, "#9C7420", 1)], radial=True, cx=0.4, cy=0.38, r=0.7, fx=0.32, fy=0.28)
        svg.add(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{g}"/><rect x="{f(x-2.5)}" y="{f(y-r-3)}" width="5" height="4" fill="#B08A3A"/>')
    # bayas sueltas a lo largo de las ramas
    for p in ramas[:2]:
        for t in (0.55, 0.85):
            x, y = bezier(*p, t)
            bayas(svg, x, y + 4, 4.2, "#E0343A", "#8E1418", 3, rnd)
    svg.guardar("navidad-esquina.svg")

    # el lazo rojo, para el centro del borde de arriba
    lazo = Svg(220, 150)
    rojo = lazo.degradado([(0, "#E2484D", 1), (1, "#9C1C22", 1)], x1=0, y1=0, x2=0, y2=1)
    rojo2 = lazo.degradado([(0, "#C9262D", 1), (1, "#7E1218", 1)], x1=0, y1=0, x2=0, y2=1)
    lazo.add(f'<path d="M110 52 C90 80 70 110 52 146 L70 140 L82 150 C92 112 102 86 110 60Z" fill="{rojo2}"/>')
    lazo.add(f'<path d="M110 52 C130 80 150 110 168 146 L150 140 L138 150 C128 112 118 86 110 60Z" fill="{rojo2}"/>')
    lazo.add(f'<path d="M110 50 C88 18 34 6 22 34 C12 58 50 78 110 58Z" fill="{rojo}" stroke="#7E1218" stroke-width="1"/>')
    lazo.add(f'<path d="M110 50 C132 18 186 6 198 34 C208 58 170 78 110 58Z" fill="{rojo}" stroke="#7E1218" stroke-width="1"/>')
    lazo.add('<path d="M100 46 C80 28 52 24 40 34" fill="none" stroke="#F3A1A4" stroke-width="2" stroke-linecap="round" opacity="0.6"/>')
    lazo.add('<path d="M120 46 C140 28 168 24 180 34" fill="none" stroke="#F3A1A4" stroke-width="2" stroke-linecap="round" opacity="0.6"/>')
    lazo.add(f'<ellipse cx="110" cy="54" rx="15" ry="13" fill="{rojo2}" stroke="#7E1218" stroke-width="1"/>')
    lazo.add('<ellipse cx="106" cy="50" rx="5" ry="3.5" fill="#F3A1A4" opacity="0.55"/>')
    lazo.guardar("navidad-lazo.svg")

    # un copo de nieve (para el fondo, muy suave)
    copo = Svg(40, 40)
    s = '<g transform="translate(20 20)" stroke="#9FB8C8" stroke-width="1.6" stroke-linecap="round" fill="none">'
    for i in range(6):
        s += f'<g transform="rotate({i*60})"><path d="M0 0 L0 -17"/><path d="M0 -10 L-5 -15 M0 -10 L5 -15"/><path d="M0 -5 L-3.5 -8 M0 -5 L3.5 -8"/></g>'
    copo.add(s + "</g>")
    copo.guardar("copo.svg")

    # una estrella de cuatro puntas, dorada
    est = Svg(24, 24)
    g = est.degradado([(0, "#F1D27A", 1), (1, "#B8892E", 1)], x1=0, y1=0, x2=1, y2=1)
    est.add(f'<path d="M12 1 L14.6 9.4 L23 12 L14.6 14.6 L12 23 L9.4 14.6 L1 12 L9.4 9.4Z" fill="{g}"/>')
    est.guardar("estrella.svg")


# ---------- Clásico: esquinas barrocas doradas (hojas de acanto, volutas, un rosetón y perlas), adornos en el centro de los
# bordes y un florón bajo el título ----------

def acanto(L, W, lobulos=4, puntos=48):
    """Una hoja de acanto a lo largo del eje x: el borde ondulado en lóbulos."""
    arriba, abajo = [], []
    for i in range(puntos + 1):
        t = i / puntos
        w = W * math.sin(math.pi * t) ** 0.85 * (1 + 0.22 * math.sin(t * math.pi * 2 * lobulos))
        arriba.append((L * t, -w)); abajo.append((L * t, w * 0.8))
    pts = arriba + abajo[::-1]
    return "M" + " L".join(f"{f(x)} {f(y)}" for x, y in pts) + "Z"


def oro(svg):
    return svg.degradado([(0, "#F0D88A", 1), (0.5, "#C9A24A", 1), (1, "#97712A", 1)], x1=0, y1=0, x2=1, y2=1)


def clasico():
    svg = Svg(170, 170)
    g, trazo = oro(svg), "#8C6A22"
    # las volutas a lo largo de los dos bordes (la de la izquierda es la de arriba reflejada en la diagonal)
    voluta = (f'<path d="M16 16 C60 10 100 12 128 26 C150 37 150 60 131 62 C117 63 113 47 125 45" fill="none" stroke="{g}" stroke-width="2.6" stroke-linecap="round"/>'
              f'<path d="M26 24 C62 22 92 26 114 38 C126 45 128 54 122 56" fill="none" stroke="{g}" stroke-width="1.1" stroke-linecap="round"/>'
              f'<path d="M128 26 C142 18 152 16 160 18" fill="none" stroke="{g}" stroke-width="1.4" stroke-linecap="round"/>')
    for x, r in ((150, 2.6), (158, 2.1), (165, 1.6)):
        voluta += f'<circle cx="{x}" cy="18" r="{r}" fill="{g}"/>'
    # hojitas sobre la voluta
    for x, y, a in ((70, 15, -25), (98, 18, -10)):
        voluta += f'<path transform="{tr(x, y, a)}" d="{acanto(16, 4.5, 2, 24)}" fill="{g}" stroke="{trazo}" stroke-width="0.4"/>'
    svg.add(voluta)
    svg.add(f'<g transform="matrix(0 1 1 0 0 0)">{voluta}</g>')
    # tres hojas de acanto en abanico desde la esquina
    for a, L in ((22, 62), (45, 78), (68, 62)):
        svg.add(f'<g transform="{tr(18, 18, a)}"><path d="{acanto(L, L*0.2)}" fill="{g}" stroke="{trazo}" stroke-width="0.6"/>'
                f'<path d="M4 0 Q{f(L*0.5)} {f(-L*0.03)} {f(L*0.92)} 0" fill="none" stroke="{trazo}" stroke-width="0.9" opacity="0.8"/></g>')
    # el rosetón de la esquina
    svg.add(f'<g transform="translate(18 18)">' + "".join(
        f'<path transform="rotate({i*45})" d="{petalo(13, 6)}" fill="{g}" stroke="{trazo}" stroke-width="0.5"/>' for i in range(8))
        + f'<circle r="5.5" fill="#8C6A22"/><circle r="3" fill="#F0D88A"/></g>')
    svg.guardar("clasico-esquina.svg")

    # el adorno del centro de los bordes: una concha entre dos volutas
    c = Svg(140, 40)
    g = oro(c)
    lado = (f'<path d="M58 20 C46 8 30 8 22 16 C16 22 20 30 27 28 C32 26 31 20 26 21" fill="none" stroke="{g}" stroke-width="2" stroke-linecap="round"/>'
            f'<path d="M22 16 C14 12 8 16 2 20" fill="none" stroke="{g}" stroke-width="1.2" stroke-linecap="round"/>'
            f'<path transform="{tr(56, 22, 160)}" d="{acanto(18, 4, 2, 24)}" fill="{g}"/>')
    c.add(lado + f'<g transform="matrix(-1 0 0 1 140 0)">{lado}</g>')
    concha = "".join(f'<path transform="translate(70 30) rotate({a})" d="{petalo(20, 5)}" fill="{g}" stroke="#8C6A22" stroke-width="0.5"/>' for a in (-60, -30, 0, 30, 60))
    c.add(concha + '<circle cx="70" cy="30" r="3.5" fill="#8C6A22"/>')
    c.guardar("clasico-centro.svg")

    # el florón bajo el título: una flor entre dos hojas de acanto y dos líneas que se afinan
    sep = Svg(320, 30)
    g = oro(sep)
    sep.add(f'<path d="M0 15 L112 14.2 L112 15.8Z" fill="{g}"/><path d="M320 15 L208 14.2 L208 15.8Z" fill="{g}"/>')
    sep.add(f'<path transform="{tr(148, 15, 180)}" d="{acanto(36, 6, 3, 36)}" fill="{g}" stroke="#8C6A22" stroke-width="0.4"/>')
    sep.add(f'<path transform="{tr(172, 15, 0)}" d="{acanto(36, 6, 3, 36)}" fill="{g}" stroke="#8C6A22" stroke-width="0.4"/>')
    sep.add('<g transform="translate(160 15)">' + "".join(f'<path transform="rotate({i*60})" d="{petalo(10, 4.5)}" fill="{g}" stroke="#8C6A22" stroke-width="0.4"/>' for i in range(6))
            + '<circle r="3" fill="#8C6A22"/></g>')
    sep.guardar("clasico-separador.svg")


# ---------- Pergamino: rúbricas de tinta en las esquinas y bajo el título, y un sello de lacre con el árbol ----------

TINTA = "#4A2E17"


def pergamino():
    svg = Svg(150, 150)
    # trazos de pluma: uno grueso y otro fino al lado, como cuando la plumilla se abre y se cierra
    trazo = (f'<path d="M14 44 C14 22 26 14 46 14 C80 14 104 32 138 18" fill="none" stroke="{TINTA}" stroke-width="2.2" stroke-linecap="round"/>'
             f'<path d="M46 14 C30 30 38 50 56 42 C68 36 62 20 50 27" fill="none" stroke="{TINTA}" stroke-width="1.3" stroke-linecap="round"/>'
             f'<path d="M70 20 C84 28 96 30 104 26" fill="none" stroke="{TINTA}" stroke-width="0.9" stroke-linecap="round"/>'
             f'<circle cx="138" cy="18" r="1.8" fill="{TINTA}"/>')
    svg.add(trazo + f'<g transform="matrix(0 1 1 0 0 0)">{trazo}</g>')
    svg.add(f'<path d="M24 24 C34 30 40 40 42 52" fill="none" stroke="{TINTA}" stroke-width="1" stroke-linecap="round"/><circle cx="14" cy="14" r="3.2" fill="{TINTA}"/>')
    for x, y, a in ((86, 24, -20), (24, 86, 110)):
        svg.add(f'<path transform="{tr(x, y, a)}" d="M0 0 C6 -5 14 -5 18 0 C14 5 6 5 0 0Z" fill="none" stroke="{TINTA}" stroke-width="0.9"/>')
    svg.guardar("pergamino-esquina.svg")

    # la rúbrica bajo el título: una línea que se enrosca en el centro
    r = Svg(360, 40)
    r.add(f'<path d="M8 26 C70 32 130 30 168 20 C184 15 186 4 176 5 C164 6 166 24 186 26 C232 30 296 26 352 14" fill="none" stroke="{TINTA}" stroke-width="2" stroke-linecap="round"/>'
          f'<path d="M30 30 C80 34 120 33 150 27" fill="none" stroke="{TINTA}" stroke-width="0.8" stroke-linecap="round" opacity="0.8"/>'
          f'<path d="M210 30 C250 32 290 30 330 22" fill="none" stroke="{TINTA}" stroke-width="0.8" stroke-linecap="round" opacity="0.8"/>'
          f'<circle cx="8" cy="26" r="1.8" fill="{TINTA}"/><circle cx="352" cy="14" r="1.8" fill="{TINTA}"/>')
    r.guardar("pergamino-rubrica.svg")

    # el sello de lacre: dos cintas, la gota de lacre con el borde irregular, un aro en relieve y un árbol
    s = Svg(130, 150)
    rnd = random.Random(5)
    cinta = s.degradado([(0, "#9E2A22", 1), (1, "#5E1410", 1)], x1=0, y1=0, x2=0, y2=1)
    s.add(f'<path d="M58 70 L30 140 L40 132 L46 146 L72 76Z" fill="{cinta}"/><path d="M72 70 L100 140 L90 132 L84 146 L58 76Z" fill="{cinta}"/>')
    pts = []
    for i in range(36):
        a = i / 36 * 2 * math.pi
        rr = 46 + rnd.uniform(-2.5, 2.5) + 2.5 * math.sin(a * 5)
        pts.append((65 + math.cos(a) * rr, 62 + math.sin(a) * rr))
    lacre = s.degradado([(0, "#D2493E", 1), (0.6, "#A42A22", 1), (1, "#6E1712", 1)], radial=True, cx=0.42, cy=0.38, r=0.65, fx=0.35, fy=0.3)
    s.add(f'<path d="M{" L".join(f"{f(x)} {f(y)}" for x, y in pts)}Z" fill="{lacre}" stroke="#5E1410" stroke-width="0.8"/>')
    s.add('<circle cx="65" cy="62" r="33" fill="none" stroke="#6E1712" stroke-width="2.4"/><circle cx="64" cy="61" r="33" fill="none" stroke="#E07A6E" stroke-width="0.8" opacity="0.6"/>')
    hondo = "#7A1A14"
    arbol = (f'<path d="M62 86 L63 70 L56 62 M63 72 L70 64 M64 70 L65 58" fill="none" stroke="{hondo}" stroke-width="3" stroke-linecap="round"/>'
             f'<path d="M60 88 L70 88 L66 72 L64 72Z" fill="{hondo}"/>')
    for cx, cy, rr in ((65, 48, 11), (54, 54, 9), (76, 54, 9), (58, 42, 8), (72, 42, 8), (65, 37, 7)):
        arbol += f'<circle cx="{cx}" cy="{cy}" r="{rr}" fill="{hondo}"/>'
    s.add(f'<g transform="translate(-0.8 -0.8)" opacity="0.5">{arbol.replace(hondo, "#E88A7E")}</g>' + arbol)
    s.add('<ellipse cx="46" cy="36" rx="12" ry="6" transform="rotate(-35 46 36)" fill="#FFFFFF" opacity="0.18"/>')
    s.guardar("pergamino-sello.svg")


# ---------- Sencillo: una ramita de trazo fino junto al título ----------

def sencillo():
    svg = Svg(60, 60)
    v = "#2F7A3A"
    s = f'<g fill="none" stroke="{v}" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 54 C22 40 34 26 50 8"/>'
    for t, lado, L in ((0.3, 1, 13), (0.45, -1, 13), (0.6, 1, 11), (0.75, -1, 10), (0.9, 1, 8)):
        x, y = 10 + 40 * t, 54 - 46 * t
        a = -50 + lado * 42
        s += f'<path transform="{tr(x, y, a)}" d="M0 0 C{f(L*0.3)} {f(-L*0.32)} {f(L*0.75)} {f(-L*0.32)} {f(L)} 0 C{f(L*0.75)} {f(L*0.32)} {f(L*0.3)} {f(L*0.32)} 0 0Z"/>'
    svg.add(s + "</g>")
    svg.guardar("sencillo-rama.svg")


if __name__ == "__main__":
    floral(); navidad(); clasico(); pergamino(); sencillo()
    for viejo in ("pergamino-adorno.svg",):
        if os.path.exists(os.path.join(SALIDA, viejo)): os.remove(os.path.join(SALIDA, viejo))
    print("ok:", sorted(os.listdir(SALIDA)))
