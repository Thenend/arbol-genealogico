# Capturas crudas de la aplicación del móvil (la ventana de escritorio con --captura) para enmarcarlas como imágenes
# promocionales de Google Play. Uso: python tienda/promo/crudas/capturar.py [idioma…] [--solo a,b…] [--equipo telefono|tablet]
# Necesita compilada la ventana: dotnet build src/ArbolGenealogico.Movil.Escritorio -c Release
import json, os, shutil, subprocess, sys, tempfile
from concurrent.futures import ThreadPoolExecutor

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
EXE = os.path.join(RAIZ, "src", "ArbolGenealogico.Movil.Escritorio", "bin", "Release", "net10.0", "ArbolGenealogico.Movil.Escritorio.exe")
GARCIA = os.path.join(RAIZ, "Ejemplos", "familia-garcia.ramo")

# los nombres del ejemplo en cada idioma (TraducirEjemplo)
NOMBRES = {
    "es": {"padre": "Padre", "abuelo": "Abuelo paterno", "buscar": "Garc"},
    "en": {"padre": "Father", "abuelo": "Paternal grandfather", "buscar": "Smi"},
    "fr": {"padre": "Père", "abuelo": "Grand-père paternel", "buscar": "Mar"},
    "it": {"padre": "Padre", "abuelo": "Nonno paterno", "buscar": "Ros"},
    "de": {"padre": "Vater", "abuelo": "Großvater väterlicherseits", "buscar": "Mül"},
    "pt": {"padre": "Pai", "abuelo": "Avô paterno", "buscar": "Sil"},
}
ANA = {"es": "Ana", "en": "Anna", "fr": "Anne", "it": "Anna", "de": "Anna", "pt": "Ana"}

# lo que el «árbol de Ana» tiene de más: una nieta, la pareja de «Hijo 1» la fecha entera de nacimiento de «Madre» y otro año para «Padre»
NUEVOS = {"es": ("Lucía", "Sara", "Ortega"), "en": ("Lucy", "Sarah", "Brown"), "fr": ("Lucie", "Sarah", "Dubois"),
          "it": ("Lucia", "Sara", "Bianchi"), "de": ("Lena", "Sarah", "Becker"), "pt": ("Luísa", "Sara", "Costa")}

def variante(l):
    """El árbol de García con dos personas más y un dato más preciso, para la pantalla de fusionar."""
    d = json.load(open(GARCIA, encoding="utf-8"))
    nieta, pareja, apellido = NUEVOS[l]
    d["personas"] += [{"id": "a0000000000000000000000000000001", "nombre": nieta, "apellidos": "García", "sexo": "F", "nacimiento": "2024", "historia": ""},
                      {"id": "a0000000000000000000000000000002", "nombre": pareja, "apellidos": apellido, "sexo": "F", "nacimiento": "2004", "historia": ""}]
    for q in d["personas"]:
        if q["nombre"] == "Madre": q["nacimiento"] = "1958-05-12"
        if q["nombre"] == "Padre": q["nacimiento"] = "1956"  # un dato distinto: pregunta cuál se queda
    for u in d["uniones"]:
        if u["id"] == "7a138ef8c934477daca17677d0bed4c2":
            u["parejas"].append("a0000000000000000000000000000002"); u["hijos"].append("a0000000000000000000000000000001")
    ruta = os.path.join(tempfile.mkdtemp(prefix="ramo-ana-"), "familia-garcia.ramo")
    json.dump(d, open(ruta, "w", encoding="utf-8"), ensure_ascii=False)
    return ruta

EQUIPOS = {"telefono": ("432", "864", "2.5"), "tablet": ("1280", "720", "2")}

def pasos(l, equipo):
    n = NOMBRES[l]
    t = equipo == "tablet"
    abrir = "abrir:" + GARCIA
    # en la tableta, el árbol algo más grande y subido para que se vean los hijos debajo de «Padre»
    z = "zoom:1.1|mover:0,-60" if t else "zoom:0.85"
    return {
        "a-arbol": [abrir, "sel:" + n["padre"], *z.split("|")],
        "b-familiar": [abrir, "sel:" + n["padre"], *z.split("|"), "familiar:" + n["padre"]],
        "c-compartir": [abrir, "sel:" + n["padre"], *z.split("|"), "menu"],
        "d-imprimir": [abrir, "imprimir", "esperar:800", "imprimir:hojas=2", "esperar:800"],
        "e-fusion": [abrir, "fusionar:" + variante(l) + "|" + ANA[l], "esperar:800"],
        "f-ficha": [abrir, "ficha:" + n["abuelo"]],
        "g-buscar": [abrir, "buscar:" + n["buscar"]] + (["zoom:1.2", "mover:0,-180"] if t else []),
    }

def capturar(l, equipo, nombre, lista):
    ancho, alto, escala = EQUIPOS[equipo]
    carpeta = os.path.join(RAIZ, "tienda", "promo", "crudas", equipo, l)
    os.makedirs(carpeta, exist_ok=True)
    datos = tempfile.mkdtemp(prefix="ramo-")
    env = dict(os.environ, ARBOL_DATOS=datos, ARBOL_IDIOMA=l, ARBOL_ESCALA=escala, ARBOL_LETRA="1", ARBOL_COMPLETA="1")
    salida = os.path.join(carpeta, nombre + ".png")
    extra = []
    if nombre == "a-arbol" and equipo == "telefono":
        extra = ["pdf:" + os.path.join(carpeta, "pdf.pdf"), "imagen:" + os.path.join(carpeta, "imagen.png")]
    subprocess.run([EXE, "--captura", salida, ancho, alto] + lista + extra, env=env, timeout=120)
    shutil.rmtree(datos, ignore_errors=True)
    if os.path.exists(salida + ".error.txt"): print("ERROR", salida, open(salida + ".error.txt").read()[:300])
    else: print("ok", equipo, l, nombre)

if __name__ == "__main__":
    args = sys.argv[1:]
    solo = None; equipos = list(EQUIPOS)
    if "--solo" in args: i = args.index("--solo"); solo = args[i + 1].split(","); del args[i:i + 2]
    if "--equipo" in args: i = args.index("--equipo"); equipos = [args[i + 1]]; del args[i:i + 2]
    idiomas = args or list(NOMBRES)
    trabajos = [(l, e, k, v) for l in idiomas for e in equipos for k, v in pasos(l, e).items() if not solo or k[0] in solo]
    with ThreadPoolExecutor(4) as ex: list(ex.map(lambda t: capturar(*t), trabajos))
