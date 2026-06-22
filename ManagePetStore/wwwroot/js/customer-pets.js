(function () {
    'use strict';

    var maxImageBytes = 20 * 1024 * 1024;
    var allowedImageExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.tiff', '.tif', '.svg'];
    var lettersOnlyPattern = /^[\p{L}\s]+$/u;
    var weightPattern = /^\d+(\.\d+)?$/;
    var validatedFields = ['name', 'species', 'breed', 'dateOfBirth', 'weight', 'avatarFile'];

    function isLettersOnly(value) {
        return typeof value === 'string' && value.trim().length > 0 && lettersOnlyPattern.test(value.trim());
    }

    function validateName(value) {
        if (!isLettersOnly(value)) {
            return 'Tên thú cưng phải có ít nhất 1 ký tự, chỉ được chứa chữ cái.';
        }
        return '';
    }

    function validateSpecies(value) {
        if (!isLettersOnly(value)) {
            return ;
        }
        return '';
    }

    function validateBreed(value) {
        if (!isLettersOnly(value)) {
            return 'Giống (breed) phải có ít nhất 1 ký tự, chỉ được chứa chữ cái.';
        }
        return '';
    }

    function validateDateOfBirth(value) {
        if (!value) {
            return 'Vui lòng chọn ngày sinh.';
        }

        var birthDate = new Date(value + 'T00:00:00');
        if (Number.isNaN(birthDate.getTime())) {
            return 'Ngày sinh không hợp lệ.';
        }

        var today = new Date();
        today.setHours(0, 0, 0, 0);

        if (birthDate > today) {
            return 'Ngày sinh phải nhỏ hơn thời gian hiện tại.';
        }

        if (birthDate.getFullYear() < 2000) {
            return 'Ngày sinh phải từ năm 2000 đến nay.';
        }

        return '';
    }

    function validateWeight(value) {
        var normalized = (value || '').trim().replace(',', '.');
        if (!weightPattern.test(normalized) || Number(normalized) <= 0) {
            return 'Cân nặng phải là số lớn hơn 0, không chứa chữ cái hoặc ký tự đặc biệt , và có ít nhất 1 kí tự ';
        }
        return '';
    }

    function validateImageFile(file) {
        if (!file) {
            return '';
        }

        if (file.size > maxImageBytes) {
            return 'Ảnh avatar không được vượt quá 20MB.';
        }

        var fileName = file.name.toLowerCase();
        var extension = fileName.includes('.') ? fileName.substring(fileName.lastIndexOf('.')) : '';
        if (!allowedImageExtensions.includes(extension)) {
            return 'Ảnh chỉ được phép định dạng JPG, JPEG, PNG, GIF, TIFF, SVG.';
        }

        return '';
    }

    function getFieldValue(form, fieldName) {
        var input = form.querySelector('[name="' + fieldName + '"]');
        if (!input) {
            return '';
        }

        if (fieldName === 'avatarFile') {
            return input.files && input.files[0] ? input.files[0] : null;
        }

        return input.value || '';
    }

    function validateField(form, fieldName) {
        var value = getFieldValue(form, fieldName);

        switch (fieldName) {
            case 'name':
                return validateName(value);
            case 'species':
                return validateSpecies(value);
            case 'breed':
                return validateBreed(value);
            case 'dateOfBirth':
                return validateDateOfBirth(value);
            case 'weight':
                return validateWeight(value);
            case 'avatarFile':
                return validateImageFile(value);
            default:
                return '';
        }
    }

    function setFieldError(form, fieldName, message) {
        var fieldWrap = form.querySelector('.cp-form-field[data-field="' + fieldName + '"]');
        if (!fieldWrap) {
            return;
        }

        var input = fieldWrap.querySelector('[name="' + fieldName + '"]');
        var errorEl = fieldWrap.querySelector('.cp-field-error');
        if (!input || !errorEl) {
            return;
        }

        if (message) {
            input.classList.add('is-invalid');
            errorEl.textContent = message;
            errorEl.classList.add('visible');
        } else {
            input.classList.remove('is-invalid');
            errorEl.textContent = '';
            errorEl.classList.remove('visible');
        }
    }

    function clearAllFieldErrors(form) {
        validatedFields.forEach(function (fieldName) {
            setFieldError(form, fieldName, '');
        });
    }

    function validateFormFields(form, fields, showErrors) {
        var isValid = true;
        var firstInvalid = null;

        fields.forEach(function (fieldName) {
            var message = validateField(form, fieldName);
            if (showErrors) {
                setFieldError(form, fieldName, message);
            }

            if (message) {
                isValid = false;
                if (!firstInvalid) {
                    firstInvalid = form.querySelector('.cp-form-field[data-field="' + fieldName + '"] [name="' + fieldName + '"]');
                }
            }
        });

        if (!isValid && firstInvalid) {
            firstInvalid.focus();
        }

        return isValid;
    }

    function showAllFieldErrors(form, serverErrors) {
        if (!form) {
            return false;
        }

        form.dataset.attempted = 'true';
        clearAllFieldErrors(form);

        if (serverErrors) {
            validatedFields.forEach(function (fieldName) {
                if (serverErrors[fieldName]) {
                    setFieldError(form, fieldName, serverErrors[fieldName]);
                }
            });
        }

        return validateFormFields(form, validatedFields, true);
    }

    function bindPetFormValidation(formId) {
        var form = document.getElementById(formId);
        if (!form) {
            return;
        }

        validatedFields.forEach(function (fieldName) {
            var input = form.querySelector('[name="' + fieldName + '"]');
            if (!input) {
                return;
            }

            var events = fieldName === 'avatarFile' || fieldName === 'dateOfBirth' || fieldName === 'species'
                ? ['change', 'input']
                : ['input'];

            events.forEach(function (eventName) {
                input.addEventListener(eventName, function () {
                    if (form.dataset.attempted !== 'true') {
                        return;
                    }

                    var message = validateField(form, fieldName);
                    setFieldError(form, fieldName, message);
                });
            });
        });

        form.addEventListener('submit', function (e) {
            if (form.dataset.allowSubmit === 'true') {
                form.dataset.allowSubmit = 'false';
                return;
            }

            e.preventDefault();
            e.stopPropagation();

            var isValid = showAllFieldErrors(form, null);
            if (isValid) {
                form.dataset.allowSubmit = 'true';
                form.requestSubmit();
            }
        });
    }

    function setupImagePreview(inputId, previewId, defaultSrc, formId) {
        var input = document.getElementById(inputId);
        var preview = document.getElementById(previewId);
        var form = document.getElementById(formId);
        if (!input || !preview) {
            return;
        }

        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            if (file && form) {
                var imageError = validateImageFile(file);
                if (form.dataset.attempted === 'true') {
                    setFieldError(form, 'avatarFile', imageError);
                }
                if (imageError) {
                    input.value = '';
                    if (defaultSrc) {
                        preview.src = defaultSrc;
                        preview.classList.add('visible');
                    } else {
                        preview.classList.remove('visible');
                    }
                    return;
                }
            } else if (form) {
                setFieldError(form, 'avatarFile', '');
            }

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

    setupImagePreview('createAvatarFile', 'createAvatarPreview', null, 'createPetForm');
    setupImagePreview('editAvatarFile', 'editAvatarPreview', document.getElementById('editAvatarPreview')?.dataset.defaultSrc || '', 'editPetForm');

    document.querySelectorAll('[data-edit-pet]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var petId = btn.getAttribute('data-edit-pet');
            window.location.href = '/CustomerPet?editId=' + petId;
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

    bindPetFormValidation('createPetForm');
    bindPetFormValidation('editPetForm');

    if (window.petPageOpenCreate) {
        openModal('createPetModal');
        if (window.petFieldErrors) {
            showAllFieldErrors(document.getElementById('createPetForm'), window.petFieldErrors);
        }
    }

    if (window.petPageOpenEdit) {
        openModal('editPetModal');
        if (window.petFieldErrors) {
            showAllFieldErrors(document.getElementById('editPetForm'), window.petFieldErrors);
        }
    }

    function viewPetMedicalRecords(petId, petName) {
        document.getElementById('medRecordsPetName').textContent = petName;
        var container = document.getElementById('customerMedicalRecordsContainer');
        container.innerHTML = '<div style="text-align: center; padding: 24px;"><i class="bi bi-arrow-repeat spin" style="font-size: 2rem; color: #f97316; display: inline-block; animation: spin 1s linear infinite;"></i><p style="color: #666; margin-top: 8px;">Đang tải lịch sử khám...</p></div>';
        
        openModal('customerMedicalRecordsModal');

        if (!document.getElementById('spinStyle')) {
            var style = document.createElement('style');
            style.id = 'spinStyle';
            style.innerHTML = '@keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }';
            document.head.appendChild(style);
        }

        $.ajax({
            url: '/CustomerPet/GetMedicalRecords',
            type: 'GET',
            data: { petId: petId },
            success: function (res) {
                if (res.success && res.data && res.data.length > 0) {
                    var html = '<div style="display: flex; flex-direction: column; gap: 16px;">';
                    res.data.forEach(function (rec) {
                        var healthClass = rec.healthStatus === 'Khỏe mạnh' ? 'bg-success' : (rec.healthStatus === 'Cần theo dõi' ? 'bg-warning' : 'bg-danger');
                        html += '<div style="border: 1px solid #eee; border-left: 4px solid #f97316; border-radius: 8px; padding: 12px 16px; background-color: #fff; box-shadow: 0 2px 8px rgba(0,0,0,0.02); text-align: left;">';
                        html += '  <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #f5f5f5; padding-bottom: 8px; margin-bottom: 8px;">';
                        html += '    <span style="font-weight: 700; color: #f97316; font-size: 0.85rem;"><i class="bi bi-calendar3"></i> ' + rec.dateCreated + '</span>';
                        html += '    <span class="badge ' + healthClass + '" style="font-size: 0.72rem; padding: 4px 8px; border-radius: 12px; color: white;">' + rec.healthStatus + '</span>';
                        html += '  </div>';
                        html += '  <div style="font-size: 0.82rem; color: #555; margin-bottom: 6px;">Cân nặng: <strong>' + rec.weight + ' kg</strong></div>';
                        html += '  <div style="margin-bottom: 6px;">';
                        html += '    <strong style="font-size: 0.82rem; color: #333;">Chẩn đoán / Triệu chứng:</strong>';
                        html += '    <p style="margin: 4px 0 0 0; font-size: 0.8rem; color: #666; background: #fafafa; padding: 6px 10px; border-radius: 6px; border-left: 3px solid #00c6ff;">' + rec.symptoms + '</p>';
                        html += '  </div>';
                        html += '  <div style="margin-bottom: 6px;">';
                        html += '    <strong style="font-size: 0.82rem; color: #333;">Điều trị / Kê đơn:</strong>';
                        html += '    <p style="margin: 4px 0 0 0; font-size: 0.8rem; color: #666; background: #fafafa; padding: 6px 10px; border-radius: 6px; border-left: 3px solid #2ecc71;">' + rec.treatment + '</p>';
                        html += '  </div>';

                        var hasSpecifics = false;
                        var specHtml = '<div style="margin-top: 8px; padding-top: 8px; border-top: 1px dashed #eee; font-size: 0.78rem; color: #777;"><div style="display: flex; flex-wrap: wrap; gap: 12px;">';
                        if (rec.vaccinationStatus || rec.parasitePrevention || rec.physicalCheck) {
                            hasSpecifics = true;
                            specHtml += '<div><strong>Tiêm chủng:</strong> ' + (rec.vaccinationStatus || 'N/A') + '</div>';
                            specHtml += '<div><strong>Ký sinh trùng:</strong> ' + (rec.parasitePrevention || 'N/A') + '</div>';
                            specHtml += '<div><strong>Thể chất:</strong> ' + (rec.physicalCheck || 'N/A') + '</div>';
                        } else if (rec.shellStatus || rec.rearingConditions || rec.abnormalSymptoms) {
                            hasSpecifics = true;
                            specHtml += '<div><strong>Tình trạng mai:</strong> ' + (rec.shellStatus || 'N/A') + '</div>';
                            specHtml += '<div><strong>ĐK nuôi:</strong> ' + (rec.rearingConditions || 'N/A') + '</div>';
                            specHtml += '<div><strong>Bất thường:</strong> ' + (rec.abnormalSymptoms || 'N/A') + '</div>';
                        } else if (rec.incisorCheck || rec.furSkinCheck || rec.digestiveSigns) {
                            hasSpecifics = true;
                            specHtml += '<div><strong>Răng cửa:</strong> ' + (rec.incisorCheck || 'N/A') + '</div>';
                            specHtml += '<div><strong>Da/Lông:</strong> ' + (rec.furSkinCheck || 'N/A') + '</div>';
                            specHtml += '<div><strong>Tiêu hóa:</strong> ' + (rec.digestiveSigns || 'N/A') + '</div>';
                        }
                        specHtml += '</div></div>';
                        if (hasSpecifics) {
                            html += specHtml;
                        }

                        html += '</div>';
                    });
                    html += '</div>';
                    container.innerHTML = html;
                } else {
                    container.innerHTML = '<div style="text-align: center; padding: 24px; color: #888;"><i class="bi bi-info-circle" style="font-size: 2rem;"></i><p style="margin-top: 8px; font-size: 0.85rem;">Chưa có lịch sử khám bệnh nào được ghi nhận cho thú cưng này.</p></div>';
                }
            },
            error: function () {
                container.innerHTML = '<div style="text-align: center; padding: 24px; color: #d9534f;"><i class="bi bi-exclamation-triangle" style="font-size: 2rem;"></i><p style="margin-top: 8px; font-size: 0.85rem;">Đã xảy ra lỗi khi tải dữ liệu lịch sử.</p></div>';
            }
        });
    }

    window.viewPetMedicalRecords = viewPetMedicalRecords;
})();
