# Fotos del árbol de ejemplo: recorta los retratos de tienda/promo/fotos (1024 × 1024) en un cuadrado centrado en la
# cara, los reduce a 256 px y los guarda en JPEG en Ejemplos/fotos/<persona>.jpg, de donde los toma GenerarEjemplos
# (tests/ArbolGenealogico.Tests/RenderDebugTests.cs) para meterlos en Ejemplos/familia-garcia.ramo.
#   python tienda/promo/fotos-ejemplo.py
import os
from PIL import Image

AQUI = os.path.dirname(os.path.abspath(__file__))
ORIGEN = os.path.join(AQUI, "fotos")
DESTINO = os.path.join(AQUI, "..", "..", "Ejemplos", "fotos")
LADO, CORTE = 256, 700  # el cuadrado que se recorta del original, en px de 1024

# persona del ejemplo → (archivo, centro de la cara en px de 1024)
FOTOS = {
    "Manuel": ("abuelo-paterno.jpg", 512, 430),
    "Dolores": ("abuela-paterna.jpg", 512, 380),
    "Francisco": ("abuelo-materno.jpg", 512, 440),
    "Josefa": ("abuela-materna.jpg", 530, 450),
    "Antonio": ("padre.jpg", 512, 500),
    "Mercedes": ("madre.jpg", 512, 460),
    "Juan": ("tio-juan.jpg", 512, 430),
    "Marta": ("tia-marta.jpg", 512, 480),
    "Rosa": ("tia-rosa.jpg", 512, 410),
    "Pedro": ("tio-pedro.jpg", 512, 440),
    "Elena": ("tia-elena.jpg", 512, 440),
    "Javier": ("yo.jpg", 512, 410),
    "Laura": ("mi-esposa.jpg", 512, 460),
    "Beatriz": ("hermana.jpg.jpg", 512, 440),
    "Lucía": ("prima-lucia.jpg", 512, 490),
    "Daniel": ("hijo-1.jpg", 512, 440),
    "Adrián": ("hijo-2.jpg", 512, 460),
    "Sergio": ("joven antiguo A.jpg", 530, 410),
    "Eusebio": ("anciano moderno A.jpg", 512, 380),
}

def archivo(persona):
    return persona.lower().translate(str.maketrans("áéíóú", "aeiou")) + ".jpg"

if __name__ == "__main__":
    os.makedirs(DESTINO, exist_ok=True)
    for persona, (nombre, cx, cy) in FOTOS.items():
        im = Image.open(os.path.join(ORIGEN, nombre)).convert("RGB")
        # un poco por debajo de la cara, para que entren los hombros; sin salirse de la foto
        x = min(max(cx - CORTE // 2, 0), im.width - CORTE)
        y = min(max(cy + 40 - CORTE // 2, 0), im.height - CORTE)
        im.crop((x, y, x + CORTE, y + CORTE)).resize((LADO, LADO), Image.LANCZOS) \
          .save(os.path.join(DESTINO, archivo(persona)), "JPEG", quality=80, optimize=True)
        print(persona, "<", nombre)
