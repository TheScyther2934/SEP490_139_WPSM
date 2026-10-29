(() => {
  const status = document.getElementById("wpsm-api-status");
  const formatMoney = value => new Intl.NumberFormat("vi-VN").format(value) + " ₫";
  const bindRoadmapEvents = () => {
    document.querySelectorAll('[data-wpsm-screen="C03"] [data-toggle-milestone]').forEach(button => {
      button.addEventListener("click", () => {
        const body = button.closest("article").querySelector("[data-milestone-body]");
        body.hidden = !body.hidden;
        button.setAttribute("aria-expanded", String(!body.hidden));
      });
    });
    document.querySelectorAll('[data-wpsm-screen="C03"] [data-task-id]').forEach(button => {
      button.addEventListener("click", () => {
        const taskId = Number(button.dataset.taskId);
        const task = window.wpsm?.roadmap?.find(item => item.taskId === taskId);
        if (!task) return;
        openRoadmapTaskDialog(task);
      });
    });
  };
  const openRoadmapTaskDialog = task => {
    document.getElementById("wpsm-roadmap-dialog")?.remove();
    const statusText = task.status === "Done" ? "Đã hoàn thành" : task.status === "Overdue" ? "Quá hạn" : "Chờ thực hiện";
    const dueText = new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(`${task.dueDate}T00:00:00`));
    const completedText = task.completedAt ? new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }).format(new Date(task.completedAt)) : "";
    const canComplete = task.assigneeRole === "Customer" && task.status !== "Done";
    const dialog = document.createElement("div");
    dialog.id = "wpsm-roadmap-dialog";
    dialog.className = "fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4";
    dialog.innerHTML = `<div role="dialog" aria-modal="true" aria-label="Chi tiết task" class="w-full max-w-lg rounded-2xl bg-surface-container-lowest shadow-xl border border-outline-variant/40 p-6">
      <div class="flex justify-between gap-4"><div><p class="text-xs font-semibold text-primary">${escapeHtml(task.milestone)}</p><h2 class="text-xl font-bold text-on-surface mt-1">${escapeHtml(task.title)}</h2></div><button type="button" data-close-dialog class="text-on-surface-variant hover:text-primary material-symbols-outlined">close</button></div>
      <dl class="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-5 text-sm"><div><dt class="text-on-surface-variant">Phụ trách</dt><dd class="font-semibold">${task.assigneeRole === "Customer" ? "Khách hàng" : "Quản lý"}</dd></div><div><dt class="text-on-surface-variant">Deadline</dt><dd class="font-semibold">${dueText}</dd></div><div><dt class="text-on-surface-variant">Trạng thái</dt><dd class="font-semibold">${statusText}</dd></div><div><dt class="text-on-surface-variant">Hoàn thành lúc</dt><dd class="font-semibold">${completedText || "Chưa hoàn thành"}</dd></div></dl>
      <div class="mt-5 space-y-3"><section><h3 class="font-semibold">Hướng dẫn</h3><p class="text-sm text-on-surface-variant mt-1">Thông tin mô tả chưa được cấu hình.</p></section><section><h3 class="font-semibold">Checklist</h3><p class="text-sm text-on-surface-variant mt-1">Checklist guidance chưa được cấu hình.</p></section></div>
      <p data-dialog-error class="text-sm text-error mt-4 min-h-5"></p>
      <div class="flex justify-end gap-2 mt-2"><button type="button" data-close-dialog class="px-4 py-2 rounded-lg border border-outline-variant/60">Đóng</button>${canComplete ? `<button type="button" data-complete-task class="px-4 py-2 rounded-lg bg-primary text-on-primary disabled:opacity-60">Xác nhận hoàn thành</button>` : ""}</div></div>`;
    document.body.appendChild(dialog);
    dialog.querySelectorAll("[data-close-dialog]").forEach(button => button.addEventListener("click", () => dialog.remove()));
    dialog.addEventListener("click", event => { if (event.target === dialog) dialog.remove(); });
    const completeButton = dialog.querySelector("[data-complete-task]");
    completeButton?.addEventListener("click", async () => {
      if (completeButton.disabled) return;
      completeButton.disabled = true;
      completeButton.textContent = "Đang cập nhật…";
      const errorText = dialog.querySelector("[data-dialog-error]");
      errorText.textContent = "";
      try {
        const projectCode = window.wpsm.project.id;
        const result = await send(`customer/projects/${encodeURIComponent(projectCode)}/roadmap/tasks/${task.taskId}/complete`, "POST", {});
        const index = window.wpsm.roadmap.findIndex(item => item.taskId === task.taskId);
        if (index >= 0) window.wpsm.roadmap[index] = result;
        window.wpsm.project.roadmapProgress = window.wpsm.roadmap.length ? Math.round(window.wpsm.roadmap.filter(item => item.status === "Done").length * 100 / window.wpsm.roadmap.length) : 0;
        dialog.remove();
        window.wpsmRenderRoadmap?.();
        message("Đã xác nhận hoàn thành task.");
      } catch (error) {
        completeButton.disabled = false;
        completeButton.textContent = "Xác nhận hoàn thành";
        errorText.textContent = error.message;
      }
    });
  };
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

    const specBody = document.querySelector('[data-wpsm-screen="M05"] table tbody');
    const specification = data.project?.specification || [];
    if (specBody) specBody.innerHTML = specification.length ? specification.map((item, index) => `<tr class="hover:bg-surface-container-low/40 transition-colors" data-spec-item-id="${item.catalogItemId}">
      <td class="py-3 px-3 text-center">${index + 1}</td><td class="py-3 px-3"><div class="font-semibold">${escapeHtml(item.name)}</div><span class="text-xs">${escapeHtml(item.category)}</span></td>
      <td class="py-3 px-3"><input class="w-full rounded border px-2 py-1" data-spec-description value="${escapeHtml(item.description)}"></td>
      <td class="py-3 px-3 text-center"><input class="w-20 rounded border px-2 py-1 text-center" data-spec-quantity type="number" min="1" value="${item.quantity}"></td>
      <td class="py-3 px-3 text-right"><input class="w-32 rounded border px-2 py-1 text-right" data-spec-price type="text" value="${item.unitPrice}"></td>
      <td class="py-3 px-3 text-right">${money(item.lineTotal)}</td><td class="py-3 px-3 text-center">—</td></tr>`).join("") : `<tr><td colspan="7" class="py-6 text-center text-on-surface-variant">Chưa có hạng mục đặc tả.</td></tr>`;

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
      const milestones = ["T-90", "T-30", "T-7", "Event Day"];
      const roadmap = Array.isArray(data.roadmap) ? data.roadmap : [];
      const renderRoadmap = () => {
        if (!roadmap.length) {
          roadmapSection.innerHTML = `<p class="rounded-xl bg-surface-container-low p-5">Roadmap sẽ được tạo sau khi Manager đóng băng baseline.</p>`;
          return;
        }
        roadmapSection.innerHTML = milestones.map((milestone, index) => {
          const tasks = roadmap.filter(task => task.milestone === milestone).sort((a, b) => `${a.dueDate}`.localeCompare(`${b.dueDate}`));
          const done = tasks.filter(task => task.status === "Done").length;
          const eventDate = milestone === "Event Day" ? data.project.eventDate : null;
          const relatedDate = eventDate || tasks[0]?.dueDate;
          const completedClass = done === tasks.length && tasks.length ? "bg-emerald-100 text-emerald-800" : "bg-primary-container text-on-primary";
          return `<article class="relative z-10 bg-surface-container-lowest border border-outline-variant/40 rounded-xl p-5 shadow-sm" data-milestone="${milestone}">
            <button type="button" class="w-full flex items-center justify-between gap-3 text-left" data-toggle-milestone aria-expanded="${index !== 0 || done !== tasks.length}">
              <div class="flex items-center gap-3"><span class="w-8 h-8 rounded-full flex items-center justify-center ${completedClass}">${done === tasks.length && tasks.length ? "✓" : escapeHtml(String(index + 1))}</span>
                <div><h3 class="text-title-lg font-bold text-primary">${escapeHtml(milestone)}</h3><p class="text-sm text-on-surface-variant">${relatedDate ? `Ngày liên quan: ${new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(`${relatedDate}T00:00:00`))}` : "Ngày liên quan chưa cấu hình"}</p></div>
              </div><div class="text-right"><div class="font-semibold">${done}/${tasks.length} nhiệm vụ hoàn thành</div><span class="text-sm text-on-surface-variant">${done === tasks.length && tasks.length ? "Đã hoàn thành" : "Đang tiến hành"}</span></div>
            </button>
            <div class="mt-4 space-y-2" data-milestone-body ${index === 0 && done === tasks.length && tasks.length ? "hidden" : ""}>
              ${tasks.length ? tasks.map(task => {
                const status = task.status === "Done" ? "Đã hoàn thành" : task.status === "Overdue" ? "Quá hạn" : "Chờ thực hiện";
                const statusClass = task.status === "Done" ? "bg-emerald-100 text-emerald-800" : task.status === "Overdue" ? "bg-error/10 text-error" : "bg-surface-container text-on-surface-variant";
                return `<button type="button" class="w-full grid grid-cols-1 md:grid-cols-[1fr_auto_auto] gap-3 items-center rounded-lg border border-outline-variant/30 px-4 py-3 text-left hover:bg-surface-container-low transition-colors" data-task-id="${task.taskId}">
                  <div><div class="font-semibold text-on-surface">${escapeHtml(task.title)}</div><div class="text-sm text-on-surface-variant">Phụ trách: ${task.assigneeRole === "Customer" ? "Khách hàng" : "Quản lý"}</div></div>
                  <div class="text-sm"><span class="font-medium">Hạn:</span> ${new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(`${task.dueDate}T00:00:00`))}</div>
                  <span class="px-2 py-1 rounded-full text-xs font-semibold ${statusClass}">${status}</span></button>`;
              }).join("") : `<p class="text-sm text-on-surface-variant px-3 py-2">Không có task trong mốc này.</p>`}
            </div></article>`;
        }).join("");
        bindRoadmapEvents();
      };
      renderRoadmap();
      window.wpsmRenderRoadmap = renderRoadmap;
    }
  };
  const showError = error => { status.textContent = error.message; status.classList.remove("is-ready"); status.classList.add("is-error"); };

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
    if (screen === "M05" && button.id === "confirmFreezeBtn") {
      event.preventDefault(); event.stopImmediatePropagation();
      const project = window.wpsm?.project;
      if (!project) return showError(new Error("Không tìm thấy dự án để đóng băng."));
      try {
        const frozen = await send(`projects/${encodeURIComponent(project.id)}/freeze`, "POST", {});
        window.wpsm.project = frozen;
        message(`Đã đóng băng baseline ${frozen.id} và tạo roadmap.`);
      } catch (error) { showError(error); }
      return;
    }
    if (screen === "M05" && /lưu nháp đặc tả/i.test(label)) {
      event.preventDefault(); event.stopImmediatePropagation();
      try {
        const project = window.wpsm?.project;
        if (project) {
          const items = [...document.querySelectorAll('[data-wpsm-screen="M05"] tr[data-spec-item-id]')].map(row => ({
            catalogItemId: Number(row.dataset.specItemId),
            description: row.querySelector("[data-spec-description]")?.value || "",
            quantity: Number(row.querySelector("[data-spec-quantity]")?.value || 0),
            unitPrice: moneyValue(row.querySelector("[data-spec-price]"))
          }));
          const updated = await send(`projects/${encodeURIComponent(project.id)}/specification`, "PUT", { summary: "Đặc tả baseline", guestCount: 0, items });
          window.wpsm.project = updated;
          hydrateTables(window.wpsm);
        }
        message("Đã lưu đặc tả vào database.");
      } catch (error) { showError(error); }
      return;
    }
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
