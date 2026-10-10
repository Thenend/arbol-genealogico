// Acceso a los datos: Supabase (cuentas, árboles compartidos y tiempo real) o, si no está configurado, el propio navegador
// (modo de prueba, sin cuentas ni compartir). Los árboles van y vienen como texto JSON, igual que los archivos de escritorio.

let sb = null;          // cliente de Supabase
let local = false;      // modo de prueba en el navegador
let canal = null;       // suscripción de tiempo real al árbol abierto

const CLAVE_LOCAL = "arbol-genealogico.local.arboles";

function error(e) {
    if (!e) return null;
    const m = e.message || String(e);
    if (/Invalid login credentials/i.test(m)) return "Correo o contraseña incorrectos.";
    if (/Email not confirmed/i.test(m)) return "Falta confirmar el correo: revisa tu bandeja de entrada (y la de spam).";
    if (/User already registered/i.test(m)) return "Ya hay una cuenta con ese correo: entra con tu contraseña.";
    if (/Password should be at least/i.test(m)) return "La contraseña debe tener al menos 6 caracteres.";
    if (/rate limit/i.test(m)) return "Se han enviado demasiados correos seguidos. Espera un rato y vuelve a probar.";
    if (/Could not find the (function|table)|schema cache|relation "public\.\w+" does not exist/i.test(m))
        return "La base de datos de Supabase aún no está preparada: falta ejecutar entero el archivo supabase/esquema.sql en su «SQL Editor» (ver WEB.md, paso 1).";
    if (/Failed to fetch|NetworkError|Load failed/i.test(m)) return "Sin conexión con el servidor. Comprueba tu conexión a Internet.";
    return m;
}

async function cargarSupabase() {
    if (window.supabase) return;
    await new Promise((ok, mal) => {
        const s = document.createElement("script");
        s.src = "https://cdn.jsdelivr.net/npm/@supabase/supabase-js@2.45.4/dist/umd/supabase.min.js";
        s.onload = ok; s.onerror = () => mal(new Error("No se pudo cargar Supabase"));
        document.head.appendChild(s);
    });
}

export async function iniciar(url, clave) {
    url = (url || "").trim(); clave = (clave || "").trim();
    if (!url || !clave) { local = true; return "local"; }
    // Solo vale la raíz del proyecto (https://xxxx.supabase.co): si se copió con «/rest/v1/» u otra ruta detrás, se quita.
    try { url = new URL(url.includes("://") ? url : "https://" + url).origin; } catch { }
    await cargarSupabase();
    sb = window.supabase.createClient(url, clave, { auth: { persistSession: true, autoRefreshToken: true, detectSessionInUrl: true } });
    return "supabase";
}

export function esLocal() { return local; }

// ---------- Cuentas ----------

export async function usuario() {
    if (local) return { id: "local", email: "" };
    const { data } = await sb.auth.getSession();
    const u = data?.session?.user;
    return u ? { id: u.id, email: u.email || "" } : null;
}

export function alCambiarSesion(dotnet) {
    if (local) return;
    sb.auth.onAuthStateChange((evento, sesion) => {
        const u = sesion?.user;
        dotnet.invokeMethodAsync("SesionCambiada", evento, u ? u.id : null, u ? (u.email || "") : null);
    });
}

function redireccion() { return location.origin + location.pathname.replace(/[^/]*$/, ""); }

export async function entrar(email, clave) {
    const { error: e } = await sb.auth.signInWithPassword({ email, password: clave });
    return error(e);
}

export async function registrar(email, clave) {
    const { data, error: e } = await sb.auth.signUp({ email, password: clave, options: { emailRedirectTo: redireccion() } });
    if (e) return error(e);
    // Si el proyecto pide confirmar el correo, no hay sesión hasta que se pulse el enlace.
    return data.session ? null : "confirmar";
}

export async function enlaceMagico(email) {
    const { error: e } = await sb.auth.signInWithOtp({ email, options: { emailRedirectTo: redireccion() } });
    return error(e);
}

export async function recuperar(email) {
    const { error: e } = await sb.auth.resetPasswordForEmail(email, { redirectTo: redireccion() });
    return error(e);
}

export async function cambiarClave(clave) {
    const { error: e } = await sb.auth.updateUser({ password: clave });
    return error(e);
}

export async function salir() { if (!local) await sb.auth.signOut(); }

// ---------- Árboles ----------

