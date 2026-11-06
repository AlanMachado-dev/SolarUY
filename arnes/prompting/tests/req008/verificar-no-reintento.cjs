const fs = require('node:fs');
const { spawn, execFileSync } = require('node:child_process');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SOLARUY_PLAYWRIGHT || 'playwright');
const base = process.env.SOLARUY_BASE_URL || 'https://localhost:8443';
const compose = ['compose', '--env-file', '.solaruy-acceptance/compose.env', '-f', 'docker-compose.yml', '-f', 'arnes/prompting/tests/req008/docker-compose.aceptacion.yml'];
assert(fs.readFileSync('.solaruy-acceptance/compose.env', 'utf8').includes('SOLARUY_PROYECTO=solaruy-req008-aceptacion'));
async function esperarArchivo(nombre) {
  const fin = Date.now() + 30000;
  while (!fs.existsSync('.solaruy-acceptance/' + nombre)) {
    if (Date.now() > fin) throw new Error('Tiempo de espera agotado.');
    await new Promise(r => setTimeout(r, 100));
  }
}
(async () => {
  const navegador = await chromium.launch({ channel: process.env.SOLARUY_BROWSER || 'msedge', headless: true });
  let auxiliar;
  try {
    for (const nombre of ['bloqueo-listo', 'escritura-enviada', 'liberar-bloqueo', 'no-reintento-resultado.json'])
      if (fs.existsSync('.solaruy-acceptance/' + nombre)) fs.unlinkSync('.solaruy-acceptance/' + nombre);
    const sesiones = JSON.parse(fs.readFileSync('.solaruy-acceptance/sesiones.json', 'utf8'));
    let contexto, pagina;
    for (let i = 0; i < 30; i++) {
      contexto = await navegador.newContext({ ignoreHTTPSErrors: true });
      await contexto.addCookies([{ name: 'SolarUY.Sesion.Oidc', value: sesiones.Administrador, domain: 'localhost', path: '/', secure: true, httpOnly: true }]);
      pagina = await contexto.newPage();
      const r = await pagina.goto(base + '/parques-solares/nuevo');
      if (r.headers()['x-solaruy-instancia'] === 'app1') break;
      await contexto.close(); pagina = null;
    }
    assert(pagina);
    const marca = `No reintentar req008 ${Date.now()}`;
    fs.writeFileSync('.solaruy-acceptance/no-reintento.json', JSON.stringify({ marca }));
    await pagina.locator('#nombre').fill(marca);
    await pagina.locator('#ubicacion').fill(marca);
    auxiliar = spawn('dotnet', ['arnes/prompting/tests/req008/bin/Release/net10.0/VerificarBalanceo.dll', 'bloqueo-no-reintento'], { stdio: 'ignore' });
    await esperarArchivo('bloqueo-listo');
    const respuestaPendiente = pagina.evaluate(async () => {
      const formulario = [...document.querySelectorAll('form')].find(f => f.querySelector('#nombre'));
      return (await fetch(formulario.action, { method: 'POST', body: new FormData(formulario) })).status;
    });
    await esperarArchivo('escritura-enviada');
    execFileSync('docker', [...compose, 'kill', '-s', 'SIGKILL', 'app1'], { stdio: 'ignore' });
    fs.writeFileSync('.solaruy-acceptance/liberar-bloqueo', 'liberar');
    const codigo = await respuestaPendiente;
    assert([502, 504].includes(codigo));
    await esperarArchivo('no-reintento-resultado.json');
    const resultado = JSON.parse(fs.readFileSync('.solaruy-acceptance/no-reintento-resultado.json', 'utf8'));
    assert.equal(resultado.Cantidad, 0);
    fs.writeFileSync('artifacts/req008/no-reintento.json', JSON.stringify({ tipo: 'auxiliar_sesion_sintetica', codigo, escriturasGuardadas: resultado.Cantidad,
      resultado: 'POST ya recibido por app1 no se reprodujo en app2 después de interrumpir app1 antes del commit.' }, null, 2));
    console.log('C5.4: POST recibido por app1, caída antes del commit, respuesta 502/504 y ninguna escritura reintentada en app2.');
  } finally {
    fs.writeFileSync('.solaruy-acceptance/liberar-bloqueo', 'liberar');
    execFileSync('docker', [...compose, 'start', 'app1'], { stdio: 'ignore' });
    await navegador.close();
  }
})().catch(error => { console.error(`Control detenido: ${error.name}. Revisar los marcadores privados de aceptación.`); process.exitCode = 1; });
