(() => {
    const root = document.querySelector('[data-notification-root]');
    if (!root) return;

    const toggle = root.querySelector('[data-notification-toggle]');
    const menu = root.querySelector('[data-notification-menu]');
    const list = root.querySelector('[data-notification-list]');
    const badge = root.querySelector('[data-notification-count]');
    const toast = document.querySelector('[data-care-toast]');
    let toastTimer;

    toggle.addEventListener('click', () => {
        const willOpen = menu.hidden;
        menu.hidden = !willOpen;
        toggle.setAttribute('aria-expanded', String(willOpen));
    });

    document.addEventListener('click', event => {
        if (!root.contains(event.target)) {
            menu.hidden = true;
            toggle.setAttribute('aria-expanded', 'false');
        }
    });

    const increaseUnreadCount = () => {
        const current = Number.parseInt(badge.textContent, 10) || 0;
        const next = current + 1;
        badge.textContent = next > 99 ? '99+' : String(next);
        badge.classList.remove('d-none');
    };

    const prependNotification = data => {
        list.querySelector('[data-notification-empty]')?.remove();
        const item = document.createElement('a');
        item.className = 'ps-notification-item is-unread';
        item.href = `/Customer/Notifications/Open/${data.notificationId}`;

        const title = document.createElement('strong');
        title.textContent = data.title;
        const message = document.createElement('span');
        message.textContent = data.message;
        const time = document.createElement('time');
        time.textContent = new Intl.DateTimeFormat('vi-VN', {
            dateStyle: 'short', timeStyle: 'short'
        }).format(new Date(data.occurredAt));

        item.append(title, message, time);
        list.prepend(item);
        while (list.querySelectorAll('.ps-notification-item').length > 5) {
            list.lastElementChild?.remove();
        }
    };

    const showToast = data => {
        if (!toast) return;
        toast.querySelector('[data-care-toast-title]').textContent = data.title;
        toast.querySelector('[data-care-toast-message]').textContent = data.message;
        toast.querySelector('[data-care-toast-link]').href = `/Customer/Notifications/Open/${data.notificationId}`;
        toast.hidden = false;
        window.clearTimeout(toastTimer);
        toastTimer = window.setTimeout(() => { toast.hidden = true; }, 8000);
    };

    if (!window.signalR) return;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hotelCareHub')
        .withAutomaticReconnect()
        .build();

    connection.on('CareLogUpdated', data => {
        increaseUnreadCount();
        prependNotification(data);
        showToast(data);
        document.dispatchEvent(new CustomEvent('hotelCareLogUpdated', { detail: data }));
    });

    connection.start().catch(error => console.warn('Hotel care realtime unavailable:', error));
})();
