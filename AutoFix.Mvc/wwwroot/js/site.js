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

// Visor de fotografías: <a data-lightbox href="imagen" data-titulo="texto">
(function () {
    let visor = null;
    function cerrar() { if (visor) { visor.remove(); visor = null; document.removeEventListener('keydown', alTeclear); } }
    function alTeclear(e) { if (e.key === 'Escape') cerrar(); }
    document.addEventListener('click', function (e) {
        const enlace = e.target instanceof Element ? e.target.closest('a[data-lightbox]') : null;
        if (!enlace || e.ctrlKey || e.metaKey || e.shiftKey) return;
        e.preventDefault();
        cerrar();
        visor = document.createElement('div');
        visor.className = 'visor';
        visor.setAttribute('role', 'dialog');
        visor.setAttribute('aria-modal', 'true');
        const img = document.createElement('img');
        img.src = enlace.href;
        img.alt = enlace.dataset.titulo || '';
        const pie = document.createElement('div');
        pie.className = 'visor-pie';
        pie.textContent = enlace.dataset.titulo || '';
        const x = document.createElement('button');
        x.type = 'button'; x.className = 'visor-cerrar'; x.setAttribute('aria-label', 'Cerrar'); x.textContent = '×';
        visor.append(img, pie, x);
        visor.addEventListener('click', function (ev) { if (ev.target !== img) cerrar(); });
        document.body.appendChild(visor);
        document.addEventListener('keydown', alTeclear);
        x.focus();
    });
})();

// Avisos de éxito se cierran solos.
document.querySelectorAll('.alert.aviso-ok').forEach(function (a) {
    setTimeout(function () { a.classList.remove('show'); setTimeout(function () { a.remove(); }, 300); }, 6000);
});
