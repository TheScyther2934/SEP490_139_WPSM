import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

const orNullOn404 = (request) =>
  request
    .then((r) => r.data)
    .catch((error) => {
      if (error.response?.status === 404) return null;
      throw error;
    });

export const paymentService = {
  getOverview: (projectCode) =>
    orNullOn404(axiosClient.get(ENDPOINTS.customerPayments.overview(projectCode))),

  getSummary: (projectCode) =>
    orNullOn404(axiosClient.get(ENDPOINTS.customerPayments.summary(projectCode))),

  getMilestones: (projectCode) =>
    orNullOn404(axiosClient.get(ENDPOINTS.customerPayments.milestones(projectCode))),

  downloadReceipt: (paymentType, projectCode) =>
    axiosClient
      .get(ENDPOINTS.customerPayments.receipt(paymentType, projectCode), {
        responseType: "blob",
      })
      .then((r) => ({
        blob: r.data,
        fileName: extractFileName(r.headers["content-disposition"]) || `Bien_nhan_${paymentType}.pdf`,
      })),

  downloadFinalReceipt: (projectCode) =>
    axiosClient
      .get(ENDPOINTS.customerPayments.finalReceipt(projectCode), {
        responseType: "blob",
      })
      .then((r) => ({
        blob: r.data,
        fileName: extractFileName(r.headers["content-disposition"]) || "Bien_nhan_quyet_toan.pdf",
      })),
};

function extractFileName(contentDisposition) {
  if (!contentDisposition) return null;
  const match = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
  if (match && match[1]) {
    return match[1].replace(/['"]/g, "").trim();
  }
  return null;
}

export function saveBlobAsFile(blob, filename) {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.setAttribute("download", filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.URL.revokeObjectURL(url);
}
