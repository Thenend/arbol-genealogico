// El lienzo del árbol: mover (arrastrar, rueda, dos dedos), zoom, toques sobre las tarjetas, fotos arrastradas o pegadas,
// encoger los nombres que no caben, y descargar (JSON, imagen) e imprimir.

let v = null;   // { vp, mundo, dotnet, x, y, z, ancho, alto }

const ZMIN = 0.05, ZMAX = 3;

function aplicar(animar, duracion) {
    if (!v) return;
    v.mundo.style.transition = animar ? `transform ${duracion || ".35s"} cubic-bezier(.2,.7,.2,1)` : "none";
    v.mundo.style.transform = `translate(${v.x}px, ${v.y}px) scale(${v.z})`;
    v.vp.style.setProperty("--zoom", v.z);
    clearTimeout(v.tAviso);
    v.tAviso = setTimeout(() => v && v.clave && guardarPreferencia(v.clave, JSON.stringify([v.x, v.y, v.z])), 300);
}

function zoomEn(px, py, factor, animar) {
    const z = Math.min(ZMAX, Math.max(ZMIN, v.z * factor));
    const k = z / v.z;
    v.x = px - (px - v.x) * k;
    v.y = py - (py - v.y) * k;
    v.z = z;
    aplicar(animar);
}

function rel(e) {
    const r = v.vp.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
}

function enCampo(el) {
    return el && (el.closest("input, textarea, select, [contenteditable=true], .dialogo, .menu, .panel"));
}

