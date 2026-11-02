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
  const message = text => {
    status.textContent = text;
    status.classList.add("is-ready");
    window.setTimeout(() => {
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
  const hydrateCustomerContract = project => {
    const page = document.querySelector('[data-wpsm-screen="C02"]');
    if (!page) return;
    const set = (key, value) => {
      const target = page.querySelector(`[data-c02="${key}"]`);
      if (target) target.textContent = value;
    };
    const date = project ? new Date(`${project.eventDate}T00:00:00`) : null;
    const dateLabel = date && !Number.isNaN(date.getTime()) ? new Intl.DateTimeFormat("vi-VN").format(date) : "—";
    const frozen = !!project?.baselineFrozenAt;
    const status = page.querySelector('[data-c02="baseline-status"]');
    if (status) {
      status.className = frozen
        ? "inline-flex items-center gap-1 rounded-full border border-emerald-300 bg-emerald-50 px-3 py-1 text-label-md font-semibold text-emerald-800"
        : "inline-flex items-center gap-1 rounded-full border border-amber-300 bg-amber-50 px-3 py-1 text-label-md font-semibold text-amber-800";
      status.textContent = frozen ? "Baseline v1 · Đã đóng băng" : "Đang soạn đặc tả";
    }
    if (!project) {
      ["project-code", "hall", "event-date", "couple-name", "summary-hall", "slot", "summary-date", "base-value", "nrd-amount", "nrd-status", "frozen-at", "base-value-total", "spec-total"].forEach(key => set(key, "—"));
      set("status-description", "Tài khoản hiện chưa có hồ sơ hợp đồng để hiển thị.");
      set("days-left", "");
      set("item-count", "Chưa có đặc tả");
      const groups = page.querySelector('[data-c02="specification-groups"]');
      if (groups) groups.innerHTML = '<p class="p-6 text-center text-body-md text-on-surface-variant">Chưa có dự án hoặc đặc tả cho tài khoản này.</p>';
      return;
    }

    const specification = project.specification || [];
    const total = specification.reduce((sum, item) => sum + Number(item.lineTotal || 0), 0);
    const nrdLabels = { Paid: "Đã xác nhận thanh toán", Due: "Chờ xác nhận thanh toán", Upcoming: "Chưa đến hạn", Unknown: "Chưa có trạng thái thanh toán" };
    set("project-code", project.id);
    set("hall", project.hall);
    set("event-date", dateLabel);
    set("couple-name", project.coupleName);
    set("summary-hall", project.hall);
    set("slot", project.slot);
    set("summary-date", dateLabel);
    set("base-value", money(project.baseValue));
    set("base-value-total", money(project.baseValue));
    set("nrd-amount", money(project.nrdAmount));
    set("nrd-status", nrdLabels[project.nrdStatus] || "Chưa có trạng thái thanh toán");
    set("frozen-at", frozen
      ? `Đóng băng lúc ${new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" }).format(new Date(project.baselineFrozenAt))}`
      : "Chưa đóng băng");
    set("status-description", frozen
      ? "Đây là bản đặc tả gốc đã được đóng băng. Các hạng mục dưới đây được hiển thị chỉ đọc để bạn đối chiếu với hợp đồng."
      : "Quản lý đang hoàn thiện đặc tả hợp đồng. Các hạng mục hiện có chỉ để bạn theo dõi; phiên bản gốc chưa được đóng băng.");
    const daysLeft = date ? Math.ceil((date.setHours(0, 0, 0, 0) - new Date().setHours(0, 0, 0, 0)) / 86400000) : null;
    set("days-left", daysLeft === null ? "" : daysLeft >= 0 ? `Còn ${daysLeft} ngày đến ngày tổ chức` : `Đã qua ngày tổ chức ${Math.abs(daysLeft)} ngày`);
    set("item-count", `${specification.length} hạng mục`);
    set("spec-total", money(total));

    const container = page.querySelector('[data-c02="specification-groups"]');
    if (!container) return;
    if (!specification.length) {
      container.innerHTML = '<p class="p-6 text-center text-body-md text-on-surface-variant">Chưa có hạng mục đặc tả.</p>';
      return;
    }
    const groups = specification.reduce((result, item) => {
      (result[item.category] ||= []).push(item);
      return result;
    }, {});
    container.innerHTML = Object.entries(groups).map(([category, items]) => {
      const categoryTotal = items.reduce((sum, item) => sum + Number(item.lineTotal || 0), 0);
      return `<details open class="group">
        <summary class="flex cursor-pointer list-none flex-wrap items-center justify-between gap-2 bg-surface-container-high/40 px-5 py-3.5 hover:bg-surface-container-high/70">
          <span class="flex items-center gap-2 font-title-sm text-title-sm font-bold text-primary"><span class="material-symbols-outlined text-[18px] transition-transform group-open:rotate-90">chevron_right</span>${escapeHtml(category)} <span class="rounded border border-outline-variant/30 bg-white px-2 py-0.5 text-[11px] font-medium text-on-surface-variant">${items.length} hạng mục</span></span>
          <span class="text-body-sm font-semibold text-on-surface">Tổng nhóm: ${money(categoryTotal)}</span>
        </summary>
        <div class="divide-y divide-outline-variant/20 px-5 sm:px-6">${items.map(item => `<article class="grid grid-cols-1 items-start gap-2 py-4 md:grid-cols-12">
          <div class="space-y-1 md:col-span-6"><h3 class="font-title-sm text-title-sm font-semibold text-on-surface">${escapeHtml(item.name)}</h3><p class="whitespace-pre-line text-body-sm leading-relaxed text-on-surface-variant">${escapeHtml(item.description)}</p></div>
          <div class="text-body-md text-on-surface md:col-span-2 md:text-center"><span class="text-body-sm text-on-surface-variant md:hidden">Số lượng / ĐVT: </span>${Number(item.quantity)} ${escapeHtml(item.unit)}</div>
          <div class="text-body-md text-on-surface-variant md:col-span-2 md:text-right"><span class="text-body-sm md:hidden">Đơn giá: </span>${money(item.unitPrice)}</div>
          <div class="font-title-sm text-title-sm font-semibold text-primary md:col-span-2 md:text-right"><span class="text-body-sm font-normal text-on-surface-variant md:hidden">Thành tiền: </span>${money(item.lineTotal)}</div>
        </article>`).join("")}</div>
      </details>`;
    }).join("");
  };
  const m05Categories = ["Sảnh", "Ẩm thực", "Trang trí", "MC", "Âm thanh & ánh sáng"];
  const m05Items = () => [...document.querySelectorAll('[data-wpsm-screen="M05"] #specItemsBody tr[data-spec-item-id]')].map(row => ({
    catalogItemId: Number(row.dataset.specItemId),
    description: row.querySelector("[data-spec-description]")?.value.trim() || "",
    quantity: Number(row.querySelector("[data-spec-quantity]")?.value || 0),
    unitPrice: moneyValue(row.querySelector("[data-spec-price]"))
  }));
  const renderManagerSpecification = data => {
    const page = document.querySelector('[data-wpsm-screen="M05"]');
    if (!page) return;
    const project = data.project;
    const set = (key, value) => { const node = page.querySelector(`[data-m05="${key}"]`); if (node) node.textContent = value; };
    const selector = page.querySelector("#m05ProjectSelect");
    if (selector) {
      selector.innerHTML = data.projects.length
        ? data.projects.map(item => `<option value="${escapeHtml(item.id)}">${escapeHtml(item.id)} · ${escapeHtml(item.coupleName)}</option>`).join("")
        : '<option value="">Chưa có dự án được phân công</option>';
      selector.value = project?.id || "";
    }
    set("project-code", project?.id || "Chưa chọn");
    set("hall", project ? `${project.hall} · ${project.slot}` : "Chọn dự án để bắt đầu");
    set("couple-name", project?.coupleName || "—");
    set("event-date", project ? new Intl.DateTimeFormat("vi-VN").format(new Date(`${project.eventDate}T00:00:00`)) : "—");
    set("base-value", project ? money(project.baseValue) : "—");

    const frozen = !!project && project.status !== "Active Baseline";
    const status = page.querySelector('[data-m05="baseline-status"]');
    if (status) {
      status.className = frozen
        ? "rounded-full border border-emerald-300 bg-emerald-50 px-3 py-1 text-label-md font-semibold text-emerald-800"
        : project ? "rounded-full border border-amber-300 bg-amber-50 px-3 py-1 text-label-md font-semibold text-amber-800" : "rounded-full border border-outline-variant/40 bg-surface-container-low px-3 py-1 text-label-md font-semibold text-on-surface-variant";
      status.textContent = frozen ? `Baseline v1 · ${project.status}` : project ? "Active Baseline · Đang soạn" : "Chưa chọn dự án";
    }
    const banner = page.querySelector("#freezeBanner");
    banner?.classList.toggle("hidden", !frozen);
    banner?.classList.toggle("flex", frozen);
    set("frozen-at", project?.baselineFrozenAt ? `Thời điểm: ${new Intl.DateTimeFormat("vi-VN", { dateStyle: "medium", timeStyle: "short" }).format(new Date(project.baselineFrozenAt))}` : "");

    const body = page.querySelector("#specItemsBody");
    const specification = project?.specification || [];
    if (body) body.innerHTML = project
      ? specification.length ? specification.map((item, index) => {
        const frame = data.priceFrames.find(candidate => Number(candidate.catalogItemId) === Number(item.catalogItemId));
        const catalog = data.catalog.find(candidate => Number(candidate.id) === Number(item.catalogItemId));
        const validFrame = !!frame && Number(item.unitPrice) >= Number(frame.minPrice) && Number(item.unitPrice) <= Number(frame.maxPrice);
        const frameLabel = !frame ? "Thiếu PriceFrame" : validFrame ? `${money(frame.minPrice)} – ${money(frame.maxPrice)}` : `Ngoài khung ${money(frame.minPrice)} – ${money(frame.maxPrice)}`;
        const frameClass = validFrame ? "text-emerald-700" : "text-red-700";
        const inputState = frozen ? "disabled" : "";
        return `<tr data-spec-item-id="${item.catalogItemId}" data-category="${escapeHtml(item.category)}" class="align-top">
          <td class="px-3 py-4 text-center text-on-surface-variant">${index + 1}</td>
          <td class="px-3 py-4"><div class="font-semibold text-on-surface">${escapeHtml(item.name)}</div><span class="mt-1 inline-flex rounded-full bg-surface-container-low px-2 py-0.5 text-[11px] text-on-surface-variant">${escapeHtml(item.category)}</span>${catalog?.active === false ? '<p class="mt-1 text-xs text-red-700">Catalog item đã ngừng hoạt động.</p>' : ""}</td>
          <td class="px-3 py-4"><textarea data-spec-description rows="2" ${inputState} class="w-full min-w-48 rounded-lg border border-outline-variant px-2.5 py-2 text-sm focus:border-primary focus:ring-primary disabled:bg-surface-container-low disabled:text-on-surface-variant">${escapeHtml(item.description)}</textarea></td>
          <td class="px-3 py-4"><div class="flex items-center gap-2"><input data-spec-quantity type="number" min="1" step="1" value="${Number(item.quantity)}" ${inputState} class="w-20 rounded-lg border border-outline-variant px-2 py-2 text-center text-sm focus:border-primary focus:ring-primary disabled:bg-surface-container-low"><span class="text-xs text-on-surface-variant">${escapeHtml(item.unit)}</span></div></td>
          <td class="px-3 py-4"><input data-spec-price inputmode="numeric" value="${Number(item.unitPrice)}" ${inputState} class="w-32 rounded-lg border border-outline-variant px-2 py-2 text-right text-sm focus:border-primary focus:ring-primary disabled:bg-surface-container-low"></td>
          <td data-line-total class="px-3 py-4 whitespace-nowrap text-right font-semibold text-primary">${money(Number(item.quantity) * Number(item.unitPrice))}</td>
          <td class="px-3 py-4 min-w-40"><span data-price-status class="text-xs font-medium ${frameClass}">${escapeHtml(frameLabel)}</span></td>
          <td class="px-3 py-4 text-right"><button type="button" data-remove-spec-item ${inputState} aria-label="Xóa ${escapeHtml(item.name)}" class="rounded-lg p-2 text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-40">×</button></td>
        </tr>`;
      }).join("") : '<tr><td colspan="8" class="p-8 text-center text-on-surface-variant">Chưa có hạng mục. Thêm dịch vụ từ Catalog để bắt đầu.</td></tr>'
      : '<tr><td colspan="8" class="p-8 text-center text-on-surface-variant">Chưa có dự án được phân công cho tài khoản này.</td></tr>';

    const canEdit = !!project && !frozen;
    const save = page.querySelector("#saveSpecificationBtn");
    const add = page.querySelector("#openCatalogBtn");
    if (save) save.disabled = !canEdit;
    if (add) add.disabled = !canEdit;
    updateManagerSummary(data);
  };
  const updateManagerSummary = data => {
    const page = document.querySelector('[data-wpsm-screen="M05"]');
    if (!page) return;
    const rows = m05Items();
    const catalog = data.catalog || [];
    const frames = data.priceFrames || [];
    const selectedIds = new Set(rows.map(item => item.catalogItemId));
    let total = 0;
    const categories = new Set();
    let pricesValid = rows.length > 0;
    rows.forEach(item => {
      const row = page.querySelector(`#specItemsBody tr[data-spec-item-id="${item.catalogItemId}"]`);
      const catalogItem = catalog.find(candidate => Number(candidate.id) === item.catalogItemId);
      if (catalogItem) categories.add(catalogItem.category);
      const lineTotal = item.quantity * item.unitPrice;
      total += lineTotal;
      row?.querySelector("[data-line-total]")?.replaceChildren(document.createTextNode(money(lineTotal)));
      const frame = frames.find(candidate => Number(candidate.catalogItemId) === item.catalogItemId);
      const valid = !!frame && item.unitPrice >= Number(frame.minPrice) && item.unitPrice <= Number(frame.maxPrice);
      if (!valid || item.quantity <= 0 || !item.description || catalogItem?.active === false) pricesValid = false;
      const priceStatus = row?.querySelector("[data-price-status]");
      if (priceStatus) {
        priceStatus.textContent = !frame ? "Thiếu PriceFrame" : valid ? `${money(frame.minPrice)} – ${money(frame.maxPrice)}` : `Ngoài khung ${money(frame.minPrice)} – ${money(frame.maxPrice)}`;
        priceStatus.className = `text-xs font-medium ${valid ? "text-emerald-700" : "text-red-700"}`;
      }
    });
    const specTotal = page.querySelector('[data-m05="spec-total"]');
    if (specTotal) specTotal.textContent = money(total);
    const itemCount = page.querySelector('[data-m05="item-count"]');
    if (itemCount) itemCount.textContent = String(rows.length);
    const tableTotal = page.querySelector("#specTableTotal");
    if (tableTotal) tableTotal.textContent = money(total);

    const missing = m05Categories.filter(category => !categories.has(category));
    const coreStatus = page.querySelector("#coreComboStatus");
    if (coreStatus) {
      coreStatus.className = missing.length
        ? "rounded-full border border-amber-300 bg-amber-50 px-3 py-1 text-label-sm font-semibold text-amber-800"
        : "rounded-full border border-emerald-300 bg-emerald-50 px-3 py-1 text-label-sm font-semibold text-emerald-800";
      coreStatus.textContent = missing.length ? `Thiếu Core Combo: ${missing.join(", ")}` : "Đủ 5 nhóm Core Combo";
    }
    const project = data.project;
    const freeze = page.querySelector("#openFreezeBtn");
    if (freeze) freeze.disabled = !project || project.status !== "Active Baseline" || missing.length > 0 || !pricesValid;
    const modal = page.querySelector("#catalogItems");
    if (modal) {
      const choices = catalog.filter(item => item.active && !selectedIds.has(Number(item.id)));
      modal.innerHTML = choices.length ? choices.map(item => {
        const frame = frames.find(candidate => Number(candidate.catalogItemId) === Number(item.id));
        const priceText = frame ? `${money(frame.minPrice)} – ${money(frame.maxPrice)}` : "Chưa có PriceFrame";
        return `<article class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-outline-variant/40 p-3"><div><h3 class="font-semibold text-on-surface">${escapeHtml(item.name)}</h3><p class="text-xs text-on-surface-variant">${escapeHtml(item.category)} · ${escapeHtml(item.unit)} · Giá mặc định ${money(item.basePrice)}</p><p class="mt-1 text-xs ${frame ? "text-emerald-700" : "text-red-700"}">PriceFrame: ${priceText}</p></div><button type="button" data-add-spec-item="${Number(item.id)}" class="rounded-lg border border-primary px-3 py-1.5 text-sm font-medium text-primary hover:bg-primary hover:text-white">Thêm</button></article>`;
      }).join("") : '<p class="rounded-lg bg-surface-container-low p-4 text-sm text-on-surface-variant">Không còn dịch vụ đang hoạt động để thêm.</p>';
    }
  };
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

    renderManagerSpecification(data);

    hydrateCustomerContract(data.project);

    const roadmapSection = document.querySelector('[data-wpsm-screen="C03"] main section');
    if (roadmapSection && data.project) {
      const roadmap = data.roadmap || [];
      roadmapSection.innerHTML = roadmap.length ? roadmap.map(task => `<article class="relative z-10 bg-surface-container-lowest border border-outline-variant/40 rounded-xl p-5 shadow-sm">
        <div class="flex flex-wrap items-center justify-between gap-3"><div><h3 class="text-title-lg font-title-lg text-primary font-bold">${escapeHtml(task.milestone)} · ${escapeHtml(task.title)}</h3><p class="text-body-sm text-on-surface-variant mt-1">Phụ trách: ${task.assigneeRole === "Customer" ? "Khách hàng" : "Quản lý"}</p></div>
        <div class="text-right"><div class="font-semibold">Hạn: ${new Intl.DateTimeFormat("vi-VN").format(new Date(`${task.dueDate}T00:00:00`))}</div><span class="text-sm">${task.status === "Done" ? "Đã hoàn thành" : task.status === "Overdue" ? "Quá hạn" : "Chờ thực hiện"}</span></div></div></article>`).join("") : `<p class="rounded-xl bg-surface-container-low p-5">Roadmap sẽ được tạo sau khi Manager đóng băng baseline.</p>`;
    }
  };
  const showError = error => { status.textContent = error.message; status.classList.remove("is-ready"); status.classList.add("is-error"); };
  const showM05Error = error => {
    showError(error);
    const box = document.querySelector('[data-wpsm-screen="M05"] #m05Error');
    if (box) { box.textContent = error.message; box.classList.remove("hidden"); }
  };
  const showM05Notice = text => {
    const box = document.querySelector('[data-wpsm-screen="M05"] #m05Notice');
    if (box) { box.textContent = text; box.classList.remove("hidden"); }
    message(text);
  };
  const clearM05Messages = () => {
    document.querySelector('[data-wpsm-screen="M05"] #m05Error')?.classList.add("hidden");
    document.querySelector('[data-wpsm-screen="M05"] #m05Notice')?.classList.add("hidden");
  };
  const saveM05Specification = async () => {
    const data = window.wpsm;
    const project = data?.project;
    if (!project) throw new Error("Chưa chọn dự án để lưu đặc tả.");
    if (project.status !== "Active Baseline") throw new Error("Baseline đã đóng băng; không thể chỉnh sửa đặc tả.");
    const items = m05Items();
    if (items.some(item => !item.description || item.quantity < 1 || item.unitPrice < 0))
      throw new Error("Mỗi hạng mục cần có diễn giải, số lượng nguyên dương và đơn giá không âm.");
    const catering = items.find(item => data.catalog.find(catalogItem => Number(catalogItem.id) === item.catalogItemId)?.category === "Ẩm thực");
    const summary = `${items.length} hạng mục đặc tả`;
    const updated = await send(`projects/${encodeURIComponent(project.id)}/specification`, "PUT", {
      summary,
      guestCount: catering?.quantity || 0,
      items
    });
    window.wpsm.project = updated;
    hydrateTables(window.wpsm);
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
    if (screen === "M05" && button.dataset.closeModal) {
      event.preventDefault();
      const modal = document.getElementById(button.dataset.closeModal);
      modal?.classList.add("hidden"); modal?.classList.remove("flex");
      return;
    }
    if (screen === "M05" && button.id === "openCatalogBtn") {
      clearM05Messages();
      const modal = document.getElementById("catalogModal");
      modal?.classList.remove("hidden"); modal?.classList.add("flex");
      updateManagerSummary(window.wpsm);
      return;
    }
    if (screen === "M05" && button.dataset.addSpecItem) {
      const id = Number(button.dataset.addSpecItem);
      const catalogItem = window.wpsm?.catalog.find(item => Number(item.id) === id);
      if (!catalogItem || !window.wpsm?.project) return;
      const project = window.wpsm.project;
      const frame = window.wpsm.priceFrames.find(item => Number(item.catalogItemId) === id);
      project.specification = [...(project.specification || []), {
        catalogItemId: id, name: catalogItem.name, category: catalogItem.category,
        description: catalogItem.name, quantity: 1, unit: catalogItem.unit,
        unitPrice: catalogItem.basePrice, lineTotal: catalogItem.basePrice,
        priceFrameMin: frame?.minPrice, priceFrameMax: frame?.maxPrice
      }];
      hydrateTables(window.wpsm);
      updateManagerSummary(window.wpsm);
      return;
    }
    if (screen === "M05" && button.hasAttribute("data-remove-spec-item")) {
      const row = button.closest("tr[data-spec-item-id]");
      const id = Number(row?.dataset.specItemId);
      window.wpsm.project.specification = window.wpsm.project.specification.filter(item => Number(item.catalogItemId) !== id);
      hydrateTables(window.wpsm);
      return;
    }
    if (screen === "M05" && button.id === "saveSpecificationBtn") {
      clearM05Messages();
      button.disabled = true;
      try {
        await saveM05Specification();
        showM05Notice("Đã lưu đặc tả vào database.");
      } catch (error) { showM05Error(error); }
      finally { if (window.wpsm) renderManagerSpecification(window.wpsm); }
      return;
    }
    if (screen === "M05" && button.id === "openFreezeBtn") {
      clearM05Messages();
      const modal = document.getElementById("freezeModal");
      const consent = document.getElementById("freezeConsent");
      const confirm = document.getElementById("confirmFreezeBtn");
      if (consent) consent.checked = false;
      if (confirm) { confirm.disabled = true; confirm.classList.add("opacity-50"); }
      modal?.classList.remove("hidden"); modal?.classList.add("flex");
      return;
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
    if (screen === "M05" && button.id === "confirmFreezeBtn") {
      event.preventDefault(); event.stopImmediatePropagation();
      clearM05Messages();
      button.disabled = true;
      try {
        const saved = await saveM05Specification();
        const frozen = await send(`projects/${encodeURIComponent(saved.id)}/freeze`, "POST", {});
        window.wpsm.project = frozen;
        const modal = document.getElementById("freezeModal");
        modal?.classList.add("hidden"); modal?.classList.remove("flex");
        hydrateTables(window.wpsm);
        showM05Notice(`Đã đóng băng Baseline ${frozen.id} và tạo roadmap.`);
      } catch (error) {
        showM05Error(error);
        if (window.wpsm) renderManagerSpecification(window.wpsm);
      }
      return;
    }
  }, true);

  document.addEventListener("change", event => {
    const screen = document.querySelector("[data-wpsm-screen]")?.dataset.wpsmScreen;
    if (screen !== "M05") return;
    if (event.target.id === "m05ProjectSelect" && event.target.value)
      window.location.href = `/M05?id=${encodeURIComponent(event.target.value)}`;
    if (event.target.id === "freezeConsent") {
      const confirm = document.getElementById("confirmFreezeBtn");
      if (confirm) { confirm.disabled = !event.target.checked; confirm.classList.toggle("opacity-50", !event.target.checked); }
    }
    if (event.target.matches("#specItemsBody input, #specItemsBody textarea")) updateManagerSummary(window.wpsm);
  }, true);
  document.addEventListener("input", event => {
    if (document.querySelector('[data-wpsm-screen="M05"]') && event.target.matches("#specItemsBody input, #specItemsBody textarea"))
      updateManagerSummary(window.wpsm);
  }, true);

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
        const specificationLink = document.getElementById("projectSpecificationLink");
        if (specificationLink) specificationLink.href = `/M05?id=${encodeURIComponent(project.id)}`;
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
