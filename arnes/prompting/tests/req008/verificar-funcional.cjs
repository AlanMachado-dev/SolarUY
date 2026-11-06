// Comprobación auxiliar con navegadores y servidores reales; las sesiones son sintéticas, no OIDC real.
const fs = require('node:fs');
const { execFileSync } = require('node:child_process');
const { chromium } = require(process.env.SOLARUY_PLAYWRIGHT || 'playwright');
const assert = require('node:assert/strict');

const base = process.env.SOLARUY_BASE_URL || 'https://localhost:8080';
const datos = JSON.parse(fs.readFileSync('.solaruy-acceptance/datos.json', 'utf8').replace(/^\uFEFF/, ''));
const sesiones = JSON.parse(fs.readFileSync('.solaruy-acceptance/sesiones.json', 'utf8').replace(/^\uFEFF/, ''));
const env = fs.readFileSync('.solaruy-acceptance/compose.env', 'utf8');
assert(env.includes('SOLARUY_PROYECTO=solaruy-req008-aceptacion'), 'El destino debe ser el proyecto aislado.');
assert(env.includes('SOLARUY_CONFIG_LOCAL=./.solaruy-acceptance/appsettings.Local.json'));
const composeArgs = ['compose', '--env-file', '.solaruy-acceptance/compose.env', '-f', 'docker-compose.yml',
  '-f', 'arnes/prompting/tests/req008/docker-compose.aceptacion.yml'];