function leerLocal() {
    try { return JSON.parse(localStorage.getItem(CLAVE_LOCAL) || "{}"); } catch { return {}; }
}
function escribirLocal(todo) {
    try { localStorage.setItem(CLAVE_LOCAL, JSON.stringify(todo)); return null; }
    catch { return "El navegador no tiene sitio para guardar más (las fotos ocupan mucho). Configura Supabase para guardar en la nube."; }
}

export async function aceptarInvitaciones() {
    if (local) return 0;
    const { data } = await sb.rpc("aceptar_invitaciones");
    return data || 0;
}

/** Lista de árboles del usuario: [{id, nombre, rol, personas, actualizado, actualizadoPor, propietarioEmail}] */
export async function listar() {
    if (local) {
        return Object.values(leerLocal()).map(a => ({
            id: a.id, nombre: a.nombre, rol: "propietario", personas: (JSON.parse(a.datos).personas || []).length,
            actualizado: a.actualizado, actualizadoPor: "", propietarioEmail: "",
        })).sort((x, y) => (y.actualizado || "").localeCompare(x.actualizado || ""));
    }
    const { data, error: e } = await sb.rpc("mis_arboles");
    if (e) throw new Error(error(e));
    return data.map(a => ({
        id: a.id, nombre: a.nombre, rol: a.rol, personas: a.personas, actualizado: a.actualizado,
        actualizadoPor: a.actualizado_por || "", propietarioEmail: a.propietario_email || "",
    }));
}

/** {id, nombre, datos (texto JSON), version, rol} o null si no existe o no hay acceso. */
export async function cargar(id) {
    if (local) {
        const a = leerLocal()[id];
        return a ? { id, nombre: a.nombre, datos: a.datos, version: a.version, rol: "propietario" } : null;
    }
    const { data, error: e } = await sb.from("arboles").select("id,nombre,datos,version").eq("id", id).maybeSingle();
    if (e) throw new Error(error(e));
    if (!data) return null;
    const { data: rol } = await sb.rpc("rol_en", { p_arbol: id });
    return { id: data.id, nombre: data.nombre, datos: JSON.stringify(data.datos), version: data.version, rol: rol || "lector" };
}

export async function version(id) {
    if (local) return leerLocal()[id]?.version ?? 0;
    const { data } = await sb.from("arboles").select("version,actualizado_por").eq("id", id).maybeSingle();
    return data ? data.version : 0;
}

export async function crear(nombre, datos) {
    if (local) {
        const todo = leerLocal();
        const id = crypto.randomUUID();
        todo[id] = { id, nombre, datos, version: 1, actualizado: new Date().toISOString() };
        const e = escribirLocal(todo);
        if (e) throw new Error(e);
        return id;
    }
    const { data, error: e } = await sb.from("arboles").insert({ nombre, datos: JSON.parse(datos) }).select("id").single();
    if (e) throw new Error(error(e));
    return data.id;
}

/** Devuelve la versión nueva, o -1 si otra persona guardó antes. */
export async function guardar(id, nombre, datos, ver) {
    if (local) {
        const todo = leerLocal();
        const a = todo[id];
        if (!a) throw new Error("El árbol ya no existe.");
        if (ver != null && a.version !== ver) return -1;
        todo[id] = { ...a, nombre, datos, version: a.version + 1, actualizado: new Date().toISOString() };
        const e = escribirLocal(todo);
        if (e) throw new Error(e);
        return a.version + 1;
    }
    const { data, error: e } = await sb.rpc("guardar_arbol", { p_id: id, p_nombre: nombre, p_datos: JSON.parse(datos), p_version: ver });
    if (e) throw new Error(error(e));
    return data;
}

export async function borrar(id) {
    if (local) { const todo = leerLocal(); delete todo[id]; escribirLocal(todo); return; }
    const { error: e } = await sb.from("arboles").delete().eq("id", id);
    if (e) throw new Error(error(e));
}

export async function salirDeArbol(id) {
    const u = await usuario();
    const { error: e } = await sb.from("miembros").delete().eq("arbol_id", id).eq("usuario", u.id);
    if (e) throw new Error(error(e));
}

// ---------- Compartir ----------

/** [{usuario, email, rol, pendiente}] */
export async function miembros(id) {
    if (local) return [];
    const [m, i] = await Promise.all([
        sb.from("miembros").select("usuario,email,rol").eq("arbol_id", id),
        sb.from("invitaciones").select("email,rol").eq("arbol_id", id),
    ]);
    if (m.error) throw new Error(error(m.error));
    const orden = { propietario: 0, editor: 1, lector: 2 };
    const res = m.data.map(x => ({ usuario: x.usuario, email: x.email, rol: x.rol, pendiente: false }))
        .concat((i.data || []).map(x => ({ usuario: "", email: x.email, rol: x.rol, pendiente: true })));
    return res.sort((a, b) => (orden[a.rol] - orden[b.rol]) || a.email.localeCompare(b.email));
}

