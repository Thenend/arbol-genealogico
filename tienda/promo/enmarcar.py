# Capturas de Google Play enmarcadas: un titular y la pantalla dentro de un teléfono o una tableta, en los seis idiomas.
#   python tienda/promo/enmarcar.py [idioma…] [--album]
# Parte de las capturas crudas (tienda/promo/crudas, ver su LEEME.md) y deja
#   tienda/promo/telefono/<idioma>/1.png…8.png  (1080 × 1920, 9:16), en el orden en que se suben
#   tienda/promo/tablet/<idioma>/1.png…8.png    (2560 × 1440, 16:9)
# Con --album, las de la variante con el tema Álbum: de tienda/variante-album/promo/crudas a tienda/variante-album/promo,
# con los colores del tema Álbum en vez de los de Jardín.
import os, re, subprocess, sys, tempfile
from PIL import Image, ImageDraw, ImageFilter, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
FUENTES = os.path.join(AQUI, "..", "..", "src", "ArbolGenealogico.Movil", "Recursos", "Fuentes")
NEGRITA = os.path.join(FUENTES, "BarlowSemiCondensed-SemiBold.ttf")
NORMAL = os.path.join(FUENTES, "BarlowSemiCondensed-Medium.ttf")
# la letra de la aplicación, para lo que se dibuja dentro de la pantalla (el aviso, el visor del PDF)
APP = os.path.join(FUENTES, "AtkinsonHyperlegibleNext-Medium.ttf")
APP_NEGRITA = os.path.join(FUENTES, "AtkinsonHyperlegibleNext-SemiBold.ttf")
IDIOMAS = os.path.join(AQUI, "..", "..", "src", "ArbolGenealogico.Core", "Idiomas")
LOGO = os.path.join(AQUI, "..", "destacada-fuente", "ramo.png")
ALBUM = "--album" in sys.argv
# dónde están las crudas y dónde van las enmarcadas
PROMO = os.path.join(AQUI, "..", "variante-album", "promo") if ALBUM else AQUI
# los colores del tema de la primera vez. Jardín: tinta verde oscura, verde de los botones y gris verdoso; Álbum: tinta
# sepia oscura, marrón dorado y gris pardo. VERDE es el del titular, aunque en Álbum no lo sea.
if ALBUM:
    TINTA, VERDE, GRIS = (58, 42, 26), (138, 90, 30), (118, 98, 75)
    # el fondo (abajo y el halo), la sombra y el marco de los aparatos, la pantalla de bloqueo (degradado, halo, botones,
    # fecha) y la conversación (fondo, burbuja propia, botón de enviar)
    K = dict(fondo=(230, 217, 189), halo=(251, 247, 238), sombra=(58, 42, 26), marco=(32, 27, 22), borde_marco=(88, 76, 64),
             bloqueo=((112, 82, 54), (38, 27, 18)), halo_bloqueo=(214, 172, 120), boton_bloqueo=(74, 56, 40), fecha=(232, 220, 200),
             charla=(240, 233, 220), mia=(238, 222, 194), enviar=(107, 74, 43))
else:
    TINTA, VERDE, GRIS = (31, 45, 34), (47, 122, 58), (86, 104, 90)
    K = dict(fondo=(222, 234, 213), halo=(247, 250, 243), sombra=(31, 45, 34), marco=(24, 30, 26), borde_marco=(70, 84, 74),
             bloqueo=((52, 92, 66), (16, 34, 26)), halo_bloqueo=(120, 170, 120), boton_bloqueo=(44, 70, 54), fecha=(210, 225, 212),
             charla=(236, 240, 232), mia=(214, 236, 206), enviar=VERDE)
ORO = (216, 168, 64)

# el orden de la tienda (como pide el estudio de mercado, lo que más vende en las tres primeras): el árbol, compartirlo
# en PDF o imagen, imprimirlo y regalarlo; después, el aspecto, las historias, los cumpleaños, la fusión y lo fácil que es
# añadir a alguien. «c-pdf» e «i-cumple» se componen aquí (ver pantalla_pdf y pantalla_bloqueo).
PANTALLAS = ["a-arbol", "c-pdf", "d-lamina", "h-aspecto", "f-ficha", "i-cumple", "e-fusion", "b-familiar"]

