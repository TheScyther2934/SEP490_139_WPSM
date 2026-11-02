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
  const homeForRole = role => role === "Admin" ? "/A02" : role === "Manager" ? "/M02" : "/C01";
  const setText = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value; };
  const dateVi = value => value ? new Intl.DateTimeFormat("vi-VN").format(new Date(`${value}T00:00:00`)) : "—";
  let createHalls = [];
  const updateCreateSummary = () => {
    if (!document.getElementById("summaryBase")) return;
    const value = id => document.getElementById(id)?.value || "";
    const base = moneyValue(document.getElementById("baseValue"));
    const nrd = moneyValue(document.getElementById("nrdAmount"));
    const ratio = window.wpsm?.policies?.nrdRatio;
    const minimum = ratio == null ? null : Math.round(base * ratio / 100);
    const hall = createHalls.find(h => h.name === value("hallSelect"));
    const shift = document.querySelector('input[name="shift_selection"]:checked:not(:disabled)');
    setText("summaryCouple", value("coupleName") || "Chưa nhập tên khách hàng");
    setText("summaryEmail", value("customerEmail") || "—");
    setText("summaryPhone", value("customerPhone") || "—");
    setText("summaryHall", hall?.name || "Chưa chọn sảnh");
    setText("summaryCapacity", hall ? `Tối đa ${hall.maxCapacity} khách` : "—");
    setText("summaryDateSlot", `${dateVi(value("eventDate"))} · ${shift ? `Ca ${slotLabel(shift.value)}` : "Chưa chọn ca"}`);
    setText("summaryBase", money(base));
    setText("summaryNrd", money(nrd));
    setText("summaryMinimum", minimum == null ? "Đang tải chính sách" : money(minimum));
    setText("summaryRatio", `Tỷ lệ NRD yêu cầu${ratio == null ? "" : ` (${ratio}%)`}:`);
    setText("summaryBalance", money(Math.max(0, base - nrd)));
    const percent = base > 0 ? Math.round(nrd / base * 100) : 0;
    setText("summaryPercent", `${percent}% / 100%`);
    const progress = document.getElementById("summaryProgress");
    if (progress) progress.style.width = `${Math.min(percent, 100)}%`;
    setText("nrdMinimumLabel", minimum == null ? "Đang tải chính sách" : `${ratio}% · ${money(minimum)}`);
    setText("nrdMinimumHint", !base ? "Nhập giá trị hợp đồng để tính mức NRD tối thiểu." :
      minimum == null ? "Chưa tải được chính sách NRD." : nrd < minimum ? `NRD cần tối thiểu ${money(minimum)}.` : nrd > base ? "NRD không được vượt giá trị hợp đồng." : "NRD đạt mức tối thiểu theo chính sách.");
    const weekday = value("eventDate") ? new Intl.DateTimeFormat("vi-VN", { weekday: "long" }).format(new Date(`${value("eventDate")}T00:00:00`)) : "";
    setText("eventDateWeekday", weekday);
    setText("hallAvailabilityHint", shift ? "Ca còn trống tại thời điểm kiểm tra" : "Chọn sảnh, ngày và ca để kiểm tra");
  };
  document.addEventListener("input", updateCreateSummary);
  document.addEventListener("change", updateCreateSummary);
  const statusStyles = {
    "Active Baseline": { bg: "bg-blue-50", text: "text-blue-700", border: "border-blue-200", dot: "bg-blue-600" },
    "Baseline Frozen": { bg: "bg-[#8B2E4A]/10", text: "text-[#8B2E4A]", border: "border-[#8B2E4A]/30", dot: "bg-[#8B2E4A]" },
    "Event Completed": { bg: "bg-amber-50", text: "text-amber-800", border: "border-amber-300", dot: "bg-amber-600" },
    "Settled": { bg: "bg-emerald-50", text: "text-emerald-700", border: "border-emerald-300", dot: "bg-emerald-600" },
    "Closed": { bg: "bg-gray-100", text: "text-gray-700", border: "border-gray-300", dot: "bg-gray-500" }
  };
  const slotLabel = slot => slot === "Evening" || slot === "Tối" ? "Tối" : "Trưa";
  const slotClass = slot => (slot === "Evening" || slot === "Tối")
    ? "bg-surface-variant text-on-surface-variant" : "bg-secondary-container/40 text-on-secondary-container";
  const tDayBadge = (eventDate) => {
    const diff = Math.ceil((new Date(`${eventDate}T00:00:00`).getTime() - new Date().setHours(0,0,0,0)) / 86400000);
    if (diff <= 0) {
      if (diff === 0) return `<span class="inline-block px-3 py-0.5 rounded-full text-xs font-bold bg-amber-100 text-amber-900 border border-amber-300">Hôm nay</span>`;
      return `<span class="inline-block px-2.5 py-0.5 rounded-full text-xs font-medium bg-surface-container text-on-surface-variant border border-outline-variant">Đã diễn ra</span>`;
    }
    return `<span class="inline-block px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-50 text-amber-800 border border-amber-300">T-${diff}</span>`;
  };

  const renderProjectsTable = (projects, filteredCount = projects.length, page = 1, pageSize = 5) => {
    const table = document.getElementById("projects-table");
    const emptyState = document.getElementById("empty-state");
    const tbody = table?.querySelector("tbody");
    if (!tbody) return;

    if (!projects || projects.length === 0) {
      if (table) table.classList.add("hidden");
      if (emptyState) emptyState.classList.remove("hidden");
    }

    else {
      if (table) table.classList.remove("hidden");
      if (emptyState) emptyState.classList.add("hidden");
    }

    tbody.innerHTML = projects.map(project => {

      const date = new Intl.DateTimeFormat("vi-VN").format(new Date(`${project.eventDate}T00:00:00`));
      const st = statusStyles[project.status] || statusStyles["Active Baseline"];
      return `<tr class="hover:bg-surface-container-low/60 transition-colors group">
<td class="py-4 px-4 whitespace-nowrap"><span class="font-semibold text-primary font-title-sm">${escapeHtml(project.id)}</span></td>
<td class="py-4 px-4"><div class="font-medium text-on-surface">${escapeHtml(project.coupleName)}</div></td>
<td class="py-4 px-4 whitespace-nowrap"><span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-title-sm bg-surface-container text-on-surface border border-secondary-container">${escapeHtml(project.hall)}</span></td>
<td class="py-4 px-3 text-center whitespace-nowrap"><span class="inline-block px-2 py-0.5 rounded text-xs font-title-sm ${slotClass(project.slot)}">${slotLabel(project.slot)}</span></td>
<td class="py-4 px-4 whitespace-nowrap font-medium text-on-surface">${date}</td>
<td class="py-4 px-4 text-center whitespace-nowrap">${tDayBadge(project.eventDate)}</td>
<td class="py-4 px-4 text-center whitespace-nowrap"><span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold ${st.bg} ${st.text} ${st.border}"><span class="w-1.5 h-1.5 rounded-full ${st.dot}"></span> ${escapeHtml(project.status)}</span></td>
<td class="py-4 px-4 text-right whitespace-nowrap"><a class="inline-flex items-center gap-1 px-3 py-1 text-xs font-title-sm text-primary hover:text-on-primary hover:bg-primary border border-outline-variant hover:border-primary rounded-lg transition-all duration-150 active:scale-95" href="/M04?id=${encodeURIComponent(project.id)}"><span>Mở</span><span class="material-symbols-outlined text-sm" data-icon="arrow_forward">arrow_forward</span></a></td>
</tr>`;
    }).join("");

    const pagInfo = document.getElementById("projects-pagination-info");
    if (pagInfo) pagInfo.textContent = `Hiển thị ${filteredCount ? (page - 1) * pageSize + 1 : 0}–${Math.min(page * pageSize, filteredCount)} trên ${filteredCount} dự án`;
  };

  const wireProjectFilters = (allProjects) => {
    const pageSize = 5;
    let page = 1;
    const searchInput = document.getElementById("search-input");
    const statusFilter = document.getElementById("status-filter");
    const hallFilter = document.getElementById("hall-filter");
    const monthFilter = document.getElementById("month-filter");
    const resetBtn = document.getElementById("reset-btn");
    const clearSearchBtn = document.getElementById("clear-search-btn");
    const prev = document.querySelector("[data-page-prev]");
    const next = document.querySelector("[data-page-next]");
    const pageLabel = document.querySelector("[data-page-label]");
    if (hallFilter) hallFilter.innerHTML = '<option value="">Tất cả sảnh</option>' + [...new Set(allProjects.map(p => p.hall))].sort().map(h => `<option value="${escapeHtml(h)}">${escapeHtml(h)}</option>`).join("");
    if (monthFilter) monthFilter.innerHTML = '<option value="">Tất cả các tháng</option>' + [...new Set(allProjects.map(p => p.eventDate.slice(0, 7)))].sort().map(m => `<option value="${m}">${m.slice(5)}/${m.slice(0, 4)}</option>`).join("");
    const badge = document.getElementById("projects-nav-badge");
    if (badge) badge.textContent = allProjects.length;
    const scope = document.getElementById("projects-scope-pill");
    if (scope) scope.textContent = `${allProjects.length} dự án được phân công`;

    const applyFilters = () => {
      const q = (searchInput?.value || "").toLowerCase().trim();
      const st = statusFilter?.value || "";
      const hl = hallFilter?.value || "";
      const mo = monthFilter?.value || "";

      const filtered = allProjects.filter(p => {
        const matchQuery = !q || [p.id, p.coupleName, p.customerEmail].some(value => (value || "").toLowerCase().includes(q));
        const matchStatus = !st || p.status === st;
        const matchHall = !hl || p.hall === hl;
        const matchMonth = !mo || p.eventDate.startsWith(mo);
        return matchQuery && matchStatus && matchHall && matchMonth;
      });
      const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
      page = Math.min(page, pages);
      renderProjectsTable(filtered.slice((page - 1) * pageSize, page * pageSize), filtered.length, page, pageSize);
      if (prev) prev.disabled = page === 1;
      if (next) next.disabled = page === pages;
      if (pageLabel) pageLabel.textContent = `${page} / ${pages}`;
    };

    const filterChanged = () => { page = 1; applyFilters(); };
    searchInput?.addEventListener("input", filterChanged);
    statusFilter?.addEventListener("change", filterChanged);
    hallFilter?.addEventListener("change", filterChanged);
    monthFilter?.addEventListener("change", filterChanged);
    prev?.addEventListener("click", () => { page--; applyFilters(); });
    next?.addEventListener("click", () => { page++; applyFilters(); });

    const reset = () => {
      if (searchInput) searchInput.value = "";
      if (statusFilter) statusFilter.value = "";
      if (hallFilter) hallFilter.value = "";
      if (monthFilter) monthFilter.value = "";
      filterChanged();
    };
    resetBtn?.addEventListener("click", reset);
    clearSearchBtn?.addEventListener("click", reset);
    applyFilters();
  };

  const hydrateTables = data => {
    if (document.querySelector('[data-wpsm-screen="M02"]')) {
      wireProjectFilters(data.projects);
    }

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

  const openHubModule = module => {
    const project = window.wpsm?.project;
    if (!project) return showError(new Error("Không tìm thấy dự án."));
    if (module === "overview") return;
    if (module === "specification") { window.location.href = `/M05?id=${encodeURIComponent(project.id)}`; return; }
    const dialog = document.getElementById("hubModuleDialog");
    const content = document.getElementById("hubModuleContent");
    if (!dialog || !content) return;
    const titles = { roadmap: "Lộ trình dự án", changes: "Yêu cầu thay đổi", beos: "BEO", payments: "Thanh toán", dayOfTasks: "Ngày sự kiện", complaints: "Khiếu nại" };
    setText("hubModuleTitle", `${titles[module]} · ${project.id}`);
    if (module === "payments") {
      content.innerHTML = `<p>Tổng hợp đồng hiện tại: <strong>${money(project.currentTotal)}</strong> · Còn lại: <strong>${money(project.outstandingBalance)}</strong></p>` +
        (project.payments || []).map(p => `<article class="p-3 border border-outline-variant rounded-lg"><strong>${escapeHtml(p.type)} · ${money(p.amount)}</strong><p>${escapeHtml(p.status)} · Hạn: ${dateVi(p.dueDate)}${p.paidDate ? ` · Đã thu: ${dateVi(p.paidDate)}` : ""}</p>${p.type === "NRD" && p.hasReceipt ? `<a class="text-primary underline" href="/api/wpsm/projects/${encodeURIComponent(project.id)}/nrd-receipt" target="_blank" rel="noopener">Tải biên nhận NRD</a>` : ""}</article>`).join("");
    } else {
      const items = module === "roadmap" ? (window.wpsm.roadmap || []).map(t => ({ title: `${t.milestone} · ${t.title}`, status: t.status, detail: `${t.assigneeRole === "Manager" ? "Quản lý" : "Khách hàng"} · ${dateVi(t.dueDate)}` })) : project[module] || [];
      content.innerHTML = items.length ? items.map(item => `<article class="p-3 border border-outline-variant rounded-lg"><strong>${escapeHtml(item.title)}</strong><p>${escapeHtml(item.status)}</p><p class="text-sm text-on-surface-variant">${escapeHtml(item.detail || "")}</p></article>`).join("") : '<p class="p-4 bg-surface-container-low rounded-lg">Chưa có dữ liệu trong dự án này.</p>';
    }
    dialog.showModal();
  };
  document.addEventListener("keydown", event => {
    if ((event.key === "Enter" || event.key === " ") && event.target.matches('[role="button"][data-hub-module]')) {
      event.preventDefault(); openHubModule(event.target.dataset.hubModule);
    }
  });


  document.addEventListener("submit", async event => {
    const form = event.target;
    const screen = document.querySelector("[data-wpsm-screen]")?.dataset.wpsmScreen;
    if (!(form instanceof HTMLFormElement)) return;
    if (screen === "G01" && form.id === "loginForm") {
      event.preventDefault(); event.stopImmediatePropagation();
      const errArea = document.getElementById("errorNotificationArea");
      const errMsg = document.getElementById("loginErrorMessage");
      if (errArea) errArea.classList.add("hidden");
      const submit = form.querySelector('button[type="submit"]');
      if (submit?.disabled) return;
      if (submit) submit.disabled = true;
      try {
        const user = await send("auth/login", "POST", { email: form.querySelector("#email").value, password: form.querySelector("#password").value });
        if (user.mustChangePassword) { window.location.href = "/G03"; return; }
        message(`Đăng nhập thành công · ${user.fullName}`);
        window.location.href = homeForRole(user.role);
      } catch (error) {
        showError(error);
        if (errArea && errMsg) {
          errMsg.textContent = error.message || "Email hoặc mật khẩu không đúng. Vui lòng kiểm tra lại.";
          errArea.classList.remove("hidden");
        }
      }
      finally { if (submit) submit.disabled = false; }
      return;
    }
    if (screen === "G03") {
      event.preventDefault(); event.stopImmediatePropagation();
      const curErr = document.getElementById("current_password_error");
      const newErr = document.getElementById("new_password_error");
      const cfmErr = document.getElementById("confirm_password_error");
      if (curErr) curErr.classList.add("hidden");
      if (newErr) newErr.classList.add("hidden");
      if (cfmErr) cfmErr.classList.add("hidden");

      const currentPassword = form.querySelector("#current_password")?.value || "";
      const newPassword = form.querySelector("#new_password")?.value || "";
      const confirmPassword = form.querySelector("#confirm_password")?.value || "";

      if (!currentPassword) {
        if (curErr) {
          document.getElementById("current_password_error_text").textContent = "Vui lòng nhập mật khẩu hiện tại.";
          curErr.classList.remove("hidden");
        }
        return;
      }
      if (newPassword.length < 8) {
        if (newErr) {
          document.getElementById("new_password_error_text").textContent = "Mật khẩu mới phải dài ít nhất 8 ký tự.";
          newErr.classList.remove("hidden");
        }
        return;
      }
      if (newPassword === currentPassword) {
        if (newErr) {
          document.getElementById("new_password_error_text").textContent = "Mật khẩu mới không được trùng với mật khẩu hiện tại.";
          newErr.classList.remove("hidden");
        }
        return;
      }
      if (newPassword !== confirmPassword) {
        if (cfmErr) {
          document.getElementById("confirm_password_error_text").textContent = "Mật khẩu xác nhận không khớp với mật khẩu mới.";
          cfmErr.classList.remove("hidden");
        }
        return;
      }

      try {
        await send("auth/change-password", "POST", { currentPassword, newPassword, confirmPassword });
        const toast = document.getElementById("changePasswordSuccessToast");
        if (toast) toast.classList.remove("hidden");
        message("Đổi mật khẩu thành công!");
        form.reset();
        setTimeout(() => {
          const role = window.wpsm?.profile?.role;
          window.location.href = homeForRole(role);
        }, 1500);
      } catch (error) {
        showError(error);
        if (curErr) {
          document.getElementById("current_password_error_text").textContent = error.message || "Mật khẩu hiện tại không đúng.";
          curErr.classList.remove("hidden");
        }
      }
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
    const button = event.target.closest("button, a, [data-hub-module]");
    if (!button) return;
    const screen = document.querySelector("[data-wpsm-screen]")?.dataset.wpsmScreen;
    const label = button.innerText.replace(/\s+/g, " ").trim();
    if (label.includes("Đăng xuất")) {
      event.preventDefault(); event.stopImmediatePropagation();
      try { await send("auth/logout", "POST", {}); } catch (error) { showError(error); return; }
      window.location.href = "/G01";
      return;
    }
    if (label.includes("Đổi mật khẩu")) {
      event.preventDefault(); window.location.href = "/G03"; return;
    }
    if (/^(account_circle\s*)?Hồ sơ( cá nhân)?$/.test(label)) {
      event.preventDefault(); window.location.href = "/G02"; return;
    }
    if (label.includes("Danh sách dự án") || screen === "M02" && /Dự án$/.test(label)) {
      event.preventDefault(); window.location.href = "/M02"; return;
    }
    if (screen?.startsWith("M") && /Khởi tạo hợp đồng/.test(label)) {
      event.preventDefault(); window.location.href = "/M03"; return;
    }
    if (screen === "M04" && label.includes("Xuất") && label.includes("PDF")) {
      event.preventDefault(); window.print(); return;
    }
    if (button.dataset.hubModule) {
      event.preventDefault(); openHubModule(button.dataset.hubModule); return;
    }
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
      if (button.disabled || button.dataset.projectCreated || window.wpsm?.createdProjectId) return;
      const fileInput = document.getElementById("nrdReceiptFile");
      const receipt = fileInput?.files?.[0];
      const paymentMethod = window.wpsm?.pendingNrdProject || document.getElementById("nrdPaymentMethod")?.value.includes("Chuyển khoản") ? "Bank" : "Cash";
      if (!window.wpsm?.pendingNrdProject && paymentMethod === "Bank" && !receipt)
        return showError(new Error("Vui lòng chọn biên nhận NRD trước khi tạo dự án."));
      if (window.wpsm?.pendingNrdProject && !receipt)
        return showError(new Error("Dự án đã tạo nhưng chưa ghi nhận được biên nhận. Vui lòng chọn tệp để hoàn tất."));
      const fields = ["coupleName", "customerEmail", "customerPhone", "hallSelect", "eventDate", "baseValue", "nrdAmount"]
        .map(id => document.getElementById(id));
      if (!window.wpsm?.pendingNrdProject) {
        for (const field of fields) {
          if (!field?.reportValidity()) return;
        }
        if (!document.querySelector('input[name="shift_selection"]:checked:not(:disabled)'))
          return showError(new Error("Vui lòng chọn ca tiệc còn trống cho sảnh và ngày tổ chức."));
        const base = moneyValue(document.getElementById("baseValue"));
        const nrd = moneyValue(document.getElementById("nrdAmount"));
        const ratio = window.wpsm?.policies?.nrdRatio;
        if (ratio == null) return showError(new Error("Chưa tải được chính sách NRD. Vui lòng tải lại trang."));
        if (base <= 0 || nrd < Math.round(base * ratio / 100) || nrd > base)
          return showError(new Error(`Giá trị hợp đồng phải dương; NRD từ ${ratio}% đến 100% giá trị hợp đồng.`));
      }
      if (receipt && (receipt.size <= 0 || receipt.size > 10 * 1024 * 1024 || !/\.(pdf|jpe?g|png)$/i.test(receipt.name)))
        return showError(new Error("Biên nhận phải là PDF/JPG/PNG, tối đa 10 MB."));
      const confirmation = document.getElementById("createProjectConfirm");
      if (confirmation && button.id !== "createProjectConfirmSubmit" && !window.wpsm?.pendingNrdProject) {
        document.getElementById("createProjectConfirmSummary").textContent = ["summaryCouple", "summaryHall", "summaryDateSlot", "summaryBase", "summaryNrd"]
          .map(id => document.getElementById(id).textContent).join(" · ");
        confirmation.showModal();
        return;
      }
      button.disabled = true;
      try {
        let project = window.wpsm?.pendingNrdProject;
        let temporaryPassword = window.wpsm?.pendingCustomerPassword;
        if (!project) {
          const result = await send("projects", "POST", {
          coupleName: document.getElementById("coupleName").value.trim(),
          customerEmail: document.getElementById("customerEmail").value.trim(),
          customerPhone: document.getElementById("customerPhone").value.trim(),
          hall: document.getElementById("hallSelect").value,
          slot: document.querySelector('input[name="shift_selection"]:checked').value,
          eventDate: document.getElementById("eventDate").value,
          baseValue: moneyValue(document.getElementById("baseValue")),
          nrdAmount: moneyValue(document.getElementById("nrdAmount")),
          paymentMethod
          });
          project = result.project;
          temporaryPassword = result.temporaryPassword;
          window.wpsm = { ...(window.wpsm || {}), project, pendingNrdProject: paymentMethod === "Bank" ? project : null };
          window.wpsm.pendingCustomerPassword = temporaryPassword;
          window.wpsm.credentialEmailStatus = result.emailStatus;
        }
        if (paymentMethod === "Bank") {
          await sendFile(`projects/${encodeURIComponent(project.id)}/nrd-receipt`, receipt);
          window.wpsm.pendingNrdProject = null;
        }
        message(temporaryPassword
          ? `Đã tạo ${project.id}, ghi nhận NRD${temporaryPassword ? `. Mật khẩu tạm của khách: ${temporaryPassword} (chỉ hiển thị lần này).` : ""}`
: `Đã tạo dự án ${project.id} và ghi nhận NRD qua database.`);
        const success = document.getElementById("createProjectSuccess");
        if (success) {
          const emailNote = { Sent: "Email tài khoản đã được gửi.", Pending: "Email tài khoản đang chờ gửi. Trạng thái được lưu và cập nhật trong dự án.", NotConfigured: "Chưa cấu hình gửi email. Vui lòng chuyển thông tin đăng nhập cho khách.", Failed: "Gửi email chưa thành công. Vui lòng chuyển thông tin đăng nhập cho khách.", ExistingAccount: "Khách hàng sử dụng tài khoản đã có." }[window.wpsm.credentialEmailStatus] || "";
          success.innerHTML = `<h3 class="font-semibold text-primary">Đã tạo ${escapeHtml(project.id)}</h3><p>${escapeHtml(emailNote)}</p>` +
            (temporaryPassword ? `<p>Email: ${escapeHtml(document.getElementById("customerEmail").value)}</p><p>Mật khẩu tạm: <code>${escapeHtml(temporaryPassword)}</code></p><p>Vui lòng lưu thông tin này trước khi rời trang. Khách phải đổi mật khẩu khi đăng nhập lần đầu.</p>` : "") +
            `<a class="inline-block px-4 py-2 rounded-lg bg-primary text-on-primary" href="/M04?id=${encodeURIComponent(project.id)}">Mở dự án</a> <a class="text-primary underline" href="/M02">Danh sách dự án</a>`;
          success.classList.remove("hidden");
          success.scrollIntoView({ behavior: "smooth", block: "center" });
        }
        confirmation?.close();
        delete window.wpsm.pendingCustomerPassword;
        button.dataset.projectCreated = project.id;
        window.wpsm.createdProjectId = project.id;
        button.disabled = true;
        document.querySelectorAll('[data-wpsm-screen="M03"] input, [data-wpsm-screen="M03"] select').forEach(field => { field.disabled = true; });

      } catch (error) { showError(error); }
      finally { button.disabled = !!button.dataset.projectCreated; }
    }
    if (screen === "C02" && button.id === "btnAddenda") {
      event.preventDefault(); window.location.href = "/C09"; return;
    }
    if (screen === "C09" && button.dataset.action === "download-addendum") {
      event.preventDefault(); event.stopImmediatePropagation();
      try {
        const signed = await send(`files/${encodeURIComponent(button.dataset.fileId)}`, "GET");
        window.location.href = signed.url;
      } catch (error) { showError(error); }
      return;
    }
    if (screen === "M04" && button.dataset.action === "resend-credentials") {
      event.preventDefault(); event.stopImmediatePropagation();
      const project = window.wpsm?.project;
      if (!project) return;
      try {
        const result = await send(`projects/${encodeURIComponent(project.id)}/resend-credentials`, "POST", {});
        document.getElementById("credentialAlert")?.remove();
        message(result.temporaryPassword ? `Đã xếp lại email. Mật khẩu tạm: ${result.temporaryPassword} (chỉ hiển thị lần này).` : "Đã xếp lại email thông tin đăng nhập.");
      } catch (error) { showError(error); }
      return;
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
  if (document.querySelector('[data-wpsm-screen="G03"]')) {
    fetch("/api/wpsm/auth/me").then(r => {
      if (!r.ok) { window.location.href = "/G01"; throw new Error("Phiên đăng nhập đã hết hạn."); }
      return r.json();
    }).then(session => {
      window.wpsm = { profile: session.user };
      const roleText = session.user.role === "Manager" ? "Quản lý" : session.user.role === "Admin" ? "Quản trị viên" : "Khách hàng";
      document.querySelectorAll("[data-profile-name]").forEach(el => { el.textContent = session.user.fullName; });
      document.querySelectorAll("[data-profile-role]").forEach(el => { el.textContent = roleText; });
      document.querySelectorAll("[data-profile-initials]").forEach(el => { el.textContent = session.user.fullName.split(" ").slice(-2).map(w => w[0]).join(""); });
      if (session.mustChangePassword) {
        document.querySelectorAll('[onclick*="window.history"]').forEach(button => { button.hidden = true; });
        document.getElementById("changePasswordForm").insertAdjacentHTML("afterbegin", '<p class="p-3 rounded-lg bg-secondary-container text-on-secondary-container">Vui lòng đổi mật khẩu tạm để bắt đầu sử dụng tài khoản.</p>');
      }
      message("Tài khoản đã được xác thực.");
    }).catch(showError);
    return;
  }
  if (document.querySelector('[data-wpsm-screen="M02"]')) {
    fetch("/api/wpsm/halls").then(r => { if (!r.ok) throw new Error("Không tải được thống kê sảnh."); return r.json(); }).then(halls => {
      const slots = halls.flatMap(h => h.slots);
      const booked = slots.filter(s => !s.available).length;
      const percent = slots.length ? Math.round(booked / slots.length * 100) : 0;
      setText("activeHallCount", `${halls.length} sảnh tiệc hoạt động`);
      setText("hallOccupancyPercent", `${percent}%`);
      setText("hallOccupancySummary", `${booked}/${slots.length} ca tiệc đã đặt hôm nay`);
      const bar = document.getElementById("hallOccupancyBar");
      if (bar) bar.style.width = `${percent}%`;
    }).catch(showError);
  }

  const hallSelect = document.querySelector('[data-wpsm-screen="M03"] #hallSelect');
  if (hallSelect) {
    const eventDate = document.getElementById("eventDate");
    const today = new Date();
    eventDate.min = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
    const shiftOptions = document.getElementById("shiftOptions");
    let halls = [];
    let requestVersion = 0;
    const renderSlots = () => {
      const selected = halls.find(h => h.name === hallSelect.value);
      shiftOptions.innerHTML = selected && eventDate.value
        ? selected.slots.map(slot => `<label class="relative flex items-center gap-3 p-4 rounded-lg border border-outline-variant bg-surface-container-lowest ${slot.available ? "cursor-pointer hover:border-primary" : "opacity-50"}">
            <input type="radio" name="shift_selection" value="${escapeHtml(slot.slotName)}" ${slot.available ? "required" : "disabled"} class="accent-primary" />
            <span class="flex-1"><span class="block font-title-sm text-on-surface">${slotLabel(slot.slotName)} (${escapeHtml(slot.startTime.slice(0, 5))} – ${escapeHtml(slot.endTime.slice(0, 5))})</span>
            <span class="block text-xs ${slot.available ? "text-emerald-700" : "text-error"}">${slot.available ? "Còn trống" : "Đã đặt"}</span></span>
          </label>`).join("")
        : '<p class="md:col-span-2 text-sm text-on-surface-variant">Chọn sảnh và ngày tổ chức để xem ca tiệc còn trống.</p>';
    };
    const loadHalls = async () => {
      const version = ++requestVersion;
      shiftOptions.textContent = "Đang kiểm tra lịch sảnh...";
      hallSelect.disabled = true;
      try {
        const response = await fetch(`/api/wpsm/halls${eventDate.value ? `?date=${encodeURIComponent(eventDate.value)}` : ""}`, { headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error("Không tải được lịch sảnh. Vui lòng đổi ngày để thử lại.");
        const result = await response.json();
        if (version !== requestVersion) return;
        halls = result;
        createHalls = halls;
        const previousHall = hallSelect.value;
        hallSelect.innerHTML = '<option value="">Chọn sảnh tiệc</option>' + halls.map(h =>
          `<option value="${escapeHtml(h.name)}">${escapeHtml(h.name)} – ${h.minCapacity}~${h.maxCapacity} khách – ${money(h.basePrice)}</option>`
        ).join("");
        hallSelect.value = previousHall;
        renderSlots();
        updateCreateSummary();
      } catch (error) {
        if (version !== requestVersion) return;
        halls = [];
        createHalls = [];
        shiftOptions.textContent = error.message;
        showError(error);
      } finally {
        if (version === requestVersion) hallSelect.disabled = false;
      }
    };
    hallSelect.addEventListener("change", () => { renderSlots(); updateCreateSummary(); });
    eventDate.addEventListener("change", loadHalls);
    loadHalls();
  }

  const selectedProject = new URLSearchParams(window.location.search).get("id");
  fetch(`/api/wpsm/bootstrap${selectedProject ? `?projectCode=${encodeURIComponent(selectedProject)}` : ""}`, { headers: { Accept: "application/json" } })
    .then(response => { if (!response.ok) throw new Error(`HTTP ${response.status}`); return response.json(); })
    .then(data => {
      window.wpsm = { ...(window.wpsm || {}), ...data };
      hydrateTables(data);
      updateCreateSummary();
      document.querySelectorAll("[data-unread-count]").forEach(el => { el.textContent = data.unreadNotifications || 0; el.hidden = !data.unreadNotifications; });
      document.querySelectorAll("[data-overdue-count]").forEach(el => { el.textContent = data.overdueTasks || 0; el.hidden = !data.overdueTasks; });
      const setEl = (id, text) => { const el = document.getElementById(id); if (el) el.textContent = text; };

      // 1. Hydrate User Profile (from DB data.profile)
      if (data.profile) {
        const p = data.profile;
        const initials = (p.fullName || "User").split(" ").map(w => w[0]).filter(Boolean).slice(-2).join("").toUpperCase();
        const roleText = p.role === "Admin" ? "Quản trị viên" : p.role === "Manager" ? "Quản lý Vận hành" : "Khách hàng";

        setEl("profileInitials", initials);
        setEl("profileHeaderName", p.fullName);
        setEl("profileFullName", p.fullName);
        setEl("profileEmail", p.email);
        setEl("profilePhone", p.phone || "Chưa cập nhật");
        setEl("profileRoleText", roleText);
        setEl("profileRoleBadge", roleText);
        document.querySelectorAll("[data-profile-name]").forEach(el => { el.textContent = p.fullName; });
        document.querySelectorAll("[data-profile-initials]").forEach(el => { el.textContent = initials; });
        document.querySelectorAll("[data-profile-role]").forEach(el => { el.textContent = roleText; });
        const profileProject = p.role === "Admin" ? null : data.project;
        setEl("profileProjectHall", profileProject?.hall || roleText);
        setEl("profileProjectCountdown", profileProject?.status || "Tài khoản hệ thống");
        setEl("profileProjectDate", profileProject ? `${dateVi(profileProject.eventDate)} · Ca ${slotLabel(profileProject.slot)}` : "Chưa có dự án liên kết");
        const profileNavigation = document.getElementById("profileNavigation");
        if (profileNavigation) {
          const links = p.role === "Manager" ? [["/M02", "Danh sách dự án"], ["/M03", "Khởi tạo hợp đồng"]] : p.role === "Admin" ? [["/A02", "Danh mục dịch vụ"], ["/A04", "Khung giá"], ["/A06", "Chính sách"]] : [["/C01", "Tổng quan dự án"], ["/C02", "Hợp đồng"], ["/C03", "Lộ trình"]];
          profileNavigation.innerHTML = links.concat([["/G02", "Hồ sơ cá nhân"], ["/G03", "Đổi mật khẩu"]]).map(([href, label]) => `<a class="block px-3 py-2 rounded-lg hover:bg-surface-container text-primary" href="${href}">${label}</a>`).join("") + '<button class="px-3 py-2 text-error" type="button">Đăng xuất</button>';
        }
        setEl("profileContractCode", profileProject?.id || "Không có hợp đồng liên kết");
        setEl("profileContractStatus", profileProject?.status || "—");
        const contractLink = document.getElementById("profileContractLinkLabel")?.closest("a");
        if (contractLink) {
          contractLink.hidden = !profileProject;
          contractLink.href = p.role === "Customer" ? "/C02" : `/M04?id=${encodeURIComponent(data.project?.id || "")}`;
        }
        setEl("projectsUpdatedAt", new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit" }).format(new Date()));
        document.querySelectorAll('a[href="/M04"], a[href="/M05"]').forEach(link => { if (data.project) link.href += `?id=${encodeURIComponent(data.project.id)}`; });
      }

      // 2. Hydrate Project Hub (M-04) from DB data.project
      const project = data.project;
      if (project && document.querySelector('[data-wpsm-screen="M04"]')) {
        const eventDateLabel = dateVi(project.eventDate);
        const daysLeft = Math.max(0, Math.ceil((new Date(`${project.eventDate}T00:00:00`).getTime() - new Date().setHours(0, 0, 0, 0)) / 86400000));

        setEl("hubProjectCode", project.id);
        setEl("hubHall", project.hall);
        setEl("hubSlot", project.slot === "Evening" || project.slot === "Tối" ? "Ca Tối (17:30 – 21:30)" : "Ca Trưa (11:00 – 14:00)");
        setEl("hubDate", eventDateLabel);
        setEl("hubStatus", project.status);
        setEl("hubCoupleName", project.coupleName);
        setEl("hubCountdown", daysLeft ? `T-${daysLeft} ngày` : new Date(`${project.eventDate}T00:00:00`) < new Date(new Date().setHours(0, 0, 0, 0)) ? "Đã diễn ra" : "Hôm nay");
        setEl("hubProgressPercent", `${project.roadmapProgress}%`);
        setEl("hubOutstandingBalance", formatMoney(project.outstandingBalance));
        setEl("hubPaymentDetails", `NRD: ${formatMoney(project.nrdAmount)} · Tổng HĐ: ${formatMoney(project.currentTotal)}`);
        setEl("hubCrBadge", `${project.openChangeRequests} đang xử lý`);
        setEl("hubBeoBadge", project.beoStatus);
        setEl("hubSummarySubtitle", `${project.customerEmail} · ${project.customerPhone}`);
        setEl("hubContractTitle", project.status === "Active Baseline" ? "Đặc tả đang soạn thảo" : "Baseline đã đóng băng");
        setEl("hubContractBadge", project.status);
        setEl("hubContractSubtitle", `${project.specificationSummary} · ${formatMoney(project.baseValue)}`);
        setEl("hubCredentialEmail", `Email tài khoản: ${{ Pending: "Đang chờ gửi", Sent: "Đã gửi", Failed: "Gửi thất bại", ExistingAccount: "Tài khoản đã có" }[project.credentialEmailStatus] || "Chưa có thông tin"}`);
        const tasks = data.roadmap || [];
        const completed = tasks.filter(t => t.status === "Done").length;
        const next = tasks.find(t => t.status !== "Done");
        setEl("hubProgressTasks", `${completed}/${tasks.length} nhiệm vụ đã hoàn thành`);
        setEl("hubProgressNext", next ? `Tiếp theo: ${next.title} · ${dateVi(next.dueDate)}` : tasks.length ? "Đã hoàn thành lộ trình." : "Lộ trình được tạo sau khi đóng băng baseline.");
        setEl("hubCrTabBadge", project.openChangeRequests);
        setEl("hubCrSubtitle", project.openChangeRequests ? `${project.openChangeRequests} yêu cầu cần xử lý.` : "Không có yêu cầu thay đổi đang mở.");
        document.getElementById("hubBeoStaleDot")?.classList.toggle("hidden", project.beoStatus !== "Stale");
        setEl("hubBeoSubtitle", project.beoStatus === "Stale" ? "BEO cần được kiểm tra và phê duyệt lại." : project.beoStatus === "Not started" ? "Dự án chưa có BEO." : `Trạng thái hiện tại: ${project.beoStatus}`);
        const nrd = project.payments?.find(p => p.type === "NRD");
        setEl("hubPaymentStatus", nrd?.status === "Paid" ? "NRD: Đã thu" : "NRD: Chưa xác nhận thu");
        setEl("hubDayTitle", `${project.dayOfCompleted || 0}/${project.dayOfTotal || 0} nhiệm vụ hoàn thành`);
        setEl("hubDaySubtitle", `Ngày tổ chức: ${dateVi(project.eventDate)}`);
        setEl("hubDayState", project.dayOfTotal ? `${project.dayOfCompleted}/${project.dayOfTotal} hoàn thành` : "Chưa có nhiệm vụ");
        setEl("hubComplaintCount", project.openComplaints || 0);
        setEl("hubComplaintSummary", project.openComplaints ? "khiếu nại đang mở." : "Chưa có khiếu nại đang mở.");
        setEl("hubMiniHall", project.hall);
        const timeline = document.getElementById("hubTimeline");
        if (timeline && tasks.length) timeline.innerHTML = tasks.map(t => `<article class="rounded-lg bg-surface p-3 border border-outline-variant"><strong class="text-primary">${escapeHtml(t.milestone)}</strong><p class="text-xs">${dateVi(t.dueDate)}</p><p class="text-sm">${escapeHtml(t.title)}</p><span class="text-xs">${escapeHtml(t.status)}</span></article>`).join("");

        const bar = document.getElementById("hubProgressBar");
        if (bar) bar.style.width = `${project.roadmapProgress}%`;

        const staleBanner = document.getElementById("hubStaleBeoBanner");
        if (staleBanner) {
          if (project.beoStatus === "Stale") {
            staleBanner.classList.remove("hidden");
          } else {
            staleBanner.classList.add("hidden");
          }
        }
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