export function crear(vp, mundo, dotnet, claveVista) {
    destruir();
    v = { vp, mundo, dotnet, clave: claveVista, x: 0, y: 0, z: 1, ancho: 1, alto: 1, punteros: new Map(), quitar: [] };
    const on = (el, ev, f, o) => { el.addEventListener(ev, f, o); v.quitar.push(() => el.removeEventListener(ev, f, o)); };

    let arrastre = null;     // { x0, y0, vx, vy, movido }
    let pinza = null;        // { d0, z0, cx, cy, vx, vy }
    let ultimoToque = { id: null, t: 0 };
    let largo = null;

    on(vp, "wheel", e => {
        e.preventDefault();
        const p = rel(e);
        const rueda = e.deltaMode !== 0 || (e.deltaX === 0 && Math.abs(e.deltaY) >= 40);
        if (e.ctrlKey || rueda) {
            // rueda del ratón (o pellizco en el trackpad, que llega con Ctrl): zoom alrededor del puntero
            const d = e.deltaMode === 1 ? e.deltaY * 33 : e.deltaY;
            zoomEn(p.x, p.y, Math.exp(-Math.max(-150, Math.min(150, d)) * (e.ctrlKey ? 0.01 : 0.0018)), false);
        } else {
            // trackpad con dos dedos: mover
            v.x -= e.deltaX; v.y -= e.deltaY; aplicar(false);
        }
    }, { passive: false });

    on(vp, "pointerdown", e => {
        if (e.button !== 0 && e.pointerType === "mouse") return;
        if (enCampo(e.target)) return;
        vp.setPointerCapture?.(e.pointerId);
        v.punteros.set(e.pointerId, rel(e));
        if (v.punteros.size === 1) {
            const p = rel(e);
            arrastre = { x0: p.x, y0: p.y, vx: v.x, vy: v.y, movido: false, objetivo: e.target };
            clearTimeout(largo);
            const tarjeta = e.target.closest("[data-id]");
            if (tarjeta && e.pointerType !== "mouse") {
                largo = setTimeout(() => {
                    if (arrastre && !arrastre.movido) {
                        arrastre.movido = true;   // ya no es un toque
                        navigator.vibrate?.(15);
                        v.dotnet.invokeMethodAsync("Menu", tarjeta.dataset.id, e.clientX, e.clientY);
                    }
                }, 550);
            }
        } else if (v.punteros.size === 2) {
            clearTimeout(largo);
            const [a, b] = [...v.punteros.values()];
            pinza = { d0: Math.hypot(a.x - b.x, a.y - b.y) || 1, z0: v.z, cx: (a.x + b.x) / 2, cy: (a.y + b.y) / 2, vx: v.x, vy: v.y };
            if (arrastre) arrastre.movido = true;
        }
    });

    on(vp, "pointermove", e => {
        if (!v.punteros.has(e.pointerId)) return;
        const p = rel(e);
        v.punteros.set(e.pointerId, p);
        if (pinza && v.punteros.size >= 2) {
            const [a, b] = [...v.punteros.values()];
            const d = Math.hypot(a.x - b.x, a.y - b.y);
            const cx = (a.x + b.x) / 2, cy = (a.y + b.y) / 2;
            const z = Math.min(ZMAX, Math.max(ZMIN, pinza.z0 * d / pinza.d0));
            const k = z / pinza.z0;
            v.x = cx - (pinza.cx - pinza.vx) * k;
            v.y = cy - (pinza.cy - pinza.vy) * k;
            v.z = z;
            aplicar(false);
        } else if (arrastre) {
            const dx = p.x - arrastre.x0, dy = p.y - arrastre.y0;
            if (!arrastre.movido && Math.hypot(dx, dy) > (e.pointerType === "mouse" ? 4 : 9)) { arrastre.movido = true; clearTimeout(largo); vp.classList.add("moviendo"); }
            if (arrastre.movido) { v.x = arrastre.vx + dx; v.y = arrastre.vy + dy; aplicar(false); }
        }
    });

    const fin = e => {
        if (!v.punteros.has(e.pointerId)) return;
        v.punteros.delete(e.pointerId);
        clearTimeout(largo);
        vp.classList.remove("moviendo");
        if (v.punteros.size < 2) pinza = null;
        if (v.punteros.size > 0) {
            // queda un dedo: sigue moviendo desde donde está
            const p = [...v.punteros.values()][0];
            arrastre = { x0: p.x, y0: p.y, vx: v.x, vy: v.y, movido: true };
            return;
        }
        const a = arrastre; arrastre = null;
        if (!a || a.movido || e.type === "pointercancel") return;
        const objetivo = a.objetivo;
        const accion = objetivo.closest("[data-accion]");
        const tarjeta = objetivo.closest("[data-id]");
        // el «+» aparece al seleccionar, justo bajo el dedo: un segundo toque rápido ahí sigue siendo un doble toque
        const rapido = tarjeta && ultimoToque.id === tarjeta.dataset.id && performance.now() - ultimoToque.t < 380;
        if (accion && tarjeta && !(rapido && accion.dataset.accion === "mas")) {
            const r = accion.getBoundingClientRect();
            v.dotnet.invokeMethodAsync(accion.dataset.accion === "mas" ? "Menu" : "Enlace", tarjeta.dataset.id, r.left + r.width / 2, r.bottom);
            return;
        }
        if (!tarjeta) { v.dotnet.invokeMethodAsync("Fondo"); return; }
        const id = tarjeta.dataset.id, ahora = performance.now();
        if (ultimoToque.id === id && ahora - ultimoToque.t < 380) {
            ultimoToque = { id: null, t: 0 };
            v.dotnet.invokeMethodAsync("DobleToque", id);
        } else {
            ultimoToque = { id, t: ahora };
            v.dotnet.invokeMethodAsync("Tocar", id);
        }
    };
    on(vp, "pointerup", fin);
    on(vp, "pointercancel", fin);

    on(vp, "contextmenu", e => {
        const tarjeta = e.target.closest("[data-id]");
        e.preventDefault();
        if (tarjeta) v.dotnet.invokeMethodAsync("Menu", tarjeta.dataset.id, e.clientX, e.clientY);
    });

    // Fotos arrastradas sobre una tarjeta (desde el ordenador o desde otra página).
    let sobre = null;
    const marcar = t => { if (sobre === t) return; sobre?.classList.remove("destino"); sobre = t; sobre?.classList.add("destino"); };
    on(vp, "dragover", e => {
        const t = e.target.closest("[data-id]");
        if (!t || !v.editable) return;
        e.preventDefault(); e.dataTransfer.dropEffect = "copy"; marcar(t);
    });
    on(vp, "dragleave", e => { if (!vp.contains(e.relatedTarget)) marcar(null); });
    on(vp, "drop", async e => {
        const t = e.target.closest("[data-id]");
        marcar(null);
        if (!t || !v.editable) return;
        e.preventDefault();
        const foto = await fotoDe(e.dataTransfer);
        if (foto) v.dotnet.invokeMethodAsync("FotoSoltada", t.dataset.id, foto);
        else v.dotnet.invokeMethodAsync("Aviso", "No se ha podido leer esa imagen.");
    });

    // Teclado (no cuando se escribe en un campo o hay un diálogo abierto: eso lo decide la parte de C#).
    on(document, "keydown", e => {
        if (enCampo(document.activeElement) && e.key !== "Escape") return;
        if (e.key === "+" && (e.ctrlKey || e.metaKey) || e.key === "=" && (e.ctrlKey || e.metaKey)) { e.preventDefault(); zoomCentro(1.25); return; }
        if (e.key === "-" && (e.ctrlKey || e.metaKey)) { e.preventDefault(); zoomCentro(0.8); return; }
        const usada = v.dotnet.invokeMethod("Tecla", e.key, e.ctrlKey || e.metaKey, e.shiftKey, e.altKey);
        if (usada) e.preventDefault();
    });

    on(document, "paste", async e => {
        if (enCampo(document.activeElement) || !v.editable) return;
        const foto = await fotoDe(e.clipboardData);
        if (foto) { e.preventDefault(); v.dotnet.invokeMethodAsync("FotoPegada", foto); }
    });

    const ro = new ResizeObserver(() => v && v.dotnet.invokeMethodAsync("Redimensionado"));
    ro.observe(vp);
    v.quitar.push(() => ro.disconnect());
    document.fonts?.ready.then(() => ajustarTextos());
}

