# Las capturas de tienda/capturas/<idioma>/ (1080 × 2160, las del teléfono sin marco): el árbol, «Mis árboles», recibir un
# árbol, fusionar y la ficha. Tres son pantallas en bruto de capturar.py (que hay que sacar antes); las otras dos se sacan aquí.
#   python tienda/promo/crudas/capturas-tienda.py [idioma…]
import os, shutil, subprocess, sys, tempfile

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
EXE = os.path.join(RAIZ, "src", "ArbolGenealogico.Movil.Escritorio", "bin", "Release", "net10.0",
                   "ArbolGenealogico.Movil.Escritorio" + (".exe" if os.name == "nt" else ""))
EJEMPLOS = os.path.join(RAIZ, "Ejemplos")

def captura(l, salida, pasos, completa=True):
    datos = tempfile.mkdtemp(prefix="ramo-")
    env = dict(os.environ, ARBOL_DATOS=datos, ARBOL_IDIOMA=l, ARBOL_ESCALA="2.5", ARBOL_LETRA="1")
    # sin la versión completa, «Mis árboles» lleva la estrella de «Ramo completo», como la aplicación recién instalada
    if completa: env["ARBOL_COMPLETA"] = "1"
    subprocess.run([EXE, "--captura", salida, "432", "864"] + pasos, env=env, timeout=120)
    shutil.rmtree(datos, ignore_errors=True)

if __name__ == "__main__":
    for l in sys.argv[1:] or ["es", "en", "fr", "it", "de", "pt"]:
        crudas, sal = os.path.join(AQUI, "telefono", l), os.path.join(RAIZ, "tienda", "capturas", l)
        os.makedirs(sal, exist_ok=True)
        for origen, destino in (("a-arbol", "1-arbol"), ("e-fusion", "4-fusion"), ("f-ficha", "5-ficha")):
            shutil.copyfile(os.path.join(crudas, origen + ".png"), os.path.join(sal, destino + ".png"))
        captura(l, os.path.join(sal, "2-mis-arboles.png"), ["ejemplo", "inicio"], completa=False)
        captura(l, os.path.join(sal, "3-recibir.png"), ["abrir:" + os.path.join(EJEMPLOS, "familia-perez.ramo"),
                                                         "recibir:" + os.path.join(EJEMPLOS, "familia-garcia.ramo")])
        print(l)
