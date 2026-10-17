(() => {
    const form = document.querySelector('[data-native-login]');
    if (!form) return;

    const password = form.querySelector('#password');
    const toggle = form.querySelector('[data-password-toggle]');
    const submit = form.querySelector('[data-login-submit]');

    if (password && toggle) {
        toggle.addEventListener('click', () => {
            const show = password.type === 'password';
            password.type = show ? 'text' : 'password';
            toggle.textContent = show ? 'Ocultar' : 'Mostrar';
            toggle.setAttribute('aria-pressed', String(show));
        });
        toggle.hidden = false;
    }

    form.addEventListener('submit', event => {
        if (event.defaultPrevented || !submit) return;
        // Feedback only: the browser still posts the native Razor form.
        submit.disabled = true;
        submit.textContent = 'A entrar…';
        form.setAttribute('aria-busy', 'true');
    });

    window.addEventListener('pageshow', () => {
        if (submit) {
            submit.disabled = false;
            submit.textContent = 'Entrar';
        }
        form.removeAttribute('aria-busy');
        if (password && toggle) {
            password.type = 'password';
            toggle.textContent = 'Mostrar';
            toggle.setAttribute('aria-pressed', 'false');
        }
    });
})();

(() => {
    // Mudança de palavra-passe: um único envio. Um segundo «Guardar» com o
    // mesmo formulário chegava com o token antigo e reabria o formulário.
    const form = document.querySelector('[data-password-change]');
    if (!form) return;

    const submit = form.querySelector('[data-password-change-submit]');
    let sent = false;

    form.addEventListener('submit', event => {
        if (event.defaultPrevented) return;
        if (sent) {
            event.preventDefault();
            return;
        }
        sent = true;
        if (submit) {
            submit.disabled = true;
            submit.textContent = 'A guardar…';
        }
        form.setAttribute('aria-busy', 'true');
    });

    window.addEventListener('pageshow', () => {
        sent = false;
        if (submit) {
            submit.disabled = false;
            submit.textContent = 'Guardar';
        }
        form.removeAttribute('aria-busy');
    });
})();
