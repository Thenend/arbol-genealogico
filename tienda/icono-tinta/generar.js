// Icono de «Ramo», estilo «papel y tinta»: el mismo ramo de tienda/ramo.cs (las tarjetas son las flores, los tallos las
// líneas del árbol), pero dibujado con contorno de tinta a mano sobre papel crema y atado con un lazo rojo, para que no
// tenga el aire de los iconos planos sobre fondo oscuro. Se dibuja en SVG (lienzo de 512) y se pasa a PNG con Playwright:
//   node tienda/icono-tinta/generar.js            → Android, escritorio, web y .ico (los de la aplicación)
//   node tienda/icono-tinta/generar.js --tienda   → además tienda/icono-play-512.png y tienda/web/icono.png
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const Tinta = '#2B2622', Papel = '#F2E8D6', Lazo = '#C0463A', Hoja = '#8FA876';
// las flores: centro, giro y color (como en ramo.cs)
const flores = [[256, 118, 0, '#D9735B'], [164, 160, -12, '#7C9CC4'], [348, 160, 12, '#E2B24A'], [98, 236, -24, '#A98BB0'], [414, 236, 24, '#8FA876']];
const w = 92, h = 66, nudo = [256, 362];

function ramo(mono = false) {
  const C = c => mono ? '#fff' : c;
  const raya = (x1, y1, x2, y2) => `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${C(Tinta)}" stroke-width="7" stroke-linecap="round"/>`;
  const borde = mono ? '' : `stroke="${Tinta}" stroke-width="5" stroke-linejoin="round"`;
  let s = '';
  // tallos hasta el nudo y los que asoman por debajo
  for (const [x, y] of flores) s += raya(x, y + h / 2 - 6, ...nudo);
  for (const [x, y] of [[256, 452], [222, 444], [290, 444]]) s += raya(...nudo, x, y);
  // dos hojas junto a los tallos
  for (const l of [-1, 1]) s += `<path transform="translate(${256 + l * 46},322) rotate(${l * -38})" d="M0,-30 Q20,0 0,30 Q-20,0 0,-30Z" fill="${C(Hoja)}" ${borde}/>`;
  // el lazo: dos lazadas, dos cintas y el nudo
  s += `<path d="M256,362 C228,330 196,336 200,360 C204,384 236,378 256,362Z" fill="${C(Lazo)}" ${borde}/>`
    + `<path d="M256,362 C284,330 316,336 312,360 C308,384 276,378 256,362Z" fill="${C(Lazo)}" ${borde}/>`
    + `<path d="M252,364 L228,414 L240,410 L246,422 L258,366Z" fill="${C(Lazo)}" ${borde}/>`
    + `<path d="M260,364 L284,414 L272,410 L266,422 L254,366Z" fill="${C(Lazo)}" ${borde}/>`
    + `<circle cx="256" cy="362" r="10" fill="${C(Lazo)}" ${borde}/>`;
  // las flores, encima: las tarjetas con su contorno de tinta
  for (const [x, y, g, col] of flores)
    s += `<rect transform="translate(${x},${y}) rotate(${g})" x="${-w / 2}" y="${-h / 2}" width="${w}" height="${h}" rx="14" fill="${C(col)}" ${mono ? '' : `stroke="${Tinta}" stroke-width="6"`}/>`;
  // el temblor de la mano
  return `<g filter="url(#mano)">${s}</g>`;
}

const defs = `<filter id="mano"><feTurbulence type="fractalNoise" baseFrequency="0.035" numOctaves="2" seed="7"/>`
  + `<feDisplacementMap in="SourceGraphic" scale="4" xChannelSelector="R" yChannelSelector="G"/></filter>`
  + `<filter id="grano" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="3" seed="4"/>`
  + `<feColorMatrix values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 0.3 -0.1"/><feComposite in2="SourceGraphic" operator="in"/></filter>`;
// el ramo centrado en (256, 256) con la escala pedida (su centro en el lienzo de 512 es (256, 268))
const colocado = (s, mono) => `<g transform="translate(256,256) scale(${s}) translate(-256,-268)">${ramo(mono)}</g>`;
const svg = cuerpo => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512"><defs>${defs}</defs>${cuerpo}</svg>`;
const papel = `<rect width="512" height="512" fill="${Papel}"/>`, grano = `<rect width="512" height="512" filter="url(#grano)"/>`;

