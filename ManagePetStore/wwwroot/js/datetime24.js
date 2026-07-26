(function () {
    'use strict';

    const selector = 'input[type="datetime-local"]';

    function formatValue(value) {
        const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value || '');
        return match
            ? `${match[3]}/${match[2]}/${match[1]} ${match[4]}:${match[5]}`
            : '';
    }

    function refresh(input) {
        if (!input) return;
        const shell = input.closest('.datetime24-shell');
        const display = shell?.querySelector('.datetime24-display');
        if (!display) return;

        const formatted = formatValue(input.value);
        display.textContent = formatted || 'dd/mm/yyyy HH:mm';
        display.classList.toggle('is-placeholder', !formatted);
    }

    function enhance(input) {
        if (!input || input.dataset.datetime24Enhanced === 'true') return;

        const shell = document.createElement('span');
        shell.className = 'datetime24-shell';
        input.parentNode.insertBefore(shell, input);
        shell.appendChild(input);

        const display = document.createElement('span');
        display.className = 'datetime24-display';
        display.setAttribute('aria-hidden', 'true');
        shell.appendChild(display);

        input.dataset.datetime24Enhanced = 'true';
        input.classList.add('datetime24-native');
        input.setAttribute('lang', 'vi-VN');
        input.addEventListener('input', () => refresh(input));
        input.addEventListener('change', () => refresh(input));
        refresh(input);
    }

    function enhanceAll(root) {
        (root || document).querySelectorAll(selector).forEach(enhance);
    }

    window.DateTime24 = { enhance, enhanceAll, refresh, formatValue };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => enhanceAll(document));
    } else {
        enhanceAll(document);
    }
})();