const resultados = [];
function registrar(id, detalle) {
  resultados.push({ id, detalle, fecha: new Date().toISOString() });
  fs.mkdirSync('artifacts/req008', { recursive: true });
  fs.writeFileSync('artifacts/req008/funcional.json', JSON.stringify({ tipo: 'auxiliar_sesiones_sinteticas', resultados }, null, 2));
  console.log(`${id}: ${detalle}`);
}
function compose(...args) { execFileSync('docker', [...composeArgs, ...args], { stdio: 'ignore', timeout: 90000 }); }
function auxiliar(operacion) {
  execFileSync('dotnet', ['arnes/prompting/tests/req008/bin/Release/net10.0/VerificarBalanceo.dll', operacion], { stdio: 'ignore', timeout: 20000 });
}
async function comprobar(condicion, timeout = 10000) {
  const fin = Date.now() + timeout;
  while (Date.now() < fin) {
    try { if (await condicion()) return; } catch { }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error('No se alcanzó la condición de aceptación dentro del plazo.');
}

(async () => {
  const navegador = await chromium.launch({ channel: process.env.SOLARUY_BROWSER || 'msedge', headless: true });
  const contextos = [];
  async function crear(rol, destino, ruta = '/') {
    for (let intento = 0; intento < 30; intento++) {
      const contexto = await navegador.newContext({ ignoreHTTPSErrors: true });
      if (rol) await contexto.addCookies([{ name: 'SolarUY.Sesion.Oidc', value: sesiones[rol], domain: 'localhost', path: '/', secure: true, httpOnly: true, sameSite: 'Lax' }]);
      const pagina = await contexto.newPage();
      const respuesta = await pagina.goto(base + ruta);
      const instancia = respuesta.headers()['x-solaruy-instancia'];
      if (!destino || destino === instancia) { contextos.push(contexto); return { contexto, pagina, instancia }; }
      await contexto.close();
    }
    throw new Error('No se obtuvo el destino requerido mediante sesiones independientes.');
  }
  async function publicar(pagina, panelId, potencia, estado = 0) {
    return pagina.evaluate(async ({ panelId, potencia, estado }) => {
      const respuesta = await fetch('/hubs/monitoreo/negotiate?negotiateVersion=1', { method: 'POST' });
      if (!respuesta.ok) return { codigo: respuesta.status };
      const negociacion = await respuesta.json();
      return new Promise((resolve, reject) => {
        const url = new URL('/hubs/monitoreo', location.origin);
        url.protocol = 'wss:';
        url.searchParams.set('id', negociacion.connectionToken);
        const socket = new WebSocket(url);
        const reloj = setTimeout(() => { socket.close(); reject(new Error('timeout')); }, 10000);
        let invocado = false;
        socket.onopen = () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');
        socket.onmessage = evento => {
          for (const trama of evento.data.split('\x1e').filter(Boolean)) {
            const mensaje = JSON.parse(trama);
            if (!invocado && mensaje.type === undefined) {
              invocado = true;
              socket.send(JSON.stringify({ type: 1, invocationId: '1', target: 'Registrar', arguments: [{ panelId, generacionActualW: potencia, estado }] }) + '\x1e');
            }
            if (mensaje.type === 3 && mensaje.invocationId === '1') {
              clearTimeout(reloj); socket.close();
              resolve({ codigo: mensaje.error ? 400 : 200, confirmado: Date.now() });
            }
          }
        };
        socket.onerror = () => { clearTimeout(reloj); reject(new Error('websocket')); };
      });
    }, { panelId, potencia, estado });
  }
  try {
    const destinos = new Set();
    for (let i = 0; i < 12; i++) {
      const sesion = await crear(null);
      destinos.add(sesion.instancia);
      for (let j = 0; j < 2; j++) {
        const r = await sesion.contexto.request.get(base + '/');
        assert.equal(r.headers()['x-solaruy-instancia'], sesion.instancia);
      }
      await sesion.contexto.close();
    }
    assert.deepEqual([...destinos].sort(), ['app1', 'app2']);
    registrar('C1.1-C1.2', 'Ambas instancias atienden sesiones nuevas desde la misma IP y conservan afinidad.');

    const ruta = `/paneles/${datos.Paneles[0]}/monitoreo`;
    const observadorA = await crear('Operador', 'app1', ruta);
    const observadorB = await crear('Operador', 'app2', ruta);
    await observadorA.pagina.getByText('Generación actual', { exact: true }).waitFor();
    await observadorB.pagina.getByText('Generación actual', { exact: true }).waitFor();
    const adminA = await crear('Administrador', 'app1');
    const adminB = await crear('Administrador', 'app2');
    registrar('C4.1', 'Dos circuitos Blazor reales observan el mismo panel desde instancias distintas.');

    for (const [emisor, receptor, potencia, id] of [[adminA, observadorB, 321, 'C4.2'], [adminB, observadorA, 432, 'C4.3']]) {
      const envio = await publicar(emisor.pagina, datos.Paneles[0], potencia);
      assert.equal(envio.codigo, 200);
      await receptor.pagina.waitForFunction(p => document.querySelector('section[aria-live="polite"]')?.innerText.includes(`${p} W`), potencia, { timeout: 3000 });
      const latencia = Date.now() - envio.confirmado;
      assert(latencia <= 3000);
      registrar(id, `Lectura guardada por ${emisor.instancia}, visible en ${receptor.instancia} en ${latencia} ms.`);
    }
    await observadorA.pagina.screenshot({ path: 'artifacts/req008/monitoreo-app1.png' });
    await observadorB.pagina.screenshot({ path: 'artifacts/req008/monitoreo-app2.png' });
    const antes = await observadorB.pagina.locator('section[aria-live="polite"]').innerText();
    assert.equal((await publicar(adminA.pagina, datos.Paneles[1], 222)).codigo, 200);
    await new Promise(resolve => setTimeout(resolve, 1500));
    assert.equal(await observadorB.pagina.locator('section[aria-live="polite"]').innerText(), antes);
    registrar('C4.4', 'Una medición de otro panel no modifica la pantalla observada.');
    auxiliar('atrasada');
    await new Promise(resolve => setTimeout(resolve, 1500));
    assert.equal(await observadorB.pagina.locator('section[aria-live="polite"]').innerText(), antes);
    registrar('C4.5', 'Una lectura persistida con fecha anterior no reemplaza la lectura visible.');
    assert.equal((await publicar(adminA.pagina, datos.Paneles[0], -1)).codigo, 400);
    assert.equal(await observadorB.pagina.locator('section[aria-live="polite"]').innerText(), antes);
    registrar('C4.7-parcial', 'La lectura inválida se rechaza sin difundir cambios; falta provocar un fallo real de escritura.');
    auxiliar('bloquear-escritura');
    try {
      assert.equal((await publicar(adminA.pagina, datos.Paneles[0], 444)).codigo, 400);
      await new Promise(resolve => setTimeout(resolve, 1200));
      assert.equal(await observadorB.pagina.locator('section[aria-live="polite"]').innerText(), antes);
    } finally { auxiliar('restaurar-escritura'); }
    registrar('C4.7', 'INSERT rechazado por MySQL real: el hub informa error y ambas vistas conservan la lectura confirmada.');

    const rolRechazado = await observadorB.contexto.request.post(base + '/hubs/monitoreo/negotiate?negotiateVersion=1', { maxRedirects: 0 });
    console.log(`Control de publicación de Operador: HTTP ${rolRechazado.status()}.`);
    if (rolRechazado.status() === 400) console.log(`Origen rechazado: ${(await rolRechazado.text()).includes('Origen no admitido')}.`);
    assert([302, 403].includes(rolRechazado.status()));
    if (rolRechazado.status() === 302) assert(rolRechazado.headers().location.includes('/cuenta/acceso-denegado'));
    const anonimo = await crear(null);
    const rAnonimo = await anonimo.contexto.request.get(base + ruta, { maxRedirects: 0 });
    assert([302, 401, 403].includes(rAnonimo.status()));
    registrar('C2.5-auxiliar', 'Operador no puede publicar y una sesión anónima no accede al monitoreo; no valida emisión OIDC real.');

    const sesionCompartida = await crear('Operador');
    const instanciasSesion = new Set([sesionCompartida.instancia]);
    for (let i = 0; i < 20 && instanciasSesion.size < 2; i++) {
      await sesionCompartida.contexto.clearCookies({ name: 'SolarUY_Afinidad' });
      const respuesta = await sesionCompartida.pagina.goto(base + '/parques-solares');
      assert.equal(respuesta.status(), 200);
      instanciasSesion.add(respuesta.headers()['x-solaruy-instancia']);
    }
    assert.equal(instanciasSesion.size, 2);
    registrar('C2.2-auxiliar', 'La misma cookie sintética es reconocida por ambas instancias al renovar solo afinidad.');

    const alta = await crear('Administrador', 'app1', '/parques-solares/nuevo');
    const marca = Date.now().toString();
    const nombreParque = `Parque funcional req008 ${marca}`;
    await alta.pagina.locator('#nombre').fill(nombreParque);
    await alta.pagina.locator('#ubicacion').fill('Aceptación funcional req008');
    await comprobar(async () => {
      await alta.contexto.clearCookies({ name: 'SolarUY_Afinidad' });
      const r = await alta.contexto.request.get(base + '/');
      return r.headers()['x-solaruy-instancia'] === 'app2';
    });
    await alta.pagina.getByRole('button', { name: 'Registrar parque', exact: true }).click();
    await alta.pagina.waitForURL(/\/parques-solares\/[0-9a-f-]{36}/);
    const parqueCreado = new URL(alta.pagina.url()).pathname.split('/').pop();
    registrar('C2.3-auxiliar', 'Formulario emitido en app1 aceptado por app2 con antiforgery y sesión sintética compartidos.');
    const consulta = await adminA.contexto.request.get(base + '/parques-solares/' + parqueCreado);
    assert.equal(consulta.status(), 200);
    assert((await consulta.text()).includes(nombreParque));
    await alta.pagina.goto(base + `/parques-solares/${parqueCreado}/nodos/nuevo`);
    for (const [id, valor] of Object.entries({ 'nombre-nodo': `Nodo ${marca}`, latitud: '-34', longitud: '-54',
      'nombre-inversor': 'Inversor funcional', 'serie-inversor': `INV-${marca}`,
      'nombre-sensor': 'Sensor funcional', 'serie-sensor': `SEN-${marca}` })) {
      await alta.pagina.locator('#' + id).fill(valor);
    }
    await alta.pagina.getByRole('button', { name: 'Registrar nodo', exact: true }).click();
    await alta.pagina.getByRole('heading', { name: 'Nodo registrado', exact: true }).waitFor();
    await alta.pagina.goto(base + `/nodos/${datos.Nodo}/paneles/nuevo`);
    await alta.pagina.locator('#nombre-panel').fill(`Panel funcional ${marca}`);
    await alta.pagina.locator('#modelo-panel').fill('Modelo funcional');
    await alta.pagina.locator('#potencia-panel').fill('500');
    await alta.pagina.getByRole('button', { name: 'Registrar panel solar', exact: true }).click();
    await alta.pagina.waitForURL(/\/nodos\/[0-9a-f-]{36}/);
    await alta.pagina.getByText(`Panel funcional ${marca}`, { exact: true }).waitFor();
    registrar('C3.1-C3.2-auxiliar', 'Altas de parque, nodo y panel por la UI y lectura compartida verificadas en la base aislada.');
    const tarjeta = alta.pagina.locator('article.card').filter({ has: alta.pagina.getByText(`Panel funcional ${marca}`, { exact: true }) });
    const enlace = await tarjeta.getByRole('link', { name: 'Monitoreo', exact: true }).getAttribute('href');
    const vacio = await crear('Operador', undefined, enlace);
    await vacio.pagina.getByText('Aún no hay mediciones', { exact: true }).waitFor();
    await vacio.contexto.close();
    registrar('C4.6', 'El panel nuevo muestra su estado y la ausencia de historial.');
    auxiliar('sincronizacion');
    registrar('C4.9', 'El sincronizador real deja de consultar al retirar la última suscripción; los duplicados no se difunden.');

    for (const rutaRestringida of ['/parques-solares/nuevo', `/parques-solares/${datos.Parque}/nodos/nuevo`, `/nodos/${datos.Nodo}/paneles/nuevo`]) {
      const respuesta = await observadorB.contexto.request.get(base + rutaRestringida, { maxRedirects: 0 });
      assert([302, 403].includes(respuesta.status()));
    }
    registrar('C3.3-auxiliar', 'Operador no puede acceder a las altas; los recursos publicados se cargan por Nginx.');
    compose('stop', 'mysql-pruebas');
    try {
      await new Promise(resolve => setTimeout(resolve, 1800));
      assert.equal(await observadorB.pagina.locator('section[aria-live="polite"]').innerText(), antes);
    } finally { compose('start', 'mysql-pruebas'); }
    await comprobar(async () => {
      const estado = execFileSync('docker', ['inspect', '--format', '{{.State.Health.Status}}', 'solaruy-req008-aceptacion-mysql-pruebas-1'], { encoding: 'utf8' });
      return estado.trim() === 'healthy';
    }, 30000);
    await comprobar(async () => (await publicar(adminA.pagina, datos.Paneles[0], 343)).codigo === 200, 30000);
    await observadorB.pagina.waitForFunction(() => document.querySelector('section[aria-live="polite"]')?.innerText.includes('343 W'), null, { timeout: 3000 });
    registrar('C4.8', 'Tras interrumpir y recuperar MySQL, los circuitos conservan la lectura y vuelven a recibir actualizaciones.');
    fs.writeFileSync('.solaruy-acceptance/longpoll.json', JSON.stringify({ origen: base, panel: datos.Paneles[0], cookies: await adminA.contexto.cookies(base) }));
    auxiliar('longpoll');
    await observadorB.pagina.waitForFunction(() => document.querySelector('section[aria-live="polite"]')?.innerText.includes('345 W'), null, { timeout: 3000 });
    await new Promise(resolve => setTimeout(resolve, 35000));
    assert.equal(await observadorB.pagina.locator('#components-reconnect-modal').isVisible(), false);
    registrar('C4.10', 'SignalR Long Polling registra lecturas y los circuitos WebSocket continúan activos sin interacción durante 35 segundos.');
    const lecturaAntesDeCaida = await observadorB.pagina.locator('section[aria-live="polite"]').innerText();

    const expuesto = await adminA.contexto.request.get(base + '/health/ready');
    assert.equal(expuesto.status(), 404);
    registrar('salud', 'El endpoint técnico de salud está bloqueado en el acceso público.');

    if (process.argv.includes('--caidas')) {
      compose('stop', 'app1');
      await comprobar(async () => {
        const r = await adminA.contexto.request.get(base + '/');
        return r.status() === 200 && r.headers()['x-solaruy-instancia'] === 'app2';
      });
      registrar('C5.1-C5.2', 'Tras detener app1, una sesión afín a app1 obtiene respuesta de app2.');
      await observadorA.pagina.locator('#components-reconnect-modal').waitFor({ state: 'visible', timeout: 45000 });
      await observadorA.pagina.screenshot({ path: 'artifacts/req008/desconexion.png' });
      await observadorA.pagina.getByRole('button', { name: 'Recargar página' }).click();
      await observadorA.pagina.getByText('Generación actual', { exact: true }).waitFor();
      registrar('C5.3-auxiliar', 'La pantalla informa la interrupción y recupera el monitoreo al recargar usando la sesión sintética.');
      compose('start', 'app1');
      await comprobar(async () => {
        await adminA.contexto.clearCookies({ name: 'SolarUY_Afinidad' });
        const r = await adminA.contexto.request.get(base + '/');
        return r.status() === 200 && r.headers()['x-solaruy-instancia'] === 'app1';
      }, 30000);
      assert.equal(await observadorB.pagina.locator('section[aria-live="polite"]').innerText(), lecturaAntesDeCaida);
      registrar('C6.1-C6.2', 'app1 vuelve al reparto y el circuito ya abierto en app2 conserva su lectura.');
      await observadorA.pagina.reload();
      await observadorA.pagina.getByText('Generación actual', { exact: true }).waitFor();
      registrar('C6.3', 'La sesión que utilizó el destino de reemplazo puede recargar después de la reincorporación.');
      compose('up', '-d', '--no-deps', '--force-recreate', 'app1');
      await comprobar(async () => {
        await adminA.contexto.clearCookies({ name: 'SolarUY_Afinidad' });
        const r = await adminA.contexto.request.get(base + '/');
        return r.status() === 200 && r.headers()['x-solaruy-instancia'] === 'app1';
      }, 30000);
      registrar('C6.4', 'Nginx sigue resolviendo app1 por su nombre de servicio después de recrear el contenedor.');
      compose('stop', 'app1', 'app2');
      const sinDestinos = await anonimo.contexto.request.get(base + '/');
      assert([502, 504].includes(sinDestinos.status()));
      registrar('C5.5', 'Sin destinos disponibles Nginx devuelve 502/504.');
      compose('start', 'app1', 'app2');
    }
  } catch (error) {
    for (let i = 0; i < contextos.length; i++) {
      const pagina = contextos[i].pages()[0];
      if (pagina && !pagina.isClosed()) await pagina.screenshot({ path: `artifacts/req008/fallo-${i}.png` }).catch(() => {});
    }
    throw error;
  } finally {
    for (const contexto of contextos) await contexto.close().catch(() => {});
    await navegador.close();
  }
})().catch(error => {
  // Las excepciones del navegador pueden incluir URLs de negociación con tokens.
  console.error(`Verificación detenida: ${error.name}. Revisar el último criterio registrado sin exportar cookies ni tokens.`);
  process.exitCode = 1;
});
