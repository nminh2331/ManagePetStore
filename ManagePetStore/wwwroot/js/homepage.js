(function () {
    'use strict';

    const overlay = document.getElementById('hotelModalOverlay');
    const closeBtn = document.getElementById('closeHotelModal');
    const form = document.getElementById('hotelBookingForm');
    const petSelect = document.getElementById('hotelPet');
    const roomSelect = document.getElementById('hotelRoomType');
    const cageSelect = document.getElementById('hotelCage');
    const roomTypeSummary = document.getElementById('hotelRoomTypeSummary');
    const cageAvailability = document.getElementById('hotelCageAvailability');
    const checkIn = document.getElementById('hotelCheckIn');
    const checkOut = document.getElementById('hotelCheckOut');
    const totalEl = document.getElementById('hotelTotal');
    const nightsEl = document.getElementById('hotelNights');
    const foodOption = document.getElementById('hotelFoodOption');
    const foodPricingNote = document.getElementById('hotelFoodPricingNote');
    const foodTotalEl = document.getElementById('hotelFoodTotal');
    const dayInMilliseconds = 24 * 60 * 60 * 1000;
    let cageAvailabilitySequence = 0;

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

    function formatDateTimeInput(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    function parseDateTimeInput(value) {
        if (!value) return null;
        const parsed = new Date(value);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    function getChargeableDays() {
        const start = parseDateTimeInput(checkIn?.value);
        const end = parseDateTimeInput(checkOut?.value);

        if (!start || !end) return 0;
        return Math.ceil((end - start) / dayInMilliseconds);
    }

    function roundUpToQuarterHour(date) {
        const rounded = new Date(date);
        const currentMinutes = rounded.getMinutes();
        rounded.setSeconds(0, 0);
        rounded.setMinutes(currentMinutes + 15 - (currentMinutes % 15));
        return rounded;
    }

    function configureHotelDates() {
        if (!checkIn || !checkOut) return;

        const now = new Date();
        const earliestCheckIn = roundUpToQuarterHour(now);
        const latestCheckIn = new Date(earliestCheckIn);
        latestCheckIn.setDate(latestCheckIn.getDate() + 365);

        checkIn.min = formatDateTimeInput(earliestCheckIn);
        checkIn.max = formatDateTimeInput(latestCheckIn);

        if (!checkIn.value) {
            const tomorrow = new Date(earliestCheckIn);
            tomorrow.setDate(tomorrow.getDate() + 1);
            tomorrow.setHours(14, 0, 0, 0);
            checkIn.value = formatDateTimeInput(tomorrow);
        }

        updateCheckOutLimits();

        if (!checkOut.value) {
            const defaultCheckOut = parseDateTimeInput(checkIn.value);
            defaultCheckOut.setDate(defaultCheckOut.getDate() + 1);
            checkOut.value = formatDateTimeInput(defaultCheckOut);
        }
    }

    function updateCheckOutLimits() {
        if (!checkIn || !checkOut || !checkIn.value) return;

        const start = parseDateTimeInput(checkIn.value);
        const minimumCheckOut = new Date(start);
        const maximumCheckOut = new Date(start);
        minimumCheckOut.setMinutes(minimumCheckOut.getMinutes() + 15);
        maximumCheckOut.setDate(maximumCheckOut.getDate() + 90);

        checkOut.min = formatDateTimeInput(minimumCheckOut);
        checkOut.max = formatDateTimeInput(maximumCheckOut);

        const currentCheckOut = parseDateTimeInput(checkOut.value);
        if (currentCheckOut && (currentCheckOut <= start || currentCheckOut > maximumCheckOut)) {
            const defaultCheckOut = new Date(start);
            defaultCheckOut.setDate(defaultCheckOut.getDate() + 1);
            checkOut.value = formatDateTimeInput(defaultCheckOut);
        }
    }

    function validateHotelDates() {
        if (!checkIn || !checkOut) return true;

        checkIn.setCustomValidity('');
        checkOut.setCustomValidity('');

        const now = new Date();
        const start = parseDateTimeInput(checkIn.value);
        const end = parseDateTimeInput(checkOut.value);
        if (start && start < now) {
            checkIn.setCustomValidity('Thời gian nhận phòng không được ở trong quá khứ.');
            return false;
        }

        if (start && end && end <= start) {
            checkOut.setCustomValidity('Thời gian trả phòng phải sau thời gian nhận phòng.');
            return false;
        }

        if (start && end && end - start > 90 * dayInMilliseconds) {
            checkOut.setCustomValidity('Mỗi lượt đặt phòng không được vượt quá 90 ngày.');
            return false;
        }

        return true;
    }

    function updateRoomTypeSummary() {
        if (!roomSelect || !roomTypeSummary) return;
        const selected = roomSelect.options[roomSelect.selectedIndex];
        if (!selected?.value) {
            roomTypeSummary.textContent = 'Chọn loại phòng để xem kích thước và tiện ích.';
            return;
        }

        const amenities = [];
        if (selected.dataset.ac === 'true') amenities.push('Điều hòa');
        if (selected.dataset.camera === 'true') amenities.push('Camera');
        const serviceSummary = selected.dataset.serviceSummary || 'Chăm sóc tiêu chuẩn';
        roomTypeSummary.textContent = `${selected.dataset.code} · ${selected.dataset.size} · 1 pet · ${amenities.length ? amenities.join(', ') : 'Tiện ích cơ bản'} · Dịch vụ: ${serviceSummary}`;
    }

    async function loadAvailableCages() {
        if (!cageSelect) return;
        const sequence = ++cageAvailabilitySequence;
        cageSelect.setCustomValidity('');

        if (!roomSelect?.value || !checkIn?.value || !checkOut?.value || !validateHotelDates()) {
            cageSelect.disabled = true;
            cageSelect.innerHTML = '<option value="">Chọn loại phòng và thời gian trước</option>';
            if (cageAvailability) cageAvailability.textContent = 'Danh sách được kiểm tra theo đúng thời gian nhận và trả.';
            return;
        }

        cageSelect.disabled = true;
        cageSelect.innerHTML = '<option value="">Đang kiểm tra chuồng trống...</option>';
        if (cageAvailability) cageAvailability.textContent = 'Đang kiểm tra lịch đặt trùng...';

        const query = new URLSearchParams({
            roomTypeId: roomSelect.value,
            checkInDate: checkIn.value,
            checkOutDate: checkOut.value
        });

        try {
            const response = await fetch('/Customer/HotelBooking/AvailableCages?' + query.toString(), {
                headers: { 'Accept': 'application/json' }
            });
            const data = await response.json();
            if (sequence !== cageAvailabilitySequence) return;
            if (!response.ok || !data.success) throw new Error(data.message || 'Không thể kiểm tra chuồng trống.');

            cageSelect.disabled = false;
            cageSelect.innerHTML = '<option value="">Chọn chuồng</option>';
            data.cages.forEach(function (cage) {
                const option = document.createElement('option');
                option.value = cage.cageId;
                option.textContent = `Chuồng ${cage.cageId}`;
                cageSelect.appendChild(option);
            });

            if (data.cages.length === 0) {
                cageSelect.setCustomValidity('Loại phòng này đã hết chuồng trống trong khoảng thời gian đã chọn.');
                if (cageAvailability) cageAvailability.textContent = 'Không còn chuồng trống. Hãy đổi loại phòng hoặc thời gian.';
            } else if (cageAvailability) {
                cageAvailability.textContent = `Có ${data.cages.length} chuồng còn trống trong khoảng thời gian này.`;
            }
        } catch (error) {
            if (sequence !== cageAvailabilitySequence) return;
            cageSelect.disabled = false;
            cageSelect.innerHTML = '<option value="">Không thể tải danh sách chuồng</option>';
            cageSelect.setCustomValidity(error.message || 'Không thể kiểm tra chuồng trống.');
            if (cageAvailability) cageAvailability.textContent = error.message || 'Không thể kiểm tra chuồng trống.';
        }
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
        const nights = Math.max(1, getChargeableDays());
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
        const nights = Math.max(1, getChargeableDays());
        const discountPercent = parseFloat(form?.dataset.discountPercent) || 0;

        const subtotal = dailyPrice * nights;
        const discounted = subtotal * (1 - discountPercent / 100);
        const foodTotal = getFoodQuote().total;
        totalEl.textContent = formatCurrency(discounted + foodTotal);
        if (foodTotalEl) foodTotalEl.textContent = formatCurrency(foodTotal);
        if (nightsEl) nightsEl.textContent = nights + ' ngày';
    }

    function updateFoodPlan() {
        const selectedFood = foodOption?.options[foodOption.selectedIndex];
        const quote = getFoodQuote();
        if (foodPricingNote) {
            if (!selectedFood?.value) {
                foodPricingNote.textContent = 'Chọn gói thức ăn để xem giá theo cân nặng.';
            } else if (!quote.weight) {
                foodPricingNote.textContent = 'Hồ sơ pet phải có cân nặng hợp lệ để tính giá tạm tính.';
            } else {
                foodPricingNote.textContent = `Cân nặng hồ sơ ${quote.weight.toLocaleString('vi-VN')}kg · ${formatCurrency(quote.pricePerDay)}/ngày · tổng gói ăn ${formatCurrency(quote.total)}. Giá cuối cùng xác nhận khi tiếp nhận.`;
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
            petSelect.setCustomValidity('Hồ sơ pet phải có cân nặng hợp lệ trước khi đặt chuồng.');
            return false;
        }
        const requiredUnits = quote.inventoryUnits;
        if (availableUnits < requiredUnits) {
            foodOption.setCustomValidity(`Gói này chỉ còn ${availableUnits} suất chuẩn, không đủ ${requiredUnits} suất theo cân nặng và thời gian lưu trú.`);
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

    updateRoomTypeSummary();
    loadAvailableCages();
    if (roomSelect) roomSelect.addEventListener('change', function () {
        updateRoomTypeSummary();
        updateTotal();
        loadAvailableCages();
    });
    if (petSelect) petSelect.addEventListener('change', updateFoodPlan);
    if (foodOption) foodOption.addEventListener('change', updateFoodPlan);
    updateFoodPlan();
    if (checkIn) {
        checkIn.addEventListener('change', function () {
            checkIn.setCustomValidity('');
            updateCheckOutLimits();
            validateHotelDates();
            validateFoodAvailability();
            updateTotal();
            loadAvailableCages();
        });
    }
    if (checkOut) {
        checkOut.addEventListener('change', function () {
            checkOut.setCustomValidity('');
            validateHotelDates();
            validateFoodAvailability();
            updateTotal();
            loadAvailableCages();
        });
    }

    if (form) {
        form.addEventListener('submit', function (e) {
            const hasValidDates = validateHotelDates();
            const hasAvailableFood = validateFoodAvailability();
            if (cageSelect && (!cageSelect.value || cageSelect.disabled)) {
                cageSelect.disabled = false;
                cageSelect.setCustomValidity('Vui lòng chọn một chuồng còn trống.');
            }
            if (!hasValidDates || !hasAvailableFood || !form.checkValidity()) {
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

    // Highlight "Dịch vụ Spa" category card when clicked or when URL hash is #spa-services
    const spaCategoryCard = document.querySelector('.ps-category-card[href="#spa-services"]');
    if (spaCategoryCard) {
        spaCategoryCard.addEventListener('click', function () {
            document.querySelectorAll('.ps-category-card').forEach(function (card) {
                card.classList.remove('active');
            });
            spaCategoryCard.classList.add('active');
        });

        if (window.location.hash === '#spa-services' || window.location.href.indexOf('#spa-services') !== -1) {
            document.querySelectorAll('.ps-category-card').forEach(function (card) {
                card.classList.remove('active');
            });
            spaCategoryCard.classList.add('active');
        }
    }
})();
