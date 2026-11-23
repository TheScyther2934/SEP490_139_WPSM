// API paths still used by pages: day-of, proof photos, resources, admin configuration.
const seg = encodeURIComponent;
const project = (code) => `/projects/${seg(code)}`;

export const OPS = {
  dayOf: (code) => `${project(code)}/day-of`,
  dayOfLoad: (code) => `${project(code)}/day-of/load`,
  dayOfComplete: (code, taskId) => `${project(code)}/day-of/${taskId}/complete`,
  dayOfPhotos: (code, taskId) => `${project(code)}/day-of/${taskId}/photos`,
  dayOfTasks: (code) => `${project(code)}/day-of/tasks`,
  dayOfTask: (code, taskId) => `${project(code)}/day-of/${taskId}`,

  proofPhotos: (code) => `${project(code)}/proof-photos`,

  resourceCalendar: "/resources/calendar",
  allocations: "/resource-allocations",
  allocation: (id) => `/resource-allocations/${id}`,
  allocationConflict: (id) => `/resource-allocations/${id}/conflict`,
  allocationException: "/resource-allocations/exception-requests",

  catalogItem: (id) => `/catalog/${id}`,
  catalogStatus: (id) => `/catalog/${id}/status`,
  priceFrameHistory: (itemId) => `/price-frames/history/${itemId}`,
};
