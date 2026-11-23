// The four departments that carry out an event, in the order the coordination sheet lists them.
export const DEPARTMENTS = {
  Kitchen: { label: "Bếp", full: "Bộ phận Bếp" },
  Service: { label: "Phục vụ", full: "Bộ phận Phục vụ" },
  Decor: { label: "Trang trí", full: "Bộ phận Trang trí" },
  "Tech-MC": { label: "Âm thanh – MC", full: "Bộ phận Âm thanh, ánh sáng và MC" },
};
export const DEPARTMENT_KEYS = Object.keys(DEPARTMENTS);
export const departmentLabel = (key) => DEPARTMENTS[key]?.label ?? key;

// Task counts for a progress bar or a summary.
export const taskStats = (tasks) => ({
  total: tasks.length,
  done: tasks.filter((t) => t.status === "Done").length,
  overdue: tasks.filter((t) => t.status === "Overdue").length,
  missing: tasks.filter((t) => t.missingProof).length,
  proofs: tasks.reduce((n, t) => n + t.photos.length, 0),
});

export const TASK_FILTERS = {
  all: { label: "Tất cả", test: () => true },
  todo: { label: "Chưa xong", test: (t) => t.status !== "Done" },
  missing: { label: "Thiếu minh chứng", test: (t) => t.missingProof },
  done: { label: "Đã xong", test: (t) => t.status === "Done" },
};
