// Genera tienda/destacada/<idioma>.png (1024 × 500) a partir de destacada.html, con Playwright y Chromium:
//   node tienda/destacada-fuente/generar.js [idiomas…]
// Sin Node, sale igual con Edge: msedge --headless=new --allow-file-access-from-files --force-device-scale-factor=1
//   --virtual-time-budget=3000 --window-size=1024,500 --screenshot=tienda\destacada\es.png "file:///…/destacada.html?v=A&l=es"
const path = require('path');
const { chromium } = require('playwright');
(async () => {
  const idiomas = process.argv.length > 2 ? process.argv.slice(2) : ['es', 'en', 'fr', 'it', 'de', 'pt'];
  const b = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  const p = await b.newPage({ viewport: { width: 1024, height: 500 }, deviceScaleFactor: 1 });
  for (const l of idiomas) {
    await p.goto('file://' + path.join(__dirname, 'destacada.html') + '?v=A&l=' + l);
    await p.evaluate(() => document.fonts.ready);
    await p.waitForTimeout(300);
    await p.screenshot({ path: path.join(__dirname, '..', 'destacada', l + '.png') });
    console.log(l);
  }
  await b.close();
})();
