// Genera las capturas de Google Play con titular a partir de promo.html, con Playwright y Chromium:
//   node tienda/promo/generar.js [tel|tab] [idiomas…]   →   tienda/promo/<telefono|tablet>/<idioma>/<n>.png
const path = require('path'), fs = require('fs');
const { chromium } = require('playwright');
(async () => {
  const a = process.argv.slice(2);
  const disp = a[0] === 'tel' || a[0] === 'tab' ? [a.shift()] : ['tel', 'tab'];
  const idiomas = a.length ? a : ['es', 'en', 'fr', 'it', 'de', 'pt'];
  const b = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  for (const d of disp) {
    const p = await b.newPage({ viewport: d === 'tel' ? { width: 1080, height: 1920 } : { width: 1920, height: 1080 } });
    for (const l of idiomas)
      for (let n = 1; n <= 6; n++) {
        const dir = path.join(__dirname, d === 'tel' ? 'telefono' : 'tablet', l);
        fs.mkdirSync(dir, { recursive: true });
        await p.goto('file://' + path.join(__dirname, 'promo.html') + `?d=${d}&n=${n}&l=${l}`);
        await p.evaluate(() => document.fonts.ready);
        await p.waitForTimeout(250);
        await p.screenshot({ path: path.join(dir, n + '.png') });
      }
    await p.close();
  }
  await b.close();
})();
