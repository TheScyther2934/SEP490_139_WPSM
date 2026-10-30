(() => {
  const status = document.getElementById("wpsm-api-status");
  const formatMoney = value => new Intl.NumberFormat("vi-VN").format(value) + " ₫";
  const replaceText = (root, values) => {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    const nodes = [];
    while (walker.nextNode()) nodes.push(walker.currentNode);
    for (const node of nodes) {
      let text = node.nodeValue;
      for (const [from, to] of Object.entries(values)) text = text.replaceAll(from, to);
      node.nodeValue = text;
    }
  };
  let messageTimer;
  const message = text => {
    status.textContent = text;
    status.classList.remove("is-error");
    status.classList.add("is-ready");
    window.clearTimeout(messageTimer);
    messageTimer = window.setTimeout(() => {
      if (window.wpsm?.project) status.textContent = `API kết nối · ${window.wpsm.project.id}`;
    }, 4500);
  };
  const send = async (path, method, payload) => {
    const response = await fetch(`/api/wpsm/${path}`, {
      method,
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(payload)
    });
    const result = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) throw new Error(result?.message || `API trả về ${response.status}`);
    return result;
  };
  const sendFile = async (path, file) => {
    const body = new FormData();
    body.append("receipt", file);
    const response = await fetch(`/api/wpsm/${path}`, { method: "POST", body });
    const result = await response.json().catch(() => null);
    if (!response.ok) throw new Error(result?.message || `API trả về ${response.status}`);
    return result;
  };
  const moneyValue = input => Number((input?.value || "").replace(/[^0-9]/g, "")) || 0;
  const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[char]);
  const money = value => new Intl.NumberFormat("vi-VN").format(value) + " ₫";
  const hydrateTables = data => {
    const projectBody = document.querySelector('[data-wpsm-screen="M02"] #projects-table tbody');
    if (projectBody) projectBody.innerHTML = data.projects.map(project => {
      const date = new Intl.DateTimeFormat("vi-VN").format(new Date(`${project.eventDate}T00:00:00`));
      return `<tr class="hover:bg-surface-container-low/60 transition-colors"><td class="py-4 px-4 whitespace-nowrap"><span class="font-semibold text-primary">${escapeHtml(project.id)}</span></td><td class="py-4 px-4"><div class="font-medium">${escapeHtml(project.coupleName)}</div></td><td class="py-4 px-4">${escapeHtml(project.hall)}</td><td class="py-4 px-3 text-center">${escapeHtml(project.slot)}</td><td class="py-4 px-4">${date}</td><td class="py-4 px-4 text-center">—</td><td class="py-4 px-4 text-center">${escapeHtml(project.status)}</td><td class="py-4 px-4 text-right"><a class="text-primary font-semibold" href="/M04?id=${encodeURIComponent(project.id)}">Mở dự án</a></td></tr>`;
    }).join("");

    const catalogBody = document.querySelector('[data-wpsm-screen="A02"] table tbody');
    if (catalogBody) catalogBody.innerHTML = data.catalog.map(item => `<tr class="hover:bg-surface-container-low transition-colors"><td class="py-4 px-6"><div class="font-semibold text-primary">${escapeHtml(item.name)}</div><div class="text-xs text-on-surface-variant">SKU: ${escapeHtml(item.id)}</div></td><td class="py-4 px-4">${escapeHtml(item.category)}</td><td class="py-4 px-4">${escapeHtml(item.unit)}</td><td class="py-4 px-6 text-right">${money(item.basePrice)}</td><td class="py-4 px-6 text-center">${item.active ? "Đang hoạt động" : "Đã tắt"}</td><td class="py-4 px-6 text-right">—</td></tr>`).join("");

    const priceBody = document.querySelector('[data-wpsm-screen="A04"] table tbody');
    if (priceBody) priceBody.innerHTML = data.priceFrames.map(frame => `<tr class="hover:bg-surface-container-low transition-colors"><td class="py-3.5 px-4"><div class="font-semibold text-primary">${escapeHtml(frame.serviceName)}</div><div class="text-xs text-on-surface-variant">${escapeHtml(frame.id)}</div></td><td class="py-3 px-3">${escapeHtml(frame.variant)}</td><td class="py-3 px-3 text-right">${money((frame.minPrice + frame.maxPrice) / 2)}</td><td class="py-3 px-4">${money(frame.minPrice)} – ${money(frame.maxPrice)}</td><td class="py-3 px-3">Đang hiệu lực</td><td class="py-3 px-3 text-center">—</td><td class="py-3 px-4 text-right">${escapeHtml(frame.currency)}</td></tr>`).join("");

    if (document.querySelector('[data-wpsm-screen="M05"]')) renderSpecificationEditor(data);

    const contract = document.querySelector('[data-wpsm-screen="C02"]');
    const categoriesContainer = contract?.querySelector(".category-block")?.parentElement;
    if (categoriesContainer && data.project) {
      const groups = (data.project.specification || []).reduce((result, item) => {
        (result[item.category] ||= []).push(item);
        return result;
      }, {});
      categoriesContainer.innerHTML = Object.entries(groups).map(([category, items]) => `<section class="border-b border-outline-variant/30 px-6 py-4">
        <h3 class="font-title-sm text-title-sm font-bold text-primary mb-3">${escapeHtml(category)}</h3>
        ${items.map(item => `<div class="grid grid-cols-1 md:grid-cols-12 gap-2 py-3 border-t border-outline-variant/20"><div class="md:col-span-6"><div class="font-semibold">${escapeHtml(item.name)}</div><p class="text-sm text-on-surface-variant">${escapeHtml(item.description)}</p></div><div class="md:col-span-2">${item.quantity} ${escapeHtml(item.unit)}</div><div class="md:col-span-2">${money(item.unitPrice)}</div><div class="md:col-span-2 font-semibold">${money(item.lineTotal)}</div></div>`).join("")}
        </section>`).join("");
    }

    const roadmapSection = document.querySelector('[data-wpsm-screen="C03"] main section');
    if (roadmapSection && data.project) {
      const roadmap = data.roadmap || [];
      roadmapSection.innerHTML = roadmap.length ? roadmap.map(task => `<article class="relative z-10 bg-surface-container-lowest border border-outline-variant/40 rounded-xl p-5 shadow-sm">
        <div class="flex flex-wrap items-center justify-between gap-3"><div><h3 class="text-title-lg font-title-lg text-primary font-bold">${escapeHtml(task.milestone)} · ${escapeHtml(task.title)}</h3><p class="text-body-sm text-on-surface-variant mt-1">Phụ trách: ${task.assigneeRole === "Customer" ? "Khách hàng" : "Quản lý"}</p></div>
        <div class="text-right"><div class="font-semibold">Hạn: ${new Intl.DateTimeFormat("vi-VN").format(new Date(`${task.dueDate}T00:00:00`))}</div><span class="text-sm">${task.status === "Done" ? "Đã hoàn thành" : task.status === "Overdue" ? "Quá hạn" : "Chờ thực hiện"}</span></div></div></article>`).join("") : `<p class="rounded-xl bg-surface-container-low p-5">Roadmap sẽ được tạo sau khi Manager đóng băng baseline.</p>`;
    }
  };
  const showError = error => { window.clearTimeout(messageTimer); status.textContent = error.message; status.classList.remove("is-ready"); status.classList.add("is-error"); };

  // ---- M-05 Specification Editor & Freeze Baseline (UC-20, GBR-01/02/15) ----
  const CORE_COMBO_CATEGORIES = ["Sảnh", "Ẩm thực", "Trang trí", "MC", "Âm thanh & ánh sáng"];
  const SLOT_LABELS = { Lunch: "Trưa", Evening: "Tối" };
  const setField = (root, name, text) => { const el = root.querySelector(`[data-field="${name}"]`); if (el) el.textContent = text; };
  const specFilter = { category: "", query: "", catalogCategory: "", catalogQuery: "" };
  const plainNumber = value => new Intl.NumberFormat("vi-VN").format(value);
  const priceFrameFor = name => (window.wpsm?.priceFrames || []).find(frame => frame.serviceName === name);
  const specRows = root => [...root.querySelectorAll("tr[data-spec-item-id]")];
  // Rows are the source of truth while editing, so unsaved inputs survive add/remove/freeze.
  const readSpecRows = root => specRows(root).map(row => ({
    catalogItemId: Number(row.dataset.specItemId),
    description: row.querySelector("[data-spec-description]")?.value || "",
    quantity: Number(row.querySelector("[data-spec-quantity]")?.value || 0),
    unitPrice: moneyValue(row.querySelector("[data-spec-price]"))
  }));

  const renderCoreCombo = (root, specification) => {
    const present = new Set(specification.map(item => item.category));
    const checklist = root.querySelector("#coreComboChecklist");
    let ok = 0;
    if (checklist) checklist.querySelectorAll("[data-combo-category]").forEach(li => {
      const category = li.dataset.comboCategory;
      const has = present.has(category);
      if (has) ok++;
      const icon = li.querySelector("[data-combo-icon]");
      const note = li.querySelector("[data-combo-note]");
      if (icon) { icon.textContent = has ? "check_circle" : "warning"; icon.className = `material-symbols-outlined text-lg shrink-0 mt-0.5 ${has ? "text-[#15803D]" : "text-[#C2410C]"}`; }
      if (note) {
        const items = specification.filter(item => item.category === category).map(item => item.name).join(", ");
        note.textContent = has ? items : `Thiếu dịch vụ bắt buộc: ${category}.`;
        note.className = has ? "text-on-surface-variant text-[12px] mt-0.5" : "text-[#C2410C] text-[12px] mt-0.5";
      }
    });
    setField(root, "comboCount", `${ok}/${CORE_COMBO_CATEGORIES.length} Bắt buộc`);
    const badge = root.querySelector('[data-field="comboCount"]');
    if (badge) badge.className = ok === CORE_COMBO_CATEGORIES.length
      ? "px-2 py-0.5 rounded-full text-label-sm font-label-sm bg-[#ECFDF5] text-[#065F46] border border-[#A7F3D0] font-bold"
      : "px-2 py-0.5 rounded-full text-label-sm font-label-sm bg-[#FEF2F2] text-[#991B1B] border border-[#FECACA] font-bold";
  };

  const renderReconciliation = (root, project, specTotal) => {
    const baseValue = project.baseValue || 0;
    const delta = specTotal - baseValue;
    setField(root, "specTotalFooter", money(specTotal));
    setField(root, "reconBaseValue", money(baseValue));
    setField(root, "reconSpecTotal", money(specTotal));
    const deltaEl = root.querySelector('[data-field="reconDelta"]');
    const badgeEl = root.querySelector('[data-field="reconMatchBadge"]');
    const matched = delta === 0;
    if (deltaEl) deltaEl.innerHTML = matched
      ? `<span class="material-symbols-outlined text-sm">check</span>0 ₫ (Khớp 100%)`
      : `<span class="material-symbols-outlined text-sm">${delta > 0 ? "trending_up" : "trending_down"}</span>${delta > 0 ? "+" : ""}${money(delta)}`;
    if (deltaEl) deltaEl.className = `text-label-sm font-label-sm font-bold flex items-center gap-1 ${matched ? "text-[#15803D]" : "text-[#B91C1C]"}`;
    if (badgeEl) { badgeEl.textContent = matched ? "Khớp 100%" : "Lệch số liệu"; badgeEl.className = `px-2 py-0.5 rounded-full text-label-sm font-label-sm border font-bold ${matched ? "bg-[#ECFDF5] text-[#065F46] border-[#A7F3D0]" : "bg-[#FEF2F2] text-[#991B1B] border-[#FECACA]"}`; }
  };

  const renderCatalogPicker = (root, catalog, specification) => {
    const list = root.querySelector("#catalogPickerList");
    if (!list) return;
    const usedIds = new Set(specification.map(item => item.catalogItemId));
    const active = (catalog || []).filter(item => item.active);
    const select = root.querySelector("#catalogCategoryFilter");
    if (select) {
      const categories = [...new Set(active.map(item => item.category))];
      if (!categories.includes(specFilter.catalogCategory)) specFilter.catalogCategory = "";
      select.innerHTML = `<option value="">Tất cả danh mục</option>${categories.map(category => `<option value="${escapeHtml(category)}">${escapeHtml(category)}</option>`).join("")}`;
      select.value = specFilter.catalogCategory;
    }
    const query = specFilter.catalogQuery.toLowerCase();
    const visible = active.filter(item => (!specFilter.catalogCategory || item.category === specFilter.catalogCategory) && item.name.toLowerCase().includes(query));
    list.innerHTML = visible.length ? visible.map(item => {
      const used = usedIds.has(Number(item.id));
      const frame = priceFrameFor(item.name);
      const startPrice = frame ? Math.min(frame.maxPrice, Math.max(frame.minPrice, item.basePrice)) : item.basePrice;
      return `<div class="p-3.5 rounded-xl border border-[#E8E2DD] ${used ? "" : "hover:border-primary/60"} transition-all flex items-center justify-between gap-4 bg-white">
        <div class="flex items-start gap-3">
          <div class="w-10 h-10 rounded-lg bg-surface-container flex items-center justify-center text-primary shrink-0"><span class="material-symbols-outlined text-xl">inventory_2</span></div>
          <div>
            <div class="flex items-center gap-2"><h4 class="font-title-sm text-title-sm font-semibold text-on-surface">${escapeHtml(item.name)}</h4>
              <span class="text-[10px] font-bold px-2 py-0.5 rounded bg-[#FAF7F5] text-secondary border border-[#C9A66B]/40">${escapeHtml(item.category)}</span></div>
            <span class="text-[11px] font-medium text-primary mt-1 block">Đơn giá tham chiếu: ${money(item.basePrice)} / ${escapeHtml(item.unit)}${frame ? ` · Khung giá: ${money(frame.minPrice)} - ${money(frame.maxPrice)}` : ""}</span>
          </div>
        </div>
        <button class="shrink-0 px-3 py-1.5 rounded-lg font-label-md text-label-md transition-colors ${used ? "border border-outline-variant text-on-surface-variant cursor-not-allowed" : "border border-primary text-primary hover:bg-primary hover:text-white"}"
          data-action="add-catalog-item" data-catalog-id="${item.id}" data-catalog-name="${escapeHtml(item.name)}" data-catalog-price="${startPrice}" ${used ? "disabled" : ""}>
          ${used ? "Đã thêm" : "+ Chọn dùng"}
        </button>
      </div>`;
    }).join("") : `<p class="text-center text-on-surface-variant text-body-sm py-6">Không có hạng mục Catalog phù hợp.</p>`;
  };

  const applySpecFilter = root => {
    const query = specFilter.query.toLowerCase();
    specRows(root).forEach(row => {
      const matches = (!specFilter.category || row.dataset.specCategory === specFilter.category) && row.dataset.specName.toLowerCase().includes(query);
      row.classList.toggle("hidden", !matches);
    });
  };

  const renderCategoryFilter = (root, specification) => {
    const list = root.querySelector("#specCategoryList");
    if (!list) return;
    const counts = specification.reduce((result, item) => result.set(item.category, (result.get(item.category) || 0) + 1), new Map());
    if (!counts.has(specFilter.category)) specFilter.category = "";
    setField(root, "categoryCount", `${counts.size} mục`);
    const entry = (category, label, count) => {
      const selected = specFilter.category === category;
      return `<li><button class="w-full flex items-center justify-between px-3 py-2.5 rounded-lg transition-all ${selected ? "bg-primary-container text-white font-medium shadow-sm" : "text-on-surface-variant hover:bg-surface-container hover:text-primary"}" data-action="filter-spec-category" data-category="${escapeHtml(category)}">
        <span>${escapeHtml(label)}</span><span class="px-2 py-0.5 rounded-full text-[11px] font-bold ${selected ? "bg-white/20 text-white" : "bg-surface-container text-on-surface-variant"}">${count}</span></button></li>`;
    };
    list.innerHTML = entry("", "Tất cả hạng mục", specification.length) + [...counts].map(([category, count]) => entry(category, category, count)).join("");
    applySpecFilter(root);
  };

  const applyFreezeState = (root, project) => {
    const frozen = project.status !== "Active Baseline";
    const banner = root.querySelector("#freezeBanner");
    if (banner) banner.classList.toggle("hidden", !frozen);
    const workspace = root.querySelector("#specWorkspace");
    if (workspace) workspace.classList.toggle("opacity-60", frozen);
    root.querySelectorAll("#specTableBody input").forEach(el => el.disabled = frozen);
    for (const id of ["btnOpenCatalog", "btnSaveDraft", "btnOpenFreeze"]) {
      const el = root.querySelector(`#${id}`);
      if (el) { el.disabled = frozen; el.classList.toggle("opacity-50", frozen); el.classList.toggle("pointer-events-none", frozen); }
    }
    root.querySelectorAll('[data-action="remove-spec-item"]').forEach(el => { el.disabled = frozen; el.classList.toggle("opacity-30", frozen); });
    setField(root, "statusLabel", frozen ? "Baseline Frozen (Đã đóng băng)" : "Active Baseline (Đang nhập đặc tả)");
  };

  const renderSpecificationEditor = data => {
    const root = document.querySelector('[data-wpsm-screen="M05"]');
    const project = data.project;
    if (!root) return;
    if (!project) {
      const emptyBody = root.querySelector("#specTableBody");
      if (emptyBody) emptyBody.innerHTML = `<tr><td colspan="7" class="py-6 text-center text-on-surface-variant">Chưa có dự án để nhập đặc tả.</td></tr>`;
      return;
    }
    const specification = project.specification || [];
    setField(root, "projectCode", project.id);
    setField(root, "coupleName", project.coupleName);
    setField(root, "coupleNameConfirm", project.coupleName);
    setField(root, "hallName", project.hall);
    setField(root, "slotLabel", SLOT_LABELS[project.slot] || project.slot);
    setField(root, "eventDate", new Intl.DateTimeFormat("vi-VN").format(new Date(`${project.eventDate}T00:00:00`)));
    const daysLeft = Math.max(0, Math.ceil((new Date(`${project.eventDate}T00:00:00`).getTime() - new Date().setHours(0, 0, 0, 0)) / 86400000));
    setField(root, "countdown", `T-${daysLeft} ngày`);
    setField(root, "specItemCount", `${specification.length} hạng mục đã cấu hình`);
    setField(root, "specRowCount", String(specification.length));
    setField(root, "progressLabel", `${project.roadmapProgress}%`);
    const progressBar = root.querySelector('[data-field="progressBar"]');
    if (progressBar) progressBar.style.width = `${project.roadmapProgress}%`;
    const nrdPercent = project.baseValue ? Math.round(project.nrdAmount * 100 / project.baseValue) : 0;
    setField(root, "nrdAmount", money(project.nrdAmount));
    setField(root, "nrdPercent", `${nrdPercent}%`);
    setField(root, "nrdPercentTier", `${nrdPercent}%`);
    // T-30 is cumulative in policy: T30 required = CurrentTotal × T30 ratio − NRD (SDS 4.4).
    const t30Ratio = data.policies?.t30Ratio ?? 70;
    const tiers = { tierNrdBar: nrdPercent, tierT30Bar: Math.max(0, t30Ratio - nrdPercent), tierFinalBar: Math.max(0, 100 - Math.max(t30Ratio, nrdPercent)) };
    for (const [name, width] of Object.entries(tiers)) { const bar = root.querySelector(`[data-field="${name}"]`); if (bar) bar.style.width = `${width}%`; }
    setField(root, "tierT30", `${tiers.tierT30Bar}%`);
    setField(root, "tierFinal", `${tiers.tierFinalBar}%`);

    const specBody = root.querySelector("#specTableBody") || root.querySelector("table tbody");
    if (specBody) specBody.innerHTML = specification.length ? specification.map((item, index) => `<tr class="hover:bg-surface-container-low/40 transition-colors" data-spec-item-id="${item.catalogItemId}" data-spec-category="${escapeHtml(item.category)}" data-spec-name="${escapeHtml(item.name)}">
      <td class="py-3 px-3 text-center">${index + 1}</td><td class="py-3 px-3"><div class="font-semibold">${escapeHtml(item.name)}</div><span class="text-xs">${escapeHtml(item.category)}</span></td>
      <td class="py-3 px-3"><input class="w-full rounded border px-2 py-1" data-spec-description value="${escapeHtml(item.description)}"></td>
      <td class="py-3 px-3 text-center"><input class="w-20 rounded border px-2 py-1 text-center" data-spec-quantity type="number" min="1" value="${item.quantity}"></td>
      <td class="py-3 px-3 text-right"><input class="w-32 rounded border px-2 py-1 text-right" data-spec-price type="text" value="${plainNumber(item.unitPrice)}"></td>
      <td class="py-3 px-3 text-right" data-spec-line-total>${money(item.lineTotal)}</td>
      <td class="py-3 px-3 text-center"><button class="p-1 text-outline hover:text-error transition-colors rounded" data-action="remove-spec-item" data-spec-item-id="${item.catalogItemId}" title="Xóa hạng mục"><span class="material-symbols-outlined text-base">delete</span></button></td></tr>`).join("") : `<tr><td colspan="7" class="py-6 text-center text-on-surface-variant">Chưa có hạng mục đặc tả. Bấm "+ Thêm hạng mục từ Catalog" để bắt đầu.</td></tr>`;

    renderCoreCombo(root, specification);
    renderCategoryFilter(root, specification);
    renderReconciliation(root, project, specification.reduce((sum, item) => sum + item.lineTotal, 0));
    renderCatalogPicker(root, data.catalog, specification);
    applyFreezeState(root, project);
  };

  const saveSpecification = async (project, items) => {
    const updated = await send(`projects/${encodeURIComponent(project.id)}/specification`, "PUT", { summary: "Đặc tả baseline", guestCount: 0, items });
    window.wpsm.project = updated;
    renderSpecificationEditor(window.wpsm);
    const root = document.querySelector('[data-wpsm-screen="M05"]');
    setField(root, "lastSaved", `Đã lưu lúc ${new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`);
    return updated;
  };

  document.addEventListener("submit", async event => {
    const form = event.target;
    const screen = document.querySelector("[data-wpsm-screen]")?.dataset.wpsmScreen;
    if (!(form instanceof HTMLFormElement)) return;
    if (screen === "G01" && form.id === "loginForm") {
      event.preventDefault(); event.stopImmediatePropagation();
      try {
        const user = await send("auth/login", "POST", { email: form.querySelector("#email").value, password: form.querySelector("#password").value });
        if (user.mustChangePassword) { window.location.href = "/G03"; return; }
        message(`Đăng nhập thành công · ${user.user.fullName}`);
        window.location.href = user.user.role === "Admin" ? "/A02" : user.user.role === "Manager" ? "/M02" : "/C01";
      } catch (error) { showError(error); }
      return;
    }
    if (screen === "G03") {
      event.preventDefault(); event.stopImmediatePropagation();
      try {
        await send("auth/change-password", "POST", {
          currentPassword: form.querySelector("#current_password")?.value || "",
          newPassword: form.querySelector("#new_password")?.value || "",
          confirmPassword: form.querySelector("#confirm_password")?.value || ""
        });
        message("Đã gửi yêu cầu đổi mật khẩu tới API.");
      } catch (error) { showError(error); }
      return;
    }
    if (screen === "A04") {
      event.preventDefault(); event.stopImmediatePropagation();
      const values = [moneyValue(form.querySelector("#minPrice")), moneyValue(form.querySelector("#maxPrice"))];
      const frame = window.wpsm?.priceFrames?.[0];
      if (frame) {
        try {
          const dateText = form.querySelector("#effectiveDate")?.value || "";
          const match = dateText.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
          const effectiveFrom = match ? `${match[3]}-${match[2].padStart(2, "0")}-${match[1].padStart(2, "0")}` : undefined;
          await send(`price-frames/${encodeURIComponent(frame.id)}`, "PUT", { minPrice: values[0], maxPrice: values[1], effectiveFrom });
          message("Đã lưu phiên bản khung giá qua API.");
        } catch (error) { showError(error); }
      }
      return;
    }
    if (screen === "A06") {
      event.preventDefault(); event.stopImmediatePropagation();
      const values = [...form.querySelectorAll("input[type=number]")].map(input => Number(input.value || 0));
      const current = window.wpsm?.policies;
      if (!current) return;
      const isDeposit = !!form.closest("#modal-a07");
      const effectiveFrom = form.querySelector('input[type="date"]')?.value || undefined;
      const policy = isDeposit
        ? { ...current, nrdRatio: values[0] ?? current.nrdRatio, t30Ratio: values[1] ?? current.t30Ratio, finalRatio: Math.max(0, 100 - (values[1] ?? current.t30Ratio)), effectiveFrom }
        : { ...current, crPenaltyDays: values[0] ?? current.crPenaltyDays, crPenaltyPercent: values[1] ?? current.crPenaltyPercent, effectiveFrom };
      try { await send("policies", "PUT", policy); message("Đã cập nhật chính sách qua API."); }
      catch (error) { showError(error); }
      return;
    }
    if (screen === "A02") {
      event.preventDefault(); event.stopImmediatePropagation();
      const fields = [...form.querySelectorAll("input[type=text]")];
      const item = { name: fields[0]?.value || "Dịch vụ mới", category: form.querySelector("select")?.value || "Khác", unit: fields[1]?.value || "Gói", basePrice: moneyValue(fields.at(-1)), active: true };
      try { const saved = await send("catalog", "POST", item); message(`Đã lưu ${saved.name} qua API.`); }
      catch (error) { showError(error); }
    }
  }, true);

  document.addEventListener("click", async event => {
    const button = event.target.closest("button");
    if (!button) return;
    const screen = document.querySelector("[data-wpsm-screen]")?.dataset.wpsmScreen;
    const label = button.innerText.replace(/\s+/g, " ").trim();
    if (screen === "M03" && button.id === "nrdReceiptBrowse") {
      event.preventDefault(); document.getElementById("nrdReceiptFile")?.click(); return;
    }
    if (screen === "M03" && button.id === "nrdReceiptClear") {
      event.preventDefault();
      const fileInput = document.getElementById("nrdReceiptFile");
      if (fileInput) fileInput.value = "";
      document.getElementById("nrdReceiptPreview")?.classList.add("hidden");
      return;
    }
    if (screen === "G02" && label.includes("Đổi mật khẩu")) {
      event.preventDefault(); window.location.href = "/G03"; return;
    }
    if (screen === "M03" && label.includes("Xác nhận") && label.includes("tạo dự án")) {
      event.preventDefault(); event.stopImmediatePropagation();
      const fileInput = document.getElementById("nrdReceiptFile");
      const receipt = fileInput?.files?.[0];
      const paymentMethod = document.getElementById("nrdPaymentMethod")?.value.includes("Chuyển khoản") ? "Bank" : "Cash";
      if (!window.wpsm?.pendingNrdProject && paymentMethod === "Bank" && !receipt)
        return showError(new Error("Vui lòng chọn biên nhận NRD trước khi tạo dự án."));
      if (window.wpsm?.pendingNrdProject && !receipt)
        return showError(new Error("Dự án đã tạo nhưng chưa ghi nhận được biên nhận. Vui lòng chọn tệp để hoàn tất."));
      const inputs = [...document.querySelectorAll('[data-wpsm-screen="M03"] input')];
      const names = inputs.find(input => input.type === "text" && (input.value || "").includes(" & "));
      const email = inputs.find(input => input.type === "email");
      const phone = inputs.find(input => input.type === "tel");
      const date = inputs.find(input => /^\d{1,2}\/\d{1,2}\/\d{4}$/.test(input.value || ""));
      const amounts = inputs.filter(input => (input.value || "").includes("₫"));
      const selects = [...document.querySelectorAll('[data-wpsm-screen="M03"] select')];
      const selectedShift = document.querySelector('[data-wpsm-screen="M03"] input[name="shift_selection"]:checked')?.closest("label")?.innerText || "Trưa";
      const [day, month, year] = (date?.value || "20/12/2026").split("/");
      try {
        let project = window.wpsm?.pendingNrdProject;
        let temporaryPassword;
        if (!project) {
          const result = await send("projects", "POST", {
          coupleName: names?.value || "Cặp đôi mới",
          customerEmail: email?.value || "",
          customerPhone: phone?.value || "",
          hall: (selects.map(select => select.options[select.selectedIndex]?.text || "").find(text => text.includes("Sảnh")) || window.wpsm?.project.hall || "Sảnh Hoàng Gia").split(" – ")[0].trim(),
          slot: selectedShift.includes("Tối") ? "Evening" : "Lunch", eventDate: `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`,
          baseValue: moneyValue(amounts[0]), nrdAmount: moneyValue(amounts[1]),
          paymentMethod
          });
          project = result.project;
          temporaryPassword = result.temporaryPassword;
          window.wpsm = { ...(window.wpsm || {}), project, pendingNrdProject: paymentMethod === "Bank" ? project : null };
        }
        if (paymentMethod === "Bank") {
          await sendFile(`projects/${encodeURIComponent(project.id)}/nrd-receipt`, receipt);
          window.wpsm.pendingNrdProject = null;
        }
        message(temporaryPassword
          ? `Đã tạo ${project.id}, ghi nhận NRD${temporaryPassword ? `. Mật khẩu tạm của khách: ${temporaryPassword} (chỉ hiển thị lần này).` : ""}`
          : `Đã tạo dự án ${project.id} và ghi nhận NRD qua database.`);
      } catch (error) { showError(error); }
    }
    if (screen === "M05" && button.id === "btnOpenFreeze") {
      const consent = document.getElementById("freezeConsent");
      const confirm = document.getElementById("confirmFreezeBtn");
      if (consent) consent.checked = false;
      if (confirm) { confirm.disabled = true; confirm.classList.add("opacity-50"); }
      return;
    }
    if (screen === "M05" && button.id === "btnBackToProject") {
      event.preventDefault();
      const project = window.wpsm?.project;
      window.location.href = project ? `/M04?id=${encodeURIComponent(project.id)}` : "/M02";
      return;
    }
    if (screen === "M05" && button.dataset.action === "filter-spec-category") {
      event.preventDefault();
      specFilter.category = button.dataset.category;
      renderCategoryFilter(document.querySelector('[data-wpsm-screen="M05"]'), window.wpsm?.project?.specification || []);
      return;
    }
    if (screen === "M05" && button.id === "confirmFreezeBtn") {
      event.preventDefault(); event.stopImmediatePropagation();
      const project = window.wpsm?.project;
      if (!project) return showError(new Error("Không tìm thấy dự án để đóng băng."));
      button.disabled = true;
      document.getElementById("freezeModal")?.classList.add("hidden");
      try {
        // Freeze what the manager sees: persist pending edits first so the baseline is not stale.
        await saveSpecification(project, readSpecRows(document.querySelector('[data-wpsm-screen="M05"]')));
        const frozen = await send(`projects/${encodeURIComponent(project.id)}/freeze`, "POST", {});
        window.wpsm.project = frozen;
        renderSpecificationEditor(window.wpsm);
        window.scrollTo({ top: 0, behavior: "smooth" });
        message(`Đã đóng băng baseline ${frozen.id} và tạo roadmap.`);
      } catch (error) { showError(error); }
      return;
    }
    if (screen === "M05" && /lưu nháp đặc tả/i.test(label)) {
      event.preventDefault(); event.stopImmediatePropagation();
      try {
        const project = window.wpsm?.project;
        if (project) await saveSpecification(project, readSpecRows(document.querySelector('[data-wpsm-screen="M05"]')));
        message("Đã lưu đặc tả vào database.");
      } catch (error) { showError(error); }
      return;
    }
    if (screen === "M05" && button.dataset.action === "add-catalog-item") {
      event.preventDefault(); event.stopImmediatePropagation();
      if (button.disabled) return;
      const project = window.wpsm?.project;
      if (!project) return;
      try {
        const current = readSpecRows(document.querySelector('[data-wpsm-screen="M05"]'));
        current.push({
          catalogItemId: Number(button.dataset.catalogId),
          description: button.dataset.catalogName,
          quantity: 1,
          unitPrice: Number(button.dataset.catalogPrice)
        });
        await saveSpecification(project, current);
        document.getElementById("catalogModal")?.classList.add("hidden");
        message("Đã thêm hạng mục từ Catalog vào đặc tả.");
      } catch (error) { showError(error); }
      return;
    }
    if (screen === "M05" && button.dataset.action === "remove-spec-item") {
      event.preventDefault(); event.stopImmediatePropagation();
      if (button.disabled) return;
      const project = window.wpsm?.project;
      if (!project) return;
      try {
        const removedId = Number(button.dataset.specItemId);
        const remaining = readSpecRows(document.querySelector('[data-wpsm-screen="M05"]')).filter(item => item.catalogItemId !== removedId);
        await saveSpecification(project, remaining);
        message("Đã xóa hạng mục khỏi đặc tả.");
      } catch (error) { showError(error); }
      return;
    }
  }, true);

  document.addEventListener("input", event => {
    const root = document.querySelector('[data-wpsm-screen="M05"]');
    const project = window.wpsm?.project;
    if (!root || !project) return;
    const target = event.target;
    if (target.id === "specSearch") { specFilter.query = target.value.trim(); applySpecFilter(root); return; }
    if (target.id === "catalogSearch" || target.id === "catalogCategoryFilter") {
      specFilter[target.id === "catalogSearch" ? "catalogQuery" : "catalogCategory"] = target.value.trim();
      renderCatalogPicker(root, window.wpsm.catalog, project.specification || []);
      return;
    }
    const row = target.closest("tr[data-spec-item-id]");
    if (!row) return;
    const line = readSpecRows(row.parentElement).find(item => item.catalogItemId === Number(row.dataset.specItemId));
    row.querySelector("[data-spec-line-total]").textContent = money(line.quantity * line.unitPrice);
    const frame = priceFrameFor(row.dataset.specName);
    const priceInput = row.querySelector("[data-spec-price]");
    const outOfFrame = !!frame && (line.unitPrice < frame.minPrice || line.unitPrice > frame.maxPrice);
    priceInput.classList.toggle("text-[#B91C1C]", outOfFrame);
    priceInput.title = outOfFrame ? `Ngoài khung giá ${money(frame.minPrice)} - ${money(frame.maxPrice)}` : "";
    renderReconciliation(root, project, readSpecRows(root).reduce((sum, item) => sum + item.quantity * item.unitPrice, 0));
  });

  const receiptInput = document.getElementById("nrdReceiptFile");
  const paymentSelector = document.getElementById("nrdPaymentMethod");
  const syncReceiptRequirement = () => document.getElementById("nrdReceiptRequired")?.classList.toggle("hidden", !paymentSelector?.value.includes("Chuyển khoản"));
  paymentSelector?.addEventListener("change", syncReceiptRequirement);
  syncReceiptRequirement();
  receiptInput?.addEventListener("change", () => {
    const file = receiptInput.files?.[0];
    const preview = document.getElementById("nrdReceiptPreview");
    if (!file || !preview) return;
    document.getElementById("nrdReceiptName").textContent = file.name;
    document.getElementById("nrdReceiptSize").textContent = `${(file.size / 1024 / 1024).toFixed(2)} MB`;
    preview.classList.remove("hidden");
  });

  if (document.querySelector('[data-wpsm-screen="G01"]')) return;
  const selectedProject = new URLSearchParams(window.location.search).get("id");
  fetch(`/api/wpsm/bootstrap${selectedProject ? `?projectCode=${encodeURIComponent(selectedProject)}` : ""}`, { headers: { Accept: "application/json" } })
    .then(response => { if (!response.ok) throw new Error(`HTTP ${response.status}`); return response.json(); })
    .then(data => {
      window.wpsm = data;
      hydrateTables(data);
      const project = data.project;
      if (project) {
        document.querySelectorAll('a[href="/M04"], a[href="/M05"]').forEach(link => { link.href = `${link.getAttribute("href")}?id=${encodeURIComponent(project.id)}`; });
        const dateVi = new Intl.DateTimeFormat("vi-VN").format(new Date(`${project.eventDate}T00:00:00`));
        const daysLeft = Math.max(0, Math.ceil((new Date(`${project.eventDate}T00:00:00`).getTime() - new Date().setHours(0, 0, 0, 0)) / 86400000));
        const displayValues = {
          "Minh Anh & Thu Hà": project.coupleName,
          "WPSM-2026-014": project.id,
          "Sảnh Hoàng Gia": project.hall,
          "20/12/2026": dateVi,
          "185.000.000 ₫": formatMoney(project.baseValue),
          "18.500.000 ₫": formatMoney(project.nrdAmount),
          "NL Long & Linh": data.profile.fullName,
          "minhanh@example.com": data.profile.email,
          "0912 345 678": data.profile.phone
        };
        if (document.querySelector('[data-wpsm-screen="C03"]')) displayValues["T-58 ngày"] = `T-${daysLeft} ngày`;
        if (document.querySelector('[data-wpsm-screen="A06"]')) {
          displayValues["30%"] = `${data.policies.nrdRatio}%`;
          displayValues["70%"] = `${data.policies.t30Ratio}%`;
        }
        replaceText(document.body, displayValues);
      }
      status.textContent = project ? `Database · ${project.id}` : "Database · chưa có dự án";
      status.classList.add("is-ready");
    })
    .catch(error => {
      status.textContent = "Không kết nối được WPSM API";
      status.title = `${error.message}. Hãy chạy WPSM.API cùng WPSM.Web.`;
      status.classList.add("is-error");
    });
})();
