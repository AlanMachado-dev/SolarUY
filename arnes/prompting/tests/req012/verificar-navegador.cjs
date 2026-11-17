const assert = require('node:assert/strict');
const { chromium } = require(process.env.SOLARUY_PLAYWRIGHT || '../req009/node_modules/playwright');
const { setTimeout: esperar } = require('node:timers/promises');

const origenA = 'http://127.0.0.1:39313';
const origenB = 'http://127.0.0.1:39314';

async function verificar() {
    let etapa = 'inicio del navegador';
    const navegador = await chromium.launch();
    try {
        for (const origen of [origenA, origenB]) {
            for (let intento = 0; intento < 60; intento++) {
                if (await fetch(origen + '/req012/panel').then(r => r.ok).catch(() => false)) break;
                await esperar(500);
            }
        }

        const { id, nodoId } = await fetch(origenA + '/req012/panel').then(r => r.json());
        const ruta = '/paneles/' + id + '/monitoreo';
        const permiso = '/api/monitoreo/paneles/' + id + '/transmision';
        const mediciones = () => fetch(origenA + '/req012/mediciones/' + id).then(r => r.json());
        const mantenimiento = () => fetch(origenA + '/req012/mantenimiento/' + id).then(r => r.json());

        async function sesion(origen, rol, destino = ruta, observar = () => {}) {
            const contexto = await navegador.newContext();
            assert.equal((await contexto.request.get(origen + '/req012/entrar/' + rol)).status(), 200);
            const pagina = await contexto.newPage();
            observar(pagina);
            await pagina.goto(origen + destino);
            return pagina;
        }

        const accion = (pagina, texto) => pagina.getByRole('button', { name: texto, exact: true });
        const estado = (pagina, texto) => pagina.getByText(texto, { exact: true }).first().waitFor();
        const conexiones = [];
        const admin = await sesion(origenA, 'Administrador', ruta, pagina => pagina.on('websocket', socket => {
            if (new URL(socket.url()).pathname === '/hubs/monitoreo') conexiones.push(socket);
        }));
        const operador = await sesion(origenB, 'Operador');

        async function programar(pagina, demora) {
            await accion(pagina, 'Programar Mantenimiento').click();
            const modal = pagina.getByRole('dialog');
            await modal.waitFor();
            await accion(modal, 'Programar').click();
            assert.equal(await modal.locator('input:invalid').count(), 3);
            await modal.getByLabel('Tipo').fill('Limpieza');
            const fecha = new Date(Date.now() + demora);
            const partes = Object.fromEntries(new Intl.DateTimeFormat('sv-SE', {
                timeZone: 'America/Montevideo', hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit',
                hour: '2-digit', minute: '2-digit', second: '2-digit'
            }).formatToParts(fecha).map(parte => [parte.type, parte.value]));
            await modal.getByLabel(/Fecha Programada/).fill(
                partes.year + '-' + partes.month + '-' + partes.day + 'T' + partes.hour + ':' + partes.minute + ':' + partes.second);
            await modal.getByLabel('Responsable').fill('Técnico');
            await accion(modal, 'Programar').click();
            await modal.waitFor({ state: 'hidden' });
            if (demora > 0)
                await pagina.getByText(partes.day + '/' + partes.month + '/' + partes.year + ' ' + partes.hour + ':' + partes.minute + ':' + partes.second + ' (hora de Uruguay)', { exact: true }).waitFor();
        }

        async function finalizar(pagina, observaciones) {
            await accion(pagina, 'Finalizar Mantenimiento').click();
            const modal = pagina.getByRole('dialog');
            if (observaciones)
                await modal.getByLabel(/Observaciones/).fill(observaciones);
            await accion(modal, 'Finalizar').click();
            await modal.waitFor({ state: 'hidden' });
        }

        async function comprobarPausa() {
            await estado(admin, 'Telemetría en pausa.');
            await estado(admin, '0 W');
            assert.ok(conexiones.length > 0 && conexiones.every(socket => socket.isClosed()));
            const cantidad = await mediciones();
            await esperar(2200);
            assert.equal(await mediciones(), cantidad);
        }

        etapa = 'sensor operativo';
        await estado(admin, 'Último envío confirmado.');
        assert.ok(conexiones.some(socket => !socket.isClosed()));
        await estado(admin, 'Operativo');
        const datos = admin.getByText(/^[\d.,]+ W$/).first();
        const potencia = await datos.textContent();
        await admin.waitForFunction(anterior => [...document.querySelectorAll('dd span')].some(
            span => /^\d.* W$/.test(span.textContent) && span.textContent !== anterior), potencia, { timeout: 26000 });
        await estado(admin, 'Operativo');
        console.log('OK Navegador: aumenta el historial operativo con potencia variable.');

        etapa = 'programación remota y corte';
        assert.equal(await accion(operador, 'Simular Falla').count(), 0);
        assert.equal(await accion(operador, 'Desactivar Panel').count(), 0);
        assert.equal((await operador.request.get(origenB + permiso)).status(), 403);
        assert.equal((await fetch(origenB + permiso)).status, 401);
        await programar(operador, 7500);
        assert.match((await mantenimiento()).id, /^[0-9a-f-]{36}$/);
        await admin.getByText('Mantenimiento programado', { exact: true }).waitFor();
        await estado(admin, 'EnMantenimiento');
        await comprobarPausa();
        console.log('OK Navegador: fecha Uruguay y corte confirmado desde otra instancia con sensor detenido.');

        etapa = 'cierre normal';
        await finalizar(operador, 'Limpieza verificada');
        await estado(admin, 'Último envío confirmado.');
        const cerrado = await mantenimiento();
        assert.equal(cerrado.observaciones, 'Limpieza verificada');
        assert.ok(Math.abs(Date.now() - Date.parse(cerrado.fechaRealizada)) < 15000);
        const detalle = await admin.context().newPage();
        await detalle.goto(origenA + '/nodos/' + nodoId);
        await detalle.getByText('Último mantenimiento', { exact: true }).first().waitFor();
        assert.ok(await detalle.locator('dd').filter({ hasText: /\d{2}\/\d{2}\/\d{4}.*UTC/ }).count() > 0);
        await detalle.close();
        console.log('OK Navegador: cierre por Operador, fecha de servidor, observaciones y reconexión.');

        etapa = 'falla y reparación';
        await accion(admin, 'Simular Falla').click();
        await accion(admin, 'Simular Arreglo').waitFor();
        await estado(admin, 'EnFalla');
        await comprobarPausa();
        await accion(admin, 'Simular Arreglo').click();
        await estado(admin, 'Último envío confirmado.');
        console.log('OK Navegador: falla simple, 0 W y reanudación al arreglar.');

        etapa = 'alternancia sobre un mantenimiento';
        await programar(admin, -5000);
        await estado(admin, 'EnMantenimiento');
        await accion(admin, 'Simular Falla').click();
        await accion(admin, 'Simular Arreglo').click();
        await estado(admin, 'EnMantenimiento');
        await comprobarPausa();
        await accion(admin, 'Desactivar Panel').click();
        await accion(admin, 'Activar Panel').click();
        await estado(admin, 'EnMantenimiento');
        await comprobarPausa();
        await accion(admin, 'Simular Falla').click();
        await accion(admin, 'Desactivar Panel').click();
        await accion(admin, 'Simular Falla').waitFor();
        await accion(admin, 'Activar Panel').waitFor();
        await accion(admin, 'Simular Falla').click();
        await accion(admin, 'Desactivar Panel').waitFor();
        await accion(admin, 'Simular Arreglo').waitFor();
        await finalizar(admin);
        await estado(admin, 'EnFalla');
        await comprobarPausa();
        await accion(admin, 'Simular Arreglo').click();
        await estado(admin, 'Último envío confirmado.');
        await programar(admin, -5000);
        await accion(admin, 'Desactivar Panel').click();
        await finalizar(admin);
        await estado(admin, 'Desactivado');
        await comprobarPausa();
        await accion(admin, 'Activar Panel').click();
        await estado(admin, 'Último envío confirmado.');
        console.log('OK Navegador: alternancia en ambos órdenes, recuperación al mantenimiento y cierre durante falla y desactivación.');

        etapa = 'reversión visible';
        const corte = origenA + '/req012/escritura/';
        assert.equal((await fetch(corte + 'true', { method: 'POST' })).status, 204);
        try {
            await accion(admin, 'Simular Falla').click();
            await estado(admin, 'No se pudo guardar el cambio.');
            await accion(admin, 'Simular Falla').waitFor();
            await estado(admin, 'Operativo');
        } finally {
            assert.equal((await fetch(corte + 'false', { method: 'POST' })).status, 204);
        }
        console.log('OK Navegador: un fallo real de escritura deja visible el último estado confirmado.');

        etapa = 'monitoreo después del reinicio';
        const panelReinicio = await fetch(origenA + '/req012/panel-reinicio').then(r => r.json());
        const reabierta = await sesion(origenB, 'Operador', '/paneles/' + panelReinicio + '/monitoreo');
        await estado(reabierta, 'EnMantenimiento');
        await estado(reabierta, '0 W');
        console.log('OK Navegador: el inicio persistido durante el reinicio aparece al abrir el monitoreo.');
    } catch (error) {
        console.error('FALLO ' + etapa + ' (' + error.name + ').');
        const mensaje = String(error.message).split('\n').filter(linea => /Expected|Received|waiting for|ERR_|Error:/.test(linea));
        for (const linea of mensaje) console.error(linea);
        process.exitCode = 1;
    } finally {
        await navegador.close();
    }
}

verificar().catch(error => {
    console.error('FALLO inicio del navegador (' + error.name + ').');
    process.exitCode = 1;
});
