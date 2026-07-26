(function () {
    'use strict';

    const selector = 'input[type="datetime-local"]';
    let activeShell = null;

    function parseValue(value) {
        // [nam][Flow] Đọc giá trị chuẩn yyyy-MM-ddTHH:mm của input gốc mà ASP.NET model binding cần.
        const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value || '');
        return match
            ? { date: `${match[1]}-${match[2]}-${match[3]}`, hour: match[4], minute: match[5] }
            : null;
    }

    function formatValue(value) {
        // [nam][Flow] Chỉ lớp hiển thị đổi sang dd/MM/yyyy HH:mm; không đổi giá trị gửi về server.
        const parts = parseValue(value);
        if (!parts) return '';
        const [year, month, day] = parts.date.split('-');
        return `${day}/${month}/${year} ${parts.hour}:${parts.minute}`;
    }

    function createOptions(select, start, end) {
        for (let value = start; value <= end; value += 1) {
            const option = document.createElement('option');
            option.value = String(value).padStart(2, '0');
            option.textContent = option.value;
            select.appendChild(option);
        }
    }

    function refresh(input) {
        // [nam][Validate] Đồng bộ trạng thái valid của input gốc sang ô 24h mà người dùng nhìn thấy.
        if (!input) return;
        const shell = input.closest('.datetime24-shell');
        const trigger = shell?.querySelector('.datetime24-trigger');
        if (!trigger) return;

        trigger.value = formatValue(input.value);
        trigger.placeholder = 'dd/mm/yyyy HH:mm';
        trigger.classList.toggle('datetime24-invalid', !input.validity.valid && input.value !== '');
    }

    function close(shell) {
        const target = shell || activeShell;
        if (!target) return;
        const picker = target.querySelector('.datetime24-picker');
        if (picker) picker.hidden = true;
        if (activeShell === target) activeShell = null;
    }

    function findPickerBoundary(shell) {
        // [nam][Flow] Tìm modal/khung cuộn gần nhất để popup không bị overflow cắt mất nội dung.
        let element = shell.parentElement;

        while (element && element !== document.body) {
            const style = window.getComputedStyle(element);
            const overflowValues = `${style.overflow} ${style.overflowX} ${style.overflowY}`;
            if (/(auto|scroll|hidden|clip)/.test(overflowValues)) return element.getBoundingClientRect();
            element = element.parentElement;
        }

        return { left: 0, right: window.innerWidth, width: window.innerWidth };
    }

    function positionPicker(shell, picker) {
        // [nam][Flow] Ô bên trái mở sang phải, ô bên phải mở sang trái và luôn giữ popup trong vùng chứa.
        const shellRect = shell.getBoundingClientRect();
        const boundary = findPickerBoundary(shell);
        const boundaryInset = 12;
        const availableWidth = Math.max(240, Math.min(boundary.width - (boundaryInset * 2), window.innerWidth - 32));
        const pickerWidth = Math.min(330, availableWidth);
        const leftBoundary = Math.max(16, boundary.left + boundaryInset);
        const rightBoundary = Math.min(window.innerWidth - 16, boundary.right - boundaryInset);
        const leftAlignedRight = shellRect.left + pickerWidth;
        const rightAlignedLeft = shellRect.right - pickerWidth;
        const canAlignLeft = shellRect.left >= leftBoundary && leftAlignedRight <= rightBoundary;
        const canAlignRight = rightAlignedLeft >= leftBoundary && shellRect.right <= rightBoundary;

        picker.style.width = `${pickerWidth}px`;
        picker.classList.toggle('datetime24-align-left', canAlignLeft || !canAlignRight);
        picker.classList.toggle('datetime24-align-right', !canAlignLeft && canAlignRight);
    }

    function open(input) {
        const shell = input.closest('.datetime24-shell');
        const picker = shell?.querySelector('.datetime24-picker');
        if (!shell || !picker) return;

        if (activeShell && activeShell !== shell) close(activeShell);

        const dateInput = picker.querySelector('.datetime24-date');
        const hourSelect = picker.querySelector('.datetime24-hour');
        const minuteSelect = picker.querySelector('.datetime24-minute');
        const current = parseValue(input.value) || parseValue(input.min);
        const now = new Date();

        // [nam][Validate] Sao chép min/max từ input gốc để bộ chọn 24h tuân thủ cùng ràng buộc ngày.
        dateInput.value = current?.date || [
            now.getFullYear(),
            String(now.getMonth() + 1).padStart(2, '0'),
            String(now.getDate()).padStart(2, '0')
        ].join('-');
        dateInput.min = input.min ? input.min.slice(0, 10) : '';
        dateInput.max = input.max ? input.max.slice(0, 10) : '';
        hourSelect.value = current?.hour || String(now.getHours()).padStart(2, '0');
        minuteSelect.value = current?.minute || String(now.getMinutes()).padStart(2, '0');
        picker.querySelector('.datetime24-error').textContent = '';
        picker.hidden = false;
        positionPicker(shell, picker);
        activeShell = shell;
        dateInput.focus();
    }

    function confirm(input) {
        const shell = input.closest('.datetime24-shell');
        const picker = shell.querySelector('.datetime24-picker');
        const date = picker.querySelector('.datetime24-date').value;
        const hour = picker.querySelector('.datetime24-hour').value;
        const minute = picker.querySelector('.datetime24-minute').value;
        const error = picker.querySelector('.datetime24-error');

        // [nam][Validate] Chặn thiếu ngày và giá trị ngoài min/max trước khi ghi lại input gốc.
        if (!date) {
            error.textContent = 'Vui lòng chọn ngày.';
            return;
        }

        const value = `${date}T${hour}:${minute}`;
        if (input.min && value < input.min.slice(0, 16)) {
            error.textContent = `Thời gian phải từ ${formatValue(input.min)} trở đi.`;
            return;
        }
        if (input.max && value > input.max.slice(0, 16)) {
            error.textContent = `Thời gian không được sau ${formatValue(input.max)}.`;
            return;
        }

        // [nam][Flow] Phát lại input/change để validation và phép tính giá hiện có tiếp tục chạy bình thường.
        input.value = value;
        input.setCustomValidity('');
        refresh(input);
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
        refresh(input);
        close(shell);
    }

    function buildPicker(input) {
        const picker = document.createElement('div');
        picker.className = 'datetime24-picker';
        picker.hidden = true;
        picker.innerHTML = `
            <label class="datetime24-date-field">
                <span>Ngày</span>
                <input type="date" class="datetime24-date" />
            </label>
            <div class="datetime24-time-field">
                <span>Giờ (24h)</span>
                <div class="datetime24-time-selects">
                    <select class="datetime24-hour" aria-label="Giờ"></select>
                    <strong>:</strong>
                    <select class="datetime24-minute" aria-label="Phút"></select>
                </div>
            </div>
            <p class="datetime24-error" role="alert"></p>
            <div class="datetime24-actions">
                <button type="button" class="datetime24-cancel">Hủy</button>
                <button type="button" class="datetime24-confirm">Xác nhận</button>
            </div>`;

        createOptions(picker.querySelector('.datetime24-hour'), 0, 23);
        createOptions(picker.querySelector('.datetime24-minute'), 0, 59);
        picker.addEventListener('click', event => event.stopPropagation());
        picker.querySelector('.datetime24-cancel').addEventListener('click', () => close(input.closest('.datetime24-shell')));
        picker.querySelector('.datetime24-confirm').addEventListener('click', () => confirm(input));
        return picker;
    }

    function enhance(input) {
        if (!input || input.dataset.datetime24Enhanced === 'true') return;

        // [nam][Flow] Giữ input datetime-local gốc cho required/min/max và model binding; chỉ ẩn khỏi UI.
        const originalParent = input.parentElement;
        const shell = document.createElement('span');
        shell.className = 'datetime24-shell';
        input.parentNode.insertBefore(shell, input);

        const trigger = document.createElement('input');
        trigger.type = 'text';
        trigger.readOnly = true;
        trigger.className = `${input.className} datetime24-trigger`.trim();
        trigger.id = input.id ? `${input.id}-display` : '';
        trigger.setAttribute('aria-haspopup', 'dialog');
        trigger.setAttribute('aria-label', 'Chọn ngày giờ theo định dạng 24 giờ');
        shell.appendChild(trigger);
        shell.appendChild(input);

        if (!originalParent.querySelector(':scope > i')) {
            const icon = document.createElement('span');
            icon.className = 'datetime24-icon';
            icon.setAttribute('aria-hidden', 'true');
            shell.appendChild(icon);
        }
        shell.appendChild(buildPicker(input));

        input.dataset.datetime24Enhanced = 'true';
        input.classList.add('datetime24-native');
        input.tabIndex = -1;
        input.setAttribute('lang', 'vi-VN');
        trigger.addEventListener('click', () => open(input));
        trigger.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                open(input);
            }
        });
        input.addEventListener('focus', () => {
            input.blur();
            open(input);
        });
        input.addEventListener('input', () => refresh(input));
        input.addEventListener('change', () => refresh(input));
        refresh(input);
    }

    function enhanceAll(root) {
        (root || document).querySelectorAll(selector).forEach(enhance);
    }

    document.addEventListener('click', event => {
        if (activeShell && !activeShell.contains(event.target)) close(activeShell);
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') close(activeShell);
    });
    window.addEventListener('resize', () => {
        // [nam][Flow] Tính lại hướng mở khi viewport đổi để popup không tràn khỏi modal trên màn nhỏ.
        if (!activeShell) return;
        const picker = activeShell.querySelector('.datetime24-picker');
        if (picker && !picker.hidden) positionPicker(activeShell, picker);
    });

    window.DateTime24 = { enhance, enhanceAll, refresh, formatValue, open, close };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => enhanceAll(document));
    } else {
        enhanceAll(document);
    }
})();
