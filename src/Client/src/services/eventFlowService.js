import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

const data = (r) => r.data;

// Event stage flow for operators (Manager, Admin read): stages and notes, cancellation, addon contracts, supplier evidence.
export const eventFlowService = {
  workspace: (code) => axiosClient.get(ENDPOINTS.workspace(code)).then(data),
  addNote: (code, noteType, content) => axiosClient.post(ENDPOINTS.stageNotes(code), { noteType, content }).then(data),
  cancelEvent: (code, reason) => axiosClient.post(ENDPOINTS.cancelEvent(code), { reason }).then(data),

  addons: (code) => axiosClient.get(ENDPOINTS.addons(code)).then(data),
  createAddon: (code, body) => axiosClient.post(ENDPOINTS.addons(code), body).then(data),
  remindAddon: (code, addonCode) => axiosClient.post(ENDPOINTS.addonRemind(code, addonCode)).then(data),
  cancelAddon: (code, addonCode, reason) => axiosClient.post(ENDPOINTS.addonCancel(code, addonCode), { reason }).then(data),

  suppliers: (code) => axiosClient.get(ENDPOINTS.suppliers(code)).then(data),
  uploadEvidence: (code, { supplierName, evidenceType, specItemId, note, file }) => {
    const form = new FormData();
    form.append("supplierName", supplierName);
    form.append("evidenceType", evidenceType);
    if (specItemId) form.append("specItemId", specItemId);
    if (note) form.append("note", note);
    form.append("file", file);
    return axiosClient.post(ENDPOINTS.suppliers(code), form).then(data);
  },
  downloadEvidence: (code, id) => axiosClient.get(ENDPOINTS.supplierFile(code, id), { responseType: "blob" }).then(data),

  lookupCustomers: (q) => axiosClient.get(ENDPOINTS.customerLookup, { params: q ? { q } : {} }).then(data),
};