# titular (primera línea en blanco, segunda en dorado) y subtítulo
TEXTOS = {
 "es": {
  "a-arbol": ("Tu familia,", "de un vistazo", "Fácil y sin cuentas: todo se queda en tu móvil"),
  "b-familiar": ("Añade familiares", "en un toque", "Padres, hermanos, pareja e hijos"),
  "c-compartir": ("Compártelo", "como quieras", "En PDF, como imagen o el archivo para la app"),
  "d-imprimir": ("Imprímelo", "en varias hojas", "Para recortar y pegar, en color o en blanco y negro"),
  "e-fusion": ("Juntad", "vuestros árboles", "Cada uno añade lo que sabe y no se borra nada"),
  "f-ficha": ("Fechas, lugares", "e historias", "Guarda la historia de cada persona"),
  "g-buscar": ("Encuentra a cualquiera", "al instante", "Busca por nombre o apellido"),
  "h-aspecto": ("Tu árbol,", "a tu manera", "Elige los colores, la colocación y las tarjetas"),
  "c-pdf": ("Compártelo", "con toda la familia", "Mándalo en PDF por mensaje o correo: llega entero"),
  "d-lamina": ("Imprímelo", "y regálalo", "En varias hojas o en una lámina lista para enmarcar"),
  "i-cumple": ("No olvides", "ningún cumpleaños", "Un aviso en tu móvil, aunque no abras la app"),
 },
 "en": {
  "a-arbol": ("Your whole family,", "at a glance", "Easy, no account: everything stays on your phone"),
  "b-familiar": ("Add relatives", "in one tap", "Parents, siblings, partners and children"),
  "c-compartir": ("Share it", "your way", "As a PDF, an image or a file for the app"),
  "d-imprimir": ("Print it", "on several sheets", "Cut and tape together, in color or black and white"),
  "e-fusion": ("Merge", "your family's trees", "Everyone adds what they know, nothing is lost"),
  "f-ficha": ("Dates, places", "and stories", "Keep every person's story"),
  "g-buscar": ("Find anyone", "instantly", "Search by first or last name"),
  "h-aspecto": ("Your tree,", "your way", "Choose the colors, the layout and the cards"),
  "c-pdf": ("Share it", "with the whole family", "Send it as a PDF by message or email: they get the whole tree"),
  "d-lamina": ("Print it", "and give it as a gift", "On several sheets or as a print ready to frame"),
  "i-cumple": ("Never forget", "a birthday", "A reminder on your phone, even with the app closed"),
 },
 "fr": {
  "a-arbol": ("Toute votre famille,", "d'un coup d'œil", "Simple et sans compte : tout reste sur votre téléphone"),
  "b-familiar": ("Ajoutez des proches", "en un geste", "Parents, frères et sœurs, conjoint, enfants"),
  "c-compartir": ("Partagez-le", "comme vous voulez", "En PDF, en image ou le fichier pour l'appli"),
  "d-imprimir": ("Imprimez-le", "sur plusieurs feuilles", "À découper et coller, en couleur ou en noir et blanc"),
  "e-fusion": ("Fusionnez", "vos arbres", "Chacun ajoute ce qu'il sait, rien n'est effacé"),
  "f-ficha": ("Dates, lieux", "et histoires", "Gardez l'histoire de chacun"),
  "g-buscar": ("Retrouvez n'importe qui", "en un instant", "Recherche par prénom ou par nom"),
  "h-aspecto": ("Votre arbre,", "à votre façon", "Choisissez les couleurs, la disposition et les cadres"),
  "c-pdf": ("Partagez-le", "avec toute la famille", "Envoyez-le en PDF par message ou e-mail : il arrive en entier"),
  "d-lamina": ("Imprimez-le", "et offrez-le", "Sur plusieurs feuilles ou en affiche prête à encadrer"),
  "i-cumple": ("N'oubliez plus", "aucun anniversaire", "Un rappel sur votre téléphone, même appli fermée"),
 },
 "it": {
  "a-arbol": ("Tutta la famiglia", "a colpo d'occhio", "Facile e senza account: tutto resta sul tuo telefono"),
  "b-familiar": ("Aggiungi parenti", "con un tocco", "Genitori, fratelli, partner e figli"),
  "c-compartir": ("Condividilo", "come vuoi", "In PDF, come immagine o come file per l'app"),
  "d-imprimir": ("Stampalo", "su più fogli", "Da ritagliare e unire, a colori o in bianco e nero"),
  "e-fusion": ("Unite", "i vostri alberi", "Ognuno aggiunge ciò che sa, non si perde nulla"),
  "f-ficha": ("Date, luoghi", "e storie", "Conserva la storia di ogni persona"),
  "g-buscar": ("Trova chiunque", "in un attimo", "Cerca per nome o cognome"),
  "h-aspecto": ("Il tuo albero,", "come vuoi tu", "Scegli i colori, la disposizione e i riquadri"),
  "c-pdf": ("Condividilo", "con tutta la famiglia", "Mandalo in PDF via messaggio o e-mail: arriva intero"),
  "d-lamina": ("Stampalo", "e regalalo", "Su più fogli o in una stampa pronta da incorniciare"),
  "i-cumple": ("Non dimenticare", "nessun compleanno", "Un avviso sul telefono, anche con l'app chiusa"),
 },
 "de": {
  "a-arbol": ("Deine ganze Familie", "auf einen Blick", "Einfach und ohne Konto: Alles bleibt auf deinem Handy"),
  "b-familiar": ("Verwandte hinzufügen", "mit einem Tipp", "Eltern, Geschwister, Partner und Kinder"),
  "c-compartir": ("Teile ihn,", "wie du willst", "Als PDF, als Bild oder als Datei für die App"),
  "d-imprimir": ("Drucke ihn", "auf mehreren Blättern", "Zum Ausschneiden und Zusammenkleben, in Farbe oder Schwarzweiß"),
  "e-fusion": ("Führt eure Stammbäume", "zusammen", "Jeder ergänzt, was er weiß – nichts geht verloren"),
  "f-ficha": ("Daten, Orte", "und Geschichten", "Bewahre die Geschichte jedes Menschen"),
  "g-buscar": ("Finde jeden", "sofort", "Suche nach Vor- oder Nachname"),
  "h-aspecto": ("Dein Stammbaum,", "wie du willst", "Wähle Farben, Anordnung und Karten"),
  "c-pdf": ("Teile ihn", "mit der ganzen Familie", "Schick ihn als PDF per Nachricht oder E-Mail: Er kommt ganz an"),
  "d-lamina": ("Drucke ihn", "und verschenke ihn", "Auf mehreren Blättern oder als Bild zum Einrahmen"),
  "i-cumple": ("Vergiss keinen", "Geburtstag mehr", "Eine Erinnerung aufs Handy, auch wenn die App zu ist"),
 },
 "pt": {
  "a-arbol": ("Sua família toda,", "de relance", "Fácil e sem conta: tudo fica no seu celular"),
  "b-familiar": ("Adicione parentes", "com um toque", "Pais, irmãos, cônjuge e filhos"),
  "c-compartir": ("Compartilhe", "do seu jeito", "Em PDF, como imagem ou o arquivo para o app"),
  "d-imprimir": ("Imprima", "em várias folhas", "Para recortar e colar, colorido ou em preto e branco"),
  "e-fusion": ("Juntem", "suas árvores", "Cada um adiciona o que sabe e nada se perde"),
  "f-ficha": ("Datas, lugares", "e histórias", "Guarde a história de cada pessoa"),
  "g-buscar": ("Encontre qualquer pessoa", "na hora", "Busque por nome ou sobrenome"),
  "h-aspecto": ("Sua árvore,", "do seu jeito", "Escolha as cores, a disposição e os cartões"),
  "c-pdf": ("Compartilhe", "com toda a família", "Mande em PDF por mensagem ou e-mail: chega inteira"),
  "d-lamina": ("Imprima", "e dê de presente", "Em várias folhas ou num quadro pronto para emoldurar"),
  "i-cumple": ("Não esqueça", "nenhum aniversário", "Um aviso no celular, mesmo com o app fechado"),
 },
}

