(function () {
    'use strict';

    const overlay = document.getElementById('hotelModalOverlay');
    const closeBtn = document.getElementById('closeHotelModal');
    const form = document.getElementById('hotelBookingForm');
    const roomSelect = document.getElementById('hotelRoomType');
    const checkIn = document.getElementById('hotelCheckIn');
    const checkOut = document.getElementById('hotelCheckOut');
    const totalEl = document.getElementById('hotelTotal');
    const nightsEl = document.getElementById('hotelNights');
    const foodOption = document.getElementById('hotelFoodOption');
    const foodDescription = document.getElementById('hotelFoodDescription');
    const feedingScheduleWrap = document.getElementById('hotelFeedingScheduleWrap');
    const feedingSchedule = document.getElementById('hotelFeedingSchedule');
    const foodTotalEl = document.getElementById('hotelFoodTotal');
    const dayInMilliseconds = 24 * 60 * 60 * 1000;

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

    function formatDateInput(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return year + '-' + month + '-' + day;
    }

    function parseDateInput(value) {
        return value ? new Date(value + 'T00:00:00') : null;
    }

    function getNights() {
        const start = parseDateInput(checkIn?.value);
        const end = parseDateInput(checkOut?.value);

        if (!start || !end) return 0;
        return Math.round((end - start) / dayInMilliseconds);
    }

    function configureHotelDates() {
        if (!checkIn || !checkOut) return;

        const today = new Date();
        today.setHours(0, 0, 0, 0);
        const latestCheckIn = new Date(today);
        latestCheckIn.setDate(latestCheckIn.getDate() + 365);

        checkIn.min = formatDateInput(today);
        checkIn.max = formatDateInput(latestCheckIn);

        if (!checkIn.value) {
            const tomorrow = new Date(today);
            tomorrow.setDate(tomorrow.getDate() + 1);
            checkIn.value = formatDateInput(tomorrow);
        }

        updateCheckOutLimits();

        if (!checkOut.value) {
            const defaultCheckOut = parseDateInput(checkIn.value);
            defaultCheckOut.setDate(defaultCheckOut.getDate() + 1);
            checkOut.value = formatDateInput(defaultCheckOut);
        }
    }

    function updateCheckOutLimits() {
        if (!checkIn || !checkOut || !checkIn.value) return;

        const start = parseDateInput(checkIn.value);
        const minimumCheckOut = new Date(start);
        const maximumCheckOut = new Date(start);
        minimumCheckOut.setDate(minimumCheckOut.getDate() + 1);
        maximumCheckOut.setDate(maximumCheckOut.getDate() + 90);

        checkOut.min = formatDateInput(minimumCheckOut);
        checkOut.max = formatDateInput(maximumCheckOut);

        if (checkOut.value && getNights() <= 0) {
            checkOut.value = formatDateInput(minimumCheckOut);
        }
    }

    function validateHotelDates() {
        if (!checkIn || !checkOut) return true;

        checkIn.setCustomValidity('');
        checkOut.setCustomValidity('');

        const today = new Date();
        today.setHours(0, 0, 0, 0);
        const start = parseDateInput(checkIn.value);
        const nights = getNights();

        if (start && start < today) {
            checkIn.setCustomValidity('Ngày nhận phòng không được ở trong quá khứ.');
            return false;
        }

        if (checkIn.value && checkOut.value && nights <= 0) {
            checkOut.setCustomValidity('Ngày trả phòng phải sau ngày nhận phòng.');
            return false;
        }

        if (nights > 90) {
            checkOut.setCustomValidity('Mỗi lượt đặt phòng không được vượt quá 90 đêm.');
            return false;
        }

        return true;
    }

    function updateTotal() {
        if (!roomSelect || !totalEl) return;

        const selected = roomSelect.options[roomSelect.selectedIndex];
        const dailyPrice = parseFloat(selected?.dataset.price) || 0;
        const nights = Math.max(1, getNights());
        const discountPercent = parseFloat(form?.dataset.discountPercent) || 0;

        const subtotal = dailyPrice * nights;
        const discounted = subtotal * (1 - discountPercent / 100);
        const selectedFood = foodOption?.options[foodOption.selectedIndex];
        const foodDailyPrice = parseFloat(selectedFood?.dataset.price) || 0;
        const foodTotal = foodDailyPrice * nights;
        totalEl.textContent = formatCurrency(discounted + foodTotal);
        if (foodTotalEl) foodTotalEl.textContent = formatCurrency(foodTotal);
        if (nightsEl) nightsEl.textContent = nights + ' đêm';
    }

    function updateFoodPlan() {
        if (feedingSchedule) {
            feedingSchedule.disabled = false;
            validateFeedingSchedule();
        }
        const selectedFood = foodOption?.options[foodOption.selectedIndex];
        if (foodDescription && selectedFood?.value) {
            const detail = selectedFood.dataset.description || 'Gói thức ăn Hotel';
            foodDescription.textContent = `${detail} · Đơn vị kho: ${selectedFood.dataset.unit || 'suất/ngày'} · Còn ${selectedFood.dataset.available || 0} suất chưa được đặt`;
        }
        validateFoodAvailability();
        updateTotal();
    }

    function validateFoodAvailability() {
        if (!foodOption) return true;
        foodOption.setCustomValidity('');
        const selectedFood = foodOption.options[foodOption.selectedIndex];
        if (!selectedFood?.value) return true;

        const availableUnits = Number(selectedFood.dataset.available || 0);
        const requiredUnits = Math.max(1, getNights());
        if (availableUnits < requiredUnits) {
            foodOption.setCustomValidity(`Gói này chỉ còn ${availableUnits} suất, không đủ cho ${requiredUnits} ngày lưu trú.`);
            return false;
        }
        return true;
    }

    function validateFeedingSchedule() {
        if (!feedingSchedule || feedingSchedule.disabled) return true;

        feedingSchedule.setCustomValidity('');
        const value = feedingSchedule.value.trim();
        if (!value) return true;

        const mealTimes = value
            .split(/\s*(?:,|;|và|and)\s*/iu)
            .filter(Boolean);
        const timePattern = /^(?:[01]?\d|2[0-3]):[0-5]\d$/;

        if (mealTimes.length === 0 || mealTimes.some(time => !timePattern.test(time))) {
            feedingSchedule.setCustomValidity('Giờ ăn phải có dạng HH:mm, ví dụ 07:00 và 18:00.');
            return false;
        }

        const outsideAllowedHours = mealTimes.some(time => {
            const [hours, minutes] = time.split(':').map(Number);
            const totalMinutes = hours * 60 + minutes;
            return totalMinutes < 7 * 60 || totalMinutes > 20 * 60;
        });

        if (outsideAllowedHours) {
            feedingSchedule.setCustomValidity('Giờ ăn chỉ được trong khoảng 07:00 đến 20:00.');
            return false;
        }

        return true;
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

    configureHotelDates();
    updateTotal();

    if (roomSelect) roomSelect.addEventListener('change', updateTotal);
    if (foodOption) foodOption.addEventListener('change', updateFoodPlan);
    if (feedingSchedule) feedingSchedule.addEventListener('input', validateFeedingSchedule);
    updateFoodPlan();
    if (checkIn) {
        checkIn.addEventListener('change', function () {
            checkIn.setCustomValidity('');
            updateCheckOutLimits();
            validateHotelDates();
            validateFoodAvailability();
            updateTotal();
        });
    }
    if (checkOut) {
        checkOut.addEventListener('change', function () {
            checkOut.setCustomValidity('');
            validateHotelDates();
            validateFoodAvailability();
            updateTotal();
        });
    }

    if (form) {
        form.addEventListener('submit', function (e) {
            const hasValidDates = validateHotelDates();
            const hasValidFeedingSchedule = validateFeedingSchedule();
            const hasAvailableFood = validateFoodAvailability();
            if (!hasValidDates || !hasValidFeedingSchedule || !hasAvailableFood || !form.checkValidity()) {
                e.preventDefault();
                form.reportValidity();
            }
        });
    }

    if (new URLSearchParams(window.location.search).get('hotel') === 'book') {
        openHotelModal();
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

    if (window.location.search.indexOf('search=') !== -1 || window.location.search.indexOf('category=') !== -1) {
        var resultSection = document.getElementById('best-sellers');
        if (resultSection) {
            resultSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }
})();