const dibujos = {
  // a sangre (Play): papel entero
  sangre: svg(papel + colocado(0.86) + grano),
  // con marco (aplicación, web, .ico y Android antiguo): cuadrado redondeado de papel con el borde de tinta
  marco: svg(`<clipPath id="caja"><rect x="22" y="22" width="468" height="468" rx="108"/></clipPath>`
    + `<g clip-path="url(#caja)">${papel}${grano}</g>`
    + `<rect x="24" y="24" width="464" height="464" rx="106" fill="none" stroke="${Tinta}" stroke-width="7" filter="url(#mano)"/>`
    + colocado(0.82)),
  // adaptativo de Android: primer plano transparente con el ramo en la zona segura (66 de 108), y monocromo aparte
  frente: svg(colocado(0.56 * 512 / 432)),
  mono: svg(colocado(0.56 * 512 / 432, true)),
};

function ico(pngs, ruta) {
  // .ico de Windows con varios tamaños (PNG dentro)
  const cab = Buffer.alloc(6 + 16 * pngs.length);
  cab.writeUInt16LE(0, 0); cab.writeUInt16LE(1, 2); cab.writeUInt16LE(pngs.length, 4);
  let desp = cab.length;
  pngs.forEach(([t, d], i) => {
    const o = 6 + 16 * i;
    cab.writeUInt8(t >= 256 ? 0 : t, o); cab.writeUInt8(t >= 256 ? 0 : t, o + 1);
    cab.writeUInt16LE(1, o + 4); cab.writeUInt16LE(32, o + 6); cab.writeUInt32LE(d.length, o + 8); cab.writeUInt32LE(desp, o + 12);
    desp += d.length;
  });
  fs.writeFileSync(ruta, Buffer.concat([cab, ...pngs.map(p => p[1])]));
}

(async () => {
  const raiz = path.join(__dirname, '..', '..');
  const src = (...p) => path.join(raiz, 'src', ...p);
  const res = src('ArbolGenealogico.Movil.Android', 'Resources');
  const b = await chromium.launch();
  const pagina = await b.newPage({ viewport: { width: 512, height: 512 } });
  async function png(nombre, lado) {
    await pagina.setViewportSize({ width: lado, height: lado });
    // en pequeño el grano del papel solo ensucia
    const dibujo = lado < 128 ? dibujos[nombre].replace(grano, '') : dibujos[nombre];
    await pagina.setContent(`<body style="margin:0;background:transparent">${dibujo.replace('width="512" height="512"', `width="${lado}" height="${lado}"`)}</body>`);
    return pagina.screenshot({ omitBackground: true });
  }
  const guardar = (ruta, datos) => { fs.mkdirSync(path.dirname(ruta), { recursive: true }); fs.writeFileSync(ruta, datos); console.log(path.relative(raiz, ruta)); };

  guardar(path.join(res, 'mipmap-xxxhdpi', 'icono_frente.png'), await png('frente', 432));
  guardar(path.join(res, 'mipmap-xxxhdpi', 'icono_mono.png'), await png('mono', 432));
  for (const [carpeta, lado] of [['mipmap-mdpi', 48], ['mipmap-hdpi', 72], ['mipmap-xhdpi', 96], ['mipmap-xxhdpi', 144], ['mipmap-xxxhdpi', 192]])
    guardar(path.join(res, carpeta, 'icono.png'), await png('marco', lado));
  const grande = await png('marco', 512);
  for (const r of [src('ArbolGenealogico.App', 'Recursos', 'icono.png'), src('ArbolGenealogico.Movil', 'Recursos', 'icono.png'), src('ArbolGenealogico.Web', 'wwwroot', 'icono.png')])
    guardar(r, grande);
  const pngs = [];
  for (const t of [16, 24, 32, 48, 64, 128, 256]) pngs.push([t, await png('marco', t)]);
  ico(pngs, src('ArbolGenealogico.App', 'Recursos', 'icono.ico')); console.log('src/ArbolGenealogico.App/Recursos/icono.ico');
  if (process.argv.includes('--tienda')) {
    const play = await png('sangre', 512);
    guardar(path.join(raiz, 'tienda', 'icono-play-512.png'), play);
    guardar(path.join(raiz, 'tienda', 'web', 'icono.png'), play);
  }
  await b.close();
})();
