(function () {
    var overlay = document.getElementById('reviewModalOverlay');
    if (!overlay) {
        return;
    }

    var form = document.getElementById('reviewForm');
    var orderIdInput = document.getElementById('reviewOrderId');
    var orderLabel = document.getElementById('reviewModalOrderLabel');
    var ratingInput = document.getElementById('reviewRating');
    var commentInput = document.getElementById('reviewComment');
    var skipBtn = document.getElementById('reviewSkipBtn');
    var stars = document.querySelectorAll('#reviewStars .co-review-star');

    function setRating(value) {
        var rating = Math.max(1, Math.min(5, value));
        ratingInput.value = String(rating);

        stars.forEach(function (star) {
            var starValue = parseInt(star.getAttribute('data-star'), 10);
            star.classList.toggle('active', starValue <= rating);
        });
    }

    function openModal(orderId, displayId) {
        orderIdInput.value = orderId;
        orderLabel.textContent = displayId;
        commentInput.value = '';
        setRating(5);
        overlay.hidden = false;
        document.body.style.overflow = 'hidden';
        commentInput.focus();
    }

    function closeModal() {
        overlay.hidden = true;
        document.body.style.overflow = '';
    }

    document.querySelectorAll('.js-open-review').forEach(function (button) {
        button.addEventListener('click', function () {
            openModal(button.getAttribute('data-order-id'), button.getAttribute('data-display-id'));
        });
    });

    stars.forEach(function (star) {
        star.addEventListener('click', function () {
            setRating(parseInt(star.getAttribute('data-star'), 10));
        });
    });

    skipBtn.addEventListener('click', closeModal);

    overlay.addEventListener('click', function (event) {
        if (event.target === overlay) {
            closeModal();
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && !overlay.hidden) {
            closeModal();
        }
    });

    var toast = document.getElementById('cpToast');
    if (toast) {
        setTimeout(function () {
            toast.style.opacity = '0';
            setTimeout(function () { toast.remove(); }, 300);
        }, 3500);
    }

    // Cancel Order Modal Logic
    var cancelModal = document.getElementById('cancelOrderModal');
    if (cancelModal) {
        var cancelOrderIdField = document.getElementById('cancelOrderIdField');
        var cancelDisplayOrderId = document.getElementById('cancelDisplayOrderId');
        var cancelReasonText = document.getElementById('cancelReasonText');
        var cancelReturnActionField = document.getElementById('cancelReturnActionField');
        var cancelForm = document.getElementById('cancelOrderForm');

        var presetRadios = document.querySelectorAll('input[name="cancelReasonPreset"]');

        function updateTextArea() {
            var selectedRadio = document.querySelector('input[name="cancelReasonPreset"]:checked');
            if (selectedRadio) {
                if (selectedRadio.value === "Khác") {
                    cancelReasonText.value = "";
                    cancelReasonText.required = true;
                    cancelReasonText.placeholder = "Vui lòng nhập lý do hủy đơn chi tiết tại đây...";
                    cancelReasonText.readOnly = false;
                    cancelReasonText.focus();
                } else {
                    cancelReasonText.value = selectedRadio.value;
                    cancelReasonText.required = false;
                    cancelReasonText.placeholder = "";
                    cancelReasonText.readOnly = true;
                }
            }
        }

        presetRadios.forEach(function (radio) {
            radio.addEventListener('change', updateTextArea);
        });

        function openCancelModal(orderId, displayId) {
            cancelOrderIdField.value = orderId;
            cancelDisplayOrderId.textContent = displayId;
            
            // Auto detect return action
            if (window.location.pathname.toLowerCase().indexOf('/details') !== -1) {
                cancelReturnActionField.value = 'Details';
            } else {
                cancelReturnActionField.value = 'Index';
            }

            // Set default preset
            var firstRadio = document.querySelector('input[name="cancelReasonPreset"]');
            if (firstRadio) {
                firstRadio.checked = true;
            }
            updateTextArea();

            cancelModal.style.display = 'flex';
            document.body.style.overflow = 'hidden';
        }

        function closeCancelModal() {
            cancelModal.style.display = 'none';
            document.body.style.overflow = '';
        }

        document.querySelectorAll('.js-open-cancel-order').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                openCancelModal(btn.getAttribute('data-order-id'), btn.getAttribute('data-display-id'));
            });
        });

        document.querySelectorAll('.js-close-cancel').forEach(function (btn) {
            btn.addEventListener('click', closeCancelModal);
        });

        cancelModal.addEventListener('click', function (event) {
            if (event.target === cancelModal) {
                closeCancelModal();
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && cancelModal.style.display === 'flex') {
                closeCancelModal();
            }
        });

        cancelForm.addEventListener('submit', function (e) {
            var selectedRadio = document.querySelector('input[name="cancelReasonPreset"]:checked');
            if (selectedRadio && selectedRadio.value === "Khác") {
                if (!cancelReasonText.value.trim()) {
                    e.preventDefault();
                    alert("Vui lòng nhập lý do hủy đơn hàng chi tiết.");
                    cancelReasonText.focus();
                }
            }
        });
    }
})();
