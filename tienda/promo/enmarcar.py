# Capturas de Google Play enmarcadas: un titular y la pantalla dentro de un teléfono o una tableta, en los seis idiomas.
#   python tienda/promo/enmarcar.py [idioma…]
# Parte de las capturas crudas (tienda/promo/crudas, ver su LEEME.md) y deja
#   tienda/promo/telefono/<idioma>/1.png…8.png  (1080 × 1920, 9:16), en el orden en que se suben
#   tienda/promo/tablet/<idioma>/1.png…8.png    (2560 × 1440, 16:9)
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
FUENTES = os.path.join(AQUI, "..", "..", "src", "ArbolGenealogico.Movil", "Recursos", "Fuentes")
NEGRITA = os.path.join(FUENTES, "BarlowSemiCondensed-SemiBold.ttf")
NORMAL = os.path.join(FUENTES, "BarlowSemiCondensed-Medium.ttf")
LOGO = os.path.join(AQUI, "..", "destacada-fuente", "ramo.png")
ORO, BLANCO, GRIS = (245, 196, 81), (255, 255, 255), (170, 178, 197)

# el orden de la tienda: lo más vistoso primero
PANTALLAS = ["a-arbol", "h-aspecto", "d-imprimir", "c-compartir", "e-fusion", "f-ficha", "b-familiar", "g-buscar"]

# titular (primera línea en blanco, segunda en dorado) y subtítulo
TEXTOS = {
 "es": {
  "a-arbol": ("Tu familia,", "de un vistazo", "Toca a alguien y se ilumina su línea"),
  "b-familiar": ("Añade familiares", "en un toque", "Padres, hermanos, pareja e hijos"),
  "c-compartir": ("Compártelo", "como quieras", "En PDF, como imagen o el archivo para la app"),
  "d-imprimir": ("Imprímelo", "en varias hojas", "Para recortar y pegar, en color o en blanco y negro"),
  "e-fusion": ("Juntad", "vuestros árboles", "Cada uno añade lo que sabe y no se borra nada"),
  "f-ficha": ("Fechas, lugares", "e historias", "Guarda la historia de cada persona"),
  "g-buscar": ("Encuentra a cualquiera", "al instante", "Busca por nombre o apellido"),
  "h-aspecto": ("Tu árbol,", "a tu manera", "Elige los colores, la colocación y las tarjetas"),
 },
 "en": {
  "a-arbol": ("Your whole family,", "at a glance", "Tap someone and their line lights up"),
  "b-familiar": ("Add relatives", "in one tap", "Parents, siblings, partners and children"),
  "c-compartir": ("Share it", "your way", "As a PDF, an image or a file for the app"),
  "d-imprimir": ("Print it", "on several sheets", "Cut and tape together, in color or black and white"),
  "e-fusion": ("Merge", "your family's trees", "Everyone adds what they know, nothing is lost"),
  "f-ficha": ("Dates, places", "and stories", "Keep every person's story"),
  "g-buscar": ("Find anyone", "instantly", "Search by first or last name"),
  "h-aspecto": ("Your tree,", "your way", "Choose the colors, the layout and the cards"),
 },
 "fr": {
  "a-arbol": ("Toute votre famille,", "d'un coup d'œil", "Touchez quelqu'un : sa lignée s'illumine"),
  "b-familiar": ("Ajoutez des proches", "en un geste", "Parents, frères et sœurs, conjoint, enfants"),
  "c-compartir": ("Partagez-le", "comme vous voulez", "En PDF, en image ou le fichier pour l'appli"),
  "d-imprimir": ("Imprimez-le", "sur plusieurs feuilles", "À découper et coller, en couleur ou en noir et blanc"),
  "e-fusion": ("Fusionnez", "vos arbres", "Chacun ajoute ce qu'il sait, rien n'est effacé"),
  "f-ficha": ("Dates, lieux", "et histoires", "Gardez l'histoire de chacun"),
  "g-buscar": ("Retrouvez n'importe qui", "en un instant", "Recherche par prénom ou par nom"),
  "h-aspecto": ("Votre arbre,", "à votre façon", "Choisissez les couleurs, la disposition et les cadres"),
 },
 "it": {
  "a-arbol": ("Tutta la famiglia", "a colpo d'occhio", "Tocca una persona e la sua linea si illumina"),
  "b-familiar": ("Aggiungi parenti", "con un tocco", "Genitori, fratelli, partner e figli"),
  "c-compartir": ("Condividilo", "come vuoi", "In PDF, come immagine o come file per l'app"),
  "d-imprimir": ("Stampalo", "su più fogli", "Da ritagliare e unire, a colori o in bianco e nero"),
  "e-fusion": ("Unite", "i vostri alberi", "Ognuno aggiunge ciò che sa, non si perde nulla"),
  "f-ficha": ("Date, luoghi", "e storie", "Conserva la storia di ogni persona"),
  "g-buscar": ("Trova chiunque", "in un attimo", "Cerca per nome o cognome"),
  "h-aspecto": ("Il tuo albero,", "come vuoi tu", "Scegli i colori, la disposizione e i riquadri"),
 },
 "de": {
  "a-arbol": ("Deine ganze Familie", "auf einen Blick", "Tippe jemanden an und seine Linie leuchtet auf"),
  "b-familiar": ("Verwandte hinzufügen", "mit einem Tipp", "Eltern, Geschwister, Partner und Kinder"),
  "c-compartir": ("Teile ihn,", "wie du willst", "Als PDF, als Bild oder als Datei für die App"),
  "d-imprimir": ("Drucke ihn", "auf mehreren Blättern", "Zum Ausschneiden und Zusammenkleben, in Farbe oder Schwarzweiß"),
  "e-fusion": ("Führt eure Stammbäume", "zusammen", "Jeder ergänzt, was er weiß – nichts geht verloren"),
  "f-ficha": ("Daten, Orte", "und Geschichten", "Bewahre die Geschichte jedes Menschen"),
  "g-buscar": ("Finde jeden", "sofort", "Suche nach Vor- oder Nachname"),
  "h-aspecto": ("Dein Stammbaum,", "wie du willst", "Wähle Farben, Anordnung und Karten"),
 },
 "pt": {
  "a-arbol": ("Sua família toda,", "de relance", "Toque em alguém e a linha dele se acende"),
  "b-familiar": ("Adicione parentes", "com um toque", "Pais, irmãos, cônjuge e filhos"),
  "c-compartir": ("Compartilhe", "do seu jeito", "Em PDF, como imagem ou o arquivo para o app"),
  "d-imprimir": ("Imprima", "em várias folhas", "Para recortar e colar, colorido ou em preto e branco"),
  "e-fusion": ("Juntem", "suas árvores", "Cada um adiciona o que sabe e nada se perde"),
  "f-ficha": ("Datas, lugares", "e histórias", "Guarde a história de cada pessoa"),
  "g-buscar": ("Encontre qualquer pessoa", "na hora", "Busque por nome ou sobrenome"),
  "h-aspecto": ("Sua árvore,", "do seu jeito", "Escolha as cores, a disposição e os cartões"),
 },
}

