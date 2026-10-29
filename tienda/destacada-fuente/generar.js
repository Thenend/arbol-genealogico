// Genera tienda/destacada/<idioma>.png (1024 × 500) a partir de destacada.html, con Playwright y Chromium:
//   node tienda/destacada-fuente/generar.js [--album] [idiomas…]
// Con --album, la de la variante con el tema Álbum, en tienda/variante-album/destacada/ (antes hay que sacar sus capturas
// crudas: ver tienda/variante-album/LEEME.md).
// Sin Node, sale igual con Edge: msedge --headless=new --allow-file-access-from-files --force-device-scale-factor=1
//   --virtual-time-budget=3000 --window-size=1024,500 --screenshot=tienda\destacada\es.png "file:///…/destacada.html?v=A&l=es"
//   (para la de Álbum, …&tema=album y --screenshot=tienda\variante-album\destacada\es.png)
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
(async () => {
  const args = process.argv.slice(2);
  const album = args.includes('--album');
  const idiomas = args.filter(a => a !== '--album');
  if (!idiomas.length) idiomas.push('es', 'en', 'fr', 'it', 'de', 'pt');
  const salida = path.join(__dirname, '..', ...(album ? ['variante-album', 'destacada'] : ['destacada']));
  fs.mkdirSync(salida, { recursive: true });
  const b = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  const p = await b.newPage({ viewport: { width: 1024, height: 500 }, deviceScaleFactor: 1 });
  for (const l of idiomas) {
    await p.goto('file://' + path.join(__dirname, 'destacada.html') + '?v=A&l=' + l + (album ? '&tema=album' : ''));
    await p.evaluate(() => document.fonts.ready);
    await p.waitForTimeout(300);
    await p.screenshot({ path: path.join(salida, l + '.png') });
    console.log(l);
  }
  await b.close();
})();
