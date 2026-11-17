export function mostrar(dialogo, abierto) {
    if (abierto && !dialogo.open) {
        dialogo.oncancel = event => event.preventDefault();
        dialogo.showModal();
    } else if (!abierto && dialogo.open) {
        dialogo.close();
    }
}
