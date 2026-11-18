import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// A-01, A-09..A-19
export const adminService = {
  getDashboard: () => axiosClient.get(ENDPOINTS.admin.dashboard).then((r) => r.data),
  getReport: (from, to) => axiosClient.get(ENDPOINTS.admin.reports, { params: { from, to } }).then((r) => r.data),
  getPhotos: (filtersOrDate) => {
    const params = typeof filtersOrDate === "object" && filtersOrDate !== null
      ? filtersOrDate
      : { date: filtersOrDate };
    return axiosClient.get(ENDPOINTS.admin.photos, { params }).then((r) => r.data);
  },
  getPhoto: (photoId) => axiosClient.get(ENDPOINTS.admin.photo(photoId)).then((r) => r.data),
  getOverdueAlerts: () => axiosClient.get(ENDPOINTS.admin.overdueAlerts).then((r) => r.data),
  getEscalatedComplaints: () => axiosClient.get(ENDPOINTS.admin.complaints).then((r) => r.data),
  resolveComplaint: (complaintId, decision) => axiosClient.post(ENDPOINTS.admin.resolveComplaint(complaintId), { decision }),
  getExceptionRequests: () => axiosClient.get(ENDPOINTS.admin.exceptionRequests).then((r) => r.data),
  decideException: (requestId, approve, note) => axiosClient.post(ENDPOINTS.admin.decideException(requestId), { approve, note }),
  // A-19 accounts
  getAccounts: () => axiosClient.get(ENDPOINTS.admin.accounts).then((r) => r.data),
  createAccount: (body) => axiosClient.post(ENDPOINTS.admin.accounts, body).then((r) => r.data),
  updateAccount: (id, body) => axiosClient.put(ENDPOINTS.admin.account(id), body).then((r) => r.data),
  setAccountStatus: (id, active) => axiosClient.post(ENDPOINTS.admin.accountStatus(id), { active }).then((r) => r.data),
  resetAccountPassword: (id) => axiosClient.post(ENDPOINTS.admin.accountResetPassword(id)).then((r) => r.data),
};
