# Variante con el tema Álbum

Todo lo de la tienda para la versión de Ramo que empieza con el tema **Álbum** (sepia, con letra de libro) en vez de
Jardín, por si se quiere comparar las dos en Google Play. Es la misma aplicación compilada con `-p:TemaAlbum=true`:
empieza con Álbum y lo pone el primero de la lista de temas (ver ANDROID.md).

- `promo/telefono/<idioma>/1.png…8.png` (1080 × 1920) y `promo/tablet/<idioma>/1.png…8.png` (2560 × 1440): las capturas
  de Play, las mismas 8 y en el mismo orden que las de `tienda/promo`, pero con Álbum y el fondo y los colores de Álbum.
  En la 4.ª («Tu árbol, a tu manera») los otros dos aspectos llevan Jardín y Día.
- `destacada/<idioma>.png` (1024 × 500): la imagen destacada, el mismo diseño que `tienda/destacada` con colores de Álbum.
- `promo/crudas/`: las pantallas sin marco de las que salen.

Cómo se rehacen (igual que las de Jardín, con `--album`; en Linux, `capturar.py` con `xvfb-run`, ver tienda/promo/crudas/LEEME.md):

```
dotnet build src/ArbolGenealogico.Movil.Escritorio -c Release -p:TemaAlbum=true -o src/ArbolGenealogico.Movil.Escritorio/bin/Album
python tienda/promo/crudas/capturar.py --album
python tienda/promo/enmarcar.py --album
node tienda/destacada-fuente/generar.js --album
```

**Para la prueba A/B en Play.** Los «Experimentos de ficha de Play Store» comparan solo la ficha (capturas, imagen
destacada, icono, textos): a todos se les instala la misma aplicación. Con estas capturas y esta destacada se puede probar
qué ficha atrae más instalaciones. Para que además la aplicación instalada empiece con Álbum para la mitad, habría que
subir la variante (`-p:TemaAlbum=true`) en lugar de la normal, o hacer que la aplicación elija el tema al azar la primera vez.
