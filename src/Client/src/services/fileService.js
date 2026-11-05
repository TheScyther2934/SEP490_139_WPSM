import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// API-02: the API checks access and answers a short-lived signed { url, expiresAt } the browser can open directly.
export const fileService = {
  getSignedUrl: (fileId) => axiosClient.get(ENDPOINTS.file(fileId)).then((r) => r.data),
};