def fondo(w, h):
    """Azul noche con un halo arriba, como la imagen destacada."""
    img = Image.new("RGB", (w, h), (10, 12, 18))
    halo = Image.new("L", (w, h), 0)
    ImageDraw.Draw(halo).ellipse((-w * 0.3, -h * 0.45, w * 1.3, h * 0.55), fill=255)
    halo = halo.filter(ImageFilter.GaussianBlur(min(w, h) * 0.25))
    img.paste(Image.new("RGB", (w, h), (31, 42, 66)), (0, 0), halo)
    return img

def fuente(ruta, tam): return ImageFont.truetype(ruta, tam)

def ajustar(d, texto, ruta, tam, ancho):
    """El tamaño mayor (hasta tam) con el que el texto cabe en una línea de ese ancho."""
    while tam > 20 and d.textlength(texto, font=fuente(ruta, tam)) > ancho: tam -= 2
    return fuente(ruta, tam)

def partir(d, texto, f, ancho):
    lineas, linea = [], ""
    for p in texto.split():
        prueba = (linea + " " + p).strip()
        if d.textlength(prueba, font=f) <= ancho or not linea: linea = prueba
        else: lineas.append(linea); linea = p
    return lineas + [linea]

def textos(img, x, y, ancho, t1, t2, sub, tam, centrado):
    """Titular en dos líneas y subtítulo; devuelve dónde acaba."""
    d = ImageDraw.Draw(img)
    f = ajustar(d, max(t1, t2, key=lambda s: d.textlength(s, font=fuente(NEGRITA, tam))), NEGRITA, tam, ancho)
    fs = fuente(NORMAL, int(tam * 0.42))
    def pon(texto, fnt, color, y):
        w = d.textlength(texto, font=fnt)
        d.text((x + (ancho - w) / 2 if centrado else x, y), texto, font=fnt, fill=color)
    pon(t1, f, BLANCO, y); y += f.size * 1.05
    pon(t2, f, ORO, y); y += f.size * 1.35
    for l in partir(d, sub, fs, ancho):
        pon(l, fs, GRIS, y); y += fs.size * 1.25
    return y

def redondear(img, r):
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, img.width - 1, img.height - 1), r, fill=255)
    img.putalpha(m)
    return img

