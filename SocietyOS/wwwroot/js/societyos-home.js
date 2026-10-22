/**
 * SocietyOS Home Screen Client Interactions
 */
document.addEventListener("DOMContentLoaded", () => {
    // 1. Expand / Collapse Quick Actions
    const toggleBtn = document.getElementById("toggleQuickActionsBtn");
    const expandedWrapper = document.getElementById("expandedActionsWrapper");
    const chevron = document.getElementById("qaChevron");
    const toggleText = document.getElementById("qaToggleText");

    if (toggleBtn && expandedWrapper && chevron) {
        toggleBtn.addEventListener("click", () => {
            const isExpanded = expandedWrapper.classList.toggle("open");
            chevron.classList.toggle("chevron-rotated", isExpanded);
            toggleText.textContent = isExpanded ? "Show Less" : "View All";
            toggleBtn.setAttribute("aria-expanded", isExpanded.toString());
        });
    }

    // 2. Profile Offcanvas Drawer
    const profileAvatarBtn = document.getElementById("profileAvatarBtn");
    const profileDrawer = document.getElementById("profileDrawer");
    const closeProfileBtn = document.getElementById("closeProfileBtn");
    const drawerBackdrop = document.getElementById("drawerBackdrop");

    function openProfile() {
        profileDrawer?.classList.add("open");
        drawerBackdrop?.classList.add("show");
        document.body.style.overflow = "hidden";
    }

    function closeProfile() {
        profileDrawer?.classList.remove("open");
        drawerBackdrop?.classList.remove("show");
        document.body.style.overflow = "";
    }

    profileAvatarBtn?.addEventListener("click", openProfile);
    closeProfileBtn?.addEventListener("click", closeProfile);
    drawerBackdrop?.addEventListener("click", closeProfile);

    // 3. Search Control Modal
    const searchTriggerBtn = document.getElementById("searchTriggerBtn");
    const searchModal = document.getElementById("searchModal");
    const closeSearchBtn = document.getElementById("closeSearchBtn");
    const searchInput = document.getElementById("societySearchInput");

    function openSearch() {
        searchModal?.classList.add("show");
        document.body.style.overflow = "hidden";
        setTimeout(() => searchInput?.focus(), 100);
    }

    function closeSearch() {
        searchModal?.classList.remove("show");
        document.body.style.overflow = "";
    }

    searchTriggerBtn?.addEventListener("click", openSearch);
    closeSearchBtn?.addEventListener("click", closeSearch);
    searchModal?.addEventListener("click", (e) => {
        if (e.target === searchModal) closeSearch();
    });

    // 4. Honest Feedback Modal for Planned Features
    const featureModal = document.getElementById("featureModal");
    const featureModalTitle = document.getElementById("featureModalTitle");
    const featureModalNotice = document.getElementById("featureModalNotice");
    const closeFeatureBtn = document.getElementById("closeFeatureBtn");

    document.querySelectorAll("[data-planned-feature='true']").forEach((card) => {
        card.addEventListener("click", (e) => {
            e.preventDefault();
            const title = card.getAttribute("data-feature-title") || "Feature";
            const notice = card.getAttribute("data-feature-notice") || "This feature is scheduled for development in upcoming tasks.";

            if (featureModalTitle && featureModalNotice && featureModal) {
                featureModalTitle.textContent = title;
                featureModalNotice.textContent = notice;
                featureModal.classList.add("show");
                document.body.style.overflow = "hidden";
            }
        });
    });

    closeFeatureBtn?.addEventListener("click", () => {
        featureModal?.classList.remove("show");
        document.body.style.overflow = "";
    });

    featureModal?.addEventListener("click", (e) => {
        if (e.target === featureModal) {
            featureModal.classList.remove("show");
            document.body.style.overflow = "";
        }
    });

    // Escape key closes open overlays
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") {
            closeProfile();
            closeSearch();
            featureModal?.classList.remove("show");
            document.body.style.overflow = "";
        }
    });
});