export async function invitar(id, email, rol) {
    const { error: e } = await sb.from("invitaciones").upsert({ arbol_id: id, email: email.trim().toLowerCase(), rol });
    return error(e);
}

export async function cambiarRol(id, usuario, email, rol, pendiente) {
    const r = pendiente
        ? await sb.from("invitaciones").update({ rol }).eq("arbol_id", id).eq("email", email)
        : await sb.from("miembros").update({ rol }).eq("arbol_id", id).eq("usuario", usuario);
    return error(r.error);
}

export async function quitar(id, usuario, email, pendiente) {
    const r = pendiente
        ? await sb.from("invitaciones").delete().eq("arbol_id", id).eq("email", email)
        : await sb.from("miembros").delete().eq("arbol_id", id).eq("usuario", usuario);
    return error(r.error);
}

export async function copiarMiembros(origen, destino) {
    if (local) return 0;
    const { data, error: e } = await sb.rpc("copiar_miembros", { p_origen: origen, p_destino: destino });
    if (e) throw new Error(error(e));
    return data;
}

// ---------- Tiempo real ----------

export function suscribir(id, dotnet) {
    desuscribir();
    if (local) return;
    canal = sb.channel("arbol-" + id)
        .on("postgres_changes", { event: "UPDATE", schema: "public", table: "arboles", filter: "id=eq." + id },
            p => dotnet.invokeMethodAsync("CambioRemoto", p.new?.version ?? 0, p.new?.actualizado_por ?? ""))
        .subscribe();
}

export function desuscribir() {
    if (canal) { sb.removeChannel(canal); canal = null; }
}

// ---------- Familia y amigos (directorio) ----------

export async function entrarPerfil(nombre) {
    if (local) return;
    await sb.rpc("entrar_perfil", { p_nombre: nombre ?? null });
}

/** Mi nombre visible en el directorio ("" si aún no lo he puesto). */
export async function miNombre() {
    if (local) return "";
    const u = await usuario();
    const { data } = await sb.from("perfiles").select("nombre").eq("usuario", u.id).maybeSingle();
    return data?.nombre || "";
}

/** [{usuario, email, nombre, visto, arboles: [{id, nombre, personas, rol, solicitado}]}] */
export async function directorio() {
    if (local) return [];
    const { data, error: e } = await sb.rpc("directorio");
    if (e) throw new Error(error(e));
    return data.map(p => ({ usuario: p.usuario, email: p.email, nombre: p.nombre || "", visto: p.visto, arboles: p.arboles || [] }));
}

export async function pedirAcceso(arbol, mensaje) {
    const { error: e } = await sb.rpc("pedir_acceso", { p_arbol: arbol, p_mensaje: mensaje || "" });
    return error(e);
}

export async function cancelarSolicitud(arbol) {
    const u = await usuario();
    const { error: e } = await sb.from("solicitudes").delete().eq("arbol_id", arbol).eq("usuario", u.id);
    return error(e);
}

/** Peticiones de acceso a mis árboles: [{arbolId, arbol, usuario, email, nombre, mensaje, creada}] */
export async function misSolicitudes() {
    if (local) return [];
    const { data, error: e } = await sb.rpc("mis_solicitudes");
    if (e) throw new Error(error(e));
    return data.map(s => ({ arbolId: s.arbol_id, arbol: s.arbol, usuario: s.usuario, email: s.email, nombre: s.nombre || "", mensaje: s.mensaje || "", creada: s.creada }));
}

export async function rechazarSolicitud(arbol, usuarioId) {
    const { error: e } = await sb.from("solicitudes").delete().eq("arbol_id", arbol).eq("usuario", usuarioId);
    return error(e);
}

export async function compartirCon(arbol, usuarioId, rol) {
    const { error: e } = await sb.rpc("compartir_con", { p_arbol: arbol, p_usuario: usuarioId, p_rol: rol });
    return error(e);
}

export async function esVisible(arbol) {
    const { data } = await sb.from("arboles").select("visible").eq("id", arbol).maybeSingle();
    return data ? data.visible !== false : true;
}

export async function ponerVisible(arbol, visible) {
    const { error: e } = await sb.rpc("poner_visible", { p_arbol: arbol, p_visible: visible });
    return error(e);
}
