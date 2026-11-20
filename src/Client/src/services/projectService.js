import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// Events (list, create, adjust services) and the shared portal snapshot.
export const projectService = {
  getProjects: () => axiosClient.get(ENDPOINTS.projects).then((r) => r.data),
  getProject: (code) => axiosClient.get(ENDPOINTS.project(code)).then((r) => r.data),
  createProject: (body) => axiosClient.post(ENDPOINTS.projects, body).then((r) => r.data),
  uploadDepositReceipt: (code, file) => {
    const form = new FormData();
    form.append("receipt", file);
    return axiosClient.post(ENDPOINTS.depositReceipt(code), form).then((r) => r.data);
  },
  downloadDepositReceipt: (code) => axiosClient.get(ENDPOINTS.depositReceipt(code), { responseType: "blob" }).then((r) => r.data),
  resendCredentials: (code) => axiosClient.post(ENDPOINTS.resendCredentials(code)).then((r) => r.data),
  saveSpecification: (code, body) => axiosClient.put(ENDPOINTS.specification(code), body).then((r) => r.data),
  getHalls: (date) => axiosClient.get(ENDPOINTS.halls, { params: date ? { date } : {} }).then((r) => r.data),
  getSnapshot: (projectCode) => axiosClient.get(ENDPOINTS.bootstrap, { params: projectCode ? { projectCode } : {} }).then((r) => r.data),

  uploadFile: (file, purpose, projectCode) => {
    const form = new FormData();
    form.append("file", file);
    form.append("purpose", purpose);
    if (projectCode) form.append("projectCode", projectCode);
    return axiosClient.post(ENDPOINTS.files, form).then((r) => r.data);
  },

  // M-10, M-11, M-12, JOB-03: BEO & Department Sheets
  getBeo: (code) => axiosClient.get(ENDPOINTS.beo(code)).then((r) => r.data),
  saveDraftBeo: (code, notes) => axiosClient.put(ENDPOINTS.beoDraft(code), { notes }).then((r) => r.data),
  freezeBeo: (code, notes) => axiosClient.post(ENDPOINTS.beoFreeze(code), { notes }).then((r) => r.data),
  generateDepartmentSheets: (code) => axiosClient.post(ENDPOINTS.beoDepartmentSheets(code)).then((r) => r.data),
  downloadDepartmentPdf: (code, department) =>
    axiosClient.get(ENDPOINTS.beoDepartmentPdf(code, department), { responseType: "blob" }).then((r) => r.data),
  compileBeoDrafts: () => axiosClient.post(ENDPOINTS.compileBeoDrafts).then((r) => r.data),

  // M-18, M-19, M-20: Manager Complaints
  getComplaints: (projectCode) =>
    axiosClient.get(ENDPOINTS.managerComplaints, { params: projectCode ? { projectCode } : {} }).then((r) => r.data),
  getComplaint: (complaintCode) => axiosClient.get(ENDPOINTS.managerComplaint(complaintCode)).then((r) => r.data),
  actOnComplaint: (complaintCode, action, note) =>
    axiosClient.post(ENDPOINTS.managerComplaintAction(complaintCode), { action, note }).then((r) => r.data),

  // Payments, dashboard, day-of
  // Co-management (Primary Manager or Admin changes the list; every operator can read it).
  getProjectManagers: (code) => axiosClient.get(ENDPOINTS.projectManagers(code)).then((r) => r.data),
  getManagerCandidates: (code) => axiosClient.get(ENDPOINTS.projectManagerCandidates(code)).then((r) => r.data),
  addProjectManager: (code, managerId, reason) => axiosClient.post(ENDPOINTS.projectManagers(code), { managerId, reason }).then((r) => r.data),
  removeProjectManager: (code, id, reason) => axiosClient.post(ENDPOINTS.removeProjectManager(code, id), { reason }).then((r) => r.data),
  getManagerPayments: (code) => axiosClient.get(ENDPOINTS.managerPayments.getPayments(code)).then((r) => r.data),
  recordPayment: (code, formData) => axiosClient.post(ENDPOINTS.managerPayments.recordPayment(code), formData).then((r) => r.data),
  getSettlement: (code) => axiosClient.get(ENDPOINTS.managerPayments.getSettlement(code)).then((r) => r.data),
  generateReceipt: (code) => axiosClient.post(ENDPOINTS.managerPayments.generateReceipt(code)).then((r) => r.data),
  downloadSettlementReceipt: (code) => axiosClient.get(ENDPOINTS.managerPayments.getReceiptFile(code), { responseType: "blob" }).then((r) => r.data),
  downloadPaymentReceipt: (code, paymentType) => axiosClient.get(ENDPOINTS.managerPayments.downloadPaymentReceipt(code, paymentType), { responseType: "blob" }).then((r) => r.data),
  getManagerDashboard: () => axiosClient.get(ENDPOINTS.manager.dashboard).then((r) => r.data),
  getManagerOverdueAlerts: () => axiosClient.get(ENDPOINTS.manager.overdueAlerts).then((r) => r.data),
  getDayOfTasks: (code) => axiosClient.get(ENDPOINTS.manager.dayOfTasks(code)).then((r) => r.data),
  completeDayOfTask: (code, taskId, formData) => axiosClient.post(ENDPOINTS.manager.completeDayOfTask(code, taskId), formData).then((r) => r.data),
  completeDayOfTaskDirect: (taskId, formData) => axiosClient.post(ENDPOINTS.manager.completeDayOfTaskDirect(taskId), formData).then((r) => r.data),
};
