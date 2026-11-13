// Selector de usuarios de demostración (solo se carga en Development).
// Elegir una opción rellena correo y contraseña; escribir un correo propio sigue funcionando.
(function () {
    const combo = document.getElementById('combo');
    if (!combo) return;
    const email = combo.querySelector('input');
    const lista = document.getElementById('comboLista');
    const boton = document.getElementById('comboBoton');
    const clave = document.getElementById('Password');
    const vacio = lista.querySelector('.combo-vacio');
    const opciones = Array.from(lista.querySelectorAll('.combo-opcion'));
    let activo = -1;

    const visibles = () => opciones.filter(o => !o.hidden);
    const abierta = () => !lista.hidden;

    function abrir() {
        if (abierta()) return;
        lista.hidden = false;
        email.setAttribute('aria-expanded', 'true');
        boton.setAttribute('aria-expanded', 'true');
    }
    function cerrar() {
        lista.hidden = true;
        email.setAttribute('aria-expanded', 'false');
        boton.setAttribute('aria-expanded', 'false');
        marcar(-1);
    }
    function marcar(i) {
        const v = visibles();
        activo = i;
        opciones.forEach(o => { o.classList.remove('activa'); o.setAttribute('aria-selected', 'false'); });
        if (i >= 0 && v[i]) {
            v[i].classList.add('activa');
            v[i].setAttribute('aria-selected', 'true');
            v[i].scrollIntoView({ block: 'nearest' });
        }
    }
    function filtrar(texto) {
        const t = texto.trim().toLowerCase();
        let n = 0;
        opciones.forEach(o => {
            const coincide = !t || o.dataset.busqueda.includes(t);
            o.hidden = !coincide;
            if (coincide) n++;
        });
        vacio.hidden = n > 0;
        marcar(-1);
    }
    function elegir(o) {
        email.value = o.dataset.email;
        if (clave) clave.value = o.dataset.clave;
        email.dispatchEvent(new Event('input', { bubbles: true }));
        cerrar();
        const form = email.form;
        if (form && window.jQuery && jQuery.fn.valid) { try { jQuery(email).valid(); jQuery(clave).valid(); } catch (e) { } }
        (clave || email).focus();
    }

    // Al escribir se filtra; si el texto ya no coincide con un usuario de demo, la contraseña no se toca.
    email.addEventListener('input', () => { filtrar(email.value); abrir(); });
    email.addEventListener('click', () => { if (!email.value) filtrar(''); abrir(); });
    email.addEventListener('keydown', e => {
        const v = visibles();
        if (e.key === 'ArrowDown') { e.preventDefault(); abrir(); marcar(Math.min(activo + 1, v.length - 1)); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); marcar(Math.max(activo - 1, 0)); }
        else if (e.key === 'Enter' && abierta() && activo >= 0) { e.preventDefault(); elegir(v[activo]); }
        else if (e.key === 'Escape' || e.key === 'Tab') cerrar();
    });
    boton.addEventListener('click', () => {
        if (abierta()) cerrar(); else { filtrar(''); abrir(); email.focus(); }
    });
    lista.addEventListener('mousedown', e => e.preventDefault()); // no quitar el foco al input
    lista.addEventListener('click', e => {
        const o = e.target.closest('.combo-opcion');
        if (o) elegir(o);
    });
    document.addEventListener('click', e => { if (!combo.contains(e.target)) cerrar(); });
})();
