(() => {
    const root = document.querySelector('[data-notification-root]');
    if (!root) return;

    const toggle = root.querySelector('[data-notification-toggle]');
    const menu = root.querySelector('[data-notification-menu]');
    const list = root.querySelector('[data-notification-list]');
    const badge = root.querySelector('[data-notification-count]');
    const clearAllBtn = root.querySelector('[data-notification-clear-all]');
    const toast = document.querySelector('[data-care-toast]');
    let toastTimer;

    const getCsrfToken = () => {
        const tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    };

    // [nam] Chuẩn hoá thuật ngữ Hotel trong nội dung thông báo thời gian thực.
    const forCageDisplay = value => String(value ?? '')
        .replace(/pet hotel/gi, 'lưu trú chuồng')
        .replace(/đặt phòng hotel/gi, 'đặt chuồng')
        .replace(/booking hotel/gi, 'lượt đặt chuồng')
        .replace(/hotel/gi, match => match === match.toUpperCase() ? 'CAGE' : 'chuồng');

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

    // [nam] Cập nhật badge thông báo chưa đọc.
    const updateUnreadBadge = count => {
        if (!badge) return;
        const unread = Number.parseInt(count, 10) || 0;
        if (unread <= 0) {
            badge.textContent = '0';
            badge.classList.add('d-none');
        } else {
            badge.textContent = unread > 99 ? '99+' : String(unread);
            badge.classList.remove('d-none');
        }
    };

    // [nam] Tăng badge thông báo chưa đọc khi có thông báo mới.
    const increaseUnreadCount = () => {
        const current = Number.parseInt(badge.textContent, 10) || 0;
        updateUnreadBadge(current + 1);
    };

    // [nam] Chèn thông báo mới lên đầu menu và giữ tối đa năm mục.
    const prependNotification = data => {
        list.querySelector('[data-notification-empty]')?.remove();
        
        const wrapper = document.createElement('div');
        wrapper.className = 'ps-notification-item-wrapper';
        wrapper.setAttribute('data-notification-id', data.notificationId);

        const item = document.createElement('a');
        item.className = 'ps-notification-item is-unread';
        item.href = `/Customer/Notifications/Open/${data.notificationId}`;

        const title = document.createElement('strong');
        title.textContent = forCageDisplay(data.title);
        const message = document.createElement('span');
        message.textContent = forCageDisplay(data.message);
        const time = document.createElement('time');
        time.textContent = new Intl.DateTimeFormat('vi-VN', {
            dateStyle: 'short', timeStyle: 'short'
        }).format(new Date(data.occurredAt));

        item.append(title, message, time);

        const deleteBtn = document.createElement('button');
        deleteBtn.type = 'button';
        deleteBtn.className = 'ps-notification-delete-btn';
        deleteBtn.setAttribute('data-notification-delete', data.notificationId);
        deleteBtn.title = 'Xóa thông báo';
        deleteBtn.innerHTML = '&times;';

        wrapper.append(item, deleteBtn);
        list.prepend(wrapper);

        if (clearAllBtn) clearAllBtn.classList.remove('d-none');

        while (list.querySelectorAll('.ps-notification-item-wrapper').length > 5) {
            list.lastElementChild?.remove();
        }
    };

    // [nam] Hiển thị toast cập nhật chăm sóc và tự ẩn sau thời gian quy định.
    const showToast = data => {
        if (!toast) return;
        toast.querySelector('[data-care-toast-title]').textContent = forCageDisplay(data.title);
        toast.querySelector('[data-care-toast-message]').textContent = forCageDisplay(data.message);
        toast.querySelector('[data-care-toast-link]').href = `/Customer/Notifications/Open/${data.notificationId}`;
        toast.hidden = false;
        window.clearTimeout(toastTimer);
        toastTimer = window.setTimeout(() => { toast.hidden = true; }, 8000);
    };

    // [nam] Xóa thông báo theo ID (hỗ trợ cả menu dropdown và trang thông báo).
    const deleteNotification = async (id, targetEl) => {
        try {
            const formData = new FormData();
            formData.append('__RequestVerificationToken', getCsrfToken());

            const response = await fetch(`/Customer/Notifications/Delete/${id}`, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            });

            if (response.ok) {
                const data = await response.json();
                if (data.success) {
                    updateUnreadBadge(data.unreadCount);

                    // Xóa trên tất cả vị trí xuất hiện (dropdown + main page)
                    document.querySelectorAll(`[data-notification-id="${id}"]`).forEach(el => el.remove());

                    // Nếu danh sách dropdown trống, hiện empty message
                    if (list.querySelectorAll('.ps-notification-item-wrapper').length === 0) {
                        list.innerHTML = '<p class="ps-notification-empty" data-notification-empty>Chưa có thông báo mới.</p>';
                        if (clearAllBtn) clearAllBtn.classList.add('d-none');
                    }

                    // Nếu trên trang thông báo chính mà trống
                    const pageList = document.querySelector('[data-cn-page-list]');
                    if (pageList && pageList.querySelectorAll('.cn-row-wrapper').length === 0) {
                        pageList.innerHTML = '<div class="cp-empty" data-cn-empty><i class="bi bi-bell"></i><p>Chưa có thông báo chăm sóc nào.</p></div>';
                        document.querySelector('.cn-delete-all')?.remove();
                        document.querySelector('.cn-mark-all')?.remove();
                    }
                }
            }
        } catch (error) {
            console.error('Lỗi khi xóa thông báo:', error);
        }
    };

    // [nam] Xóa tất cả thông báo qua AJAX khi bấm nút trên menu dropdown
    const deleteAllNotifications = async () => {
        if (!confirm('Bạn có chắc chắn muốn xóa tất cả thông báo?')) return;
        try {
            const formData = new FormData();
            formData.append('__RequestVerificationToken', getCsrfToken());

            const response = await fetch('/Customer/Notifications/DeleteAll', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            });

            if (response.ok) {
                const data = await response.json();
                if (data.success) {
                    updateUnreadBadge(0);
                    list.innerHTML = '<p class="ps-notification-empty" data-notification-empty>Chưa có thông báo mới.</p>';
                    if (clearAllBtn) clearAllBtn.classList.add('d-none');

                    const pageList = document.querySelector('[data-cn-page-list]');
                    if (pageList) {
                        pageList.innerHTML = '<div class="cp-empty" data-cn-empty><i class="bi bi-bell"></i><p>Chưa có thông báo chăm sóc nào.</p></div>';
                        document.querySelector('.cn-delete-all')?.remove();
                        document.querySelector('.cn-mark-all')?.remove();
                    }
                }
            }
        } catch (error) {
            console.error('Lỗi khi xóa tất cả thông báo:', error);
        }
    };

    // Event Delegation cho nút xóa từng thẻ thông báo
    document.addEventListener('click', event => {
        const deleteBtn = event.target.closest('[data-notification-delete]');
        if (deleteBtn) {
            event.preventDefault();
            event.stopPropagation();
            const id = deleteBtn.getAttribute('data-notification-delete');
            if (id) {
                deleteNotification(id, deleteBtn);
            }
        }
    });

    if (clearAllBtn) {
        clearAllBtn.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            deleteAllNotifications();
        });
    }

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
