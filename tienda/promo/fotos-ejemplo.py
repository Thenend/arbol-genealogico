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
    "Abuelo paterno": ("abuelo-paterno.jpg", 512, 430),
    "Abuela paterna": ("abuela-paterna.jpg", 512, 380),
    "Abuelo materno": ("abuelo-materno.jpg", 512, 440),
    "Abuela materna": ("abuela-materna.jpg", 530, 450),
    "Padre": ("padre.jpg", 512, 500),
    "Madre": ("madre.jpg", 512, 460),
    "Tío Juan": ("tio-juan.jpg", 512, 430),
    "Tía Marta": ("tia-marta.jpg", 512, 480),
    "Tía Rosa": ("tia-rosa.jpg", 512, 410),
    "Tío Pedro": ("tio-pedro.jpg", 512, 440),
    "Tía Elena": ("tia-elena.jpg", 512, 440),
    "Yo": ("yo.jpg", 512, 410),
    "Mi esposa": ("mi-esposa.jpg", 512, 460),
    "Hermana": ("hermana.jpg.jpg", 512, 440),
    "Prima Lucía": ("prima-lucia.jpg", 512, 490),
    "Hijo 1": ("hijo-1.jpg", 512, 440),
    "Hijo 2": ("hijo-2.jpg", 512, 460),
    "Hermano menor": ("joven antiguo A.jpg", 530, 410),
    "Bisabuelo": ("anciano moderno A.jpg", 512, 380),
}

def archivo(persona):
    return persona.lower().replace(" ", "-").replace("í", "i") + ".jpg"

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
