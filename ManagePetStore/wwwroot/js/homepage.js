(function () {
    'use strict';

    const overlay = document.getElementById('hotelModalOverlay');
    const closeBtn = document.getElementById('closeHotelModal');
    const form = document.getElementById('hotelBookingForm');
    const petSelect = document.getElementById('hotelPet');
    const roomSelect = document.getElementById('hotelRoomType');
    const checkIn = document.getElementById('hotelCheckIn');
    const checkOut = document.getElementById('hotelCheckOut');
    const totalEl = document.getElementById('hotelTotal');
    const nightsEl = document.getElementById('hotelNights');
    const foodOption = document.getElementById('hotelFoodOption');
    const foodDescription = document.getElementById('hotelFoodDescription');
    const foodPricingNote = document.getElementById('hotelFoodPricingNote');
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

    function getSelectedPetWeight() {
        const selectedPet = petSelect?.options[petSelect.selectedIndex];
        const weight = Number.parseFloat(selectedPet?.dataset.weight || '');
        return Number.isFinite(weight) && weight > 0 ? weight : null;
    }

    function getWeightMultiplier(weight) {
        if (!weight) return 0;
        if (weight <= 5) return 1;
        if (weight <= 15) return 1.25;
        if (weight <= 30) return 1.5;
        return 1.8;
    }

    function getWeightBand(weight) {
        if (!weight) return '';
        if (weight <= 5) return 'nhỏ (≤5kg)';
        if (weight <= 15) return 'trung bình (>5–15kg)';
        if (weight <= 30) return 'lớn (>15–30kg)';
        return 'rất lớn (>30kg)';
    }

    function getFoodQuote() {
        const selectedFood = foodOption?.options[foodOption.selectedIndex];
        const basePrice = Number.parseFloat(selectedFood?.dataset.price || '0') || 0;
        const weight = getSelectedPetWeight();
        const multiplier = getWeightMultiplier(weight);
        const nights = Math.max(1, getNights());
        const pricePerDay = basePrice > 0 && multiplier > 0
            ? Math.ceil((basePrice * multiplier) / 1000) * 1000
            : 0;

        return {
            weight,
            multiplier,
            weightBand: getWeightBand(weight),
            pricePerDay,
            inventoryUnits: multiplier > 0 ? Math.ceil(nights * multiplier) : 0,
            nights,
            total: pricePerDay * nights
        };
    }

    function updateTotal() {
        if (!roomSelect || !totalEl) return;

        const selected = roomSelect.options[roomSelect.selectedIndex];
        const dailyPrice = parseFloat(selected?.dataset.price) || 0;
        const nights = Math.max(1, getNights());
        const discountPercent = parseFloat(form?.dataset.discountPercent) || 0;

        const subtotal = dailyPrice * nights;
        const discounted = subtotal * (1 - discountPercent / 100);
        const foodTotal = getFoodQuote().total;
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
        const quote = getFoodQuote();
        if (foodDescription && selectedFood?.value) {
            const detail = selectedFood.dataset.description || 'Gói thức ăn Hotel';
            foodDescription.textContent = `${detail} · Đơn vị kho: ${selectedFood.dataset.unit || 'suất/ngày'} · Còn ${selectedFood.dataset.available || 0} suất chuẩn`;
        }
        if (foodPricingNote) {
            if (!selectedFood?.value) {
                foodPricingNote.textContent = 'Chọn gói thức ăn để xem giá theo cân nặng.';
            } else if (!quote.weight) {
                foodPricingNote.textContent = 'Hồ sơ pet phải có cân nặng hợp lệ để tính giá tạm tính.';
            } else {
                foodPricingNote.textContent = `Tạm tính theo hồ sơ ${quote.weight.toLocaleString('vi-VN')}kg · nhóm ${quote.weightBand} · hệ số ${quote.multiplier.toLocaleString('vi-VN')} · ${formatCurrency(quote.pricePerDay)}/ngày · dự kiến dùng ${quote.inventoryUnits} suất chuẩn. Giá cuối cùng xác nhận khi tiếp nhận.`;
            }
        }
        validateFoodAvailability();
        updateTotal();
    }

    function validateFoodAvailability() {
        if (!foodOption) return true;
        foodOption.setCustomValidity('');
        petSelect?.setCustomValidity('');
        const selectedFood = foodOption.options[foodOption.selectedIndex];
        if (!selectedFood?.value) return true;

        const availableUnits = Number(selectedFood.dataset.available || 0);
        const quote = getFoodQuote();
        if (petSelect?.value && !quote.weight) {
            petSelect.setCustomValidity('Hồ sơ pet phải có cân nặng hợp lệ trước khi đặt Hotel.');
            return false;
        }
        const requiredUnits = quote.inventoryUnits;
        if (availableUnits < requiredUnits) {
            foodOption.setCustomValidity(`Gói này chỉ còn ${availableUnits} suất chuẩn, không đủ ${requiredUnits} suất theo cân nặng và thời gian lưu trú.`);
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
    if (petSelect) petSelect.addEventListener('change', updateFoodPlan);
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
