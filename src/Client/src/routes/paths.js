// Every client URL in one place; pages build links from these, never by hand.
// Screen codes are kept so routes can be traced back to the SRS.
const seg = (value) => encodeURIComponent(value);
const project = (code) => `/manager/projects/${seg(code)}`;

export const ROLES = { ADMIN: "Admin", MANAGER: "Manager", CUSTOMER: "Customer" };

export const PATHS = {
  login: "/login", // G-01
  profile: "/profile", // G-02 (G-03 dialog)
  changePassword: "/change-password", // G-03 forced on first login
  notifications: "/notifications", // G-04
  forbidden: "/403",

  admin: {
    root: "/admin",
    dashboard: "/admin/dashboard", // A-01
    catalog: "/admin/catalog", // A-02, A-03
    priceFrames: "/admin/price-frames", // A-04, A-05
    policies: "/admin/policies", // A-06..A-08
    reports: "/admin/reports", // A-09
    hallUtilization: "/admin/reports/hall-utilization", // A-10
    netRevenue: "/admin/reports/net-revenue", // A-11
    crStatistics: "/admin/reports/change-requests", // A-12
    photoFeed: "/admin/photo-feed", // A-13
    photo: (photoId) => `/admin/photo-feed/${seg(photoId)}`, // A-14
    overdueAlerts: "/admin/overdue-alerts", // A-15
    complaints: "/admin/complaints", // A-16, A-17
    exceptionRequests: "/admin/exception-requests", // A-18
  },

  manager: {
    root: "/manager",
    dashboard: "/manager/dashboard", // M-01
    projects: "/manager/projects", // M-02
    newProject: "/manager/projects/new", // M-03
    project, // M-04
    specification: (code) => `${project(code)}/specification`, // M-05
    roadmap: (code) => `${project(code)}/roadmap`, // M-06
    changeRequests: (code) => `${project(code)}/change-requests`, // M-07
    newChangeRequest: (code) => `${project(code)}/change-requests/new`, // M-07 (create on behalf of the Customer)
    editChangeRequest: (code, crCode) => `${project(code)}/change-requests/new?crCode=${seg(crCode)}`, // C-06 for a draft
    changeRequest: (code, crCode) => `${project(code)}/change-requests/${seg(crCode)}`, // M-08
    costImpact: (code, crCode) => `${project(code)}/change-requests/${seg(crCode)}/cost-impact`, // M-09
    beo: (code) => `${project(code)}/beo`, // M-10, M-12
    departmentSheets: (code) => `${project(code)}/beo/department-sheets`, // M-11
    payments: (code) => `${project(code)}/payments`, // M-13, M-14
    settlementReceipt: (code) => `${project(code)}/payments/settlement-receipt`, // M-15
    dayOf: (code) => `${project(code)}/day-of`, // M-16, M-17
    complaints: (code) => `${project(code)}/complaints`, // M-18
    complaint: (code, complaintCode) => `${project(code)}/complaints/${seg(complaintCode)}`, // M-19, M-20
    resourceCalendar: "/manager/resource-calendar", // M-21
    conflict: (allocationId) => `/manager/resource-calendar/conflicts/${seg(allocationId)}`, // M-22
    overdueAlerts: "/manager/overdue-alerts", // M-23
  },

  // The API resolves the customer's own project (GBR-12), so no :projectCode here.
  customer: {
    root: "/customer",
    overview: "/customer/overview", // C-01
    contract: "/customer/contract", // C-02
    addenda: "/customer/contract/addenda", // C-09
    roadmap: "/customer/roadmap", // C-03, C-04
    changeRequests: "/customer/change-requests", // C-05
    newChangeRequest: "/customer/change-requests/new", // C-06
    editChangeRequest: (crCode) => `/customer/change-requests/new?crCode=${seg(crCode)}`, // C-06 for a draft
    changeRequest: (crCode) => `/customer/change-requests/${seg(crCode)}`, // C-07, C-08
    payments: "/customer/payments", // C-10
    livePhotos: "/customer/live-photos", // C-11
    postEvent: "/customer/post-event", // C-12
    ratings: "/customer/post-event/ratings", // C-13
    complaints: "/customer/complaints", // C-14
    newComplaint: "/customer/complaints/new", // C-15
    complaint: (complaintCode) => `/customer/complaints/${seg(complaintCode)}`, // C-16
  },
};

export const HOME_BY_ROLE = {
  [ROLES.ADMIN]: PATHS.admin.dashboard,
  [ROLES.MANAGER]: PATHS.manager.dashboard,
  [ROLES.CUSTOMER]: PATHS.customer.overview,
};

export const homeFor = (role) => HOME_BY_ROLE[role] ?? PATHS.login;