def aparato(img, pantalla, x, y, w, borde, radio):
    """La pantalla (ya a su tamaño final) dentro de un marco negro con sombra y un brillo dorado detrás."""
    h = int(pantalla.height * w / pantalla.width)
    W, H = w + 2 * borde, h + 2 * borde
    capa = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dc = ImageDraw.Draw(capa)
    dc.rounded_rectangle((x + W * 0.02, y + H * 0.12, x + W * 0.98, y + H * 0.88), radio * 2, fill=ORO + (46,))
    dc.rounded_rectangle((x, y + borde * 2, x + W, y + H + borde * 2), radio, fill=(0, 0, 0, 200))
    capa = capa.filter(ImageFilter.GaussianBlur(borde * 4))
    img.paste(capa, (0, 0), capa)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((x, y, x + W, y + H), radio, fill=(5, 6, 10), outline=(58, 66, 86), width=max(2, borde // 6))
    p = redondear(pantalla.convert("RGB").resize((w, h), Image.LANCZOS).convert("RGBA"), radio - borde)
    img.paste(p, (x + borde, y + borde), p)
    return y + H

def marca(img, x, y, alto, centrado_en=None):
    logo = Image.open(LOGO).convert("RGBA")
    logo = logo.resize((int(logo.width * alto / logo.height), alto), Image.LANCZOS)
    d = ImageDraw.Draw(img)
    f = fuente(NEGRITA, int(alto * 0.8))
    ancho = logo.width + alto * 0.3 + d.textlength("Ramo", font=f)
    if centrado_en is not None: x = int(centrado_en - ancho / 2)
    img.paste(logo, (x, y), logo)
    d.text((x + logo.width + alto * 0.3, y + alto * 0.02), "Ramo", font=f, fill=BLANCO)

def telefono(l, nombre, crudo):
    W, H = 1080, 1920
    img = fondo(W, H)
    t1, t2, sub = TEXTOS[l][nombre]
    marca(img, 0, 70, 54, centrado_en=W / 2)
    textos(img, 80, 170, W - 160, t1, t2, sub, 104, True)
    if isinstance(crudo, tuple):
        # dos aspectos del árbol: dos teléfonos algo más pequeños, el segundo un poco más abajo y montado sobre el primero
        w = 480
        aparato(img, crudo[0], 50, 560, w, 14, 52)
        aparato(img, crudo[1], W - w - 28 - 50, 660, w, 14, 52)
        return img
    # el teléfono entero, centrado debajo
    w = 650
    aparato(img, crudo, (W - w - 36) // 2, 540, w, 18, 64)
    return img

def tableta(l, nombre, crudo):
    W, H = 2560, 1440
    img = fondo(W, H)
    t1, t2, sub = TEXTOS[l][nombre]
    w, borde = 1640, 26
    if isinstance(crudo, tuple):
        # dos aspectos del árbol: dos tabletas más pequeñas, en diagonal y montadas
        w2, b2 = 1180, 20
        h2 = int(crudo[0].height * w2 / crudo[0].width)
        aparato(img, crudo[0], W - w - 2 * borde - 110, 120, w2, b2, 44)
        aparato(img, crudo[1], W - w2 - 2 * b2 - 110, H - h2 - 2 * b2 - 120, w2, b2, 44)
        x = W - w - 2 * borde - 110
    else:
        h = int(crudo.height * w / crudo.width)
        x = W - w - 2 * borde - 110
        aparato(img, crudo, x, (H - h) // 2 - borde, w, borde, 56)
    marca(img, 120, 140, 70)
    ancho = x - 120 - 110
    # el titular, centrado en vertical en su columna
    alto = textos(fondo(10, 10).resize((W, H)), 120, 0, ancho, t1, t2, sub, 120, False)
    textos(img, 120, (H - alto) // 2 + 40, ancho, t1, t2, sub, 120, False)
    return img

if __name__ == "__main__":
    idiomas = sys.argv[1:] or list(TEXTOS)
    for l in idiomas:
        for equipo, hacer in (("telefono", telefono), ("tablet", tableta)):
            sal = os.path.join(AQUI, equipo, l)
            os.makedirs(sal, exist_ok=True)
            for n, nombre in enumerate(PANTALLAS, 1):
                cruda = lambda n: Image.open(os.path.join(AQUI, "crudas", equipo, l, n + ".png"))
                # «h-aspecto» junta dos pantallas: compacto, en vertical y con foto; balanceado y solo con el nombre
                crudo = (cruda(nombre + "-foto"), cruda(nombre + "-nombre")) if nombre == "h-aspecto" else cruda(nombre)
                hacer(l, nombre, crudo).save(os.path.join(sal, f"{n}.png"), optimize=True)
        print(l)