export function destruir() {
    if (!v) return;
    v.quitar.forEach(f => f());
    v = null;
}

export function editable(si) { if (v) v.editable = si; }

export function tamano(ancho, alto) { if (v) { v.ancho = ancho; v.alto = alto; } }

function zoomCentro(f) { const r = v.vp.getBoundingClientRect(); zoomEn(r.width / 2, r.height / 2, f, true); }
export function zoom(f) { if (v) zoomCentro(f); }

/** Ver un rectángulo del mundo (x, y, ancho, alto) entero, sin pasar de zoomMax. */
export function verZona(x, y, w, h, zoomMax, animar) {
    if (!v) return;
    const r = v.vp.getBoundingClientRect();
    const m = 40;
    const z = Math.min(zoomMax, Math.max(ZMIN, Math.min((r.width - 2 * m) / Math.max(w, 1), (r.height - 2 * m) / Math.max(h, 1))));
    v.z = z;
    v.x = r.width / 2 - (x + w / 2) * z;
    v.y = r.height / 2 - (y + h / 2) * z;
    aplicar(animar);
}

/** Lleva el punto del mundo (x, y) al centro, con al menos zoomMin; si ya se ve con margen, no se mueve. */
export function centrarEn(x, y, w, h, zoomMin, siFuera, animar) {
    if (!v) return;
    const r = v.vp.getBoundingClientRect();
    if (siFuera) {
        const sx = x * v.z + v.x, sy = y * v.z + v.y;
        const m = 60;
        if (sx > m && sy > m && sx + w * v.z < r.width - m && sy + h * v.z < r.height - m && v.z >= zoomMin * 0.6) return;
    }
    v.z = Math.max(v.z, zoomMin);
    v.x = r.width / 2 - (x + w / 2) * v.z;
    v.y = r.height / 2 - (y + h / 2) * v.z;
    aplicar(animar);
}

