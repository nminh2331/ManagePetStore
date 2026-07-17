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
        showSuccessInvoiceModal(orderIdParam);
        
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
    let appliedVoucher = null;
    let currentPayingOrderId = '';
    let completedSpaBookings = [];

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





    // === LOAD DANH SÁCH SPA CHỜ THU ===
    async function loadCompletedSpaBookings() {
        try {
            const res = await fetch('/Cashier/Order/GetCompletedSpaBookings');
            const data = await res.json();
            const badge = document.getElementById('countSpaPending');
            const list = document.getElementById('listSpaPending');
            const empty = document.getElementById('emptySpaPending');
            
            if (data.success && data.data) {
                completedSpaBookings = data.data;
                const count = data.data.length;
                if (badge) badge.textContent = count;
                
                if (count === 0) {
                    if (empty) empty.style.display = 'block';
                    if (list) list.innerHTML = '';
                } else {
                    if (empty) empty.style.display = 'none';
                    if (list) {
                        list.innerHTML = data.data.map(b => {
                            const bookingJson = JSON.stringify(b).replace(/'/g, "&#39;");
                            return `
                                <div class="pos-spa-item" style="padding:16px; border: 1px solid var(--pos-border); border-radius:12px; display:flex; flex-direction:column; gap:8px; cursor:default; background:#fff; align-items: stretch; height: auto;">
                                    <div style="display:flex; justify-content:space-between; align-items:center;">
                                        <span style="font-weight:800; color:var(--admin-primary); font-size:14px;">Mã: #${b.bookingId}</span>
                                        <span style="font-size:11px; color:var(--pos-text-muted);"><i class="bi bi-clock"></i> ${b.dateTime}</span>
                                    </div>
                                    <div style="font-size:12px; color:var(--pos-text); text-align: left;">
                                        <div><strong>Khách:</strong> ${b.customerName} (${b.customerPhone})</div>
                                        <div><strong>Pet:</strong> ${b.petName}</div>
                                        <div><strong>Dịch vụ:</strong> ${b.serviceName}</div>
                                        <div><strong>Nhân viên:</strong> ${b.groomerName || 'Không có'}</div>
                                    </div>
                                    <div style="display:flex; justify-content:space-between; align-items:center; margin-top:8px;">
                                        <strong style="color:#ef4444; font-size:14px;">${formatCurrency(b.price)}</strong>
                                        <button class="btn-pos-primary" style="padding:4px 8px; font-size:11px; border-radius:6px;" ${b.heldForHotel ? 'disabled title="Spa thuộc lượt lưu trú chuồng đang ở"' : `onclick='handleSelectCompletedSpa(${bookingJson})'`}>
                                            <i class="bi ${b.heldForHotel ? 'bi-hourglass-split' : 'bi-plus-circle'}"></i> ${b.heldForHotel ? 'Chờ trả chuồng' : 'Thu tiền'}
                                        </button>
                                    </div>
                                </div>
                            `;
                        }).join('');
                    }
                }
            }
        } catch(err) {
            console.error("Lỗi lấy lịch Spa chờ thu:", err);
        }
    }
    loadCompletedSpaBookings();

    async function loadReadyHotelCheckouts() {
        try {
            const res = await fetch('/Cashier/Order/GetReadyHotelCheckouts');
            const data = await res.json();
            const rows = data.success && data.data ? data.data : [];
            document.getElementById('countHotelPending').textContent = rows.length;
            document.getElementById('emptyHotelPending').style.display = rows.length ? 'none' : 'block';
            document.getElementById('listHotelPending').innerHTML = rows.map(item => {
                const json = JSON.stringify(item).replace(/'/g, "&#39;");
                return `<div class="pos-spa-item" style="padding:16px;border:1px solid var(--pos-border);border-radius:8px;background:#fff;display:grid;gap:8px;">
                    <div style="display:flex;justify-content:space-between;"><strong>HB#${item.hotelBookingId}</strong><small>${item.preparedAt}</small></div>
                    <div style="font-size:12px;"><div><strong>Khách:</strong> ${item.customerName} (${item.customerPhone})</div><div><strong>Pet:</strong> ${item.petName}</div><div><strong>Phòng:</strong> ${item.roomTypeName} · ${item.cageId}</div></div>
                    <div style="display:flex;justify-content:space-between;align-items:center;"><strong style="color:#ef4444;">${formatCurrency(item.total)}</strong><button class="btn-pos-primary" style="padding:5px 9px;font-size:11px;" onclick='handleSelectHotelCheckout(${json})'><i class="bi bi-plus-circle"></i> Thu tiền</button></div>
                </div>`;
            }).join('');
        } catch (error) {
            console.error('Lỗi lấy bảng kê Hotel:', error);
        }
    }
    loadReadyHotelCheckouts();

    window.handleSelectHotelCheckout = function (item) {
        if (currentCustomer && cart.length > 0 && currentCustomer.customerId !== item.customerId) {
            if (!confirm(`Giỏ hiện thuộc ${currentCustomer.fullName}. Chuyển sang ${item.customerName} và xóa giỏ cũ?`)) return;
            clearCurrentCartAndCustomer();
        }
        handleSelectCustomer({ customerId:item.customerId, fullName:item.customerName, phone:item.customerPhone, membershipTier:'Thành viên', loyaltyPoints:0, pets:[{ petId:item.petId, name:item.petName, weight:item.petWeight }] });
        cart = cart.filter(row => !(row.type === 'Hotel' && row.hotelCheckoutId === item.hotelCheckoutId));
        cart.push({ type:'Hotel', id:String(item.roomTypeId), name:`Chuồng ${item.roomTypeName} - ${item.petName}`, quantity:1, price:item.total, total:item.total, petId:item.petId, petName:item.petName, hotelCheckoutId:item.hotelCheckoutId });

        (item.linkedSpaBookingIds || []).forEach(spaId => {
            const spa = completedSpaBookings.find(row => row.bookingId === spaId);
            if (!spa || cart.some(row => row.type === 'Spa' && row.bookingId === spa.bookingId)) return;
            cart.push({ type:'Spa', id:String(spa.serviceId), name:spa.serviceName, quantity:1, price:spa.price, total:spa.price, petId:spa.petId, petName:spa.petName, petWeight:spa.petWeight || 5, groomerId:spa.groomerId, appointmentTime:new Date().toISOString(), bookingId:spa.bookingId });
        });
        saveCartLocally();
        renderCart();
        updateSpaBookingSummary();
    };

    window.handleSelectCompletedSpa = function (b) {
        // 1. Kiểm tra nếu đã chọn khách hàng khác và giỏ hàng không trống
        if (currentCustomer && cart.length > 0 && (currentCustomer.customerId !== b.customerId || currentCustomer.phone !== b.customerPhone)) {
            if (!confirm(`Giỏ hàng hiện tại đang chứa dịch vụ của khách hàng: ${currentCustomer.fullName}. Bạn có muốn chuyển sang khách hàng mới: ${b.customerName} và xóa giỏ hàng cũ?`)) {
                return;
            }
            clearCurrentCartAndCustomer();
        }

        // 2. Nạp khách hàng vào POS
        const mockCustomer = {
            customerId: b.customerId,
            fullName: b.customerName,
            phone: b.customerPhone,
            membershipTier: "Thành viên",
            loyaltyPoints: 0,
            pets: [{ petId: b.petId, name: b.petName, weight: b.petWeight || 5 }]
        };
        handleSelectCustomer(mockCustomer);

        // 3. Thêm mặt hàng Spa này vào giỏ hàng POS với BookingId liên kết
        cart = cart.filter(item => !(item.type === 'Spa' && item.bookingId === b.bookingId));
        
        cart.push({
            type: 'Spa',
            id: String(b.serviceId),
            name: b.serviceName,
            quantity: 1,
            price: b.price,
            total: b.price,
            petId: b.petId,
            petName: b.petName,
            petWeight: b.petWeight || 5,
            groomerId: b.groomerId,
            appointmentTime: new Date().toISOString(),
            bookingId: b.bookingId
        });

        saveCartLocally();
        renderCart();
        
        // 4. Hiển thị thông tin ca Spa đang thanh toán tự động
        updateSpaBookingSummary();

        // Báo hiệu thành công
        alert(`Đã nạp thành công dịch vụ của bé ${b.petName} (Mã lịch: #${b.bookingId}) vào giỏ hàng.`);
    };

    function updateSpaBookingSummary() {
        const summary = document.getElementById('spaBookingSummary');
        if (!summary) return;

        const spaItems = cart.filter(item => item.type === 'Spa');
        if (spaItems.length === 0) {
            summary.style.display = 'none';
            return;
        }

        const listHtml = spaItems.map(item => `
            <div style="margin-bottom: 8px; border-bottom: 1px dashed #fcd34d; padding-bottom: 6px; last-child { border-bottom: none; }">
                <p style="margin: 0; font-size: 13px; color: #b45309; font-weight: 700; margin-bottom: 2px;"><i class="bi bi-scissors"></i> Ca Spa: <span style="font-weight: 500;">${item.name}</span></p>
                <p style="margin: 0; font-size: 13px; color: #b45309; font-weight: 700;"><i class="bi bi-heart"></i> Thú cưng: <span style="font-weight: 500;">${item.petName} (${item.petWeight}kg)</span></p>
            </div>
        `).join('');

        summary.innerHTML = listHtml;
        summary.style.display = 'block';
    }

    // === TABS LOGIC ===
    const tabs = document.querySelectorAll('.pos-tab');
    const tabContents = document.querySelectorAll('.pos-tab-content');
    
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            tabs.forEach(t => t.classList.remove('active'));
            tabContents.forEach(tc => tc.classList.remove('active'));
            
            tab.classList.add('active');
            const targetId = tab.getAttribute('data-target');
            document.getElementById(targetId).classList.add('active');
            
            if (targetId === 'cart-spa-pending') {
                loadCompletedSpaBookings();
            } else if (targetId === 'cart-hotel-pending') {
                loadReadyHotelCheckouts();
            }
        });
    });

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
                        <span>Thú cưng: ${pets}</span>
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
            const summary = document.getElementById('spaBookingSummary');
            if (summary) summary.style.display = 'none';
            recalculatePayment();
            return;
        }

        document.getElementById('lblCustomerName').textContent = currentCustomer.fullName;
        document.getElementById('lblCustomerPhone').textContent = currentCustomer.phone;

        document.getElementById('selectedCustomerInfo').style.display = 'block';
        updateSpaBookingSummary();
        recalculatePayment();
    }

    document.getElementById('btnClearCustomer').addEventListener('click', () => {
        currentCustomer = null;
        renderCustomerInfo();
        
        const summary = document.getElementById('spaBookingSummary');
        if (summary) summary.style.display = 'none';
        
        // Remove Spa items from cart because they require a customer
        cart = cart.filter(c => c.type !== 'Spa' && c.type !== 'Hotel');
        saveCartLocally();
        renderCart();
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

        if (!listProducts || !emptyProducts) return;

        const count = cart.length;
        const countEl = document.getElementById('countProducts');
        if (countEl) countEl.textContent = count;

        const cartItemsCard = document.getElementById('cartItemsCard');
        if (cartItemsCard) {
            cartItemsCard.style.display = count > 0 ? 'block' : 'none';
        }

        if (count === 0) {
            emptyProducts.style.display = '';
            listProducts.innerHTML = '';
        } else {
            emptyProducts.style.display = 'none';
            listProducts.innerHTML = cart.map((item, idx) => {
                if (item.type === 'Product') {
                    return `
                        <div class="pos-cart-item">
                            <div class="pos-item-icon"><i class="bi bi-box"></i></div>
                            <div class="pos-item-details">
                                <div class="pos-item-name">${item.name}</div>
                                <div class="pos-item-meta">${formatCurrency(item.price)}</div>
                            </div>
                            <div class="pos-qty-control">
                                <button class="pos-qty-btn" onclick="changeQty(${idx}, -1)">-</button>
                                <input type="text" class="pos-qty-input" value="${item.quantity}" readonly />
                                <button class="pos-qty-btn" onclick="changeQty(${idx}, 1)">+</button>
                            </div>
                            <div class="pos-item-price">${formatCurrency(item.total)}</div>
                            <button class="btn-remove-item" onclick="removeItem(${idx})"><i class="bi bi-trash"></i></button>
                        </div>
                    `;
                } else if (item.type === 'Spa') {
                    return `
                        <div class="pos-cart-item">
                            <div class="pos-item-icon"><i class="bi bi-scissors"></i></div>
                            <div class="pos-item-details">
                                <div class="pos-item-name">${item.name} (Dịch vụ Spa)</div>
                                <div class="pos-item-meta">Bé: ${item.petWeight}kg</div>
                            </div>
                            <div class="pos-qty-control" style="visibility: hidden;">
                                <button class="pos-qty-btn">-</button>
                                <input type="text" class="pos-qty-input" value="1" readonly />
                                <button class="pos-qty-btn">+</button>
                            </div>
                            <div class="pos-item-price">${formatCurrency(item.total)}</div>
                            <button class="btn-remove-item" onclick="removeItem(${idx})"><i class="bi bi-trash"></i></button>
                        </div>
                    `;
                } else {
                    return `<div class="pos-cart-item"><div class="pos-item-icon"><i class="bi bi-house-check"></i></div><div class="pos-item-details"><div class="pos-item-name">${item.name}</div><div class="pos-item-meta">Bảng kê chuồng đã chốt</div></div><div class="pos-qty-control" style="visibility:hidden;"></div><div class="pos-item-price">${formatCurrency(item.total)}</div><button class="btn-remove-item" onclick="removeItem(${idx})"><i class="bi bi-trash"></i></button></div>`;
                }
            }).join('');
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

        if (appliedVoucher) {
            discount = appliedVoucher.discount;
        }

        const finalTotal = Math.max(0, subtotal - discount);
        document.getElementById('lblTotal').textContent = formatCurrency(finalTotal);
        
        const lblModalSubtotal = document.getElementById('lblModalSubtotal');
        if (lblModalSubtotal) lblModalSubtotal.textContent = formatCurrency(subtotal);

        const rowDiscount = document.getElementById('rowModalDiscount');
        const lblModalDiscount = document.getElementById('lblModalDiscount');
        if (rowDiscount && lblModalDiscount) {
            if (discount > 0) {
                lblModalDiscount.textContent = `-${formatCurrency(discount)}`;
                rowDiscount.style.display = 'flex';
            } else {
                rowDiscount.style.display = 'none';
            }
        }

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
        if (appliedVoucher) {
            discount = appliedVoucher.discount;
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

    async function showSuccessInvoiceModal(orderId) {
        currentPayingOrderId = orderId;
        const container = document.getElementById('printReceiptContent');
        if (!container) return;

        container.innerHTML = `
            <div style="text-align: center; padding: 20px;">
                <div class="spinner-border text-success" role="status" style="width: 2rem; height: 2rem;">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p style="margin-top:12px;color:var(--pos-text-muted);">Đang tải hóa đơn...</p>
            </div>
        `;
        document.getElementById('posSuccessModal').style.display = 'flex';

        try {
            const res = await fetch(`/Cashier/Order/GetInvoiceData?orderId=${orderId}`);
            const resData = await res.json();
            if (resData.success) {
                let itemsHtml = resData.items.map(item => `
                    <tr>
                        <td style="padding: 4px 0;">${item.name}</td>
                        <td style="padding: 4px 0; text-align: center;">${item.quantity}</td>
                        <td style="padding: 4px 0; text-align: right;">${formatCurrency(item.total)}</td>
                    </tr>
                `).join('');

                let spaHtml = '';
                if (resData.spaBookings && resData.spaBookings.length > 0) {
                    spaHtml = resData.spaBookings.map(b => `
                        <div style="border-top: 2px dashed #000; margin: 15px 0;"></div>
                        <div class="spa-ticket" style="border: 1px dashed #000; padding: 12px; background: #fff; text-align: left;">
                            <div style="font-size: 13px; font-weight: bold; text-align: center; text-transform: uppercase; margin-bottom: 8px;">PHIẾU DỊCH VỤ SPA</div>
                            <table style="width: 100%; border-collapse: collapse; font-size: 11px;">
                                <tr>
                                    <td style="width: 40%; padding: 2px 0;">Mã hóa đơn:</td>
                                    <td style="font-weight: bold; padding: 2px 0;">${resData.orderId}</td>
                                </tr>
                                <tr>
                                    <td style="padding: 2px 0;">Dịch vụ:</td>
                                    <td style="font-weight: bold; padding: 2px 0;">${b.serviceName}</td>
                                </tr>
                                <tr>
                                    <td style="padding: 2px 0;">Khách hàng:</td>
                                    <td style="padding: 2px 0;">${resData.customerName}</td>
                                </tr>
                                <tr>
                                    <td style="padding: 2px 0;">Tên Pet:</td>
                                    <td style="font-weight: bold; padding: 2px 0;">${b.petName} (${b.petSpecies})</td>
                                </tr>
                                <tr>
                                    <td style="padding: 2px 0;">Cân nặng Pet:</td>
                                    <td style="padding: 2px 0;">${b.petWeight}kg</td>
                                </tr>
                                <tr>
                                    <td style="padding: 2px 0;">Groomer:</td>
                                    <td style="font-weight: bold; padding: 2px 0;">${b.groomerName}</td>
                                </tr>
                                <tr>
                                    <td style="padding: 2px 0;">Giờ hẹn:</td>
                                    <td style="font-weight: bold; padding: 2px 0; font-size: 12px;">${b.dateTime}</td>
                                </tr>
                            </table>
                        </div>
                    `).join('');
                }

                let hotelHtml = '';
                if (resData.hotelCheckouts && resData.hotelCheckouts.length > 0) {
                    hotelHtml = resData.hotelCheckouts.map(hotel => `<div style="border-top:2px dashed #000;margin:15px 0;"></div><div style="border:1px dashed #000;padding:12px;text-align:left;"><div style="font-size:13px;font-weight:bold;text-align:center;margin-bottom:8px;">BẢNG KÊ CHUỒNG HB#${hotel.hotelBookingId}</div><div style="font-size:11px;margin-bottom:6px;">Pet: <strong>${hotel.petName}</strong> · ${hotel.roomType} · chuồng ${hotel.cageId}</div>${(hotel.items || []).map(item => `<div style="display:flex;justify-content:space-between;font-size:11px;"><span>${item.description}</span><strong>${formatCurrency(item.amount)}</strong></div>`).join('')}<div style="display:flex;justify-content:space-between;border-top:1px dashed #000;margin-top:6px;padding-top:5px;"><strong>Tổng chuồng</strong><strong>${formatCurrency(hotel.totalAmount)}</strong></div></div>`).join('');
                }

                container.innerHTML = `
                    <div style="text-align: center; margin-bottom: 12px;">
                        <h1 style="font-size: 18px; margin: 0 0 5px 0; text-transform: uppercase; font-weight: bold;">PET STORE</h1>
                        <p style="margin: 2px 0; font-size: 11px;">Địa chỉ: Thôn 1, Xã Thạch Thất, Hà Nội</p>
                        <p style="margin: 2px 0; font-size: 11px;">SĐT: 0915793038</p>
                        <p style="font-weight: bold; margin: 5px 0 0 0;">HÓA ĐƠN THANH TOÁN</p>
                    </div>

                    <table style="width: 100%; border-collapse: collapse; font-size: 11px;">
                        <tr>
                            <td style="width: 40%; padding: 2px 0;">Mã hóa đơn:</td>
                            <td style="font-weight: bold; padding: 2px 0;">${resData.orderId}</td>
                        </tr>
                        <tr>
                            <td style="padding: 2px 0;">Ngày mua:</td>
                            <td style="padding: 2px 0;">${resData.date}</td>
                        </tr>
                        <tr>
                            <td style="padding: 2px 0;">Khách hàng:</td>
                            <td style="padding: 2px 0;">${resData.customerName}</td>
                        </tr>
                        <tr>
                            <td style="padding: 2px 0;">SĐT khách:</td>
                            <td style="padding: 2px 0;">${resData.customerPhone}</td>
                        </tr>
                        <tr>
                            <td style="padding: 2px 0;">Nhân viên:</td>
                            <td style="padding: 2px 0;">Thu ngân POS</td>
                        </tr>
                    </table>

                    <div style="border-top: 1px dashed #000; margin: 10px 0;"></div>

                    <table style="width: 100%; border-collapse: collapse; font-size: 11px;">
                        <thead>
                            <tr style="border-bottom: 1px solid #000;">
                                <th style="width: 50%; padding: 4px 0; text-align: left;">Tên SP/DV</th>
                                <th style="width: 15%; padding: 4px 0; text-align: center;">SL</th>
                                <th style="width: 35%; padding: 4px 0; text-align: right;">Thành tiền</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${itemsHtml}
                        </tbody>
                    </table>

                    <div style="border-top: 1px dashed #000; margin: 10px 0;"></div>

                    <div style="margin-top: 8px; font-size: 11px;">
                        <div style="display: flex; justify-content: space-between; padding: 2px 0;">
                            <span>Tạm tính:</span>
                            <span>${formatCurrency(resData.subtotal)}</span>
                        </div>
                        ${resData.discount > 0 ? `
                        <div style="display: flex; justify-content: space-between; padding: 2px 0;">
                            <span>Giảm giá voucher:</span>
                            <span>-${formatCurrency(resData.discount)}</span>
                        </div>
                        ` : ''}
                        <div style="display: flex; justify-content: space-between; padding: 4px 0; font-weight: bold; font-size: 13px;">
                            <span>TỔNG CỘNG:</span>
                            <span>${formatCurrency(resData.total)}</span>
                        </div>
                        <div style="border-top: 1px dashed #000; margin: 8px 0;"></div>
                        <div style="display: flex; justify-content: space-between; padding: 2px 0;">
                            <span>Phương thức:</span>
                            <span style="font-weight: bold;">${resData.paymentMethod}</span>
                        </div>
                    </div>

                    <div style="border-top: 1px dashed #000; margin: 10px 0;"></div>

                    <div style="font-size: 11px;">
                        <div style="display: flex; justify-content: space-between; padding: 2px 0;">
                            <span>Voucher áp dụng:</span>
                            <span style="font-weight: bold;">${resData.voucherCode ? `${resData.voucherCode} (-${formatCurrency(resData.discount)})` : 'Không áp dụng voucher'}</span>
                        </div>
                    </div>

                    <div style="border-top: 1px dashed #000; margin: 10px 0;"></div>

                    <div style="text-align: center; margin-top: 12px; font-size: 11px;">
                        <p style="font-weight: bold; margin: 0 0 4px 0;">CẢM ƠN QUÝ KHÁCH & HẸN GẶP LẠI!</p>
                        <p style="margin: 0; font-size: 10px; color: #555;">Chăm sóc thú cưng như người thân</p>
                    </div>

                    <!-- PHIẾU DỊCH VỤ SPA -->
                    ${spaHtml}
                    ${hotelHtml}
                `;
            } else {
                container.innerHTML = `<p style="color:var(--pos-danger);text-align:center;padding:20px;">Lỗi tải chi tiết hóa đơn: ${resData.message}</p>`;
            }
        } catch (err) {
            console.error(err);
            container.innerHTML = `<p style="color:var(--pos-danger);text-align:center;padding:20px;">Lỗi kết nối máy chủ.</p>`;
        }
    }

    function clearCurrentCartAndCustomer() {
        localStorage.removeItem('pos_cart');
        localStorage.removeItem('pos_current_customer');
        cart = [];
        currentCustomer = null;
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

    // Apply Voucher
    document.getElementById('btnApplyVoucher').addEventListener('click', async () => {
        const code = document.getElementById('txtVoucherCode').value.trim();
        const msg = document.getElementById('voucherMessage');
        
        if (!code) {
            msg.textContent = "Vui lòng nhập mã giảm giá.";
            msg.style.color = "var(--pos-danger)";
            msg.style.display = "block";
            return;
        }

        const subtotal = cart.reduce((acc, curr) => acc + curr.total, 0);

        try {
            const res = await fetch(`/Cashier/Order/CheckVoucher?code=${encodeURIComponent(code)}&subtotal=${subtotal}`);
            const data = await res.json();
            
            if (data.success) {
                appliedVoucher = { code: data.code, discount: data.discount };
                msg.textContent = `Áp dụng thành công voucher ${data.code}. Giảm: -${formatCurrency(data.discount)}`;
                msg.style.color = "var(--pos-success)";
                msg.style.display = "block";
                recalculatePayment();
            } else {
                appliedVoucher = null;
                msg.textContent = data.message;
                msg.style.color = "var(--pos-danger)";
                msg.style.display = "block";
                recalculatePayment();
            }
        } catch (err) {
            msg.textContent = "Lỗi kết nối máy chủ.";
            msg.style.color = "var(--pos-danger)";
            msg.style.display = "block";
        }
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



        // Reset voucher input and state when opening
        appliedVoucher = null;
        const txtVoucher = document.getElementById('txtVoucherCode');
        const hasHotelItem = cart.some(item => item.type === 'Hotel');
        if (txtVoucher) {
            txtVoucher.value = '';
            txtVoucher.disabled = hasHotelItem;
            txtVoucher.placeholder = hasHotelItem ? 'Voucher chưa áp dụng cho dịch vụ chuồng' : 'Nhập mã voucher (VD: PET20)';
        }
        document.getElementById('btnApplyVoucher').disabled = hasHotelItem;
        const voucherMsg = document.getElementById('voucherMessage');
        if (voucherMsg) {
            voucherMsg.style.display = 'none';
            voucherMsg.textContent = '';
        }
        const rowDiscount = document.getElementById('rowModalDiscount');
        if (rowDiscount) rowDiscount.style.display = 'none';
        
        // Trigger payment method change to set default cash value
        const evt = new Event('change');
        document.getElementById('posPaymentMethod').dispatchEvent(evt);
        
        document.getElementById('paymentModal').style.display = 'flex';
    });

    // Confirm Payment
    document.getElementById('btnConfirmPayment').addEventListener('click', async () => {

        const subtotal = cart.reduce((acc, curr) => acc + curr.total, 0);
        const paymentMethod = document.getElementById('posPaymentMethod').value;
        const voucherCode = appliedVoucher ? appliedVoucher.code : null;
        const voucherDiscount = appliedVoucher ? appliedVoucher.discount : 0;

        const finalTotal = Math.max(0, subtotal - voucherDiscount);
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
            totalAmount: subtotal,
            paymentMethod: paymentMethod,
            pointsUsed: 0,
            cashAmount: cashAmount,
            onlineAmount: onlineAmount,
            voucherCode: voucherCode,
            voucherDiscount: voucherDiscount,
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
                    document.getElementById('paymentModal').style.display = 'none';
                    btn.disabled = false;
                    btn.innerHTML = '<i class="bi bi-check-circle"></i> XÁC NHẬN';
                    
                    // Hiển thị trực tiếp Popup hóa đơn thành công lên màn hình
                    showSuccessInvoiceModal(data.orderId);
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

    // Đóng và dọn dẹp POS khi bấm đóng modal thành công
    const btnSuccessClose = document.getElementById('btnSuccessClose');
    if (btnSuccessClose) {
        btnSuccessClose.addEventListener('click', () => {
            document.getElementById('posSuccessModal').style.display = 'none';
            clearCurrentCartAndCustomer();
            loadCompletedSpaBookings();
            loadReadyHotelCheckouts();
        });
    }

    // Xuất PDF hóa đơn thành công
    const btnSuccessPDF = document.getElementById('btnSuccessPDF');
    if (btnSuccessPDF) {
        btnSuccessPDF.addEventListener('click', () => {
            const element = document.getElementById('printReceiptContent');
            if (!element) return;
            
            const opt = {
                margin:       [10, 10, 10, 10],
                filename:     `HoaDon_${currentPayingOrderId || 'Receipt'}.pdf`,
                image:        { type: 'jpeg', quality: 0.98 },
                html2canvas:  { scale: 2, useCORS: true },
                jsPDF:        { unit: 'mm', format: 'a4', orientation: 'portrait' }
            };
            
            const oldHtml = btnSuccessPDF.innerHTML;
            btnSuccessPDF.disabled = true;
            btnSuccessPDF.innerHTML = '<i class="bi bi-hourglass-split"></i> Đang xuất...';
            
            html2pdf().set(opt).from(element).save().then(() => {
                btnSuccessPDF.disabled = false;
                btnSuccessPDF.innerHTML = oldHtml;
            }).catch(err => {
                console.error(err);
                alert("Không thể xuất file PDF.");
                btnSuccessPDF.disabled = false;
                btnSuccessPDF.innerHTML = oldHtml;
            });
        });
    }

    // Initialize
    renderCart();
});
