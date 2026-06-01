(function () {
    'use strict';

    const overlay = document.getElementById('hotelModalOverlay');
    const closeBtn = document.getElementById('closeHotelModal');
    const form = document.getElementById('hotelBookingForm');
    const roomSelect = document.getElementById('hotelRoomType');
    const checkIn = document.getElementById('hotelCheckIn');
    const checkOut = document.getElementById('hotelCheckOut');
    const totalEl = document.getElementById('hotelTotal');

    function openHotelModal() {
        if (overlay) {
            overlay.classList.add('active');
            document.body.style.overflow = 'hidden';
            updateTotal();
        }
    }

    function closeHotelModal() {
        if (overlay) {
            overlay.classList.remove('active');
            document.body.style.overflow = '';
        }
    }

    function formatCurrency(amount) {
        return Math.round(amount).toLocaleString('vi-VN') + 'đ';
    }

    function updateTotal() {
        if (!roomSelect || !totalEl) return;

        const selected = roomSelect.options[roomSelect.selectedIndex];
        const dailyPrice = parseFloat(selected.dataset.price) || 500000;
        let nights = 1;

        if (checkIn && checkOut && checkIn.value && checkOut.value) {
            const start = new Date(checkIn.value);
            const end = new Date(checkOut.value);
            const diff = Math.ceil((end - start) / (1000 * 60 * 60 * 24));
            if (diff > 0) nights = diff;
        }

        const subtotal = dailyPrice * nights;
        const discounted = subtotal * 0.9;
        totalEl.textContent = formatCurrency(discounted);
    }

    document.querySelectorAll('.open-hotel-modal, #btnBookHotel, .ps-hotel-banner').forEach(function (el) {
        el.addEventListener('click', function (e) {
            e.preventDefault();
            openHotelModal();
        });
    });

    if (closeBtn) {
        closeBtn.addEventListener('click', closeHotelModal);
    }

    if (overlay) {
        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) closeHotelModal();
        });
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeHotelModal();
    });

    if (roomSelect) roomSelect.addEventListener('change', updateTotal);
    if (checkIn) checkIn.addEventListener('change', updateTotal);
    if (checkOut) checkOut.addEventListener('change', updateTotal);

    if (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            alert('Đặt phòng thành công! Chúng tôi sẽ liên hệ xác nhận sớm nhất.');
            closeHotelModal();
        });
    }

    // Product detail thumbnail switcher
    const mainImage = document.getElementById('pdMainImage');
    document.querySelectorAll('.ps-pd-thumb').forEach(function (thumb) {
        thumb.addEventListener('click', function () {
            document.querySelectorAll('.ps-pd-thumb').forEach(function (t) {
                t.classList.remove('active');
            });
            thumb.classList.add('active');
            if (mainImage) {
                mainImage.src = thumb.dataset.src;
            }
        });
    });

    // Quantity controls
    const qtyValue = document.getElementById('pdQty');
    const qtyMinus = document.getElementById('pdQtyMinus');
    const qtyPlus = document.getElementById('pdQtyPlus');

    if (qtyMinus && qtyValue) {
        qtyMinus.addEventListener('click', function () {
            const val = parseInt(qtyValue.textContent, 10);
            if (val > 1) qtyValue.textContent = val - 1;
        });
    }

    if (qtyPlus && qtyValue) {
        qtyPlus.addEventListener('click', function () {
            const val = parseInt(qtyValue.textContent, 10);
            const max = parseInt(qtyValue.dataset.max, 10) || 99;
            if (val < max) qtyValue.textContent = val + 1;
        });
    }
})();
