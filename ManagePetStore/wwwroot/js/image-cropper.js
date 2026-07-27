/**
 * Antigravity Image Editor & Cropper Component
 * Self-contained, pure JS/HTML5 Canvas implementation.
 * Auto-attaches to all image file inputs.
 */
(function () {
    let currentInput = null;
    let currentFile = null;
    let originalImage = null;

    // Cropper state
    let rotation = 0; // 0, 90, 180, 270
    let zoom = 1.0;
    let aspectRatio = 0; // 0 = free, 1 = 1:1, 1.333 = 4:3, 1.777 = 16:9

    // Crop box coordinates relative to image container (px)
    let cropBox = { x: 0, y: 0, w: 0, h: 0 };
    let containerSize = { w: 0, h: 0 };
    let imgDisplaySize = { w: 0, h: 0, x: 0, y: 0 };

    let isDraggingBox = false;
    let activeHandle = null;
    let startMouse = { x: 0, y: 0 };
    let startCropBox = { x: 0, y: 0, w: 0, h: 0 };

    function injectStyles() {
        if (document.getElementById('imgCropperStyles')) return;
        const css = `
            #globalImageCropperModal {
                display: none; position: fixed; top: 0; left: 0; width: 100vw; height: 100vh;
                background: rgba(0, 0, 0, 0.8); z-index: 999999; align-items: center; justify-content: center;
                backdrop-filter: blur(5px); font-family: 'Be Vietnam Pro', system-ui, -apple-system, sans-serif;
            }
            .cropper-dialog {
                background: #ffffff; border-radius: 20px; width: 94%; max-width: 680px; padding: 20px;
                box-shadow: 0 25px 50px rgba(0,0,0,0.35); display: flex; flex-direction: column; gap: 14px;
                user-select: none; -webkit-user-select: none;
            }
            .cropper-stage {
                position: relative; width: 100%; height: 380px; background: #18181b; border-radius: 14px;
                overflow: hidden; display: flex; align-items: center; justify-content: center; touch-action: none;
            }
            .cropper-img-canvas {
                position: absolute; pointer-events: none; transition: transform 0.15s ease-out;
            }
            .cropper-overlay {
                position: absolute; top: 0; left: 0; width: 100%; height: 100%; pointer-events: none;
            }
            .cropper-box {
                position: absolute; border: 2px solid #ff7815; box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.55);
                box-sizing: border-box; cursor: move; pointer-events: auto;
            }
            .cropper-grid-v, .cropper-grid-h {
                position: absolute; pointer-events: none; opacity: 0.4; border: 0 dashed #ffffff;
            }
            .cropper-grid-v {
                top: 0; bottom: 0; left: 33.33%; width: 33.33%; border-left-width: 1px; border-right-width: 1px;
            }
            .cropper-grid-h {
                left: 0; right: 0; top: 33.33%; height: 33.33%; border-top-width: 1px; border-bottom-width: 1px;
            }
            .crop-handle {
                position: absolute; width: 12px; height: 12px; background-color: #ff7815;
                border: 2px solid #ffffff; border-radius: 3px; box-sizing: border-box; z-index: 10;
            }
            .handle-nw { top: -6px; left: -6px; cursor: nwse-resize; }
            .handle-ne { top: -6px; right: -6px; cursor: nesw-resize; }
            .handle-se { bottom: -6px; right: -6px; cursor: nwse-resize; }
            .handle-sw { bottom: -6px; left: -6px; cursor: nesw-resize; }
            .handle-n  { top: -6px; left: calc(50% - 6px); cursor: ns-resize; }
            .handle-s  { bottom: -6px; left: calc(50% - 6px); cursor: ns-resize; }
            .handle-w  { left: -6px; top: calc(50% - 6px); cursor: ew-resize; }
            .handle-e  { right: -6px; top: calc(50% - 6px); cursor: ew-resize; }

            .cropper-toolbar {
                display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px;
                background: #f8fafc; padding: 10px 14px; border-radius: 12px; border: 1px solid #e2e8f0;
            }
            .cropper-btn-group { display: flex; gap: 6px; align-items: center; }
            .cropper-tool-btn {
                background: #ffffff; border: 1px solid #cbd5e1; border-radius: 8px; padding: 6px 12px;
                font-size: 0.82rem; font-weight: 600; color: #475569; cursor: pointer; transition: all 0.15s;
                display: inline-flex; align-items: center; gap: 5px;
            }
            .cropper-tool-btn:hover { background: #f1f5f9; color: #0f172a; border-color: #94a3b8; }
            .cropper-tool-btn.active { background: #ff7815; color: #ffffff; border-color: #ff7815; }
        `;
        const style = document.createElement('style');
        style.id = 'imgCropperStyles';
        style.innerHTML = css;
        document.head.appendChild(style);
    }

    function createCropperModalHTML() {
        if (document.getElementById('globalImageCropperModal')) return;
        injectStyles();

        const modalHtml = `
        <div id="globalImageCropperModal">
            <div class="cropper-dialog">
                <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #f1f5f9; padding-bottom: 10px;">
                    <h3 style="margin: 0; font-size: 1.1rem; font-weight: 700; color: #ff7815; display: flex; align-items: center; gap: 8px;">
                        <i class="bi bi-crop" style="font-size: 1.25rem;"></i> CHỈNH SỬA & CẮT ẢNH
                    </h3>
                    <button type="button" id="btnCancelGlobalCropX" style="background: none; border: none; font-size: 1.6rem; cursor: pointer; color: #94a3b8; line-height: 1;">&times;</button>
                </div>

                <!-- Stage -->
                <div class="cropper-stage" id="cropperStage">
                    <canvas id="cropperImgCanvas" class="cropper-img-canvas"></canvas>
                    <div class="cropper-box" id="cropperBox">
                        <div class="cropper-grid-v"></div>
                        <div class="cropper-grid-h"></div>
                        <div class="crop-handle handle-nw" data-handle="nw"></div>
                        <div class="crop-handle handle-ne" data-handle="ne"></div>
                        <div class="crop-handle handle-se" data-handle="se"></div>
                        <div class="crop-handle handle-sw" data-handle="sw"></div>
                        <div class="crop-handle handle-n"  data-handle="n"></div>
                        <div class="crop-handle handle-s"  data-handle="s"></div>
                        <div class="crop-handle handle-w"  data-handle="w"></div>
                        <div class="crop-handle handle-e"  data-handle="e"></div>
                    </div>
                </div>

                <!-- Toolbar -->
                <div class="cropper-toolbar">
                    <div class="cropper-btn-group">
                        <span style="font-size: 0.8rem; font-weight: 700; color: #64748b; margin-right: 2px;">Tỉ lệ:</span>
                        <button type="button" class="cropper-tool-btn ratio-btn active" data-ratio="0">Tự do</button>
                        <button type="button" class="cropper-tool-btn ratio-btn" data-ratio="1">1:1</button>
                        <button type="button" class="cropper-tool-btn ratio-btn" data-ratio="1.333">4:3</button>
                        <button type="button" class="cropper-tool-btn ratio-btn" data-ratio="1.777">16:9</button>
                    </div>

                    <div class="cropper-btn-group">
                        <button type="button" class="cropper-tool-btn" id="btnRotateLeft" title="Xoay trái 90°"><i class="bi bi-arrow-counterclockwise"></i> Xoay trái</button>
                        <button type="button" class="cropper-tool-btn" id="btnRotateRight" title="Xoay phải 90°"><i class="bi bi-arrow-clockwise"></i> Xoay phải</button>
                        <button type="button" class="cropper-tool-btn" id="btnZoomIn" title="Phóng to"><i class="bi bi-zoom-in"></i></button>
                        <button type="button" class="cropper-tool-btn" id="btnZoomOut" title="Thu nhỏ"><i class="bi bi-zoom-out"></i></button>
                    </div>
                </div>

                <!-- Footer -->
                <div style="display: flex; justify-content: space-between; align-items: center; border-top: 1px solid #f1f5f9; padding-top: 12px;">
                    <div style="font-size: 0.82rem; color: #64748b;">
                        <i class="bi bi-info-circle me-1"></i> Điều chỉnh khung cắt và nhấn <strong>Xong</strong> để hoàn tất.
                    </div>
                    <div style="display: flex; gap: 10px;">
                        <button type="button" id="btnCancelCropModal" style="background: #f1f5f9; color: #334155; border: none; padding: 8px 18px; border-radius: 20px; font-weight: 600; font-size: 0.88rem; cursor: pointer;">Hủy</button>
                        <button type="button" id="btnDoneGlobalCrop" style="background: #ff7815; color: #ffffff; border: none; padding: 8px 24px; border-radius: 20px; font-weight: 700; font-size: 0.88rem; cursor: pointer; box-shadow: 0 4px 12px rgba(255, 120, 21, 0.35);">Xong</button>
                    </div>
                </div>
            </div>
        </div>`;

        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Bind Controls
        document.getElementById('btnCancelGlobalCropX').addEventListener('click', closeModal);
        document.getElementById('btnCancelCropModal').addEventListener('click', closeModal);
        document.getElementById('btnDoneGlobalCrop').addEventListener('click', finishCropping);

        document.getElementById('btnRotateLeft').addEventListener('click', () => { rotation = (rotation - 90 + 360) % 360; renderStage(); resetCropBox(); });
        document.getElementById('btnRotateRight').addEventListener('click', () => { rotation = (rotation + 90) % 360; renderStage(); resetCropBox(); });
        document.getElementById('btnZoomIn').addEventListener('click', () => { zoom = Math.min(zoom + 0.15, 2.5); renderStage(); });
        document.getElementById('btnZoomOut').addEventListener('click', () => { zoom = Math.max(zoom - 0.15, 0.6); renderStage(); });

        document.querySelectorAll('.ratio-btn').forEach(btn => {
            btn.addEventListener('click', function () {
                document.querySelectorAll('.ratio-btn').forEach(b => b.classList.remove('active'));
                this.classList.add('active');
                aspectRatio = parseFloat(this.getAttribute('data-ratio')) || 0;
                resetCropBox();
            });
        });

        setupDragEvents();
    }

    function renderStage() {
        if (!originalImage) return;

        const stage = document.getElementById('cropperStage');
        const canvas = document.getElementById('cropperImgCanvas');
        const ctx = canvas.getContext('2d');

        containerSize.w = stage.clientWidth;
        containerSize.h = stage.clientHeight;

        let srcW = originalImage.naturalWidth;
        let srcH = originalImage.naturalHeight;

        let isRotated = (rotation % 180 !== 0);
        let effW = isRotated ? srcH : srcW;
        let effH = isRotated ? srcW : srcH;

        // Calculate fit scale inside stage
        let scale = Math.min(containerSize.w / effW, containerSize.h / effH) * 0.9 * zoom;
        let dispW = effW * scale;
        let dispH = effH * scale;

        canvas.width = effW;
        canvas.height = effH;
        canvas.style.width = dispW + 'px';
        canvas.style.height = dispH + 'px';

        imgDisplaySize = {
            w: dispW,
            h: dispH,
            x: (containerSize.w - dispW) / 2,
            y: (containerSize.h - dispH) / 2,
            scale: scale
        };

        canvas.style.left = imgDisplaySize.x + 'px';
        canvas.style.top = imgDisplaySize.y + 'px';

        ctx.save();
        ctx.translate(effW / 2, effH / 2);
        ctx.rotate((rotation * Math.PI) / 180);
        ctx.drawImage(originalImage, -srcW / 2, -srcH / 2, srcW, srcH);
        ctx.restore();
    }

    function resetCropBox() {
        if (!imgDisplaySize.w || !imgDisplaySize.h) return;

        let boxW = imgDisplaySize.w * 0.8;
        let boxH = imgDisplaySize.h * 0.8;

        if (aspectRatio > 0) {
            if (boxW / boxH > aspectRatio) {
                boxW = boxH * aspectRatio;
            } else {
                boxH = boxW / aspectRatio;
            }
        }

        cropBox.w = boxW;
        cropBox.h = boxH;
        cropBox.x = imgDisplaySize.x + (imgDisplaySize.w - boxW) / 2;
        cropBox.y = imgDisplaySize.y + (imgDisplaySize.h - boxH) / 2;

        updateCropBoxUI();
    }

    function updateCropBoxUI() {
        const box = document.getElementById('cropperBox');
        if (!box) return;

        // Constrain box within image bounds
        cropBox.x = Math.max(imgDisplaySize.x, Math.min(cropBox.x, imgDisplaySize.x + imgDisplaySize.w - cropBox.w));
        cropBox.y = Math.max(imgDisplaySize.y, Math.min(cropBox.y, imgDisplaySize.y + imgDisplaySize.h - cropBox.h));

        box.style.left = cropBox.x + 'px';
        box.style.top = cropBox.y + 'px';
        box.style.width = cropBox.w + 'px';
        box.style.height = cropBox.h + 'px';
    }

    function setupDragEvents() {
        const stage = document.getElementById('cropperStage');
        const box = document.getElementById('cropperBox');

        box.addEventListener('mousedown', startDrag);
        box.addEventListener('touchstart', startDrag, { passive: false });

        window.addEventListener('mousemove', doDrag);
        window.addEventListener('touchmove', doDrag, { passive: false });

        window.addEventListener('mouseup', stopDrag);
        window.addEventListener('touchend', stopDrag);

        function getMousePos(e) {
            if (e.touches && e.touches.length > 0) {
                return { x: e.touches[0].clientX, y: e.touches[0].clientY };
            }
            return { x: e.clientX, y: e.clientY };
        }

        function startDrag(e) {
            e.stopPropagation();
            if (e.type === 'touchstart') e.preventDefault();

            const pos = getMousePos(e);
            startMouse = pos;
            startCropBox = { ...cropBox };

            const target = e.target;
            if (target.classList.contains('crop-handle')) {
                activeHandle = target.getAttribute('data-handle');
                isDraggingBox = false;
            } else {
                activeHandle = null;
                isDraggingBox = true;
            }
        }

        function doDrag(e) {
            if (!isDraggingBox && !activeHandle) return;
            if (e.type === 'touchmove') e.preventDefault();

            const pos = getMousePos(e);
            const dx = pos.x - startMouse.x;
            const dy = pos.y - startMouse.y;

            if (isDraggingBox) {
                cropBox.x = startCropBox.x + dx;
                cropBox.y = startCropBox.y + dy;
            } else if (activeHandle) {
                let newW = startCropBox.w;
                let newH = startCropBox.h;
                let newX = startCropBox.x;
                let newY = startCropBox.y;

                if (activeHandle.includes('e')) newW = Math.max(30, startCropBox.w + dx);
                if (activeHandle.includes('s')) newH = Math.max(30, startCropBox.h + dy);
                if (activeHandle.includes('w')) {
                    let wDiff = Math.min(dx, startCropBox.w - 30);
                    newW = startCropBox.w - wDiff;
                    newX = startCropBox.x + wDiff;
                }
                if (activeHandle.includes('n')) {
                    let hDiff = Math.min(dy, startCropBox.h - 30);
                    newH = startCropBox.h - hDiff;
                    newY = startCropBox.y + hDiff;
                }

                if (aspectRatio > 0) {
                    if (activeHandle === 'e' || activeHandle === 'w' || activeHandle === 'se' || activeHandle === 'sw') {
                        newH = newW / aspectRatio;
                    } else {
                        newW = newH * aspectRatio;
                    }
                }

                // Bound checking
                newW = Math.min(newW, imgDisplaySize.x + imgDisplaySize.w - newX);
                newH = Math.min(newH, imgDisplaySize.y + imgDisplaySize.h - newY);

                cropBox.w = newW;
                cropBox.h = newH;
                cropBox.x = newX;
                cropBox.y = newY;
            }

            updateCropBoxUI();
        }

        function stopDrag() {
            isDraggingBox = false;
            activeHandle = null;
        }
    }

    function openModal(imgSrc) {
        const modal = document.getElementById('globalImageCropperModal');
        if (!modal) return;

        rotation = 0;
        zoom = 1.0;
        aspectRatio = 0;

        document.querySelectorAll('.ratio-btn').forEach(b => b.classList.remove('active'));
        document.querySelector('.ratio-btn[data-ratio="0"]')?.classList.add('active');

        originalImage = new Image();
        originalImage.onload = function () {
            modal.style.display = 'flex';
            setTimeout(() => {
                renderStage();
                resetCropBox();
            }, 50);
        };
        originalImage.src = imgSrc;
    }

    function closeModal() {
        const modal = document.getElementById('globalImageCropperModal');
        if (modal) modal.style.display = 'none';
        currentInput = null;
        currentFile = null;
        originalImage = null;
    }

    function finishCropping() {
        if (!currentInput || !currentFile || !originalImage) {
            closeModal();
            return;
        }

        const effCanvas = document.getElementById('cropperImgCanvas');
        if (!effCanvas) {
            closeModal();
            return;
        }

        // Calculate source region in the effective rotated canvas
        let relX = (cropBox.x - imgDisplaySize.x) / imgDisplaySize.scale;
        let relY = (cropBox.y - imgDisplaySize.y) / imgDisplaySize.scale;
        let relW = cropBox.w / imgDisplaySize.scale;
        let relH = cropBox.h / imgDisplaySize.scale;

        relX = Math.max(0, Math.min(relX, effCanvas.width));
        relY = Math.max(0, Math.min(relY, effCanvas.height));
        relW = Math.min(relW, effCanvas.width - relX);
        relH = Math.min(relH, effCanvas.height - relY);

        const cropCanvas = document.createElement('canvas');
        cropCanvas.width = Math.round(relW);
        cropCanvas.height = Math.round(relH);

        const ctx = cropCanvas.getContext('2d');
        ctx.drawImage(effCanvas, relX, relY, relW, relH, 0, 0, cropCanvas.width, cropCanvas.height);

        const mimeType = currentFile.type || 'image/jpeg';
        cropCanvas.toBlob(function (blob) {
            if (blob && window.DataTransfer) {
                const fileName = currentFile.name || 'cropped_image.jpg';
                const croppedFile = new File([blob], fileName, { type: mimeType });

                const dt = new DataTransfer();
                dt.items.add(croppedFile);
                currentInput.isCroppedPayload = true;
                currentInput.files = dt.files;

                // Fire change event for host form preview update
                const evt = new Event('change', { bubbles: true });
                currentInput.dispatchEvent(evt);
            }
            closeModal();
        }, mimeType);
    }

    function handleFileInputChange(e) {
        const input = e.target;
        if (!input || input.type !== 'file') return;

        if (input.isCroppedPayload) {
            input.isCroppedPayload = false;
            return;
        }

        const files = input.files;
        if (!files || files.length === 0) return;

        const file = files[0];
        if (!file.type || !file.type.startsWith('image/')) return;

        currentInput = input;
        currentFile = file;

        const reader = new FileReader();
        reader.onload = function (evt) {
            createCropperModalHTML();
            openModal(evt.target.result);
        };
        reader.readAsDataURL(file);
    }

    document.addEventListener('DOMContentLoaded', function () {
        createCropperModalHTML();
        document.addEventListener('change', handleFileInputChange, true);
    });
})();
