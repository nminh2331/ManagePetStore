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

    document.querySelectorAll('.co-table-row[data-href]').forEach(function (row) {
        row.addEventListener('click', function () {
            window.location.href = row.getAttribute('data-href');
        });
    });

    var toast = document.getElementById('cpToast');
    if (toast) {
        setTimeout(function () {
            toast.style.opacity = '0';
            setTimeout(function () { toast.remove(); }, 300);
        }, 3500);
    }
})();
