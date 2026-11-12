// Validación previa de imágenes en el navegador (la API vuelve a validar formato, tamaño y contenido).
document.addEventListener('change', function (e) {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || input.type !== 'file' || !input.files || input.files.length === 0) return;
    const archivo = input.files[0];
    const extension = archivo.name.split('.').pop().toLowerCase();
    let mensaje = '';
    if (!['jpg', 'jpeg', 'png'].includes(extension)) mensaje = 'Solo se permiten imágenes JPG, JPEG y PNG.';
    else if (archivo.size > 5 * 1024 * 1024) mensaje = 'La imagen no puede superar 5 MB.';
    input.setCustomValidity(mensaje);
    if (mensaje) { input.reportValidity(); input.value = ''; }
});

// Confirmación antes de acciones destructivas: <form data-confirmar="¿Seguro?">
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (form instanceof HTMLFormElement && form.dataset.confirmar && !window.confirm(form.dataset.confirmar)) {
        e.preventDefault();
    }
});