/** El punto del mundo que está en el centro de la pantalla. */
export function centro() {
    if (!v) return [0, 0];
    const r = v.vp.getBoundingClientRect();
    return [(r.width / 2 - v.x) / v.z, (r.height / 2 - v.y) / v.z];
}

export function vista() { return v ? [v.x, v.y, v.z] : [0, 0, 1]; }
export function ponerVista(x, y, z) { if (!v) return; v.x = x; v.y = y; v.z = z; aplicar(false); }
/** Mueve la vista (en píxeles de pantalla). Animado, va a la vez que las tarjetas a su sitio nuevo (misma duración y curva). */
export function desplazar(dx, dy, animar) {
    if (!v) return;
    v.x += dx; v.y += dy;
    aplicar(animar, ".38s");
}

/** La vista guardada de este árbol ([x, y, zoom]) o null. */
export function vistaGuardada() {
    try { const s = v && v.clave && leerPreferencia(v.clave); return s ? JSON.parse(s) : null; } catch { return null; }
}

let avisoSalida = false;
window.addEventListener("beforeunload", e => { if (avisoSalida) { e.preventDefault(); e.returnValue = ""; } });
export function avisarSalida(si) { avisoSalida = si; }

export async function copiar(texto) {
    try { await navigator.clipboard.writeText(texto); return true; } catch { return false; }
}

/** Encoge lo justo los nombres que no caben en su tarjeta (no se cortan). */
export function ajustarTextos() {
    if (!v) return;
    for (const caja of v.mundo.querySelectorAll(".ajusta")) {
        const t = caja.firstElementChild;
        if (!t) continue;
        t.style.transform = "";
        const kx = caja.clientWidth / Math.max(1, t.scrollWidth);
        const ky = caja.clientHeight / Math.max(1, t.scrollHeight);
        const k = Math.min(1, kx, caja.classList.contains("alto") ? ky : 1);
        if (k < 0.999) t.style.transform = `scale(${k})`;
    }
}

// ---------- Fotos ----------

async function fotoDe(dt) {
    if (!dt) return null;
    for (const it of dt.items || []) {
        if (it.kind === "file" && it.type.startsWith("image/")) return await reducir(it.getAsFile());
    }
    for (const f of dt.files || []) if (f.type.startsWith("image/")) return await reducir(f);
    const url = dt.getData && (dt.getData("text/uri-list") || dt.getData("text/plain"));
    if (url && /^https?:|^data:image/.test(url.trim())) {
        try { const r = await fetch(url.trim()); return await reducir(await r.blob()); } catch { return null; }
    }
    return null;
}

/** Una imagen reducida a 320 px como máximo, en JPEG y base64 (sin el «data:...»), con la orientación de las fotos de móvil. */
async function reducir(blob) {
    try {
        const img = await createImageBitmap(blob, { imageOrientation: "from-image" });
        const k = Math.min(1, 320 / Math.max(img.width, img.height));
        const c = document.createElement("canvas");
        c.width = Math.max(1, Math.round(img.width * k)); c.height = Math.max(1, Math.round(img.height * k));
        const g = c.getContext("2d");
        g.fillStyle = "#fff"; g.fillRect(0, 0, c.width, c.height);
        g.imageSmoothingQuality = "high";
        g.drawImage(img, 0, 0, c.width, c.height);
        return c.toDataURL("image/jpeg", 0.86).split(",")[1];
    } catch { return null; }
}

export async function elegirFoto() {
    return await new Promise(ok => {
        const i = document.createElement("input");
        i.type = "file"; i.accept = "image/*";
        i.onchange = async () => ok(i.files[0] ? await reducir(i.files[0]) : null);
        i.oncancel = () => ok(null);
        i.click();
    });
}

// ---------- Archivos ----------

