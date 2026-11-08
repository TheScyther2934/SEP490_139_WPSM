// Every API path in one place; services build requests from these.
export const ENDPOINTS = {
  auth: {
    token: "/auth/token",
    refresh: "/auth/token/refresh",
    revoke: "/auth/token/revoke",
    changePassword: "/auth/change-password",
  },
  profile: "/profile",
  bootstrap: "/bootstrap",
  halls: "/halls",
  projects: "/projects",
  project: (code) => `/projects/${encodeURIComponent(code)}`,
  specification: (code) => `/projects/${encodeURIComponent(code)}/specification`,
  freeze: (code) => `/projects/${encodeURIComponent(code)}/freeze`,
  nrdReceipt: (code) => `/projects/${encodeURIComponent(code)}/nrd-receipt`,
  resendCredentials: (code) => `/projects/${encodeURIComponent(code)}/resend-credentials`,
  addenda: "/addenda",
  catalog: "/catalog",
  priceFrames: "/price-frames",
  priceFrame: (id) => `/price-frames/${encodeURIComponent(id)}`,
  policies: "/policies",
  admin: {
    dashboard: "/admin/dashboard",
    reports: "/admin/reports",
    photos: "/admin/photos",
    photo: (id) => `/admin/photos/${id}`,
    overdueAlerts: "/admin/overdue-alerts",
    complaints: "/admin/complaints",
    resolveComplaint: (id) => `/admin/complaints/${id}/resolve`,
    exceptionRequests: "/admin/exception-requests",
    decideException: (id) => `/admin/exception-requests/${id}/decision`,
  },
  customer: {
    dashboard: "/customer/dashboard",
    roadmap: "/customer/roadmap",
    completeTask: (projectCode, taskId) => `/customer/projects/${encodeURIComponent(projectCode)}/roadmap/tasks/${taskId}/complete`,
  },
  // FE-07. Manager calendar, allocation dialog (M-21) and conflict detail (M-22).
  resourceCalendar: "/resource-calendar",
  resources: "/resources",
  resourceAllocations: "/resource-allocations",
  resourceAllocation: (allocationId) => `/resource-allocations/${encodeURIComponent(allocationId)}`,
  allocationException: (allocationId) => `/resource-allocations/${encodeURIComponent(allocationId)}/exception`,
  // FE-05. Customer routes resolve the caller's own project (GBR-12); Manager routes carry the code.
  changeRequests: {
    customer: {
      list: "/customer/change-requests",
      form: "/customer/change-requests/form",
      create: "/customer/change-requests",
      detail: (crCode) => `/customer/change-requests/${encodeURIComponent(crCode)}`,
      submit: (crCode) => `/customer/change-requests/${encodeURIComponent(crCode)}/submit`,
      approve: (crCode) => `/customer/change-requests/${encodeURIComponent(crCode)}/approve`,
      reject: (crCode) => `/customer/change-requests/${encodeURIComponent(crCode)}/reject`,
    },
    manager: (projectCode) => {
      const project = `/projects/${encodeURIComponent(projectCode)}/change-requests`;
      return {
        list: project,
        form: `${project}/form`,
        create: project,
        detail: (crCode) => `${project}/${encodeURIComponent(crCode)}`,
        submit: (crCode) => `${project}/${encodeURIComponent(crCode)}/submit`,
        counter: (crCode) => `${project}/${encodeURIComponent(crCode)}/counter`,
        approve: (crCode) => `${project}/${encodeURIComponent(crCode)}/approve`,
        reject: (crCode) => `${project}/${encodeURIComponent(crCode)}/reject`,
        exception: (crCode) => `${project}/${encodeURIComponent(crCode)}/exception`,
      };
    },
  },
  file: (id) => `/files/${encodeURIComponent(id)}`,
};

// One lookup for the C-05/C-06 and M-07/M-08 screens: the Manager paths need the project code.
export const changeRequestRoutes = (role, projectCode) =>
  role === "Manager" ? ENDPOINTS.changeRequests.manager(projectCode) : ENDPOINTS.changeRequests.customer;
