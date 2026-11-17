const aviso = document.getElementById("components-reconnect-modal");
const mensajes = aviso.querySelectorAll("[data-reconexion]");
const recargar = aviso.querySelector("#recargar-solaruy");

aviso.addEventListener("components-reconnect-state-changed", ({ detail }) => {
    const estado = detail.state === "show" ? "retrying" : detail.state;

    if (!["retrying", "failed", "rejected"].includes(estado)) {
        aviso.close();
        return;
    }

    for (const mensaje of mensajes) {
        mensaje.hidden = mensaje.dataset.reconexion !== estado;
    }

    recargar.hidden = estado === "retrying";
    if (!aviso.open) {
        aviso.showModal();
    }
});

recargar.addEventListener("click", () => location.reload());
