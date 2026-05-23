window.LuxStore = (() => {
    const q = (s, p = document) => p.querySelector(s);
    const qa = (s, p = document) => Array.from(p.querySelectorAll(s));

    async function updateCartBadge() {
        const badge = q("#cartBadge");
        if (!badge) return;
        try {
            const res = await fetch("/Cart/CartCount");
            const data = await res.json();
            badge.textContent = data.count || 0;
        } catch {}
    }

    async function updateWishlistBadge() {
        const badge = q("#wishlistBadge");
        if (!badge) return;
        try {
            const res = await fetch("/Wishlist/Count");
            const data = await res.json();
            badge.textContent = data.count || 0;
        } catch {}
    }

    async function loadMiniCart() {
        const body = q("#miniCartBody");
        if (!body) return;
        const html = await (await fetch("/Cart/MiniCart")).text();
        body.innerHTML = html;
    }

    function initHeader() {
        const header = q(".site-header");
        if (header) {
            window.addEventListener("scroll", () => {
                header.classList.toggle("shrink", window.scrollY > 20);
            });
        }

        const openBtn = q("#openSearchPanel");
        const closeBtn = q("#closeSearchPanel");
        const panel = q("#searchPanel");
        const overlay = q("#searchOverlay");
        const open = () => {
            panel?.classList.add("open");
            overlay?.classList.add("open");
            q("#headerSearchInput")?.focus();
        };
        const close = () => {
            panel?.classList.remove("open");
            overlay?.classList.remove("open");
        };
        openBtn?.addEventListener("click", open);
        closeBtn?.addEventListener("click", close);
        overlay?.addEventListener("click", close);
    }

    function initSearchSuggest() {
        const input = q("#headerSearchInput");
        const box = q("#searchSuggestBox");
        if (!input || !box) return;

        input.addEventListener("input", async () => {
            const val = input.value.trim();
            if (val.length < 2) {
                box.classList.add("d-none");
                box.innerHTML = "";
                return;
            }
            const data = await (await fetch(`/Product/SearchSuggest?q=${encodeURIComponent(val)}`)).json();
            if (!data.length) {
                box.innerHTML = '<div class="p-3 small text-muted">Không có kết quả.</div>';
                box.classList.remove("d-none");
                return;
            }
            box.innerHTML = data.map(x => `
                <a href="/Product/Detail/${x.id}" class="d-flex gap-2 p-2 border-bottom text-decoration-none">
                    <img src="${x.primaryImageUrl}" width="46" height="46" style="object-fit:cover;" />
                    <div>
                        <div class="small text-dark">${x.name}</div>
                        <div class="small text-muted">${Number(x.price).toLocaleString("vi-VN")} ₫</div>
                    </div>
                </a>
            `).join("") + `<a class="d-block text-center p-2 text-dark" href="/Product?q=${encodeURIComponent(val)}">Xem tất cả</a>`;
            box.classList.remove("d-none");
        });
    }

    function initMiniCart() {
        const toggle = q("#miniCartToggle");
        toggle?.addEventListener("click", async e => {
            e.preventDefault();
            await loadMiniCart();
            const el = q("#miniCartCanvas");
            if (el) new bootstrap.Offcanvas(el).show();
        });
    }

    function initCategoryMenu() {
        const menu = q(".product-cat-menu");
        const header = q(".product-cat-header", menu || document);
        const content = q(".product-cat-content", menu || document);
        if (!menu || !header || !content) return;
        const isHomeDefaultOpen = content.classList.contains("show");

        function closeAllSubMenus() {
            content.querySelectorAll(".menu-item-has-children > .sub-menu").forEach(sm => {
                sm.style.display = "";
            });
        }

        // Home keeps the menu open by default.
        if (content.classList.contains("show")) {
            menu.classList.add("menu-open");
        }

        header.addEventListener("click", e => {
            e.preventDefault();
            const isOpen = menu.classList.contains("menu-open");
            menu.classList.toggle("menu-open", !isOpen);
            header.classList.toggle("show", !isOpen);
            content.classList.toggle("show", !isOpen);
            if (isOpen) closeAllSubMenus();
        });

        document.addEventListener("click", e => {
            if (isHomeDefaultOpen) return;
            if (!menu.contains(e.target)) {
                menu.classList.remove("menu-open");
                header.classList.remove("show");
                content.classList.remove("show");
                closeAllSubMenus();
            }
        });

        document.addEventListener("keydown", e => {
            if (isHomeDefaultOpen) return;
            if (e.key === "Escape") {
                menu.classList.remove("menu-open");
                header.classList.remove("show");
                content.classList.remove("show");
                closeAllSubMenus();
            }
        });

        // Tablet/Mobile: touch không hỗ trợ hover, nên mở sub-menu bằng click.
        if (window.matchMedia && window.matchMedia("(max-width: 1024px)").matches) {
            content.addEventListener("click", e => {
                const link = e.target.closest(".menu-item-has-children > a");
                if (!link) return;
                const li = link.parentElement;
                const subMenu = li?.querySelector(".sub-menu");
                if (!subMenu) return;

                // Chỉ xử lý khi menu danh mục đang mở.
                if (!content.classList.contains("show")) return;

                e.preventDefault();

                // Đóng các sub-menu khác trong cùng menu để tránh rối.
                content.querySelectorAll(".menu-item-has-children > .sub-menu").forEach(sm => {
                    if (sm !== subMenu) sm.style.display = "";
                });

                const isSubOpen = subMenu.style.display === "block";
                subMenu.style.display = isSubOpen ? "" : "block";
            });
        }
    }

    function initProductGallery() {
        qa("[data-gallery-thumb]").forEach(x => {
            x.addEventListener("click", () => {
                const main = q("#productMainImage");
                if (main) main.src = x.getAttribute("data-gallery-thumb");
            });
        });
    }

    function initQuantityControls() {
        qa("[data-qty-plus]").forEach(btn => btn.addEventListener("click", () => {
            const target = q(btn.getAttribute("data-qty-plus"));
            if (target) target.value = String((parseInt(target.value || "1", 10) || 1) + 1);
        }));
        qa("[data-qty-minus]").forEach(btn => btn.addEventListener("click", () => {
            const target = q(btn.getAttribute("data-qty-minus"));
            if (target) {
                const curr = (parseInt(target.value || "1", 10) || 1) - 1;
                target.value = String(Math.max(1, curr));
            }
        }));
    }

    function initFadeInSections() {
        const sections = qa(".product-card");
        sections.forEach(el => el.classList.add("fade-in-up"));
        const io = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (entry.isIntersecting) entry.target.classList.add("visible");
            });
        }, { threshold: 0.1 });
        sections.forEach(el => io.observe(el));
    }

    function showToast(message, type = 'success') {
        const el = document.createElement('div');
        el.className = 'toast-notification';
        if (type === 'error') el.style.borderLeftColor = '#dc3545';
        el.textContent = message;
        document.body.appendChild(el);
        requestAnimationFrame(() => el.classList.add('show'));
        setTimeout(() => {
            el.classList.remove('show');
            setTimeout(() => el.remove(), 350);
        }, 3000);
    }

    function init() {
        initHeader();
        initSearchSuggest();
        initMiniCart();
        initCategoryMenu();
        initProductGallery();
        initQuantityControls();
        initFadeInSections();
        updateCartBadge();
        updateWishlistBadge();
    }

    return { init, updateCartBadge, updateWishlistBadge, showToast };
})();

document.addEventListener("DOMContentLoaded", LuxStore.init);
