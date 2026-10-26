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
        message(`Đăng nhập API thành công · ${user.user.fullName}`);
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
        try { await send(`price-frames/${encodeURIComponent(frame.id)}`, "PUT", { minPrice: values[0] || frame.minPrice, maxPrice: values[1] || frame.maxPrice }); message("Đã lưu khung giá qua API."); }
        catch (error) { showError(error); }
      }
      return;
    }
    if (screen === "A06") {
      event.preventDefault(); event.stopImmediatePropagation();
      const values = [...form.querySelectorAll("input[type=number]")].map(input => Number(input.value || 0));
      const current = window.wpsm?.policies;
      if (!current) return;
      const isDeposit = !!form.closest("#modal-a07");
      const policy = isDeposit
        ? { ...current, nrdRatio: values[0] ?? current.nrdRatio, finalRatio: Math.max(0, 100 - (values[0] ?? current.nrdRatio) - current.t30Ratio) }
        : { ...current, crPenaltyDays: values[0] ?? current.crPenaltyDays, crPenaltyPercent: values[1] ?? current.crPenaltyPercent, latePenaltyDays: values[2] ?? current.latePenaltyDays, latePenaltyPercent: values[3] ?? current.latePenaltyPercent };
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
    if (screen === "G02" && label.includes("Đổi mật khẩu")) {
      event.preventDefault(); window.location.href = "/G03"; return;
    }
    if (screen === "M03" && label.includes("Xác nhận") && label.includes("tạo dự án")) {
      event.preventDefault(); event.stopImmediatePropagation();
      const inputs = [...document.querySelectorAll("input")];
      const names = inputs.find(input => (input.value || "").includes(" & "));
      const date = inputs.find(input => /^\d{1,2}\/\d{1,2}\/\d{4}$/.test(input.value || ""));
      const amounts = inputs.filter(input => (input.value || "").includes("₫"));
      const selects = [...document.querySelectorAll("select")];
      const [day, month, year] = (date?.value || "20/12/2026").split("/");
      try {
        const project = await send("projects", "POST", {
          coupleName: names?.value || "Cặp đôi mới",
          hall: selects.map(select => select.options[select.selectedIndex]?.text || "").find(text => text.includes("Sảnh")) || window.wpsm?.project.hall || "Sảnh Hoàng Gia",
          slot: "Evening", eventDate: `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`,
          baseValue: moneyValue(amounts[0]), nrdAmount: moneyValue(amounts[1])
        });
        message(`Đã tạo dự án ${project.id} qua API.`);
      } catch (error) { showError(error); }
    }
    if (screen === "M05" && /lưu|chốt/i.test(label)) {
      event.preventDefault(); event.stopImmediatePropagation();
      const inputs = [...document.querySelectorAll("input[type=number]")];
      try {
        const project = window.wpsm?.project;
        if (project) await send(`projects/${encodeURIComponent(project.id)}/specification`, "PUT", {
          summary: [...document.querySelectorAll("textarea")].map(input => input.value).filter(Boolean).join(" · ") || "Đặc tả đã cập nhật",
          guestCount: Number(inputs[0]?.value || 0)
        });
        message("Đã gửi đặc tả tới API.");
      } catch (error) { showError(error); }
    }
  }, true);

  fetch("/api/wpsm/bootstrap", { headers: { Accept: "application/json" } })
    .then(response => { if (!response.ok) throw new Error(`HTTP ${response.status}`); return response.json(); })
    .then(data => {
      const project = data.project;
      const dateVi = new Intl.DateTimeFormat("vi-VN").format(new Date(`${project.eventDate}T00:00:00`));
      replaceText(document.body, {
        "Minh Anh & Thu Hà": project.coupleName,
        "WPSM-2026-014": project.id,
        "Sảnh Hoàng Gia": project.hall,
        "20/12/2026": dateVi,
        "185.000.000 ₫": formatMoney(project.baseValue),
        "18.500.000 ₫": formatMoney(project.nrdAmount),
        "NL Long & Linh": data.profile.fullName,
        "minhanh@example.com": data.profile.email,
        "0912 345 678": data.profile.phone
      });
      window.wpsm = data;
      hydrateTables(data);
      status.textContent = `API kết nối · ${project.id} · ${dateVi}`;
      status.classList.add("is-ready");
    })
    .catch(error => {
      status.textContent = "Không kết nối được WPSM API";
      status.title = `${error.message}. Hãy chạy WPSM.API cùng WPSM.Web.`;
      status.classList.add("is-error");
    });
})();
