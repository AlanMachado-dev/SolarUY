const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
let browser;
// Solo arnés loopback: no interactúa con las instancias habituales ni con OIDC/Gemini reales.
(async () => {
  browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  const origen = 'https://localhost:43320';
  const errores = [];
  page.on('pageerror', e => errores.push(e.message));
  const anonimo = await context.request.get(origen + '/api/asistente/sesion');
  assert.equal(anonimo.status(), 401);
  await page.goto(origen + '/entrar/Administrador');
  await page.getByRole('button', { name: 'Consultas SolarUY' }).waitFor();
  await page.getByRole('button', { name: 'Consultas SolarUY' }).click();
  await page.getByLabel('Tu consulta').fill('¿Cómo está funcionando todo hoy?');
  await page.getByRole('button', { name: 'Enviar', exact: true }).click();
  await page.getByText('Respuesta funcional simulada;', { exact: false }).waitFor();
  assert.equal(await page.evaluate(() => window.inyeccion), undefined);
  assert.equal(await page.locator('.chat-mensaje').count(), 2);
  console.log('OK: Componente real, envío HTTPS, Application, MCP real y MySQL aislado; texto escapado. Modelo sintético.');
  await page.reload();
  await page.getByText('Respuesta funcional simulada;', { exact: false }).waitFor();
  assert.equal(await page.locator('.chat-mensaje').count(), 2);
  await page.getByRole('link', { name: 'Otra página' }).click();
  await page.waitForURL(origen + '/verificacion');
  await page.getByText('Respuesta funcional simulada;', { exact: false }).waitFor();
  console.log('OK: sessionStorage conserva conversación al recargar y navegar dentro de la sesión validada.');
  const sesion = await (await context.request.get(origen + '/api/asistente/sesion')).json();
  const sinAntiforgery = await context.request.post(origen + '/api/asistente/mensajes', {
    data: { pregunta: 'Hola', marcaSesion: sesion.marca, historial: [], contextoPagina: null }
  });
  assert.equal(sinAntiforgery.status(), 400);
  const marcaFalsa = await context.request.post(origen + '/api/asistente/mensajes', {
    headers: { [sesion.cabecera]: sesion.token },
    data: { pregunta: 'Hola', marcaSesion: 'falsa', historial: [], contextoPagina: null }
  });
  assert.equal(marcaFalsa.status(), 403);
  console.log('OK: POST rechaza antiforgery ausente y marca de sesión fabricada.');
  await page.getByLabel('Tu consulta').fill('demora');
  await page.getByRole('button', { name: 'Enviar', exact: true }).click();
  await page.getByText('Consultando…', { exact: true }).waitFor();
  await page.getByRole('button', { name: 'Nueva conversación' }).click();
  await page.waitForTimeout(5500);
  assert.equal(await page.locator('.chat-mensaje').count(), 0);
  console.log('OK: Nueva conversación cancela e ignora respuestas tardías.');
  // Otra sesión con el mismo usuario debe descartar almacenamiento anterior incluso en el mismo segundo.
  await page.getByLabel('Tu consulta').fill('Consulta de sesión anterior');
  await page.getByRole('button', { name: 'Enviar', exact: true }).click();
  await page.getByText('Respuesta funcional simulada;', { exact: false }).waitFor();
  await page.goto(origen + '/entrar/Operador');
  await page.getByRole('button', { name: 'Consultas SolarUY' }).waitFor();
  await page.getByRole('button', { name: 'Consultas SolarUY' }).click();
  assert.equal(await page.locator('.chat-mensaje').count(), 0);
  await page.getByLabel('Tu consulta').fill('Consulta Operador');
  await page.getByRole('button', { name: 'Enviar', exact: true }).click();
  await page.getByText('Respuesta funcional simulada;', { exact: false }).waitFor();
  console.log('OK: Operador puede consultar y una sesión nueva descarta mensajes anteriores.');
  await page.setViewportSize({ width: 375, height: 667 });
  await page.getByLabel('Tu consulta').focus();
  assert.equal(await page.getByLabel('Tu consulta').evaluate(e => e === document.activeElement), true);
  const caja = await page.locator('.chat-asistente').boundingBox();
  assert(caja.x >= 0 && caja.x + caja.width <= 375);
  assert.equal(await page.locator('[role="status"][aria-live="polite"]').count(), 1);
  fs.mkdirSync('artifacts/req009', { recursive: true });
  await page.screenshot({ path: 'artifacts/req009/chat-movil.png' });
  console.log('OK: Panel móvil, foco de teclado y estado accesible.');
  await page.goto(origen + '/entrar/Invitado');
  const prohibido = await context.request.get(origen + '/api/asistente/sesion');
  assert.equal(prohibido.status(), 403);
  await page.goto(origen + '/salir');
  assert.equal((await context.request.get(origen + '/api/asistente/sesion')).status(), 401);
  console.log('OK: Otro rol y sesión cerrada no acceden al endpoint. Identidades sintéticas; no acredita OIDC real.');
  assert.deepEqual(errores, []);
  await browser.close();
})().catch(async e => { console.error(e.message); await browser?.close(); process.exitCode = 1; });
