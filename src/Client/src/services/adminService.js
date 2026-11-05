import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// A-01, A-09..A-18
export const adminService = {
  getDashboard: () => axiosClient.get(ENDPOINTS.admin.dashboard).then((r) => r.data),
  getReport: (from, to) => axiosClient.get(ENDPOINTS.admin.reports, { params: { from, to } }).then((r) => r.data),
  getPhotos: (date) => axiosClient.get(ENDPOINTS.admin.photos, { params: { date } }).then((r) => r.data),
  getPhoto: (photoId) => axiosClient.get(ENDPOINTS.admin.photo(photoId)).then((r) => r.data),
  getOverdueAlerts: () => axiosClient.get(ENDPOINTS.admin.overdueAlerts).then((r) => r.data),
  getEscalatedComplaints: () => axiosClient.get(ENDPOINTS.admin.complaints).then((r) => r.data),
  resolveComplaint: (complaintId, decision) => axiosClient.post(ENDPOINTS.admin.resolveComplaint(complaintId), { decision }),
  getExceptionRequests: () => axiosClient.get(ENDPOINTS.admin.exceptionRequests).then((r) => r.data),
  decideException: (requestId, approve, note) => axiosClient.post(ENDPOINTS.admin.decideException(requestId), { approve, note }),
};
