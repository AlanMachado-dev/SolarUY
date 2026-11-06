const clave = "solaruy.asistente.req009";
let receptor, sesion, controlador, vencimiento, instancia, generacion = 0, esperaHasta = 0, pendiente = null;
let estado = inicial();
function inicial() { return { validada: false, disponible: false, minimizado: true, ocupado: false,
    reintentar: false, accesoRequerido: false, error: null, mensajes: [] }; }
async function publicar() { if (receptor) await receptor.invokeMethodAsync("ActualizarEstado", estado); }
function guardar() {
    if (!sesion) return;
    try { sessionStorage.setItem(clave, JSON.stringify({ marca: sesion.marca, mensajes: estado.mensajes,
        minimizado: estado.minimizado, pendiente, esperaHasta })); } catch { estado.error = "No se pudo guardar la conversación en esta pestaña."; }
}
function borrarAlmacenamiento() { try { sessionStorage.removeItem(clave); } catch { /* almacenamiento no disponible */ } }
async function invalidar(mensaje = "La sesión cambió o venció. Volvé a iniciar sesión.") {
    ++generacion; controlador?.abort(); clearTimeout(vencimiento); sesion = null; pendiente = null;
    esperaHasta = 0; borrarAlmacenamiento(); estado = { ...inicial(), validada: true, minimizado: false,
        accesoRequerido: true, error: mensaje }; await publicar();
}
async function validar(restaurar = false) {
    const identificador = instancia;
    try {
        const respuesta = await fetch("/api/asistente/sesion", { credentials: "same-origin", cache: "no-store", redirect: "error" });
        if (identificador !== instancia) return false;
        if (respuesta.status === 401 || respuesta.status === 403) { await invalidar(); return false; }
        if (!respuesta.ok) throw new Error();
        const actual = await respuesta.json();
        if (identificador !== instancia) return false;
        if (Date.parse(actual.vencimientoUtc) <= Date.now()) { await invalidar(); return false; }
        if (sesion && actual.marca !== sesion.marca) { await invalidar(); return false; }
        sesion = actual; estado.validada = true; estado.disponible = actual.disponible; estado.accesoRequerido = false;
        if (!actual.disponible) estado.error = actual.aviso;
        clearTimeout(vencimiento);
        vencimiento = setTimeout(() => invalidar(), Math.max(0, Date.parse(actual.vencimientoUtc) - Date.now()));
        if (restaurar) {
            try {
                const guardado = JSON.parse(sessionStorage.getItem(clave) || "null");
                if (guardado?.marca === actual.marca && Array.isArray(guardado.mensajes)) {
                    estado.mensajes = guardado.mensajes.filter(m => ["user", "assistant"].includes(m.rol) && typeof m.texto === "string");
                    estado.minimizado = !!guardado.minimizado;
                    pendiente = guardado.pendiente || null;
                    esperaHasta = Number.isFinite(guardado.esperaHasta) ? guardado.esperaHasta : 0;
                    if (pendiente) { estado.reintentar = true; estado.error = "La consulta se interrumpió. Podés reintentar el mensaje."; }
                } else borrarAlmacenamiento();
            } catch { borrarAlmacenamiento(); }
        }
        await publicar(); return true;
    } catch {
        if (identificador !== instancia) return false;
        // No mostrar historial guardado antes de validar la sesión.
        estado.error = "No se pudo comprobar la sesión. Recargá la página para reintentar.";
        if (restaurar) { estado.validada = true; estado.mensajes = []; estado.disponible = false; estado.minimizado = false; }
        await publicar(); return false;
    }
}
function contextoPagina() {
    const ruta = location.pathname;
    const patrones = [[/^\/parques-solares\/([0-9a-f-]{36})\/?$/i, "Parque"],
        [/^\/nodos\/([0-9a-f-]{36})\/?$/i, "Nodo"],
        [/^\/paneles\/([0-9a-f-]{36})\/monitoreo\/?$/i, "Panel"]];
    for (const [patron, tipo] of patrones) { const coincidencia = ruta.match(patron); if (coincidencia) return { tipo, id: coincidencia[1] }; }
    return null;
}
export async function iniciar(dotnet, identificador) {
    ++generacion; controlador?.abort(); clearTimeout(vencimiento);
    window.removeEventListener("focus", alFoco); document.removeEventListener("submit", alFormulario, true);
    instancia = identificador;
    receptor = dotnet; estado = inicial(); sesion = null; pendiente = null;
    window.addEventListener("focus", alFoco); document.addEventListener("submit", alFormulario, true);
    await validar(true);
}
function alFoco() { if (sesion && Date.parse(sesion.vencimientoUtc) <= Date.now()) invalidar(); else validar(); }
function alFormulario(evento) { if (evento.target?.action && new URL(evento.target.action).pathname === "/cuenta/cerrar-sesion") invalidar(); }
export async function alternar() { estado.minimizado = !estado.minimizado; guardar(); await publicar(); }
export async function nueva() {
    ++generacion; controlador?.abort(); pendiente = null; esperaHasta = 0;
    estado.mensajes = []; estado.ocupado = false; estado.reintentar = false; estado.error = sesion?.aviso || null;
    borrarAlmacenamiento(); guardar(); await publicar();
}
export async function enviar(texto) {
    if (estado.ocupado || !texto?.trim() || !estado.disponible) return;
    if (texto.length > 2000) { estado.error = "La pregunta admite hasta 2.000 caracteres."; await publicar(); return; }
    if (!await validar()) return;
    // Congelar contexto e historial al enviar; reintentar conserva ambos y no duplica el mensaje.
    pendiente = { pregunta: texto.trim(), contextoPagina: contextoPagina(), historial: estado.mensajes.slice(-29),
        historialRecortado: estado.mensajes.length > 29 };
    estado.mensajes.push({ rol: "user", texto: texto.trim() });
    guardar(); await ejecutar();
}
export async function reintentar() {
    if (!pendiente || estado.ocupado) return;
    if (Date.now() < esperaHasta) { estado.error = `Esperá ${Math.ceil((esperaHasta - Date.now()) / 1000)} segundos para reintentar.`; await publicar(); return; }
    if (await validar()) await ejecutar();
}
async function ejecutar() {
    const turno = ++generacion;
    const solicitud = { ...pendiente, marcaSesion: sesion.marca };
    controlador = new AbortController(); estado.ocupado = true; estado.reintentar = false; estado.error = null; await publicar();
    try {
        const respuesta = await fetch("/api/asistente/mensajes", { method: "POST", credentials: "same-origin", redirect: "error",
            headers: { "Content-Type": "application/json", [sesion.cabecera]: sesion.token },
            body: JSON.stringify(solicitud), signal: controlador.signal });
        if (turno !== generacion) return;
        if (respuesta.status === 401 || respuesta.status === 403) { await invalidar(); return; }
        if (!respuesta.ok) throw new Error();
        const resultado = await respuesta.json();
        if (turno !== generacion) return;
        if (resultado.codigo === "Exito" || resultado.codigo === "AclaracionNecesaria") {
            estado.mensajes.push({ rol: "assistant", texto: resultado.mensaje, referencias: resultado.referencias || [] });
            pendiente = null;
        } else {
            estado.error = resultado.mensaje; estado.reintentar = true;
            esperaHasta = Date.now() + (resultado.reintentarEnSegundos || 0) * 1000;
        }
    } catch {
        if (turno !== generacion) return;
        estado.error = "La consulta se interrumpió. Recargá o reintentá el mismo mensaje; las respuestas confirmadas se conservan.";
        estado.reintentar = true;
    } finally {
        if (turno === generacion) { estado.ocupado = false; guardar(); await publicar(); }
    }
}
export function cerrar(identificador) {
    if (identificador !== instancia) return;
    instancia = null;
    ++generacion; controlador?.abort(); clearTimeout(vencimiento); receptor = null;
    window.removeEventListener("focus", alFoco); document.removeEventListener("submit", alFormulario, true);
}