export function descargar(nombre, texto, tipo) {
    const url = URL.createObjectURL(new Blob([texto], { type: tipo || "application/json" }));
    const a = document.createElement("a");
    a.href = url; a.download = nombre;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 5000);
}

/** Abre el selector de archivos y devuelve [{nombre, texto}] de los .json elegidos. */
export async function elegirJson() {
    return await new Promise(ok => {
        const i = document.createElement("input");
        i.type = "file"; i.accept = ".json,application/json"; i.multiple = true;
        i.onchange = async () => ok(await Promise.all([...i.files].map(async f => ({ nombre: f.name, texto: await f.text() }))));
        i.oncancel = () => ok([]);
        i.click();
    });
}

async function cargarHtmlToImage() {
    if (window.htmlToImage) return;
    await new Promise((ok, mal) => {
        const s = document.createElement("script");
        s.src = "https://cdn.jsdelivr.net/npm/html-to-image@1.11.11/dist/html-to-image.js";
        s.onload = ok; s.onerror = () => mal(new Error("No se pudo cargar el generador de imágenes"));
        document.head.appendChild(s);
    });
}

/** Guarda todo el árbol como imagen PNG o JPG. */
export async function exportarImagen(nombre, escala, formato, fondo) {
    if (!v) return "No hay árbol";
    await cargarHtmlToImage();
    const pixeles = v.ancho * v.alto * escala * escala;
    if (pixeles > 100e6) return "La imagen sería demasiado grande: elige un tamaño menor.";
    const opciones = {
        width: v.ancho, height: v.alto, pixelRatio: escala, backgroundColor: fondo || undefined, cacheBust: false,
        style: { transform: "none", transition: "none" },
        filter: n => !(n.classList && (n.classList.contains("mas") || n.classList.contains("no-imagen"))),
    };
    v.mundo.classList.add("exportando");
    try {
        const url = formato === "jpg"
            ? await window.htmlToImage.toJpeg(v.mundo, { ...opciones, quality: 0.92, backgroundColor: fondo || "#0e1016" })
            : await window.htmlToImage.toPng(v.mundo, opciones);
        const a = document.createElement("a");
        a.href = url; a.download = nombre;
        document.body.appendChild(a); a.click(); a.remove();
        return null;
    } catch (e) {
        return "No se pudo crear la imagen: " + (e.message || e);
    } finally {
        v.mundo.classList.remove("exportando");
    }
}

/** Imprime el árbol entero en una hoja (en el tamaño y orientación que se elijan en el diálogo de impresión). */
export function imprimir(apaisado) {
    if (!v) return;
    const estilo = document.createElement("style");
    // a 96 px por pulgada, un A4 con márgenes de 1 cm deja unos 718 × 1047 px
    const [W, H] = apaisado ? [1047, 718] : [718, 1047];
    const k = Math.min(W / v.ancho, H / v.alto, 1);
    estilo.textContent = `@page { size: A4 ${apaisado ? "landscape" : "portrait"}; margin: 10mm; }
        @media print { .mundo { transform: scale(${k}) !important; } }`;
    document.head.appendChild(estilo);
    document.body.classList.add("imprimiendo");
    const limpiar = () => { document.body.classList.remove("imprimiendo"); estilo.remove(); window.removeEventListener("afterprint", limpiar); };
    window.addEventListener("afterprint", limpiar);
    setTimeout(() => { window.print(); setTimeout(limpiar, 1000); }, 50);
}

// ---------- Varios ----------

export function guardarPreferencia(clave, valor) { try { localStorage.setItem("arbol-genealogico." + clave, valor); } catch { } }
export function leerPreferencia(clave) { try { return localStorage.getItem("arbol-genealogico." + clave); } catch { return null; } }
export function enfocar(sel) { setTimeout(() => { const e = document.querySelector(sel); e?.focus(); e?.select?.(); }, 30); }
export function tamanoPantalla() { return [window.innerWidth, window.innerHeight]; }
