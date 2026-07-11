/**
 * Màn hình POS - Cashier
 */
document.addEventListener('DOMContentLoaded', function () {
    // Lấy query parameters để check trạng thái thanh toán từ PayOS trả về
    const urlParams = new URLSearchParams(window.location.search);
    const orderIdParam = urlParams.get('orderId');
    const statusParam = urlParams.get('status');
    const codeParam = urlParams.get('code');
    const cancelParam = urlParams.get('cancel');

    if (orderIdParam && (statusParam === 'success' || statusParam === 'PAID' || codeParam === '00')) {
        const successModal = document.getElementById('posSuccessModal');
        const lblOrderId = document.getElementById('lblSuccessOrderId');
        if (successModal && lblOrderId) {
            lblOrderId.textContent = orderIdParam;
            successModal.style.display = 'flex';
            
            document.getElementById('btnSuccessPrint').onclick = function() {
                window.open(`/Cashier/Order/PrintInvoice?orderId=${orderIdParam}`, '_blank', 'width=380,height=600');
                successModal.style.display = 'none';
            };
            
            document.getElementById('btnSuccessClose').onclick = function() {
                successModal.style.display = 'none';
            };
        }
        
        // Clear giỏ hàng trong localStorage để bắt đầu đơn mới
        localStorage.removeItem('pos_cart');
        localStorage.removeItem('pos_current_customer');
        
        // Dọn URL
        window.history.replaceState(null, null, window.location.pathname);
    } else if (cancelParam === 'true' || statusParam === 'CANCELLED' || statusParam === 'cancel') {
        alert("Khách hàng đã hủy giao dịch chuyển khoản.");
        window.history.replaceState(null, null, window.location.pathname);
    }

    // === STATE ===
    let cart = []; // Mảng chứa PosCartItemDto
    let currentCustomer = null;
    let selectedPetId = null;

    // Lấy giỏ hàng tạm từ LocalStorage nếu có
    const savedCart = localStorage.getItem('pos_cart');
    if (savedCart) {
        try {
            cart = JSON.parse(savedCart);
            renderCart();
        } catch (e) {
            console.error("Lỗi parse giỏ hàng tạm", e);
        }
    }

    const savedCustomer = localStorage.getItem('pos_current_customer');
    if (savedCustomer) {
        try {
            currentCustomer = JSON.parse(savedCustomer);
            renderCustomerInfo();
            
            // Refresh customer data from server to get latest points/tier
            if (currentCustomer && currentCustomer.phone) {
                fetch(`/Cashier/Order/SearchCustomers?q=${encodeURIComponent(currentCustomer.phone)}`)
                    .then(res => res.json())
                    .then(data => {
                        if (data.success && data.data && data.data.length > 0) {
                            const freshCustomer = data.data.find(c => c.customerId === currentCustomer.customerId) || data.data[0];
                            currentCustomer = freshCustomer;
                            localStorage.setItem('pos_current_customer', JSON.stringify(currentCustomer));
                            renderCustomerInfo();
                        }
                    })
                    .catch(err => console.error("Error refreshing customer", err));
            }
        } catch(e) {}
    }

    let heldOrders = [];
    const savedHeld = localStorage.getItem('pos_held_orders');
    if (savedHeld) {
        try { heldOrders = JSON.parse(savedHeld); } catch(e) {}
    }

    function updateHeldCount() {
        const el = document.getElementById('heldOrderCount');
        if (el) el.textContent = heldOrders.length;
    }
    updateHeldCount();

    // === LOAD TẤT CẢ SPA SERVICES REMOVED ===

    // === LOAD TẤT CẢ SẢN PHẨM ===
    async function loadAllProducts() {
        try {
            const res = await fetch('/Cashier/Order/GetAllProducts');
            const data = await res.json();
            if (data.success && data.data) {
                const list = document.getElementById('allProductsList');
                if (list) {
                    if (data.data.length === 0) {
                        list.innerHTML = '<p style="color:var(--pos-text-muted);font-size:13px;">Không có sản phẩm nào.</p>';
                        return;
                    }
                    list.innerHTML = data.data.map(p => `
                        <div class="pos-spa-item" onclick="handleSelectProduct('Product', '${p.id}', '${p.name.replace(/'/g, "&#39;")}', ${p.price}, ${p.stock})">
                            <span class="pos-spa-item-name">${p.name}</span>
                            <span class="pos-spa-item-price">${formatCurrency(p.price)}</span>
                            <span style="font-size:11px;color:var(--pos-text-muted);margin-top:2px;">Tồn: ${p.stock}</span>
                        </div>
                    `).join('');
                }
            }
        } catch(err) { console.error('loadAllProducts error', err); }
    }
    loadAllProducts();

    // === TABS LOGIC REMOVED ===

    // === FORMAT CURRENCY ===
    function formatCurrency(amount) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
    }

    // === DEBOUNCE FUNCTION ===
    function debounce(func, wait) {
        let timeout;
        return function (...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    }

    // === TÌM KIẾM SẢN PHẨM / DỊCH VỤ ===
    const inputSearchProduct = document.getElementById('posSearchProduct');
    const resultBoxProduct = document.getElementById('posProductResults');

    const searchProduct = debounce(async function (query) {
        if (!query.trim()) {
            resultBoxProduct.style.display = 'none';
            return;
        }

        try {
            const res = await fetch(`/Cashier/Order/SearchProducts?q=${encodeURIComponent(query)}`);
            const data = await res.json();
            
            if (data.success) {
                renderProductResults(data.data);
            }
        } catch (err) {
            console.error(err);
        }
    }, 400);

    inputSearchProduct.addEventListener('input', (e) => searchProduct(e.target.value));

    function renderProductResults(items) {
        const productsOnly = items.filter(item => item.type === 'Product');
        if (productsOnly.length === 0) {
            resultBoxProduct.innerHTML = '<div class="pos-search-item"><div class="pos-search-item-info"><span>Không tìm thấy kết quả.</span></div></div>';
        } else {
            resultBoxProduct.innerHTML = productsOnly.map(item => `
                <div class="pos-search-item" onclick="handleSelectProduct('${item.type}', '${item.id}', '${item.name}', ${item.price}, ${item.stock})">
                    <div class="pos-search-item-info">
                        <strong>${item.name}</strong>
                        <span>Sản phẩm - SKU: ${item.id} (Tồn: ${item.stock})</span>
                    </div>
                    <div class="pos-search-item-price">${formatCurrency(item.price)}</div>
                </div>
            `).join('');
        }
        resultBoxProduct.style.display = 'block';
    }

    // === CHỌN SẢN PHẨM TỪ TÌM KIẾM ===
    window.handleSelectProduct = function (type, id, name, price, stock) {
        resultBoxProduct.style.display = 'none';
        inputSearchProduct.value = '';

        if (type === 'Product') {
            addToCartProduct(id, name, price);
        }
    };

    // Đóng popup khi click ngoài
    document.addEventListener('click', (e) => {
        if (!inputSearchProduct.contains(e.target) && !resultBoxProduct.contains(e.target)) {
            resultBoxProduct.style.display = 'none';
        }
    });

    // === TÌM KIẾM KHÁCH HÀNG ===
    const inputSearchCustomer = document.getElementById('posSearchCustomer');
    const resultBoxCustomer = document.getElementById('posCustomerResults');

    const searchCustomer = debounce(async function (query) {
        if (!query.trim()) {
            resultBoxCustomer.style.display = 'none';
            return;
        }

        try {
            const res = await fetch(`/Cashier/Order/SearchCustomers?q=${encodeURIComponent(query)}`);
            const data = await res.json();
            
            if (data.success) {
                renderCustomerResults(data.data);
            }
        } catch (err) {
            console.error(err);
        }
    }, 400);

    inputSearchCustomer.addEventListener('input', (e) => searchCustomer(e.target.value));

    function renderCustomerResults(customers) {
        if (customers.length === 0) {
            resultBoxCustomer.innerHTML = '<div class="pos-search-item"><div class="pos-search-item-info"><span>Không tìm thấy khách hàng. Hãy Đăng ký nhanh!</span></div></div>';
        } else {
            resultBoxCustomer.innerHTML = customers.map(c => {
                const pets = c.pets.map(p => p.name).join(', ') || 'Không có thú cưng';
                return `
                <div class="pos-search-item" onclick='handleSelectCustomer(${JSON.stringify(c).replace(/'/g, "&#39;")})'>
                    <div class="pos-search-item-info">
                        <strong>${c.fullName} - ${c.phone}</strong>
                        <span>Hạng: ${c.membershipTier} | Thú cưng: ${pets}</span>
                    </div>
                </div>
            `}).join('');
        }
        resultBoxCustomer.style.display = 'block';
    }

    window.handleSelectCustomer = function (c) {
        resultBoxCustomer.style.display = 'none';
        inputSearchCustomer.value = '';
        currentCustomer = c;
        renderCustomerInfo();
    };

    function renderCustomerInfo() {
        if (!currentCustomer) {
            document.getElementById('selectedCustomerInfo').style.display = 'none';
            document.getElementById('loyaltyGroup').style.display = 'none';
            document.getElementById('chkUsePoints').checked = false;
            recalculatePayment();
            return;
        }

        document.getElementById('lblCustomerName').textContent = currentCustomer.fullName;
        document.getElementById('lblCustomerPhone').textContent = currentCustomer.phone;
        document.getElementById('lblCustomerTier').textContent = currentCustomer.membershipTier;
        document.getElementById('lblCustomerPoints').textContent = currentCustomer.loyaltyPoints;

        // Pet selector logic removed

        document.getElementById('lblCustomerPointsVal').textContent = currentCustomer.loyaltyPoints;
        const totalAmount = cart.reduce((acc, curr) => acc + curr.total, 0);
        const maxDiscountPoints = Math.floor(totalAmount / 500);
        const pointsToUse = Math.min(currentCustomer.loyaltyPoints, maxDiscountPoints);
        document.getElementById('lblMaxDiscountVal').textContent = formatCurrency(pointsToUse * 500);
        document.getElementById('loyaltyGroup').style.display = 'block';

        document.getElementById('selectedCustomerInfo').style.display = 'block';
        recalculatePayment();
    }

    document.getElementById('btnClearCustomer').addEventListener('click', () => {
        currentCustomer = null;
        document.getElementById('chkUsePoints').checked = false;
        renderCustomerInfo();
        
        saveCartLocally();
        renderCart();
    });

    // === QUICK REGISTER MODAL ===
    const regModal = document.getElementById('quickRegisterModal');
    
    document.getElementById('btnShowQuickRegister').addEventListener('click', () => {
        regModal.style.display = 'flex';
        document.getElementById('regCustomerName').value = inputSearchCustomer.value; // Auto-fill if typed something
    });

    document.getElementById('btnCloseRegisterModal').addEventListener('click', () => regModal.style.display = 'none');
    document.getElementById('btnCancelRegister').addEventListener('click', () => regModal.style.display = 'none');

    // === INLINE VALIDATION HELPERS ===
    function setFieldError(inputId, errorId, message) {
        const input = document.getElementById(inputId);
        const err = document.getElementById(errorId);
        if (message) {
            if (input) input.classList.add('is-invalid');
            if (err) err.textContent = message;
        } else {
            if (input) input.classList.remove('is-invalid');
            if (err) err.textContent = '';
        }
    }

    function clearRegisterErrors() {
        ['regCustomerName', 'regPhone', 'regPetName'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.classList.remove('is-invalid');
        });
        ['errRegCustomerName', 'errRegPhone', 'errRegPetName'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.textContent = '';
        });
        const banner = document.getElementById('regFormError');
        if (banner) { banner.style.display = 'none'; banner.textContent = ''; }
    }

    // Clear error khi người dùng gõ lại
    ['regCustomerName', 'regPhone', 'regPetName'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener('input', () => {
            el.classList.remove('is-invalid');
            const errId = 'err' + id.charAt(0).toUpperCase() + id.slice(1);
            const errEl = document.getElementById(errId);
            if (errEl) errEl.textContent = '';
        });
    });

    document.getElementById('btnSaveRegister').addEventListener('click', async () => {
        const name = document.getElementById('regCustomerName').value.trim();
        const phone = document.getElementById('regPhone').value.trim();
        const petName = document.getElementById('regPetName').value.trim();
        const petType = document.getElementById('regPetType').value;

        clearRegisterErrors();
        let hasError = false;

        if (!name) {
            setFieldError('regCustomerName', 'errRegCustomerName', 'Vui lòng nhập tên khách hàng.');
            hasError = true;
        } else if (!/^[\p{L}\s]+$/u.test(name)) {
            setFieldError('regCustomerName', 'errRegCustomerName', 'Tên chỉ được nhập chữ, không chứa số hay ký tự đặc biệt.');
            hasError = true;
        }

        if (!phone) {
            setFieldError('regPhone', 'errRegPhone', 'Vui lòng nhập số điện thoại.');
            hasError = true;
        } else if (!/^\d{10,}$/.test(phone)) {
            setFieldError('regPhone', 'errRegPhone', 'SĐT phải là số, ít nhất 10 chữ số, không chứa khoảng trắng.');
            hasError = true;
        }

        if (petName && !/^[\p{L}\s]+$/u.test(petName)) {
            setFieldError('regPetName', 'errRegPetName', 'Tên thú cưng chỉ được nhập chữ, không chứa số hay ký tự đặc biệt.');
            hasError = true;
        }

        if (hasError) return;

        const dto = { customerName: name, phone: phone, petName: petName, petType: petType };

        try {
            const res = await fetch('/Cashier/Order/QuickRegister', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(dto)
            });
            const data = await res.json();
            if (data.success) {
                currentCustomer = data.data;
                renderCustomerInfo();
                clearRegisterErrors();
                regModal.style.display = 'none';
            } else {
                const banner = document.getElementById('regFormError');
                if (banner) {
                    banner.textContent = data.message || 'Lỗi tạo khách hàng.';
                    banner.style.display = 'block';
                }
            }
        } catch (err) {
            console.error(err);
            alert("Lỗi kết nối.");
        }
    });

    // === CART LOGIC ===
    function addToCartProduct(id, name, price) {
        const existing = cart.find(c => c.type === 'Product' && c.id === id);
        if (existing) {
            existing.quantity += 1;
            existing.total = existing.quantity * existing.price;
        } else {
            cart.push({
                type: 'Product',
                id: id,
                name: name,
                quantity: 1,
                price: price,
                total: price,
                petId: null,
                petWeight: null,
                groomerId: null,
                appointmentTime: null
            });
        }
        saveCartLocally();
        renderCart();
    }

    window.changeQty = function (index, delta) {
        cart[index].quantity += delta;
        if (cart[index].quantity <= 0) {
            cart.splice(index, 1);
        } else {
            cart[index].total = cart[index].quantity * cart[index].price;
        }
        saveCartLocally();
        renderCart();
    };

    window.removeItem = function (index) {
        cart.splice(index, 1);
        saveCartLocally();
        renderCart();
    };

    function saveCartLocally() {
        localStorage.setItem('pos_cart', JSON.stringify(cart));
        if (currentCustomer) {
            localStorage.setItem('pos_current_customer', JSON.stringify(currentCustomer));
        } else {
            localStorage.removeItem('pos_current_customer');
        }
    }

    function renderCart() {
        const listProducts = document.getElementById('listProducts');
        const emptyProducts = document.getElementById('emptyProducts');

        const products = cart.map((c, idx) => ({ ...c, originalIndex: idx })).filter(c => c.type === 'Product');

        const countProductsEl = document.getElementById('countProducts');
        if (countProductsEl) countProductsEl.textContent = products.length;

        // Render Products
        if (products.length === 0) {
            if (emptyProducts) emptyProducts.style.display = '';
            if (listProducts) listProducts.innerHTML = '';
        } else {
            if (emptyProducts) emptyProducts.style.display = 'none';
            if (listProducts) {
                listProducts.innerHTML = products.map(p => `
                    <div class="pos-cart-item">
                        <div class="pos-item-icon"><i class="bi bi-box"></i></div>
                        <div class="pos-item-details">
                            <div class="pos-item-name">${p.name}</div>
                            <div class="pos-item-meta">${formatCurrency(p.price)}</div>
                        </div>
                        <div class="pos-qty-control">
                            <button class="pos-qty-btn" onclick="changeQty(${p.originalIndex}, -1)">-</button>
                            <input type="text" class="pos-qty-input" value="${p.quantity}" readonly />
                            <button class="pos-qty-btn" onclick="changeQty(${p.originalIndex}, 1)">+</button>
                        </div>
                        <div class="pos-item-price">${formatCurrency(p.total)}</div>
                        <button class="btn-remove-item" onclick="removeItem(${p.originalIndex})"><i class="bi bi-trash"></i></button>
                    </div>
                `).join('');
            }
        }

        // Calculate Totals
        const total = cart.reduce((acc, curr) => acc + curr.total, 0);
        document.getElementById('lblSubtotal').textContent = formatCurrency(total);
        recalculatePayment();
    }

    // === HOLD ORDER ===
    const heldModal = document.getElementById('heldOrdersModal');
    
    document.getElementById('btnShowHeldOrders').addEventListener('click', () => {
        renderHeldOrders();
        heldModal.style.display = 'flex';
    });
    
    document.getElementById('btnCloseHeldOrders').addEventListener('click', () => heldModal.style.display = 'none');
    document.getElementById('btnCancelHeldOrders').addEventListener('click', () => heldModal.style.display = 'none');

    document.getElementById('btnHoldOrder').addEventListener('click', () => {
        if (cart.length === 0) {
            alert("Giỏ hàng đang trống.");
            return;
        }
        
        const total = cart.reduce((acc, curr) => acc + curr.total, 0);
        const customerName = currentCustomer ? currentCustomer.fullName : "Khách lẻ";
        
        heldOrders.push({
            id: Date.now(),
            time: new Date().toLocaleTimeString('vi-VN'),
            customer: currentCustomer,
            customerName: customerName,
            cart: [...cart],
            total: total
        });
        
        localStorage.setItem('pos_held_orders', JSON.stringify(heldOrders));
        updateHeldCount();
        
        // Clear current
        cart = [];
        currentCustomer = null;
        saveCartLocally();
        renderCart();
        renderCustomerInfo();
        
        alert("Đã lưu đơn tạm thành công!");
    });

    function renderHeldOrders() {
        const list = document.getElementById('heldOrdersList');
        if (heldOrders.length === 0) {
            list.innerHTML = '<p class="text-muted text-center" style="margin:20px 0;">Không có đơn lưu tạm nào.</p>';
            return;
        }
        
        list.innerHTML = heldOrders.map(ho => `
            <div class="pos-held-order-item">
                <div class="pos-held-order-info">
                    <strong>${ho.customerName}</strong>
                    <span>Lưu lúc: ${ho.time} - SP: ${ho.cart.length} - ${formatCurrency(ho.total)}</span>
                </div>
                <div class="pos-held-order-actions">
                    <button class="btn-pos-restore" onclick="restoreHeldOrder(${ho.id})">Mở lại</button>
                    <button class="btn-pos-delete" onclick="deleteHeldOrder(${ho.id})"><i class="bi bi-trash"></i></button>
                </div>
            </div>
        `).reverse().join('');
    }

    window.restoreHeldOrder = function(id) {
        if (cart.length > 0) {
            if (!confirm("Giỏ hàng hiện tại đang có sản phẩm, bạn có chắc chắn muốn ghi đè bằng đơn tạm này?")) {
                return;
            }
        }
        
        const idx = heldOrders.findIndex(h => h.id === id);
        if (idx !== -1) {
            const ho = heldOrders[idx];
            cart = ho.cart;
            currentCustomer = ho.customer;
            
            // Xóa khỏi list tạm
            heldOrders.splice(idx, 1);
            localStorage.setItem('pos_held_orders', JSON.stringify(heldOrders));
            updateHeldCount();
            
            saveCartLocally();
            renderCart();
            renderCustomerInfo();
            heldModal.style.display = 'none';
        }
    };

    window.deleteHeldOrder = function(id) {
        if (confirm("Xóa đơn lưu tạm này?")) {
            const idx = heldOrders.findIndex(h => h.id === id);
            if (idx !== -1) {
                heldOrders.splice(idx, 1);
                localStorage.setItem('pos_held_orders', JSON.stringify(heldOrders));
                updateHeldCount();
                renderHeldOrders();
            }
        }
    };

    // === CALCULATE PAYMENT REAL-TIME ===
    function recalculatePayment() {
        const subtotal = cart.reduce((acc, curr) => acc + curr.total, 0);
        let discount = 0;
        let pointsUsed = 0;

        if (currentCustomer) {
            const maxDiscountPoints = Math.floor(subtotal / 500);
            const pointsToUse = Math.min(currentCustomer.loyaltyPoints, maxDiscountPoints);
            document.getElementById('lblMaxDiscountVal').textContent = formatCurrency(pointsToUse * 500);

            if (document.getElementById('chkUsePoints').checked) {
                pointsUsed = pointsToUse;
                discount = pointsUsed * 500;
            }
        }

        const finalTotal = Math.max(0, subtotal - discount);
        document.getElementById('lblTotal').textContent = formatCurrency(finalTotal);
        document.getElementById('lblModalTotal').textContent = formatCurrency(finalTotal);

        const paymentMethod = document.getElementById('posPaymentMethod').value;
        const cashInputGroup = document.getElementById('cashInputGroup');
        const paymentInfoBox = document.getElementById('paymentInfoBox');
        const rowChangeDue = document.getElementById('rowChangeDue');
        const rowOnlineAmount = document.getElementById('rowOnlineAmount');
        const txtCashReceived = document.getElementById('txtCashReceived');

        if (paymentMethod === 'Tiền mặt') {
            cashInputGroup.style.display = 'block';
            paymentInfoBox.style.display = 'block';
            rowChangeDue.style.display = 'flex';
            rowOnlineAmount.style.display = 'none';

            const cashRaw = txtCashReceived.value.replace(/[^0-9]/g, '');
            const cashVal = parseFloat(cashRaw) || 0;
            const changeDue = Math.max(0, cashVal - finalTotal);
            document.getElementById('lblChangeDue').textContent = formatCurrency(changeDue);
        } else if (paymentMethod === 'Thanh toán online') {
            cashInputGroup.style.display = 'none';
            paymentInfoBox.style.display = 'none';
            rowChangeDue.style.display = 'none';
            rowOnlineAmount.style.display = 'none';
        } else if (paymentMethod === 'Tiền mặt + Online') {
            cashInputGroup.style.display = 'block';
            paymentInfoBox.style.display = 'block';
            rowChangeDue.style.display = 'none';
            rowOnlineAmount.style.display = 'flex';

            const cashRaw = txtCashReceived.value.replace(/[^0-9]/g, '');
            const cashVal = parseFloat(cashRaw) || 0;
            const onlineNeeded = Math.max(0, finalTotal - cashVal);
            document.getElementById('lblOnlineAmount').textContent = formatCurrency(onlineNeeded);
        }
    }

    // Attach Event Listeners for payment calculations
    document.getElementById('posPaymentMethod').addEventListener('change', () => {
        const paymentMethod = document.getElementById('posPaymentMethod').value;
        const subtotal = cart.reduce((acc, curr) => acc + curr.total, 0);
        let discount = 0;
        if (currentCustomer && document.getElementById('chkUsePoints').checked) {
            discount = Math.min(currentCustomer.loyaltyPoints, Math.floor(subtotal / 500)) * 500;
        }
        const finalTotal = Math.max(0, subtotal - discount);

        if (paymentMethod === 'Tiền mặt' && !document.getElementById('txtCashReceived').value.trim()) {
            document.getElementById('txtCashReceived').value = finalTotal.toLocaleString('vi-VN');
        } else if (paymentMethod === 'Tiền mặt + Online' && !document.getElementById('txtCashReceived').value.trim()) {
            // Default cash portion to half
            const defaultCash = Math.floor(finalTotal / 2);
            document.getElementById('txtCashReceived').value = defaultCash.toLocaleString('vi-VN');
        }
        recalculatePayment();
    });

    document.getElementById('chkUsePoints').addEventListener('change', () => {
        recalculatePayment();
    });

    document.getElementById('txtCashReceived').addEventListener('input', (e) => {
        let val = e.target.value.replace(/\D/g, "");
        if (val) {
            val = parseInt(val, 10);
            e.target.value = val.toLocaleString('vi-VN');
        } else {
            e.target.value = "";
        }
        recalculatePayment();
    });

    function printThermalInvoice(orderId) {
        window.open(`/Cashier/Order/PrintInvoice?orderId=${orderId}`, '_blank', 'width=380,height=600');
    }

    function clearCurrentCartAndCustomer() {
        localStorage.removeItem('pos_cart');
        localStorage.removeItem('pos_current_customer');
        cart = [];
        currentCustomer = null;
        document.getElementById('chkUsePoints').checked = false;
        document.getElementById('txtCashReceived').value = '';
        renderCart();
        renderCustomerInfo();
        
        const btn = document.getElementById('btnSubmitOrder');
        if(btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check-circle"></i> TẠO ĐƠN &amp; THANH TOÁN';
        }
    }

    // Modal Payment close listeners
    document.getElementById('btnCancelPayment').addEventListener('click', () => {
        document.getElementById('paymentModal').style.display = 'none';
    });

    document.getElementById('btnClosePaymentModal').addEventListener('click', () => {
        document.getElementById('paymentModal').style.display = 'none';
    });

    // Open Payment Modal
    document.getElementById('btnSubmitOrder').addEventListener('click', () => {
        if (cart.length === 0) {
            alert("Giỏ hàng đang trống!");
            return;
        }

        if (!currentCustomer) {
            alert("Vui lòng chọn hoặc thêm Khách hàng để tiếp tục!");
            return;
        }

        // Trigger payment method change to set default cash value
        const evt = new Event('change');
        document.getElementById('posPaymentMethod').dispatchEvent(evt);
        
        document.getElementById('paymentModal').style.display = 'flex';
    });

    // Confirm Payment
    document.getElementById('btnConfirmPayment').addEventListener('click', async () => {

        const subtotal = cart.reduce((acc, curr) => acc + curr.total, 0);
        const paymentMethod = document.getElementById('posPaymentMethod').value;
        const usePoints = document.getElementById('chkUsePoints').checked;

        let pointsUsed = 0;
        if (usePoints && currentCustomer) {
            pointsUsed = Math.min(currentCustomer.loyaltyPoints, Math.floor(subtotal / 500));
        }

        const finalTotal = subtotal - (pointsUsed * 500);
        const cashRaw = document.getElementById('txtCashReceived').value.replace(/[^0-9]/g, '');
        const cashVal = parseFloat(cashRaw) || 0;

        let cashAmount = 0;
        let onlineAmount = 0;

        if (paymentMethod === 'Tiền mặt') {
            if (cashVal < finalTotal) {
                alert(`Tiền mặt khách đưa (${formatCurrency(cashVal)}) không đủ thanh toán tổng tiền (${formatCurrency(finalTotal)}).`);
                return;
            }
            cashAmount = finalTotal;
        } else if (paymentMethod === 'Thanh toán online') {
            onlineAmount = finalTotal;
        } else if (paymentMethod === 'Tiền mặt + Online') {
            cashAmount = Math.min(cashVal, finalTotal);
            onlineAmount = Math.max(0, finalTotal - cashAmount);
            if (onlineAmount <= 0) {
                alert("Bạn chọn Tiền mặt + Online nhưng số tiền mặt đã đủ hoặc dư. Vui lòng chuyển sang phương thức Tiền mặt thuần túy.");
                return;
            }
        }

        const dto = {
            customerId: currentCustomer.customerId,
            IsAtCounter: true,
            totalAmount: subtotal,
            paymentMethod: paymentMethod,
            pointsUsed: pointsUsed,
            cashAmount: cashAmount,
            onlineAmount: onlineAmount,
            items: cart
        };

        const btn = document.getElementById('btnConfirmPayment');
        btn.disabled = true;
        btn.innerHTML = '<i class="bi bi-hourglass-split"></i> ĐANG XỬ LÝ...';

        try {
            const res = await fetch('/Cashier/Order/SubmitOrder', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(dto)
            });
            const data = await res.json();
            
            if (data.success) {
                if (data.redirectUrl) {
                    // Chuyển trực tiếp qua trang thanh toán PayOS
                    window.location.href = data.redirectUrl;
                } else {
                    alert("Tạo đơn hàng & Thanh toán tiền mặt thành công!");
                    document.getElementById('paymentModal').style.display = 'none';
                    btn.disabled = false;
                    btn.innerHTML = '<i class="bi bi-check-circle"></i> XÁC NHẬN';
                    
                    // Print Thermal Invoice immediately
                    printThermalInvoice(data.orderId);

                    // Clear POS state
                    clearCurrentCartAndCustomer();
                }
            } else {
                alert(data.message || "Lỗi tạo đơn hàng.");
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-check-circle"></i> XÁC NHẬN';
            }
        } catch (err) {
            console.error(err);
            alert("Lỗi kết nối.");
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check-circle"></i> XÁC NHẬN';
        }
    });

    // Initialize
    renderCart();
});