def fondo(w, h):
    """Como el fondo del árbol en el tema (verde muy claro en Jardín), con un halo más claro arriba y un poco de grano de papel."""
    img = Image.new("RGB", (w, h), K["fondo"])
    halo = Image.new("L", (w, h), 0)
    ImageDraw.Draw(halo).ellipse((-w * 0.3, -h * 0.45, w * 1.3, h * 0.55), fill=255)
    halo = halo.filter(ImageFilter.GaussianBlur(min(w, h) * 0.25))
    img.paste(Image.new("RGB", (w, h), K["halo"]), (0, 0), halo)
    grano = Image.effect_noise((w, h), 18).convert("L")
    return Image.blend(img, Image.merge("RGB", (grano, grano, grano)), 0.025)

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
    pon(t1, f, TINTA, y); y += f.size * 1.05
    pon(t2, f, VERDE, y); y += f.size * 1.35
    for l in partir(d, sub, fs, ancho):
        pon(l, fs, GRIS, y); y += fs.size * 1.25
    return y

def redondear(img, r):
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, img.width - 1, img.height - 1), r, fill=255)
    img.putalpha(m)
    return img

def aparato(img, pantalla, x, y, w, borde, radio):
    """La pantalla (ya a su tamaño final) dentro de un marco oscuro con una sombra suave y un brillo dorado detrás."""
    h = int(pantalla.height * w / pantalla.width)
    W, H = w + 2 * borde, h + 2 * borde
    capa = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dc = ImageDraw.Draw(capa)
    dc.rounded_rectangle((x + W * 0.02, y + H * 0.12, x + W * 0.98, y + H * 0.88), radio * 2, fill=ORO + (60,))
    dc.rounded_rectangle((x, y + borde * 2, x + W, y + H + borde * 2), radio, fill=K["sombra"] + (90,))
    capa = capa.filter(ImageFilter.GaussianBlur(borde * 4))
    img.paste(capa, (0, 0), capa)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((x, y, x + W, y + H), radio, fill=K["marco"], outline=K["borde_marco"], width=max(2, borde // 6))
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
    d.text((x + logo.width + alto * 0.3, y + alto * 0.02), "Ramo", font=f, fill=TINTA)

TABLAS = {"en": "Ingles", "fr": "Frances", "it": "Italiano", "de": "Aleman", "pt": "Portugues"}

def traducir(l, texto):
    """Un texto de la aplicación (o un nombre del ejemplo) en ese idioma, de las tablas de Core/Idiomas."""
    if l == "es": return texto
    cs = open(os.path.join(IDIOMAS, TABLAS[l] + ".cs"), encoding="utf-8").read()
    return re.search(r'\["' + re.escape(texto) + r'"\] = "([^"]*)"', cs).group(1)

AHORA = {"es": "ahora", "en": "now", "fr": "maintenant", "it": "ora", "de": "jetzt", "pt": "agora"}
# el día del aviso: el cumpleaños de Javier (12 de abril de 1985), en 2026, un domingo
FECHA = {"es": "domingo, 12 de abril", "en": "Sunday, April 12", "fr": "dimanche 12 avril", "it": "domenica 12 aprile",
         "de": "Sonntag, 12. April", "pt": "domingo, 12 de abril"}

def dibujar_aviso(img, l, k, x0, y0, ancho):
    """
    El aviso de cumpleaños de Android (como lo pone AvisosAndroid: título «Hoy, en tu familia» y una línea por fiesta):
    el cumpleaños de Javier y la boda de sus padres. k: píxeles por punto.
    """
    t = lambda x: traducir(l, x)
    javier = t("Javier") + " " + t("García López")
    padres = t("{0} y {1}").format(t("Antonio") + " " + t("García"), t("Mercedes") + " " + t("López"))
    lineas = [t("{0} cumple {1} años").format(javier, 41), t("{0} cumplen {1} años de casados").format(padres, 42)]
    pad = int(18 * k)
    d = ImageDraw.Draw(img)
    fp, ft, fl = fuente(APP, int(12.5 * k)), fuente(APP_NEGRITA, int(17 * k)), fuente(APP, int(15.5 * k))
    texto = []
    for li in lineas: texto += partir(d, li, fl, ancho - 2 * pad)
    alto = pad + int(26 * k) + int(26 * k) + len(texto) * int(21 * k) + pad
    sombra = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sombra).rounded_rectangle((x0, y0 + 4 * k, x0 + ancho, y0 + alto + 4 * k), 24 * k, fill=(0, 0, 0, 90))
    sombra = sombra.filter(ImageFilter.GaussianBlur(10 * k))
    img.paste(sombra, (0, 0), sombra)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((x0, y0, x0 + ancho, y0 + alto), 24 * k, fill=(250, 251, 248))
    # la cabecera: el icono de Ramo, «Ramo · ahora»
    logo = Image.open(LOGO).convert("RGBA")
    lado = int(16 * k)
    logo = logo.resize((int(logo.width * lado / logo.height), lado), Image.LANCZOS)
    y = y0 + pad
    img.paste(logo, (x0 + pad, y), logo)
    d.text((x0 + pad + logo.width + 6 * k, y - 1 * k), "Ramo  ·  " + AHORA[l], font=fp, fill=(100, 112, 104))
    y += int(26 * k)
    d.text((x0 + pad, y), t("Hoy, en tu familia"), font=ft, fill=(28, 32, 30))
    y += int(26 * k)
    for li in texto:
        d.text((x0 + pad, y), li, font=fl, fill=(60, 68, 63)); y += int(21 * k)

def pantalla_bloqueo(equipo, l):
    """
    La pantalla de bloqueo del teléfono (o de la tableta) con el aviso de cumpleaños de Ramo: así se ve que avisa aunque
    la aplicación esté cerrada. Fondo oscuro del color del tema, la hora, la fecha, un candado y el aviso.
    """
    w, h, k = (1080, 2160, 2.5) if equipo == "telefono" else (2560, 1440, 2)
    img = Image.new("RGB", (w, h))
    # un degradado oscuro (verde en Jardín, pardo en Álbum), con un halo más claro arriba
    arriba, abajo = K["bloqueo"]
    d = ImageDraw.Draw(img)
    for y in range(h):
        f = y / h
        d.line((0, y, w, y), fill=tuple(int(a + (b - a) * f) for a, b in zip(arriba, abajo)))
    halo = Image.new("L", (w, h), 0)
    ImageDraw.Draw(halo).ellipse((-w * 0.2, -h * 0.25, w * 1.2, h * 0.45), fill=70)
    halo = halo.filter(ImageFilter.GaussianBlur(min(w, h) * 0.2))
    img.paste(Image.new("RGB", (w, h), K["halo_bloqueo"]), (0, 0), halo)
    d = ImageDraw.Draw(img)
    blanco = (240, 245, 240)
    # el candado, arriba en el centro
    cx, cy, a = w // 2, int(30 * k), int(6 * k)
    d.rounded_rectangle((cx - a, cy, cx + a, cy + int(1.5 * a)), int(2 * k), fill=blanco)
    d.arc((cx - int(0.7 * a), cy - int(1.3 * a), cx + int(0.7 * a), cy + int(0.4 * a)), 180, 360, fill=blanco, width=int(1.8 * k))
    # la hora y la fecha
    # la hora con Barlow (la de Atkinson lleva el cero tachado)
    fh, ff = fuente(NORMAL, int(96 * k)), fuente(APP, int(17 * k))
    hora = "9:00"
    yh = int(70 * k) if equipo == "telefono" else int(70 * k)
    d.text(((w - d.textlength(hora, font=fh)) / 2, yh), hora, font=fh, fill=blanco)
    yf = yh + int(112 * k)
    fecha = FECHA[l]
    d.text(((w - d.textlength(fecha, font=ff)) / 2, yf), fecha, font=ff, fill=K["fecha"])
    # en el teléfono, el aviso algo más grande (como con la letra del sistema un poco aumentada)
    ka = k * 1.12 if equipo == "telefono" else k
    ancho = w - 2 * int(14 * k) if equipo == "telefono" else int(520 * k)
    dibujar_aviso(img, l, ka, (w - ancho) // 2, yf + int(52 * k), ancho)
    # abajo, los accesos de la pantalla de bloqueo: la linterna a la izquierda y la cámara a la derecha
    r, yb = int(26 * k), h - int(56 * k)
    for xb in (int(56 * k), w - int(56 * k)):
        d.ellipse((xb - r, yb - r, xb + r, yb + r), fill=K["boton_bloqueo"])
    xl = int(56 * k)
    d.rounded_rectangle((xl - 4 * k, yb - 10 * k, xl + 4 * k, yb + 10 * k), 2 * k, outline=blanco, width=int(1.8 * k))
    d.line((xl - 4 * k, yb - 3 * k, xl + 4 * k, yb - 3 * k), fill=blanco, width=int(1.5 * k))
    xc = w - int(56 * k)
    d.rounded_rectangle((xc - 10 * k, yb - 7 * k, xc + 10 * k, yb + 8 * k), 3 * k, outline=blanco, width=int(1.8 * k))
    d.ellipse((xc - 4 * k, yb - 3 * k, xc + 4 * k, yb + 5 * k), outline=blanco, width=int(1.8 * k))
    return img

def pdf_png(ruta, ancho):
    """La primera página de un PDF como imagen de ese ancho (con pdftoppm, de poppler)."""
    sal = os.path.join(tempfile.mkdtemp(prefix="ramo-pdf-"), "p")
    subprocess.run(["pdftoppm", "-png", "-singlefile", "-scale-to-x", str(ancho), "-scale-to-y", "-1", ruta, sal], check=True)
    return Image.open(sal + ".png").convert("RGB")

# la conversación en la que llega el PDF: con quién (la madre de Javier), lo que pregunta ella, lo que se dice y los textos de la aplicación de mensajes
CHARLA = {
 "es": ("Mamá", "¿Cómo va el árbol de la familia?", "¡Mira, ya está!", "¡Qué bonito! Salen todos, hasta los bisabuelos", "Lo imprimo para la abuela", "1 página", "Mensaje"),
 "en": ("Mom", "How's the family tree going?", "Look, it's done!", "It's lovely! Everyone's there, even the great-grandparents", "I'll print it for Grandma", "1 page", "Message"),
 "fr": ("Maman", "Ça avance, l'arbre de la famille ?", "Regarde, il est prêt !", "Qu'il est beau ! Tout le monde y est, même les arrière-grands-parents", "Je l'imprime pour Mamie", "1 page", "Message"),
 "it": ("Mamma", "Come va l'albero di famiglia?", "Guarda, è pronto!", "Che bello! Ci sono tutti, anche i bisnonni", "Lo stampo per la nonna", "1 pagina", "Messaggio"),
 "de": ("Mama", "Wie weit bist du mit dem Stammbaum?", "Schau, er ist fertig!", "Wie schön! Alle sind drauf, sogar die Urgroßeltern", "Ich drucke ihn für Oma aus", "1 Seite", "Nachricht"),
 "pt": ("Mãe", "Como está indo a árvore da família?", "Olha, está pronta!", "Que linda! Estão todos, até os bisavós", "Vou imprimir para a vovó", "1 página", "Mensagem"),
}

def pantalla_pdf(equipo, l):
    """
    Lo que recibe la familia: una conversación de una aplicación de mensajes cualquiera (sin marca) en la que se manda el
    PDF del árbol (crudas/telefono/<l>/pdf.pdf), con la página entera en miniatura, y la madre contesta.
    """
    w, h, k = (1080, 2160, 2.5) if equipo == "telefono" else (2560, 1440, 2)
    contacto, antes, mio, suyo1, suyo2, paginas, escribir = CHARLA[l]
    img = Image.new("RGB", (w, h), K["charla"])
    d = ImageDraw.Draw(img)
    tinta, gris = (30, 34, 32), (110, 120, 114)
    # las horas con Barlow (la de Atkinson lleva el cero tachado)
    fm, fn, fh = fuente(APP, int(16 * k)), fuente(APP_NEGRITA, int(17 * k)), fuente(NORMAL, int(12.5 * k))
    # la barra de arriba: volver, la foto del contacto (su inicial) y su nombre
    alto_barra = int(60 * k)
    d.rectangle((0, 0, w, alto_barra), fill=(250, 251, 249))
    d.line((0, alto_barra, w, alto_barra), fill=(215, 220, 214), width=max(1, int(k / 2)))
    cx, cy, a = int(22 * k), alto_barra // 2, int(7 * k)
    d.line((cx + a, cy - a, cx, cy, cx + a, cy + a), fill=tinta, width=int(2.2 * k), joint="curve")
    d.line((cx, cy, cx + 2 * a, cy), fill=tinta, width=int(2.2 * k))
    r = int(18 * k); ax = int(62 * k)
    d.ellipse((ax - r, cy - r, ax + r, cy + r), fill=(214, 168, 160))
    fi = fuente(APP_NEGRITA, int(17 * k))
    d.text((ax - d.textlength(contacto[0], font=fi) / 2, cy - fi.size * 0.62), contacto[0], font=fi, fill=(255, 255, 255))
    d.text((ax + r + int(12 * k), cy - fn.size * 0.62), contacto, font=fn, fill=tinta)
    # la barra de escribir, abajo
    alto_escribir = int(64 * k)
    yb = h - alto_escribir
    d.rounded_rectangle((int(12 * k), yb + int(10 * k), w - int(70 * k), h - int(10 * k)), int(22 * k), fill=(252, 253, 251))
    d.text((int(30 * k), yb + alto_escribir / 2 - fm.size * 0.6), escribir, font=fm, fill=(150, 158, 152))
    xe = w - int(38 * k); ye = yb + alto_escribir // 2
    d.ellipse((xe - int(22 * k), ye - int(22 * k), xe + int(22 * k), ye + int(22 * k)), fill=K["enviar"])
    d.polygon([(xe - 8 * k, ye - 9 * k), (xe + 10 * k, ye), (xe - 8 * k, ye + 9 * k), (xe - 4 * k, ye)], fill=(255, 255, 255))

    ancho_burbuja = int(w - 36 * k) if equipo == "telefono" else int(380 * k)
    margen, pad, rad = int(12 * k), int(10 * k), int(14 * k)
    def texto(t, mia, y, hora):
        lineas = partir(d, t, fm, ancho_burbuja - 2 * pad - int(40 * k))
        ancho_t = max(d.textlength(li, font=fm) for li in lineas)
        nonlocal_b[0] = int(min(ancho_burbuja, ancho_t + 2 * pad + int(44 * k)))
        alto = pad * 2 + len(lineas) * int(21 * k) + int(6 * k)
        x = w - margen - nonlocal_b[0] if mia else margen
        d.rounded_rectangle((x, y, x + nonlocal_b[0], y + alto), rad, fill=K["mia"] if mia else (255, 255, 255))
        yy = y + pad
        for li in lineas: d.text((x + pad, yy), li, font=fm, fill=tinta); yy += int(21 * k)
        d.text((x + nonlocal_b[0] - pad - d.textlength(hora, font=fh), y + alto - pad - fh.size), hora, font=fh, fill=gris)
        return y + alto + int(6 * k)
    nonlocal_b = [0]
    # el documento: la miniatura de la página entera y, debajo, el icono de PDF, el nombre y «1 página · PDF»
    pagina = pdf_png(os.path.join(PROMO, "crudas", "telefono", l, "pdf.pdf"), ancho_burbuja * 2)
    mini_w = ancho_burbuja - 2 * int(4 * k)
    mini = pagina.resize((mini_w, int(pagina.height * mini_w / pagina.width)), Image.LANCZOS)
    alto_pie = int(58 * k)
    alto_doc = int(4 * k) + mini.height + alto_pie
    hueco = yb - alto_barra
    # todo junto, abajo (como en una conversación de verdad): calcula el alto antes de dibujar
    alturas = [alto_doc + int(6 * k)]
    for t, mia in ((antes, False), (mio, True), (suyo1, False), (suyo2, False)):
        n = len(partir(d, t, fm, ancho_burbuja - 2 * pad - int(40 * k)))
        alturas.append(pad * 2 + n * int(21 * k) + int(6 * k) + int(6 * k))
    y = yb - int(14 * k) - sum(alturas) - int(10 * k)
    # lo que preguntó ella antes (arriba del todo, con la fecha encima)
    fd = fuente(APP, int(12 * k))
    hoy = {"es": "Hoy", "en": "Today", "fr": "Aujourd'hui", "it": "Oggi", "de": "Heute", "pt": "Hoje"}[l]
    tw = d.textlength(hoy, font=fd)
    d.rounded_rectangle((w / 2 - tw / 2 - 10 * k, y - 34 * k, w / 2 + tw / 2 + 10 * k, y - 12 * k), int(10 * k), fill=(250, 251, 249))
    d.text((w / 2 - tw / 2, y - 31 * k), hoy, font=fd, fill=gris)
    y = texto(antes, False, y, "19:52") + int(18 * k)
    x = w - margen - ancho_burbuja
    d.rounded_rectangle((x, y, x + ancho_burbuja, y + alto_doc), rad, fill=K["mia"])
    m = Image.new("L", mini.size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, mini.width - 1, mini.height - 1), int(10 * k), fill=255)
    img.paste(mini, (x + int(4 * k), y + int(4 * k)), m)
    yp = y + int(4 * k) + mini.height + int(10 * k)
    # el icono de PDF: una hoja roja con «PDF»
    ix = x + pad
    d.rounded_rectangle((ix, yp, ix + int(28 * k), yp + int(34 * k)), int(4 * k), fill=(214, 64, 52))
    fp = fuente(APP_NEGRITA, int(9 * k))
    d.text((ix + int(14 * k) - d.textlength("PDF", font=fp) / 2, yp + int(11 * k)), "PDF", font=fp, fill=(255, 255, 255))
    d.text((ix + int(38 * k), yp - int(1 * k)), traducir(l, "Familia García") + ".pdf", font=fuente(APP_NEGRITA, int(15 * k)), fill=tinta)
    d.text((ix + int(38 * k), yp + int(19 * k)), paginas + " · PDF · 180 kB", font=fh, fill=gris)
    d.text((x + ancho_burbuja - pad - d.textlength("20:14", font=fh), y + alto_doc - pad - fh.size), "20:14", font=fh, fill=gris)
    y += alto_doc + int(6 * k)
    y = texto(mio, True, y, "20:14") + int(6 * k)
    y = texto(suyo1, False, y, "20:21") + int(0 * k)
    texto(suyo2, False, y, "20:21")
    return img

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
    idiomas = [a for a in sys.argv[1:] if a != "--album"] or list(TEXTOS)
    for l in idiomas:
        for equipo, hacer in (("telefono", telefono), ("tablet", tableta)):
            sal = os.path.join(PROMO, equipo, l)
            os.makedirs(sal, exist_ok=True)
            for n, nombre in enumerate(PANTALLAS, 1):
                cruda = lambda n: Image.open(os.path.join(PROMO, "crudas", equipo, l, n + ".png"))
                # «h-aspecto» junta dos pantallas: compacto, en vertical y con foto; balanceado y solo con el nombre
                if nombre == "h-aspecto": crudo = (cruda(nombre + "-foto"), cruda(nombre + "-nombre"))
                elif nombre == "c-pdf": crudo = pantalla_pdf(equipo, l)
                elif nombre == "i-cumple": crudo = pantalla_bloqueo(equipo, l)
                else: crudo = cruda(nombre)
                hacer(l, nombre, crudo).save(os.path.join(sal, f"{n}.png"), optimize=True)
        print(l)
