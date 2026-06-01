(function () {
    'use strict';

    function setupImagePreview(inputId, previewId, defaultSrc) {
        var input = document.getElementById(inputId);
        var preview = document.getElementById(previewId);
        if (!input || !preview) return;

        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            if (!file) {
                if (defaultSrc) {
                    preview.src = defaultSrc;
                    preview.classList.add('visible');
                } else {
                    preview.classList.remove('visible');
                }
                return;
            }

            var reader = new FileReader();
            reader.onload = function (e) {
                preview.src = e.target.result;
                preview.classList.add('visible');
            };
            reader.readAsDataURL(file);
        });
    }

    function openModal(id) {
        var modal = document.getElementById(id);
        if (modal) {
            modal.classList.add('active');
            document.body.style.overflow = 'hidden';
        }
    }

    function closeModal(id) {
        var modal = document.getElementById(id);
        if (modal) {
            modal.classList.remove('active');
            document.body.style.overflow = '';
        }
    }

    document.querySelectorAll('[data-open-modal]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            openModal(btn.getAttribute('data-open-modal'));
        });
    });

    document.querySelectorAll('[data-close-modal]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            closeModal(btn.getAttribute('data-close-modal'));
        });
    });

    document.querySelectorAll('.cp-modal-overlay').forEach(function (overlay) {
        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) {
                overlay.classList.remove('active');
                document.body.style.overflow = '';
            }
        });
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            document.querySelectorAll('.cp-modal-overlay.active').forEach(function (overlay) {
                overlay.classList.remove('active');
            });
            document.body.style.overflow = '';
        }
    });

    setupImagePreview('createAvatarFile', 'createAvatarPreview', null);
    setupImagePreview('editAvatarFile', 'editAvatarPreview', document.getElementById('editAvatarPreview')?.dataset.defaultSrc || '');

    document.querySelectorAll('[data-edit-pet]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var petId = btn.getAttribute('data-edit-pet');
            window.location.href = '/Customer/Pet?editId=' + petId;
        });
    });

    document.querySelectorAll('[data-confirm-delete]').forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            var name = btn.getAttribute('data-confirm-delete');
            if (!confirm('Bạn có chắc muốn xóa hồ sơ bé ' + name + '?')) {
                e.preventDefault();
            }
        });
    });

    var toast = document.getElementById('cpToast');
    if (toast) {
        setTimeout(function () {
            toast.style.opacity = '0';
            toast.style.transition = 'opacity 0.4s';
            setTimeout(function () { toast.remove(); }, 400);
        }, 4000);
    }

    if (window.petPageOpenCreate) openModal('createPetModal');
    if (window.petPageOpenEdit) openModal('editPetModal');
})();
